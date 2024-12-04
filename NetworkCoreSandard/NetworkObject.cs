using System;
using System.Net.Sockets;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Events;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;
using NetworkEventHandlerDelegate = NetworkCoreStandard.Events.NetworkEventHandlerDelegate;

namespace NetworkCoreStandard;

public class ConnectionConfig
{
    public string IP { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8888;
    public int MaxClients { get; set; } = 100; // 最大客户端数
    public List<string> BlacklistIPs { get; set; } = new(); // IP黑名单
    public List<string> WhitelistIPs { get; set; } = new(); // IP白名单
    public Func<Socket, bool>? CustomValidator { get; set; } // 自定义验证
    public float TickRate { get; set; } = 20;
}

public class NetworkObject
{
    protected Dictionary<string, TickTaskState> _tickTasks = new();
    protected NetworkEventBus _eventBus;

    public NetworkObject()
    {
        AssemblyLoader.LoadEmbeddedAssemblies();
        _eventBus = new NetworkEventBus();
        DoTickAsync(() =>
        {
            // 首先发布通知，告诉所有监听者垃圾回收即将执行
            _ = RaiseEventAsync("OnGC", new NetworkEventArgs(
                socket: null!,
                eventType: NetworkEventType.HandlerEvent,
                message: "执行垃圾回收"
            ));
            // 执行垃圾回收
            GC.Collect(
                generation: GC.MaxGeneration,
                mode: GCCollectionMode.Forced
            );
        }, 5 * 1000 * 60, "DefaultTickTask");
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

        CancellationTokenSource cts = new CancellationTokenSource();
        TickTaskState state = new TickTaskState(taskName, cts, intervalMs);
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

        CancellationTokenSource cts = new CancellationTokenSource();
        TickTaskState state = new TickTaskState(taskName, cts, intervalMs)
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
        if (_tickTasks.TryGetValue(taskName, out TickTaskState? state) && !state.IsPaused)
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
        if (_tickTasks.TryGetValue(taskName, out TickTaskState? state) && state.IsPaused)
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
        if (_tickTasks.TryGetValue(taskName, out TickTaskState? state))
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
        _tickTasks.TryGetValue(taskName, out TickTaskState? state);
        return state;
    }

    public virtual async Task RaiseEventAsync(string eventName, NetworkEventArgs args)
    {
        await _eventBus.RaiseEventAsync(eventName, args);
    }

    public void AddListener(string eventName, EventHandler<NetworkEventArgs> handler)
    {
        _eventBus.AddListener(eventName, handler);
    }
}
