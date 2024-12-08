using NetworkCoreStandard.Utils.Interface;
using NetworkCoreStandard.Utils.Systems;
using System.Net.Sockets;

namespace NetworkCoreStandard.Utils.Common;

public class DRXSocket : Socket
{
    private readonly IComponentSystem _componentSystem;

    public DRXSocket(SafeSocketHandle handle) : base(handle)
    {
        _componentSystem = new ComponentSystem(this);
    }

    public DRXSocket(SocketInformation socketInformation) : base(socketInformation)
    {
        _componentSystem = new ComponentSystem(this);
    }

    public DRXSocket(SocketType socketType, ProtocolType protocolType) : base(socketType, protocolType)
    {
        _componentSystem = new ComponentSystem(this);
    }

    public DRXSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType) : base(addressFamily, socketType, protocolType)
    {
        _componentSystem = new ComponentSystem(this);
    }

    // 组件系统方法
    public virtual T AddComponent<T>() where T : IComponent, new() => _componentSystem.AddComponent<T>();
    public virtual T AddComponent<T>(T component) where T : IComponent => _componentSystem.AddComponent(component);
    public virtual T? GetComponent<T>() where T : IComponent => _componentSystem.GetComponent<T>();
    public virtual bool HasComponent<T>() where T : IComponent => _componentSystem.HasComponent<T>();
    public virtual void RemoveComponent<T>() where T : IComponent => _componentSystem.RemoveComponent<T>();
    public virtual void RemoveComponent(IComponent component) => _componentSystem.RemoveComponent(component);
    public virtual void RemoveAllComponents() => _componentSystem.RemoveAllComponents();
}
