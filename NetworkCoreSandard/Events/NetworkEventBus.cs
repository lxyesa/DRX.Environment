using System;
using System.Collections.Concurrent;
using NetworkCoreStandard.EventArgs;

namespace NetworkCoreStandard.Events;

public class NetworkEventBus
{
    private readonly ConcurrentDictionary<string, List<EventHandler<NetworkEventArgs>>> _eventHandlers = new();
    private readonly ConcurrentQueue<(string eventName, NetworkEventArgs args)> _eventQueue = new();
    private readonly SemaphoreSlim _eventSemaphore = new(1); // 用于控制事件处理的并发
    private bool _isProcessingEvents = false;
    private readonly CancellationTokenSource _processingCts = new();


    public NetworkEventBus()
    {
        StartEventProcessing();
    }

    ~NetworkEventBus()
    {
        StopEventProcessing();
        _eventSemaphore.Dispose();
        _processingCts.Dispose();
    }


    /// <summary>
    /// 开始事件队列处理
    /// </summary>
    public void StartEventProcessing()
    {
        if (!_isProcessingEvents)
        {
            _isProcessingEvents = true;
            _ = ProcessEventQueueAsync(_processingCts.Token);
        }
    }

    /// <summary>
    /// 停止事件队列处理
    /// </summary>
    public void StopEventProcessing()
    {
        _isProcessingEvents = false;
        _processingCts.Cancel();
    }

    /// <summary>
    /// 将事件添加到队列
    /// </summary>
    private void EnqueueEvent(string eventName, NetworkEventArgs args)
    {
        _eventQueue.Enqueue((eventName, args));
        if (_isProcessingEvents)
        {
            _eventSemaphore.Release(); // 通知处理线程有新事件
        }
    }

    /// <summary>
    /// 处理事件队列的异步任务
    /// </summary>
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

    /// <summary>
    /// 处理单个事件
    /// </summary>
    private async Task ProcessEventAsync(string eventName, NetworkEventArgs args)
    {
        if (_eventHandlers.TryGetValue(eventName, out List<EventHandler<NetworkEventArgs>>? handlers))
        {
            foreach (EventHandler<NetworkEventArgs>? handler in handlers.ToList())
            {
                try
                {
                    await Task.Run(() => handler.Invoke(args.Sender, args));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"执行事件处理器时发生错误: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 添加事件监听器
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="handler">事件处理器</param>
    public void AddListener(string eventName, EventHandler<NetworkEventArgs> handler)
    {
        _eventHandlers.AddOrUpdate(
            eventName,
            new List<EventHandler<NetworkEventArgs>> { handler },
            (_, existing) =>
            {
                existing.Add(handler);
                return existing;
            });
    }


    /// <summary>
    /// 移除事件监听器
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="handler">事件处理器</param>
    public void RemoveListener(string eventName, EventHandler<NetworkEventArgs> handler)
    {
        if (_eventHandlers.TryGetValue(eventName, out List<EventHandler<NetworkEventArgs>>? handlers))
        {
            handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                _eventHandlers.TryRemove(eventName, out _);
            }
        }
    }

    // /// <summary>
    // /// 异步触发事件
    // /// </summary>
    // /// <param name="eventName">事件名称</param>
    // /// <param name="args">事件参数</param>
    // public async Task RaiseEventAsync(string eventName, NetworkEventArgs args)
    // {
    //     if (_eventHandlers.TryGetValue(eventName, out List<EventHandler<NetworkEventArgs>>? handlers))
    //     {
    //         await Task.Run(() =>
    //         {
    //             foreach (EventHandler<NetworkEventArgs>? handler in handlers.ToList()) // 创建副本以防在迭代时修改集合
    //             {
    //                 try
    //                 {
    //                     handler.Invoke(args.Sender, args);
    //                 }
    //                 catch (Exception ex)
    //                 {
    //                     Console.WriteLine($"执行事件处理器时发生错误: {ex.Message}");
    //                 }
    //             }
    //         });
    //     }
    // }

    /// <summary>
    /// 异步触发事件，将事件添加到队列
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="args">事件参数</param>
    /// <returns></returns>
    public Task RaiseEventAsync(string eventName, NetworkEventArgs args)
    {
        EnqueueEvent(eventName, args);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 清除所有事件监听器
    /// </summary>
    public void ClearAllListeners()
    {
        _eventHandlers.Clear();
    }

    /// <summary>
    /// 清除指定事件的所有监听器
    /// </summary>
    /// <param name="eventName">事件名称</param>
    public void ClearEventListeners(string eventName)
    {
        _eventHandlers.TryRemove(eventName, out _);
    }
}