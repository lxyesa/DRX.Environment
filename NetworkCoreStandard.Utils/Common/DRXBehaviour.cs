using System;
using System.Collections.Concurrent;
using NetworkCoreStandard.Utils.Interface;
using NetworkCoreStandard.EventArgs;

namespace NetworkCoreStandard.Utils.Common;

public abstract class DRXBehaviour
{
    #region Component System
    private readonly HashSet<IComponent> _components = new();

    public T AddComponent<T>() where T : IComponent, new()
    {
        if (HasComponent<T>())
        {
            throw new InvalidOperationException($"Component {typeof(T).Name} already exists");
        }
        var component = new T();
        _components.Add(component);
        component.Owner = this;
        component.Awake();
        component.Start();
        return component;
    }

    public T AddComponent<T>(T component) where T : IComponent
    {
        if (HasComponent<T>())
        {
            throw new InvalidOperationException($"Component {typeof(T).Name} already exists");
        }
        _components.Add(component);
        component.Owner = this;
        component.Awake();
        component.Start();
        return component;
    }

    public T? GetComponent<T>() where T : IComponent
    {
        foreach (var component in _components)
        {
            if (component is T t)
            {
                return t;
            }
        }
        return default;
    }

    public bool HasComponent<T>() where T : IComponent
    {
        foreach (var component in _components)
        {
            if (component is T)
            {
                return true;
            }
        }
        return false;
    }

    public void RemoveComponent<T>() where T : IComponent
    {
        foreach (var component in _components)
        {
            if (component is T)
            {
                _components.Remove(component);
                component.Dispose();
                break;
            }
        }
    }

    public void RemoveComponent(IComponent component)
    {
        if (_components.Contains(component))
        {
            _components.Remove(component);
            component.Dispose();
        }
    }

    public void RemoveAllComponents()
    {
        foreach (var component in _components)
        {
            component.Dispose();
        }
        _components.Clear();
    }
    #endregion

    #region Event System
    private readonly ConcurrentDictionary<string, List<EventHandler<NetworkEventArgs>>> _eventHandlers = new();
    private readonly ConcurrentQueue<(string eventName, NetworkEventArgs args)> _eventQueue = new();
    private readonly SemaphoreSlim _eventSemaphore = new(1);
    private bool _isProcessingEvents = false;
    private readonly CancellationTokenSource _processingCts = new();

    // 全局事件处理
    private static readonly ConcurrentDictionary<string, List<EventHandler<NetworkEventArgs>>> _globalEventHandlers = new();
    private static readonly object _globalLock = new();

    protected DRXBehaviour()
    {
        StartEventProcessing();
    }

    ~DRXBehaviour()
    {
        StopEventProcessing();
        _eventSemaphore?.Dispose();
        _processingCts?.Dispose();
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

    public static void AddGlobalListener(string eventName, EventHandler<NetworkEventArgs> handler)
    {
        _ = _globalEventHandlers.AddOrUpdate(
            eventName,
            new List<EventHandler<NetworkEventArgs>> { handler },
            (_, existing) =>
            {
                lock (_globalLock)
                {
                    if (!existing.Contains(handler))
                    {
                        existing.Add(handler);
                    }
                    return existing;
                }
            });
    }

    public static void RemoveGlobalListener(string eventName, EventHandler<NetworkEventArgs> handler)
    {
        if (_globalEventHandlers.TryGetValue(eventName, out var handlers))
        {
            lock (_globalLock)
            {
                _ = handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    _ = _globalEventHandlers.TryRemove(eventName, out _);
                }
            }
        }
    }

    public Task RaiseEventAsync(string eventName, NetworkEventArgs args)
    {
        EnqueueEvent(eventName, args);
        return Task.CompletedTask;
    }

    private void StartEventProcessing()
    {
        if (!_isProcessingEvents)
        {
            _isProcessingEvents = true;
            _ = ProcessEventQueueAsync(_processingCts.Token);
        }
    }

    private void StopEventProcessing()
    {
        _isProcessingEvents = false;
        _processingCts.Cancel();
    }

    private void EnqueueEvent(string eventName, NetworkEventArgs args)
    {
        _eventQueue.Enqueue((eventName, args));
        if (_isProcessingEvents)
        {
            _ = _eventSemaphore.Release();
        }
    }

    private async Task ProcessEventQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _eventSemaphore.WaitAsync(cancellationToken);
                while (_eventQueue.TryDequeue(out var eventItem))
                {
                    await ProcessEventAsync(eventItem.eventName, eventItem.args);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理事件队列时发生错误: {ex.Message}");
            }
        }
    }

    private async Task ProcessEventAsync(string eventName, NetworkEventArgs args)
    {
        if (_eventHandlers.TryGetValue(eventName, out var instanceHandlers))
        {
            foreach (var handler in instanceHandlers.ToList())
            {
                try
                {
                    await Task.Run(() => handler.Invoke(args.Sender, args));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"执行实例事件处理器时发生错误: {ex.Message}");
                }
            }
        }

        if (_globalEventHandlers.TryGetValue(eventName, out var globalHandlers))
        {
            foreach (var handler in globalHandlers.ToList())
            {
                try
                {
                    await Task.Run(() => handler.Invoke(args.Sender, args));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"执行全局事件处理器时发生错误: {ex.Message}");
                }
            }
        }
    }
    #endregion

    #region Cleanup
    protected virtual void OnDestroy()
    {
        StopEventProcessing();
        _eventHandlers.Clear();
        foreach (var component in _components)
        {
            component.Dispose();
        }
        _components.Clear();
    }
    #endregion
}