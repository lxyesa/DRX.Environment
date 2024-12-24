using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils.Interface;
using NetworkCoreStandard.Utils.Systems;
using System.Net.Sockets;

namespace NetworkCoreStandard.Utils.Common;

public class DRXSocket : Socket
{
    private readonly IComponentSystem _componentSystem;
    private readonly IEventSystem _eventSystem;
    private readonly ITaskSystem _taskSystem;

    public DRXSocket(SafeSocketHandle handle) : base(handle)
    {
        _componentSystem = new ComponentSystem(this);
        _eventSystem = new EventSystem();
        _taskSystem = new TaskSystem(this);
    }

    public DRXSocket(SocketInformation socketInformation) : base(socketInformation)
    {
        _componentSystem = new ComponentSystem(this);
        _eventSystem = new EventSystem();
        _taskSystem = new TaskSystem(this);
    }

    public DRXSocket(SocketType socketType, ProtocolType protocolType) : base(socketType, protocolType)
    {
        _componentSystem = new ComponentSystem(this);
        _eventSystem = new EventSystem();
        _taskSystem = new TaskSystem(this);
    }

    public DRXSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType) : base(addressFamily, socketType, protocolType)
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

}
