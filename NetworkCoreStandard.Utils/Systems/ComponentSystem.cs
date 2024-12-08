using NetworkCoreStandard.Utils.Interface;
using System;

namespace NetworkCoreStandard.Utils.Systems;

public class ComponentSystem : IComponentSystem
{
    private readonly HashSet<IComponent> _components = new();
    private readonly object _owner;

    public ComponentSystem(object owner)
    {
        _owner = owner;
    }

    public T AddComponent<T>() where T : IComponent, new()
    {
        if (HasComponent<T>())
        {
            throw new InvalidOperationException($"Component {typeof(T).Name} already exists");
        }
        var component = new T();
        _components.Add(component);
        component.Owner = _owner;
        component.Awake();
        component.Start();
        return component;
    }

    public T AddComponent<T>(T component) where T : IComponent
    {
        if (HasComponent<T>())
        {
            throw new InvalidOperationException($"Component {typeof(T).Name} already exists");
        }
        _components.Add(component);
        component.Owner = _owner;
        component.Awake();
        component.Start();
        return component;
    }

    public T? GetComponent<T>() where T : IComponent
    {
        foreach (var component in _components)
        {
            if (component is T t)
            {
                return t;
            }
        }
        return default;
    }

    public bool HasComponent<T>() where T : IComponent
    {
        foreach (var component in _components)
        {
            if (component is T)
            {
                return true;
            }
        }
        return false;
    }

    public void RemoveComponent<T>() where T : IComponent
    {
        foreach (var component in _components)
        {
            if (component is T)
            {
                _components.Remove(component);
                component.Dispose();
                break;
            }
        }
    }

    public void RemoveComponent(IComponent component)
    {
        if (_components.Contains(component))
        {
            _components.Remove(component);
            component.Dispose();
        }
    }

    public void RemoveAllComponents()
    {
        foreach (var component in _components)
        {
            component.Dispose();
        }
        _components.Clear();
    }
}
