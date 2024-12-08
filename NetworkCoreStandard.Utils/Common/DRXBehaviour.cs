using System;
using NetworkCoreStandard.Utils.Interface;
using NetworkCoreStandard.Utils.Systems;
using NetworkCoreStandard.EventArgs;

namespace NetworkCoreStandard.Utils.Common;

public abstract class DRXBehaviour
{
    private readonly IComponentSystem _componentSystem;
    private readonly IEventSystem _eventSystem;

    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = typeof(DRXBehaviour).Name;

    protected DRXBehaviour()
    {
        _componentSystem = new ComponentSystem(this);
        _eventSystem = new EventSystem();
    }

    //
    // 组件系统方法
    //
    public virtual T AddComponent<T>() where T : IComponent, new() => _componentSystem.AddComponent<T>();
    public virtual T AddComponent<T>(T component) where T : IComponent => _componentSystem.AddComponent(component);
    public virtual T? GetComponent<T>() where T : IComponent => _componentSystem.GetComponent<T>();
    public virtual bool HasComponent<T>() where T : IComponent => _componentSystem.HasComponent<T>();
    public virtual void RemoveComponent<T>() where T : IComponent => _componentSystem.RemoveComponent<T>();
    public virtual void RemoveComponent(IComponent component) => _componentSystem.RemoveComponent(component);
    public virtual void RemoveAllComponents() => _componentSystem.RemoveAllComponents();

    //
    // 事件系统方法
    //
    public virtual void AddListener(string eventName, EventHandler<NetworkEventArgs> handler) => _eventSystem.AddListener(eventName, handler);
    public virtual void RemoveListener(string eventName, EventHandler<NetworkEventArgs> handler) => _eventSystem.RemoveListener(eventName, handler);
    public virtual Task PushEventAsync(string eventName, NetworkEventArgs args) => _eventSystem.PushEventAsync(eventName, args);

    protected virtual void OnDestroy()
    {
        _componentSystem.RemoveAllComponents();
        if (_eventSystem is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}