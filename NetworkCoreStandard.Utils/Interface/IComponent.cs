using System;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard.Interface;

public interface IComponent
{
    void Start();
    void Awake();
    object? Owner { get; set; }
}

public interface IComponentContainer
{
    HashSet<IComponent> Components { get; }
    void AddComponent<T>() where T : IComponent, new();
    void RemoveComponent<T>() where T : IComponent;
    T GetComponent<T>() where T : IComponent;
    bool HasComponent<T>() where T : IComponent;
}