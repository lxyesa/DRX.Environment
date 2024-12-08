// NetworkCoreStandard.Extensions/TaskExtensions.cs
using System;
using System.Collections.Concurrent;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.EventArgs;

namespace NetworkCoreStandard.Utils.Extensions
{
    public static class TaskExtensions  
    {
        // 用于存储每个对象的任务状态
        private static readonly ConcurrentDictionary<object, Dictionary<string, TickTaskState>> _taskStates = new();

        // 添加任务 
        public static string DoTickAsync(this object owner, Action action, int intervalMs, string taskName)
        {
            var tasks = _taskStates.GetOrAdd(owner, _ => new Dictionary<string, TickTaskState>());
            
            if (tasks.ContainsKey(taskName))
            {
                throw new ArgumentException($"任务名称 {taskName} 已存在"); 
            }

            CancellationTokenSource cts = new();
            var state = new TickTaskState(taskName, cts, intervalMs);
            tasks[taskName] = state;

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (!state.IsPaused)
                        {
                            action();
                            await Task.Delay(state.RemainingTime, cts.Token);
                            state.RemainingTime = intervalMs;
                        }
                        else
                        {
                            await Task.Delay(100, cts.Token);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 任务被取消,正常退出
                }
                catch (Exception ex)
                {
                    // 错误处理 
                    Console.WriteLine($"任务执行错误: {ex.Message}");
                }
                finally
                {
                    _ = tasks.Remove(taskName);
                }
            }, cts.Token);

            return taskName;
        }

        // 暂停任务
        public static bool PauseTickTask(this object owner, string taskName)
        {
            if (_taskStates.TryGetValue(owner, out var tasks) &&
                tasks.TryGetValue(taskName, out var state) && !state.IsPaused)
            {
                state.IsPaused = true;
                state.PausedTime = DateTime.Now;
                state.RemainingTime = Math.Max(1, state.RemainingTime -
                    (int)(DateTime.Now - (state.PausedTime ?? DateTime.Now)).TotalMilliseconds);
                return true;
            }
            return false;
        }

        // 恢复任务
        public static bool ResumeTickTask(this object owner, string taskName)
        {
            if (_taskStates.TryGetValue(owner, out var tasks) &&
                tasks.TryGetValue(taskName, out var state) && state.IsPaused)
            {
                state.IsPaused = false;
                state.PausedTime = null;
                return true;
            }
            return false;
        }

        // 取消任务
        public static void CancelTickTask(this object owner, string taskName)
        {
            if (_taskStates.TryGetValue(owner, out var tasks) &&
                tasks.TryGetValue(taskName, out var state))
            {
                state.CancellationSource.Cancel();
                _ = tasks.Remove(taskName);
            }
        }

        // 获取任务状态
        public static TickTaskState? GetTickTaskState(this object owner, string taskName)
        {
            return _taskStates.TryGetValue(owner, out var tasks) && 
                   tasks.TryGetValue(taskName, out var state) ? state : null;
        }
    }
}