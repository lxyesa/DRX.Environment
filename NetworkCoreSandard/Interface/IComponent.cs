using System;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard.Interface;

public abstract class IComponent
{
    public abstract void Start();
    public abstract void Awake();
    public ModelObject? Owner { get; set; }

    public ModelObject GetOwner()
    {
        return Owner!;
    }

    ~IComponent()
    {
        Owner = null;
        GCManager.Instance.CollectGarbage();
    }
}
