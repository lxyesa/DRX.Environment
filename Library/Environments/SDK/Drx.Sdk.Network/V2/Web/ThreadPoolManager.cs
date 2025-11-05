using Drx.Sdk.Shared;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.V2.Web
{
    /// <summary>
    /// 轻量级线程池管理器，接收 Action/Func<Task> 并在线程池 worker 上执行
    /// </summary>
    public class ThreadPoolManager : IDisposable
    {
        private readonly BlockingCollection<Func<Task>> _tasks = new(new ConcurrentQueue<Func<Task>>());
        private readonly Task[] _workers;
        private readonly CancellationTokenSource _cts = new();

        public ThreadPoolManager(int workerCount = 4)
        {
            if (workerCount <= 0) workerCount = Environment.ProcessorCount;
            _workers = new Task[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                _workers[i] = Task.Run(() => WorkerLoop(_cts.Token));
            }
        }

        private async Task WorkerLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    Func<Task> work = null;
                    try
                    {
                        work = _tasks.Take(token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }

                    if (work == null) continue;

                    try
                    {
                        await work().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // 记录异常以便诊断，但不要中断 worker 循环
                        Logger.Warn($"ThreadPool worker 执行工作时出现异常: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"ThreadPool worker loop 出错: {ex.Message}");
            }
        }

        public void QueueWork(Func<Task> work)
        {
            if (work == null) return;
            _tasks.Add(work);
        }

        public void QueueWork(Action work)
        {
            if (work == null) return;
            QueueWork(() => { work(); return Task.CompletedTask; });
        }

        public void Dispose()
        {
            try
            {
                _tasks.CompleteAdding();
                _cts.Cancel();
                Task.WaitAll(_workers, 5000);
                _cts.Dispose();
                _tasks.Dispose();
            }
            catch { }
        }
    }
}
