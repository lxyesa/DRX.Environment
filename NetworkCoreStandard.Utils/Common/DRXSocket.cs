using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using NetworkCoreStandard.Utils.Interface;

namespace NetworkCoreStandard.Utils.Common;

public class DRXSocket : Socket, IDisposable
{
    private readonly DRXBehaviour _componentBehaviour = new ComponentBehaviourImplementation();

    // 委托组件方法
    public T AddComponent<T>() where T : IComponent, new() => _componentBehaviour.AddComponent<T>();
    public T AddComponent<T>(T component) where T : IComponent => _componentBehaviour.AddComponent(component);
    public T? GetComponent<T>() where T : IComponent => _componentBehaviour.GetComponent<T>();
    public void RemoveComponent<T>() where T : IComponent => _componentBehaviour.RemoveComponent<T>();
    public bool HasComponent<T>() where T : IComponent => _componentBehaviour.HasComponent<T>();


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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            (_componentBehaviour as IDisposable)?.Dispose();
        }
        base.Dispose(disposing);
    }

    private class ComponentBehaviourImplementation : DRXBehaviour { }
}
