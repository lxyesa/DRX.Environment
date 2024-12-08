using System;
using System.Net.Sockets;
using NetworkCoreStandard.Utils.Interface;

namespace NetworkCoreStandard.Utils.Common;

public class DRXSocket : Socket
{
    private HashSet<IComponent> _components = new();
    public DRXSocket(SafeSocketHandle handle) : base(handle)
    {
    }

    public DRXSocket(SocketInformation socketInformation) : base(socketInformation)
    {
    }

    public DRXSocket(SocketType socketType, ProtocolType protocolType) : base(socketType, protocolType)
    {
    }

    public DRXSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType) : base(addressFamily, socketType, protocolType)
    {
    }

    public T AddComponent<T>() where T : IComponent, new()
    {
        if (HasComponent<T>())
        {
            throw new InvalidOperationException($"Component {typeof(T).Name} already exists");
        }
        var component = new T();
        _components.Add(component);
        component.Owner = this;
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
        component.Owner = this;
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
}
