using Drx.Sdk.Shared;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// 核心亲和线程池管理器：基于 per-core 分区 Channel 的高性能 Worker Pool。
    /// 
    /// 架构特点：
    ///   - 每个 CPU 核心拥有一个 dedicated Worker Thread（非 ThreadPool 线程）
    ///   - Worker 线程通过 CoreAffinityHelper 绑定到指定核心，减少上下文切换和 L1/L2 缓存失效
    ///   - 使用 PerCoreChannel 实现 per-core 任务队列，支持工作窃取（Work Stealing）
    ///   - Round-Robin 默认分发策略，支持负载感知分发
    ///   - 全局溢出 Channel 作为回退，避免丢弃任务
    ///   - 保留旧的 QueueWork API 保持向后兼容
    /// 
    /// 使用方式：
    ///   var pool = new ThreadPoolManager(workerCount: 8, enableAffinity: true);
    ///   await pool.SubmitAsync(async () => { ... });
    ///   pool.QueueWork(() => { ... });
    /// </summary>
    public sealed class ThreadPoolManager : IDisposable
    {
        private readonly Thread[] _workers;
        private readonly PerCoreChannel<Func<Task>> _perCoreChannel;
        private readonly CancellationTokenSource _cts = new();
        private readonly int _workerCount;
        private readonly bool _enableAffinity;
        private volatile bool _disposed;

        // 诊断指标
        private long _totalTasksSubmitted;
        private long _totalTasksCompleted;
        private long _totalTasksFailed;

        /// <summary>
        /// Worker 数量
        /// </summary>
        public int WorkerCount => _workerCount;

        /// <summary>
        /// 是否启用 CPU 核心亲和性
        /// </summary>
        public bool AffinityEnabled => _enableAffinity;

        /// <summary>
        /// 累计提交的任务数
        /// </summary>
        public long TotalTasksSubmitted => Interlocked.Read(ref _totalTasksSubmitted);

        /// <summary>
        /// 累计完成的任务数
        /// </summary>
        public long TotalTasksCompleted => Interlocked.Read(ref _totalTasksCompleted);

        /// <summary>
        /// 累计失败的任务数
        /// </summary>
        public long TotalTasksFailed => Interlocked.Read(ref _totalTasksFailed);

        /// <summary>
        /// 创建核心亲和线程池
        /// </summary>
        /// <param name="workerCount">Worker 数量，默认等于逻辑处理器数</param>
        /// <param name="enableAffinity">是否启用 CPU 核心亲和性绑定</param>
        /// <param name="perCoreCapacity">每核心任务队列容量</param>
        /// <param name="overflowCapacity">溢出队列容量</param>
        public ThreadPoolManager(int workerCount = 0, bool enableAffinity = true, int perCoreCapacity = 256, int overflowCapacity = 1024)
        {
            if (workerCount <= 0) workerCount = Environment.ProcessorCount;
            _workerCount = workerCount;
            _enableAffinity = enableAffinity;

            _perCoreChannel = new PerCoreChannel<Func<Task>>(_workerCount, perCoreCapacity, overflowCapacity);
            _workers = new Thread[_workerCount];

            for (int i = 0; i < _workerCount; i++)
            {
                int coreIndex = i;
                _workers[i] = new Thread(() => WorkerLoop(coreIndex))
                {
                    IsBackground = true,
                    Name = $"DrxHttpWorker-{coreIndex}",
                    Priority = ThreadPriority.Normal
                };
                _workers[i].Start();
            }
        }

        private void WorkerLoop(int coreIndex)
        {
            // 绑定到指定 CPU 核心
            if (_enableAffinity)
            {
                int targetCore = coreIndex % Environment.ProcessorCount;
                if (!CoreAffinityHelper.SetCurrentThreadAffinity(targetCore))
                {
                    Logger.Warn($"Worker-{coreIndex} 绑定到 CPU 核心 {targetCore} 失败，将在所有核心上调度");
                }
            }

            var token = _cts.Token;

            // 同步运行异步循环（Worker 是独立 Thread，不占用 ThreadPool）
            try
            {
                WorkerLoopAsync(coreIndex, token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // 正常关闭
            }
            catch (Exception ex)
            {
                Logger.Error($"Worker-{coreIndex} 异常退出: {ex}");
            }
        }

        private async Task WorkerLoopAsync(int coreIndex, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Func<Task> work;
                try
                {
                    work = await _perCoreChannel.ReadAsync(coreIndex, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (ChannelClosedException)
                {
                    break;
                }

                if (work == null) continue;

                try
                {
                    await work().ConfigureAwait(false);
                    Interlocked.Increment(ref _totalTasksCompleted);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _totalTasksFailed);
                    Logger.Warn($"Worker-{coreIndex} 执行任务时出现异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 异步提交任务到线程池，支持指定核心亲和。
        /// 任务将被分发到指定核心的 Worker，或通过 Round-Robin 自动分配。
        /// </summary>
        /// <param name="work">异步任务委托</param>
        /// <param name="preferredCore">优先核心索引，-1 表示自动分配</param>
        public async ValueTask SubmitAsync(Func<Task> work, int preferredCore = -1)
        {
            if (work == null) return;
            if (_disposed) throw new ObjectDisposedException(nameof(ThreadPoolManager));

            Interlocked.Increment(ref _totalTasksSubmitted);
            await _perCoreChannel.WriteAsync(work, preferredCore).ConfigureAwait(false);
        }

        /// <summary>
        /// 同步提交异步任务（火烧遗忘模式，保持向后兼容）
        /// </summary>
        public void QueueWork(Func<Task> work)
        {
            if (work == null || _disposed) return;
            Interlocked.Increment(ref _totalTasksSubmitted);
            // 使用 TryWrite 非阻塞提交，如果队列满则回退到 Task.Run
            var writeTask = _perCoreChannel.WriteAsync(work);
            if (!writeTask.IsCompleted)
            {
                // Channel 满，回退到 .NET ThreadPool 避免阻塞调用方
                _ = Task.Run(async () =>
                {
                    try { await writeTask.ConfigureAwait(false); }
                    catch (Exception ex) { Logger.Warn($"任务提交失败: {ex.Message}"); }
                });
            }
        }

        /// <summary>
        /// 同步提交同步任务（保持向后兼容）
        /// </summary>
        public void QueueWork(Action work)
        {
            if (work == null) return;
            QueueWork(() => { work(); return Task.CompletedTask; });
        }

        /// <summary>
        /// 获取指定核心的队列深度
        /// </summary>
        public int GetCoreQueueDepth(int coreIndex) => _perCoreChannel.GetQueueDepth(coreIndex);

        /// <summary>
        /// 获取指定核心的累计任务数
        /// </summary>
        public long GetCoreTaskCount(int coreIndex) => _perCoreChannel.GetTaskCount(coreIndex);

        /// <summary>
        /// 获取所有核心的总队列深度
        /// </summary>
        public int TotalQueueDepth => _perCoreChannel.TotalQueueDepth;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _perCoreChannel.Complete();
                _cts.Cancel();

                // 等待所有 Worker 线程退出
                foreach (var worker in _workers)
                {
                    if (worker != null && worker.IsAlive)
                    {
                        worker.Join(3000);
                    }
                }

                _perCoreChannel.Dispose();
                _cts.Dispose();

                Logger.Info($"ThreadPoolManager 已停止: submitted={TotalTasksSubmitted}, completed={TotalTasksCompleted}, failed={TotalTasksFailed}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"ThreadPoolManager 释放时出错: {ex.Message}");
            }
        }
    }
}
