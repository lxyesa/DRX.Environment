using System;
using System.Collections.Concurrent;
using NetworkCoreStandard.Utils.Interface;

namespace NetworkCoreStandard.Utils.Common;

public abstract class DRXBehaviour
{
    private readonly ConcurrentDictionary<object, IComponent> _components = new();

    public T AddComponent<T>() where T : IComponent, new()
    {
        var component = new T();
        _ = _components.TryAdd(component, component);
        component.Owner = this;
        component.Awake();
        component.Start();
        return component;
    }

    public T AddComponent<T>(T component) where T : IComponent
    {
        _components.TryAdd(component, component);
        component.Owner = this;
        component.Awake();
        component.Start();
        return component;
    }

    public T? GetComponent<T>() where T : IComponent
    {
        if (_components.TryGetValue(typeof(T), out var component))
        {
            return (T)component;
        }
        return default;
    }

    public void RemoveComponent<T>() where T : IComponent
    {
        if (_components.TryRemove(typeof(T), out var component))
        {
            component.Dispose();
        }
    }

    public bool HasComponent<T>() where T : IComponent
    {
        return _components.ContainsKey(typeof(T));
    }

    protected virtual void OnDestroy()
    {
        foreach (var component in _components.Values)
        {
            component.Dispose();
        }
        _components.Clear();
    }
}