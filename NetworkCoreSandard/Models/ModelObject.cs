using System;
using NetworkCoreStandard.Interface;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard.Models;

public class ModelObject
{
    public HashSet<IComponent> Components { get; set; } = new HashSet<IComponent>();

    public ModelObject()
    {
    }

    public void AddComponent<T>() where T : IComponent, new()
    {
        Components.Add(new T());
        GetComponent<T>().Owner = this;
        // 在添加组件后调用Awake方法
        GetComponent<T>().Awake();
        // 在添加组件后调用Start方法
        GetComponent<T>().Start();
    }

    public void RemoveComponent<T>() where T : IComponent
    {
        Components.RemoveWhere(c => c.GetType() == typeof(T));
    }

    public T GetComponent<T>() where T : IComponent
    {
        return (T)Components.FirstOrDefault(c => c.GetType() == typeof(T));
    }

    public bool HasComponent<T>() where T : IComponent
    {
        return Components.Any(c => c.GetType() == typeof(T));
    }
}
