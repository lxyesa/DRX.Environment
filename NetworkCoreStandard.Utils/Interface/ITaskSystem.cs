using System;
using NetworkCoreStandard.Models;

namespace NetworkCoreStandard.Utils.Interface;

public interface ITaskSystem : IDisposable
{
    string AddTask(Action action, int intervalMs, string taskName);
    bool PauseTask(string taskName);
    bool ResumeTask(string taskName);
    void CancelTask(string taskName);
    TickTaskState? GetTaskState(string taskName);
    void CancelAllTasks();
}