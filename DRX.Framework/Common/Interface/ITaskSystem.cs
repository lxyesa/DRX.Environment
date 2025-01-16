using DRX.Framework.Common.Models;

namespace DRX.Framework.Common.Interface;

public interface ITaskSystem : IDisposable
{
    string AddTask(Action action, int intervalMs, string taskName);
    bool PauseTask(string taskName);
    bool ResumeTask(string taskName);
    void CancelTask(string taskName);
    bool TaskExists(string taskName);
    TickTaskState? GetTaskState(string taskName);
    void CancelAllTasks();
}