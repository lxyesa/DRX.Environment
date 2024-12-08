using NetworkCoreStandard.Utils.Interface;

namespace NetworkCoreStandard.Utils.Interface;

public interface IComponentSystem
{
    T AddComponent<T>() where T : IComponent, new();
    T AddComponent<T>(T component) where T : IComponent;
    T? GetComponent<T>() where T : IComponent;
    bool HasComponent<T>() where T : IComponent;
    void RemoveComponent<T>() where T : IComponent;
    void RemoveComponent(IComponent component);
    void RemoveAllComponents();
}
