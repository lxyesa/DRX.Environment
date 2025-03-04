using System;

namespace DRX.Framework.Common.Interface;

public interface IComponent : IDisposable
{
    void Start();
    void Awake();
    object? Owner { get; set; }
    void OnDestroy(); // 组件销毁时调用
}