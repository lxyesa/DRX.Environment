using System;
using NetworkCoreStandard.Utils.Interface;
using NetworkCoreStandard.Utils.Systems;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Models;

namespace NetworkCoreStandard.Utils.Common;

public abstract class DRXBehaviour : IDisposable
{
    private readonly IComponentSystem _componentSystem;
    private readonly IEventSystem _eventSystem;
    private readonly ITaskSystem _taskSystem;

    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = typeof(DRXBehaviour).Name;

    protected DRXBehaviour()
    {
        _componentSystem = new ComponentSystem(this);
        _eventSystem = new EventSystem();
        _taskSystem = new TaskSystem(this);
    }

    //------------------------------------------------------------------------------ component system methods

    public virtual T AddComponent<T>() where T : IComponent, new() 
        => _componentSystem.AddComponent<T>();
    public virtual T AddComponent<T>(T component) where T : IComponent 
        => _componentSystem.AddComponent(component);
    public virtual T? GetComponent<T>() where T : IComponent 
        => _componentSystem.GetComponent<T>();
    public virtual bool HasComponent<T>() where T : IComponent 
        => _componentSystem.HasComponent<T>();
    public virtual void RemoveComponent<T>() where T : IComponent 
        => _componentSystem.RemoveComponent<T>();
    public virtual void RemoveComponent(IComponent component) 
        => _componentSystem.RemoveComponent(component);
    public virtual void RemoveAllComponents() 
        => _componentSystem.RemoveAllComponents();

    //------------------------------------------------------------------------------ event system methods

    public virtual void AddListener(string eventName, EventHandler<NetworkEventArgs> handler) 
        => _eventSystem.AddListener(eventName, handler);
    public virtual void RemoveListener(string eventName, EventHandler<NetworkEventArgs> handler) 
        => _eventSystem.RemoveListener(eventName, handler);
    public virtual Task PushEventAsync(string eventName, NetworkEventArgs args) 
        => _eventSystem.PushEventAsync(eventName, args);


    //------------------------------------------------------------------------------ task system methods

    public virtual string AddTask(Action action, int intervalMs, string taskName) 
        => _taskSystem.AddTask(action, intervalMs, taskName);

    public virtual bool PauseTask(string taskName) 
        => _taskSystem.PauseTask(taskName);

    public virtual bool ResumeTask(string taskName) 
        => _taskSystem.ResumeTask(taskName);

    public virtual void CancelTask(string taskName) 
        => _taskSystem.CancelTask(taskName);

    public virtual TickTaskState? GetTaskState(string taskName) 
        => _taskSystem.GetTaskState(taskName);

    
    protected virtual void OnDestroy()
    {
        _componentSystem.RemoveAllComponents();
        if (_eventSystem is IDisposable eventDisposable)
        {
            eventDisposable.Dispose();
        }
        if (_taskSystem is IDisposable taskDisposable)
        {
            taskDisposable.Dispose();
        }
    }

    public void Dispose()
    {
        OnDestroy();
    }
}