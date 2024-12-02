using System;
using System.Collections.Concurrent;
using NetworkCoreStandard.EventArgs;

namespace NetworkCoreStandard.Events;

public static class NetworkEventBus
{
    private static readonly ConcurrentDictionary<string, List<EventHandler<NetworkEventArgs>>> _eventHandlers = new();

    /// <summary>
    /// 添加事件监听器
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="handler">事件处理器</param>
    public static void AddListener(string eventName, EventHandler<NetworkEventArgs> handler)
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
    public static void RemoveListener(string eventName, EventHandler<NetworkEventArgs> handler)
    {
        if (_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                _eventHandlers.TryRemove(eventName, out _);
            }
        }
    }

    /// <summary>
    /// 异步触发事件
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="args">事件参数</param>
    public static async Task RaiseEventAsync(string eventName, NetworkEventArgs args)
    {
        if (_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            await Task.Run(() =>
            {
                foreach (var handler in handlers.ToList()) // 创建副本以防在迭代时修改集合
                {
                    try
                    {
                        handler.Invoke(null, args);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"执行事件处理器时发生错误: {ex.Message}");
                    }
                }
            });
        }
    }

    /// <summary>
    /// 清除所有事件监听器
    /// </summary>
    public static void ClearAllListeners()
    {
        _eventHandlers.Clear();
    }

    /// <summary>
    /// 清除指定事件的所有监听器
    /// </summary>
    /// <param name="eventName">事件名称</param>
    public static void ClearEventListeners(string eventName)
    {
        _eventHandlers.TryRemove(eventName, out _);
    }
}