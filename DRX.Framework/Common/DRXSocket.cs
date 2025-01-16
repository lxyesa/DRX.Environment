using DRX.Framework.Common.Args;
using DRX.Framework.Common.Interface;
using DRX.Framework.Common.Systems;
using DRX.Framework.Models;
using System.Net.Sockets;

namespace DRX.Framework.Common;

public class DRXSocket : Socket
{
    private readonly IComponentSystem _componentSystem;
    private readonly IEventSystem _eventSystem;
    private readonly ITaskSystem _taskSystem;
    private readonly CommandSystem _commandSystem;

    public bool IsSelected { get; set; }

    public DRXSocket(SafeSocketHandle handle) : base(handle)
    {
        _componentSystem = new ComponentSystem(this);
        _eventSystem = new EventSystem();
        _taskSystem = new TaskSystem(this);
        _commandSystem = new CommandSystem(this);
    }

    public DRXSocket(SocketInformation socketInformation) : base(socketInformation)
    {
        _componentSystem = new ComponentSystem(this);
        _eventSystem = new EventSystem();
        _taskSystem = new TaskSystem(this);
        _commandSystem = new CommandSystem(this);
    }

    public DRXSocket(SocketType socketType, ProtocolType protocolType) : base(socketType, protocolType)
    {
        _componentSystem = new ComponentSystem(this);
        _eventSystem = new EventSystem();
        _taskSystem = new TaskSystem(this);
        _commandSystem = new CommandSystem(this);
    }

    public DRXSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType) : base(addressFamily, socketType, protocolType)
    {
        _componentSystem = new ComponentSystem(this);
        _eventSystem = new EventSystem();
        _taskSystem = new TaskSystem(this);
        _commandSystem = new CommandSystem(this);
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

    public virtual Guid AddListener(string eventName, EventHandler<NetworkEventArgs> handler)
        => _eventSystem.AddListener(eventName, handler);

    /// <summary>
    /// 添加事件监听器，支持唯一标识符以确保监听器的唯一性。
    /// </summary>
    /// <param name="eventName">事件名称。</param>
    /// <param name="handler">事件处理方法。</param>
    /// <param name="uniqueId">监听器的唯一标识符。</param>
    public virtual Guid AddListener(string eventName, EventHandler<NetworkEventArgs> handler, string uniqueId)
        => _eventSystem.AddListener(eventName, handler, uniqueId);

    public virtual void AddListener(uint eventId, EventHandler<NetworkEventArgs> handler)
        => _eventSystem.AddListener(eventId, handler);

    public virtual void RemoveListener(Guid handlerId)
        => _eventSystem.RemoveListener(handlerId);

    public virtual void RemoveListener(string eventName, Guid handlerId)
        => _eventSystem.RemoveListener(eventName, handlerId);

    public virtual Task PushEventAsync(string eventName, NetworkEventArgs args)
        => _eventSystem.PushEventAsync(eventName, args);

    public virtual Task PushEventAsync(uint eventId, NetworkEventArgs args)
        => _eventSystem.PushEventAsync(eventId, args);

    //------------------------------------------------------------------------------ task system methods

    public virtual string AddTask(Action action, int intervalMs, string taskName)
        => _taskSystem.AddTask(action, intervalMs, taskName);

    public virtual bool PauseTask(string taskName)
        => _taskSystem.PauseTask(taskName);

    public virtual bool ResumeTask(string taskName)
        => _taskSystem.ResumeTask(taskName);

    public virtual void CancelTask(string taskName)
        => _taskSystem.CancelTask(taskName);

    public virtual bool TaskExists(string taskName)
        => _taskSystem.TaskExists(taskName);

    public virtual TickTaskState? GetTaskState(string taskName)
        => _taskSystem.GetTaskState(taskName);

    //------------------------------------------------------------------------------ command system methods

    public virtual void RegisterCommand(string commandName, ICommand command)
        => _commandSystem.RegisterCommand(commandName, command);
    public virtual void UnregisterCommand(string commandName)
        => _commandSystem.UnregisterCommand(commandName);
    public virtual object ExecuteCommand(string commandName, object[] args, object executer)
        => _commandSystem.ExecuteCommand(commandName, args, executer);
    public virtual bool HasCommand(string commandName)
        => _commandSystem.HasCommand(commandName);
}