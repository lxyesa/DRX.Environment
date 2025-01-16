// NetworkCoreStandard.Utils/Systems/TaskSystem.cs
using DRX.Framework.Common.Interface;
using DRX.Framework.Models;
using System.Collections.Concurrent;

namespace DRX.Framework.Common.Systems
{
    public class TaskSystem : ITaskSystem
    {
        private readonly object _owner;
        private readonly ConcurrentDictionary<string, TickTaskState> _taskStates = new();
        private readonly CancellationTokenSource _globalCts = new();

        public TaskSystem(object owner)
        {
            _owner = owner;
        }

        public string AddTask(Action action, int intervalMs, string taskName)
        {
            if (_taskStates.ContainsKey(taskName))
            {
                throw new ArgumentException($"任务名称 {taskName} 已存在");
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token);
            var state = new TickTaskState(taskName, cts, intervalMs);

            if (!_taskStates.TryAdd(taskName, state))
            {
                throw new InvalidOperationException($"无法添加任务 {taskName}");
            }

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
                    // 任务被取消，正常退出
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "TaskSystem", $"任务执行错误: {ex.Message}");
                }
                finally
                {
                    _ = _taskStates.TryRemove(taskName, out _);
                }
            }, cts.Token);

            return taskName;
        }

        public bool TaskExists(string taskName)
        {
            return _taskStates.ContainsKey(taskName);
        }

        public bool PauseTask(string taskName)
        {
            if (_taskStates.TryGetValue(taskName, out var state) && !state.IsPaused)
            {
                state.IsPaused = true;
                state.PausedTime = DateTime.Now;
                state.RemainingTime = Math.Max(1, state.RemainingTime -
                    (int)(DateTime.Now - (state.PausedTime ?? DateTime.Now)).TotalMilliseconds);
                return true;
            }
            return false;
        }

        public bool ResumeTask(string taskName)
        {
            if (_taskStates.TryGetValue(taskName, out var state) && state.IsPaused)
            {
                state.IsPaused = false;
                state.PausedTime = null;
                return true;
            }
            return false;
        }

        public void CancelTask(string taskName)
        {
            if (_taskStates.TryGetValue(taskName, out var state))
            {
                state.CancellationSource.Cancel();
                _ = _taskStates.TryRemove(taskName, out _);
            }
        }

        public TickTaskState? GetTaskState(string taskName)
        {
            return _taskStates.TryGetValue(taskName, out var state) ? state : null;
        }

        public void CancelAllTasks()
        {
            _globalCts.Cancel();
            _taskStates.Clear();
        }

        public void Dispose()
        {
            CancelAllTasks();
            _globalCts.Dispose();
        }
    }
}