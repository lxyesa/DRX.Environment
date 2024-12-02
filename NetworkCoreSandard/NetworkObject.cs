using System;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Events;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;
using NetworkEventHandlerDelegate = NetworkCoreStandard.Events.NetworkEventHandlerDelegate;

namespace NetworkCoreStandard;

public class NetworkObject
{
    protected Dictionary<string, TickTaskState> _tickTasks = new();
    protected NetworkEventManager _eventManager;
    

    public NetworkObject()
    {
        AssemblyLoader.LoadEmbeddedAssemblies();
        _eventManager = new NetworkEventManager();
    }

    /// <summary>
    /// 异步执行定时任务
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <param name="intervalMs">执行间隔(毫秒)</param>
    /// <param name="taskName">任务名称</param>
    /// <returns>任务名称</returns>
    public virtual string DoTickAsync(Action action, int intervalMs, string taskName)
    {
        if (_tickTasks.ContainsKey(taskName))
        {
            throw new ArgumentException($"任务名称 {taskName} 已存在");
        }

        var cts = new CancellationTokenSource();
        var state = new TickTaskState(taskName, cts, intervalMs);
        _tickTasks[taskName] = state;

        Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (!state.IsPaused)
                    {
                        action();
                        await Task.Delay(state.RemainingTime, cts.Token);
                        state.RemainingTime = intervalMs; // 重置计时器
                    }
                    else
                    {
                        await Task.Delay(100, cts.Token); // 暂停时小睡
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 任务被取消,正常退出
            }
            catch (Exception ex)
            {
                _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                    socket: null!,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"定时任务执行出错: {ex.Message}"
                ));
            }
            finally
            {
                _tickTasks.Remove(taskName);
            }
        }, cts.Token);

        return taskName;
    }

    /// <summary>
    /// 异步执行定时任务，可指定执行次数
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <param name="intervalMs">执行间隔(毫秒)</param>
    /// <param name="count">执行次数，小于等于0表示无限执行</param>
    /// <param name="canPause">任务是否可以暂停。为false时任务完成后直接销毁</param>
    /// <param name="taskName">任务名称</param>
    /// <returns>任务名称</returns>
    public virtual string DoTickAsync(Action action, int intervalMs, int count, bool canPause, string taskName)
    {
        if (_tickTasks.ContainsKey(taskName))
        {
            throw new ArgumentException($"任务名称 {taskName} 已存在");
        }

        var cts = new CancellationTokenSource();
        var state = new TickTaskState(taskName, cts, intervalMs)
        {
            MaxCount = count,
            CurrentCount = 0,
            CanPause = canPause
        };
        _tickTasks[taskName] = state;

        Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (!state.IsPaused)
                    {
                        // 检查是否达到执行次数限制
                        if (state.MaxCount > 0 && state.CurrentCount >= state.MaxCount)
                        {
                            if (canPause)
                            {
                                // 重置计数和计时器，并暂停任务
                                state.CurrentCount = 0;
                                state.RemainingTime = intervalMs;
                                state.IsPaused = true;
                                continue;
                            }
                            else
                            {
                                // 直接取消并移除任务
                                cts.Cancel();
                                break;
                            }
                        }

                        action();
                        state.CurrentCount++;
                        await Task.Delay(state.RemainingTime, cts.Token);
                        state.RemainingTime = intervalMs; // 重置计时器
                    }
                    else
                    {
                        await Task.Delay(100, cts.Token); // 暂停时小睡
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 任务被取消，正常退出
            }
            catch (Exception ex)
            {
                _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                    socket: null!,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"定时任务执行出错: {ex.Message}"
                ));
            }
            finally
            {
                _tickTasks.Remove(taskName);
            }
        }, cts.Token);

        return taskName;
    }

    /// <summary>
    /// 暂停定时任务
    /// </summary>
    /// <param name="taskName">任务名称</param>
    /// <returns>是否成功暂停</returns>
    public virtual bool PauseTickTask(string taskName)
    {
        if (_tickTasks.TryGetValue(taskName, out var state) && !state.IsPaused)
        {
            state.IsPaused = true;
            state.PausedTime = DateTime.Now;
            state.RemainingTime = Math.Max(1, state.RemainingTime -
                (int)(DateTime.Now - (state.PausedTime ?? DateTime.Now)).TotalMilliseconds);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 恢复定时任务
    /// </summary>
    /// <param name="taskName">任务名称</param>
    /// <returns>是否成功恢复</returns>
    public virtual bool ResumeTickTask(string taskName)
    {
        if (_tickTasks.TryGetValue(taskName, out var state) && state.IsPaused)
        {
            state.IsPaused = false;
            state.PausedTime = null;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 取消定时任务
    /// </summary>
    /// <param name="taskName">任务名称</param>
    public virtual void CancelTickTask(string taskName)
    {
        if (_tickTasks.TryGetValue(taskName, out var state))
        {
            state.CancellationSource.Cancel();
            _tickTasks.Remove(taskName);
        }
    }

    /// <summary>
    /// 获取定时任务状态
    /// </summary>
    /// <param name="taskName">任务名称</param>
    /// <returns>任务状态，如果任务不存在则返回null</returns>
    public virtual TickTaskState? GetTickTaskState(string taskName)
    {
        _tickTasks.TryGetValue(taskName, out var state);
        return state;
    }

    [Obsolete("事件订阅方法已不受支持，新版本将不再会有新的订阅事件，建议使用事件处理器")]
    public virtual void SubscribeNetworkEvent(NetworkEventHandlerDelegate handler)
    {
        _eventManager.OnNetworkEvent += handler;
    }

    [Obsolete("事件订阅方法已不受支持，新版本将不再会有新的订阅事件，建议使用事件处理器")]
    public virtual void SubscribeErrorEvent(NetworkErrorHandler handler)
    {
        _eventManager.OnNetworkError += handler;
    }

    [Obsolete("事件订阅方法已不受支持，新版本将不再会有新的订阅事件，建议使用事件处理器")]
    public virtual void UnsubscribeNetworkEvent(NetworkEventHandlerDelegate handler)
    {
        _eventManager.OnNetworkEvent -= handler;
    }

    [Obsolete("事件订阅方法已不受支持，新版本将不再会有新的订阅事件，建议使用事件处理器")]
    public virtual void UnsubscribeErrorEvent(NetworkErrorHandler handler)
    {
        _eventManager.OnNetworkError -= handler;
    }

    public virtual async Task RaiseEventAsync(string eventName, NetworkEventArgs args)
    {
        await NetworkEventBus.RaiseEventAsync(eventName, args);
    }
}
