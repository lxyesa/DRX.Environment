using NetworkCoreStandard.Utils.Interface;
using NetworkCoreStandard.EventArgs;
using System.Collections.Concurrent;

namespace NetworkCoreStandard.Utils.Systems;

public class EventSystem : IEventSystem, IDisposable
{
    private readonly ConcurrentDictionary<string, List<EventHandler<NetworkEventArgs>>> _eventHandlers = new();
    private readonly ConcurrentQueue<(string eventName, NetworkEventArgs args)> _eventQueue = new();
    private readonly SemaphoreSlim _eventSemaphore = new(1);
    private bool _isProcessingEvents = false;
    private readonly CancellationTokenSource _processingCts = new();

    // 全局事件处理
    private static readonly ConcurrentDictionary<string, List<EventHandler<NetworkEventArgs>>> _globalEventHandlers = new();
    private static readonly object _globalLock = new();

    public EventSystem()
    {
        StartEventProcessing();
    }

    public void AddListener(string eventName, EventHandler<NetworkEventArgs> handler)
    {
        _ = _eventHandlers.AddOrUpdate(
            eventName,
            new List<EventHandler<NetworkEventArgs>> { handler },
            (_, existing) =>
            {
                existing.Add(handler);
                return existing;
            });
    }

    public void RemoveListener(string eventName, EventHandler<NetworkEventArgs> handler)
    {
        if (_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            _ = handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                _ = _eventHandlers.TryRemove(eventName, out _);
            }
        }
    }

    public Task PushEventAsync(string eventName, NetworkEventArgs args)
    {
        _eventQueue.Enqueue((eventName, args));
        if (_isProcessingEvents)
        {
            _ = _eventSemaphore.Release();
        }
        return Task.CompletedTask;
    }

    public void StartEventProcessing()
    {
        if (!_isProcessingEvents)
        {
            _isProcessingEvents = true;
            _ = ProcessEventQueueAsync(_processingCts.Token);
        }
    }

    public void StopEventProcessing()
    {
        _isProcessingEvents = false;
        _processingCts.Cancel();
    }

    private async Task ProcessEventQueueAsync(CancellationToken cancellationToken)
    {
        // ... 与原来的ProcessEventQueueAsync相同 ...
    }

    private async Task ProcessEventAsync(string eventName, NetworkEventArgs args)
    {
        // ... 与原来的ProcessEventAsync相同 ...
    }

    public void Dispose()
    {
        StopEventProcessing();
        _eventSemaphore.Dispose();
        _processingCts.Dispose();
    }
}
