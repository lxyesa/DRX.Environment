using DRX.Framework.Common.Item;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DRX.Framework.Common.Pool;

public class DRXQueuePool : IDisposable
{
    private readonly int _maxChannels;
    private readonly int _maxQueueSize;
    private readonly int _defaultDelay;
    private readonly ConcurrentDictionary<int, ConcurrentQueue<QueueItem>> _channels = new();
    private readonly ConcurrentDictionary<Guid, QueueItem> _itemTracker = new();
    private readonly SemaphoreSlim[] _channelSemaphores;
    private readonly CancellationTokenSource _cancellationToken;
    private volatile bool _isRunning;
    private readonly ConcurrentDictionary<int, PriorityQueue<QueueItem, (int Priority, DateTime EnqueueTime)>> _priorityChannels = new();

    public event EventHandler<QueueItemEventArgs>? ItemCompleted;
    public event EventHandler<QueueItemEventArgs>? ItemFailed;

    public QueueStatistics Statistics { get; } = new();

    public DRXQueuePool(int maxChannels, int maxQueueSize, int defaultDelay = 10)
    {
        _maxChannels = maxChannels;
        _maxQueueSize = maxQueueSize;
        _defaultDelay = defaultDelay;
        _cancellationToken = new CancellationTokenSource();
        _channelSemaphores = new SemaphoreSlim[maxChannels];
        for (int i = 0; i < maxChannels; i++)
        {
            _channelSemaphores[i] = new SemaphoreSlim(1, 1);
            _channels[i] = new ConcurrentQueue<QueueItem>();
            _priorityChannels[i] = new PriorityQueue<QueueItem, (int Priority, DateTime EnqueueTime)>();
        }
        StartProcessing();
    }

    private void StartProcessing()
    {
        if (_isRunning) return;
        _isRunning = true;

        for (int i = 0; i < _maxChannels; i++)
        {
            int channelIndex = i;
            Task.Run(async () => await ProcessChannel(channelIndex), _cancellationToken.Token);
        }
    }

    public async Task<bool> PushAsync(Action callback, int priority = 0, TimeSpan? timeout = null)
    {
        ClearCompletedItems(); // 清理已完成的任务
        var item = new QueueItem(callback, priority, timeout);
        
        var channelIndex = GetOptimalChannel();
        var queue = _channels[channelIndex];
        var priorityQueue = _priorityChannels[channelIndex];

        if (queue.Count >= _maxQueueSize)
            return false;

        await _channelSemaphores[channelIndex].WaitAsync();
        try
        {
            priorityQueue.Enqueue(item, (-priority, item.EnqueueTime)); // 负priority使高优先级排在前面
            queue.Enqueue(item);
            _itemTracker.TryAdd(item.Id, item);
            Statistics.IncrementEnqueued();
        }
        finally
        {
            _channelSemaphores[channelIndex].Release();
        }
        
        return await item.CompletionSource.Task;
    }

    private void ClearCompletedItems()
    {
        var completedItems = _itemTracker.Values
            .Where(x => x.Status == QueueItemStatus.Completed || 
                       x.Status == QueueItemStatus.Cancelled || 
                       x.Status == QueueItemStatus.Failed)
            .Select(x => x.Id)
            .ToList();

        foreach (var id in completedItems)
        {
            _itemTracker.TryRemove(id, out _);
        }
    }

    private int GetOptimalChannel()
    {
        return _channels.OrderBy(c => c.Value.Count).First().Key;
    }

    private async Task ProcessChannel(int channelIndex)
    {
        var sw = new Stopwatch();
        while (!_cancellationToken.Token.IsCancellationRequested)
        {
            sw.Restart();
            QueueItem? item = null;

            await _channelSemaphores[channelIndex].WaitAsync();
            try
            {
                var priorityQueue = _priorityChannels[channelIndex];
                var queue = _channels[channelIndex];

                if (priorityQueue.TryDequeue(out var nextItem, out _))
                {
                    item = nextItem;
                    _ = queue.TryDequeue(out _); // 保持两个队列同步
                }
            }
            finally
            {
                _channelSemaphores[channelIndex].Release();
            }

            if (item != null)
            {
                if (item.IsExpired || item.CancellationSource.Token.IsCancellationRequested)
                {
                    HandleCancelledItem(item);
                }
                else
                {
                    await ProcessItem(item);
                }
            }

            var elapsed = sw.ElapsedMilliseconds;
            if (elapsed < _defaultDelay)
            {
                await Task.Delay(_defaultDelay - (int)elapsed);
            }
        }
    }

    private void HandleCancelledItem(QueueItem item)
    {
        item.Status = QueueItemStatus.Cancelled;
        item.CompletionSource.TrySetCanceled();
        _itemTracker.TryRemove(item.Id, out _);
        Statistics.IncrementFailed();
    }

    private async Task ProcessItem(QueueItem item)
    {
        if (item.IsCancelled)
        {
            HandleCancelledItem(item);
            return;
        }

        var sw = Stopwatch.StartNew();
        item.Status = QueueItemStatus.Running;
        item.StartTime = DateTime.Now;

        try
        {
            using var timeoutCts = new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                item.CancellationSource.Token, 
                timeoutCts.Token,
                _cancellationToken.Token);

            if (item.Timeout.HasValue)
            {
                timeoutCts.CancelAfter(item.Timeout.Value);
            }

            var taskId = item.Id;
            if (!_itemTracker.ContainsKey(taskId))
            {
                throw new OperationCanceledException("任务已被移除");
            }

            try
            {
                await Task.Run(() => item.CallbackWithCancellation!(linkedCts.Token), linkedCts.Token);

                if (item.IsCancelled || !_itemTracker.ContainsKey(taskId))
                {
                    throw new OperationCanceledException("任务被取消");
                }

                item.Status = QueueItemStatus.Completed;
                item.CompletionSource.TrySetResult(true);
                Statistics.RecordProcessingTime(sw.ElapsedMilliseconds);
                OnItemCompleted(item);
            }
            catch (OperationCanceledException) 
            {
                throw;
            }
        }
        catch (OperationCanceledException ex)
        {
            HandleCancelledItem(item, ex.Message);
        }
        catch (Exception ex)
        {
            HandleFailedItem(item, ex);
        }
        finally
        {
            item.CompletionTime = DateTime.Now;
            _itemTracker.TryRemove(item.Id, out _);
        }
    }

    private void HandleCancelledItem(QueueItem item, string reason = "任务被取消")
    {
        item.Status = QueueItemStatus.Cancelled;
        item.CompletionSource.TrySetResult(false);
        Statistics.IncrementFailed();
        OnItemFailed(item, new OperationCanceledException(reason));
        Logger.Log(LogLevel.Warning, "QueuePool", $"任务 {item.Id} {reason}");
    }

    private void HandleFailedItem(QueueItem item, Exception ex)
    {
        item.Status = QueueItemStatus.Failed;
        item.CompletionSource.TrySetResult(false);
        Statistics.IncrementFailed();
        OnItemFailed(item, ex);
        Logger.Log(LogLevel.Error, "QueuePool", $"处理队列项 {item.Id} 时发生错误: {ex.Message}");
    }

    public IEnumerable<QueueItemInfo> GetQueueStatus()
    {
        return _itemTracker.Values.Select(item => new QueueItemInfo
        {
            Id = item.Id,
            Status = item.Status,
            EnqueueTime = item.EnqueueTime,
            StartTime = item.StartTime,
            CompletionTime = item.CompletionTime,
            Priority = item.Priority
        });
    }

    public void CancelItem(Guid itemId)
    {
        if (_itemTracker.TryGetValue(itemId, out var item))
        {
            item.Cancel();
            // 不在这里立即移除任务，让任务自然完成其生命周期
        }
    }

    protected virtual void OnItemCompleted(QueueItem item) => 
        ItemCompleted?.Invoke(this, new QueueItemEventArgs(item));

    protected virtual void OnItemFailed(QueueItem item, Exception ex) => 
        ItemFailed?.Invoke(this, new QueueItemEventArgs(item, ex));

    public void Stop()
    {
        _isRunning = false;
        _cancellationToken.Cancel();
    }

    public void Dispose()
    {
        Stop();
        foreach (var semaphore in _channelSemaphores)
        {
            semaphore.Dispose();
        }
        _cancellationToken.Dispose();
    }
}

public class QueueStatistics
{
    private long _totalEnqueued;
    private long _totalFailed;
    private readonly ConcurrentQueue<long> _processingTimes = new();
    private const int MaxStoredTimes = 1000;

    public long TotalEnqueued => _totalEnqueued;
    public long TotalFailed => _totalFailed;
    public double AverageProcessingTime => _processingTimes.Any() ? _processingTimes.Average() : 0;

    public void IncrementEnqueued() => Interlocked.Increment(ref _totalEnqueued);
    public void IncrementFailed() => Interlocked.Increment(ref _totalFailed);

    public void RecordProcessingTime(long milliseconds)
    {
        _processingTimes.Enqueue(milliseconds);
        while (_processingTimes.Count > MaxStoredTimes)
        {
            _processingTimes.TryDequeue(out _);
        }
    }
}

public class QueueItemEventArgs : System.EventArgs
{
    public QueueItem Item { get; }
    public Exception Exception { get; }

    public QueueItemEventArgs(QueueItem item, Exception? exception = null)
    {
        Item = item;
        Exception = exception ?? new Exception("No exception provided");
    }
}

public class QueueItemInfo
{
    public Guid Id { get; set; }
    public QueueItemStatus Status { get; set; }
    public DateTime EnqueueTime { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? CompletionTime { get; set; }
    public int Priority { get; set; }
}