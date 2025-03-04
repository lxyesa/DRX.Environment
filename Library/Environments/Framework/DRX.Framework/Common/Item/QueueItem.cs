namespace DRX.Framework.Common.Item;

public class QueueItem
{
    public Guid Id { get; } = Guid.NewGuid();
    public Action Callback { get; private set; }
    public int Priority { get; private set; }
    public DateTime EnqueueTime { get; private set; }
    public DateTime? StartTime { get; internal set; }
    public DateTime? CompletionTime { get; internal set; }
    public TimeSpan? Timeout { get; private set; }
    public CancellationTokenSource CancellationSource { get; } = new();
    public TaskCompletionSource<bool> CompletionSource { get; } = new();
    public QueueItemStatus Status { get; internal set; } = QueueItemStatus.Pending;

    public delegate void ActionWithCancellation(CancellationToken token);
    public ActionWithCancellation? CallbackWithCancellation { get; private set; }

    private volatile bool _isCancellationRequested;

    public QueueItem(Action callback, int priority, TimeSpan? timeout = null)
    {
        Callback = callback;
        CallbackWithCancellation = (token) =>
        {
            if (_isCancellationRequested || token.IsCancellationRequested)
            {
                throw new OperationCanceledException("任务已被取消");
            }

            callback();

            if (_isCancellationRequested || token.IsCancellationRequested)
            {
                throw new OperationCanceledException("任务已被取消");
            }
        };
        Priority = priority;
        EnqueueTime = DateTime.Now;
        Timeout = timeout;
    }

    public void Cancel()
    {
        _isCancellationRequested = true;
        CancellationSource.Cancel();
    }

    public bool IsCancelled => _isCancellationRequested || CancellationSource.Token.IsCancellationRequested;

    public bool IsExpired => Timeout.HasValue && DateTime.Now - EnqueueTime > Timeout.Value;
}

public enum QueueItemStatus
{
    Pending,
    Running,
    Completed,
    Cancelled,
    Failed
}