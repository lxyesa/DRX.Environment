using System;
using System.Collections.Concurrent;
using NetworkCoreStandard.Interface;
using NetworkCoreStandard.Managers;

namespace NetworkCoreStandard.Extensions;

public static class ComponentExtensions
{
    // 用于存储每个对象的组件管理器
    private static readonly ConcurrentDictionary<object, ComponentManager> _managers = new();

    // 扩展方法 - 添加组件
    public static T AddComponent<T>(this object owner) where T : IComponent, new()
    {
        var manager = _managers.GetOrAdd(owner, _ => new ComponentManager());
        manager.AddComponent<T>(owner);
        return manager.GetComponent<T>()!;
    }

    public static T AddComponent<T>(this object owner, T component) where T : IComponent
    {
        var manager = _managers.GetOrAdd(owner, _ => new ComponentManager());
        manager.AddComponent(owner, component);  // 需要在 ComponentManager 中添加对应的方法
        return manager.GetComponent<T>()!;
    }

    // 扩展方法 - 获取组件
    public static T? GetComponent<T>(this object owner) where T : IComponent
    {
        if (_managers.TryGetValue(owner, out var manager))
        {
            return manager.GetComponent<T>();
        }
        return default;
    }

    // 扩展方法 - 移除组件
    public static void RemoveComponent<T>(this object owner) where T : IComponent
    {
        if (_managers.TryGetValue(owner, out var manager))
        {
            manager.RemoveComponent<T>();
        }
    }

    // 扩展方法 - 判断是否有组件
    public static bool HasComponent<T>(this object owner) where T : IComponent
    {
        if (_managers.TryGetValue(owner, out var manager))
        {
            return manager.HasComponent<T>();
        }
        return false;
    }
}