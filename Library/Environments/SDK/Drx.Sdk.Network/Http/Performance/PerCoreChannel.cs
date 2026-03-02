using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// Per-Core 分区 Channel：将任务按 CPU 核心分区，实现 NUMA 友好的负载分发。
    /// 每个核心拥有独立的 BoundedChannel，减少跨核争用。
    /// 
    /// 设计特点：
    ///   - 每核心独立 Channel，写入时按亲和提示分发
    ///   - 支持工作窃取（Work Stealing）：本核 Channel 为空时从相邻核偷取
    ///   - Round-Robin 分发作为默认策略
    ///   - 全局溢出 Channel 作为回退（避免丢弃请求）
    /// 
    /// 适用场景：
    ///   - HTTP 请求分发到 per-core worker
    ///   - 任务队列的 per-core 分区
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    internal sealed class PerCoreChannel<T> : IDisposable
    {
        private readonly Channel<T>[] _channels;
        private readonly Channel<T> _overflowChannel;
        private readonly int _coreCount;
        private int _roundRobinCounter;

        /// <summary>
        /// 各核心 Channel 的实时队列深度估计（用于负载感知分发）
        /// </summary>
        private readonly int[] _queueDepths;

        /// <summary>
        /// 各核心的累计任务计数（诊断用）
        /// </summary>
        private readonly long[] _taskCounters;

        /// <summary>
        /// 创建 Per-Core Channel
        /// </summary>
        /// <param name="coreCount">核心数量（分区数）</param>
        /// <param name="perCoreCapacity">每核心 Channel 容量</param>
        /// <param name="overflowCapacity">溢出 Channel 容量</param>
        public PerCoreChannel(int coreCount, int perCoreCapacity = 256, int overflowCapacity = 1024)
        {
            _coreCount = Math.Max(1, coreCount);
            _channels = new Channel<T>[_coreCount];
            _queueDepths = new int[_coreCount];
            _taskCounters = new long[_coreCount];

            for (int i = 0; i < _coreCount; i++)
            {
                _channels[i] = Channel.CreateBounded<T>(new BoundedChannelOptions(perCoreCapacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,    // 每核心只有一个 Worker 读取
                    SingleWriter = false,   // 多个生产者可能写入同一核心
                    AllowSynchronousContinuations = false
                });
            }

            _overflowChannel = Channel.CreateBounded<T>(new BoundedChannelOptions(overflowCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
        }

        /// <summary>
        /// 核心数量
        /// </summary>
        public int CoreCount => _coreCount;

        /// <summary>
        /// 获取指定核心的 ChannelReader（由对应核心的 Worker 使用）
        /// </summary>
        public ChannelReader<T> GetReader(int coreIndex)
        {
            if (coreIndex < 0 || coreIndex >= _coreCount)
                throw new ArgumentOutOfRangeException(nameof(coreIndex));
            return _channels[coreIndex].Reader;
        }

        /// <summary>
        /// 获取溢出 Channel 的 Reader
        /// </summary>
        public ChannelReader<T> OverflowReader => _overflowChannel.Reader;

        /// <summary>
        /// 向指定核心的 Channel 写入元素。
        /// 如果目标核心 Channel 已满，自动回退到溢出 Channel。
        /// </summary>
        /// <param name="item">要写入的元素</param>
        /// <param name="coreHint">目标核心索引。-1 表示 Round-Robin 自动分配</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async ValueTask WriteAsync(T item, int coreHint = -1, CancellationToken cancellationToken = default)
        {
            int targetCore = coreHint >= 0 && coreHint < _coreCount
                ? coreHint
                : SelectCoreRoundRobin();

            // 优先尝试写入目标核心（非阻塞）
            if (_channels[targetCore].Writer.TryWrite(item))
            {
                Interlocked.Increment(ref _queueDepths[targetCore]);
                Interlocked.Increment(ref _taskCounters[targetCore]);
                return;
            }

            // 目标核心满，尝试找最空闲的核心
            int leastLoaded = FindLeastLoadedCore(targetCore);
            if (leastLoaded != targetCore && _channels[leastLoaded].Writer.TryWrite(item))
            {
                Interlocked.Increment(ref _queueDepths[leastLoaded]);
                Interlocked.Increment(ref _taskCounters[leastLoaded]);
                return;
            }

            // 所有核心都满，写入溢出 Channel（会等待/背压）
            await _overflowChannel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 从指定核心的 Channel 读取一个元素。
        /// 如果本核心为空，尝试从溢出 Channel 读取，再尝试工作窃取。
        /// </summary>
        /// <param name="coreIndex">核心索引</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>读取到的元素</returns>
        public async ValueTask<T> ReadAsync(int coreIndex, CancellationToken cancellationToken)
        {
            var reader = _channels[coreIndex].Reader;

            // 优先从本核心读取
            if (reader.TryRead(out var item))
            {
                Interlocked.Decrement(ref _queueDepths[coreIndex]);
                return item;
            }

            // 本核心为空，尝试从溢出 Channel 偷取
            if (_overflowChannel.Reader.TryRead(out item))
            {
                return item;
            }

            // 尝试从相邻核心窃取（Work Stealing）
            if (TryStealFromNeighbors(coreIndex, out var stolen) && stolen != null)
            {
                return stolen;
            }

            // 所有源都为空，等待本核心或溢出 Channel
            // 使用 WaitToReadAsync 配合 CancellationToken 实现非忙等待
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // 并行等待本核心 Channel 和溢出 Channel
            var coreReadTask = reader.WaitToReadAsync(linkedCts.Token).AsTask();
            var overflowReadTask = _overflowChannel.Reader.WaitToReadAsync(linkedCts.Token).AsTask();

            var completedTask = await Task.WhenAny(coreReadTask, overflowReadTask).ConfigureAwait(false);

            if (completedTask == coreReadTask && await coreReadTask.ConfigureAwait(false))
            {
                if (reader.TryRead(out item))
                {
                    Interlocked.Decrement(ref _queueDepths[coreIndex]);
                    return item;
                }
            }

            if (completedTask == overflowReadTask && await overflowReadTask.ConfigureAwait(false))
            {
                if (_overflowChannel.Reader.TryRead(out item))
                {
                    return item;
                }
            }

            // 兜底：阻塞等待本核心
            item = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            Interlocked.Decrement(ref _queueDepths[coreIndex]);
            return item;
        }

        /// <summary>
        /// 从相邻核心窃取一个元素（Work Stealing）。
        /// 窃取策略：从当前核心的下一个邻居开始循环查找。
        /// </summary>
        public bool TryStealFromNeighbors(int coreIndex, out T? item)
        {
            item = default;
            if (_coreCount <= 1) return false;

            // 从相邻核心开始，循环扫描
            for (int offset = 1; offset < _coreCount; offset++)
            {
                int neighbor = (coreIndex + offset) % _coreCount;
                if (_channels[neighbor].Reader.TryRead(out item))
                {
                    Interlocked.Decrement(ref _queueDepths[neighbor]);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 完成所有 Channel 的写入（通知 Worker 没有更多元素）
        /// </summary>
        public void Complete()
        {
            for (int i = 0; i < _coreCount; i++)
            {
                _channels[i].Writer.TryComplete();
            }
            _overflowChannel.Writer.TryComplete();
        }

        /// <summary>
        /// 获取指定核心的队列深度估计
        /// </summary>
        public int GetQueueDepth(int coreIndex)
        {
            if (coreIndex < 0 || coreIndex >= _coreCount) return 0;
            return Volatile.Read(ref _queueDepths[coreIndex]);
        }

        /// <summary>
        /// 获取指定核心的累计任务数
        /// </summary>
        public long GetTaskCount(int coreIndex)
        {
            if (coreIndex < 0 || coreIndex >= _coreCount) return 0;
            return Interlocked.Read(ref _taskCounters[coreIndex]);
        }

        /// <summary>
        /// 获取所有核心的总队列深度
        /// </summary>
        public int TotalQueueDepth
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _coreCount; i++)
                    total += Volatile.Read(ref _queueDepths[i]);
                return total;
            }
        }

        private int SelectCoreRoundRobin()
        {
            return (int)((uint)Interlocked.Increment(ref _roundRobinCounter) % (uint)_coreCount);
        }

        private int FindLeastLoadedCore(int excludeCore)
        {
            int minDepth = int.MaxValue;
            int minCore = excludeCore;
            for (int i = 0; i < _coreCount; i++)
            {
                if (i == excludeCore) continue;
                var depth = Volatile.Read(ref _queueDepths[i]);
                if (depth < minDepth)
                {
                    minDepth = depth;
                    minCore = i;
                }
            }
            return minCore;
        }

        public void Dispose()
        {
            Complete();
        }
    }
}
