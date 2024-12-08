using System;
using NetworkCoreStandard.Interface;

namespace NetworkCoreStandard.Managers;

public class ComponentManager
{
    private HashSet<IComponent> _components = new();

    public void AddComponent<T>(object owner) where T : IComponent, new()
    {
        var component = new T();
        component.Owner = owner;
        _ = _components.Add(component);
        component.Awake();
        component.Start();
    }

    public void AddComponent<T>(object owner, T component) where T : IComponent
    {
        component.Owner = owner;
        _ = _components.Add(component);
        component.Awake();
        component.Start();
    }

    public T? GetComponent<T>() where T : IComponent
    {
        return _components.OfType<T>().FirstOrDefault();
    }
}
