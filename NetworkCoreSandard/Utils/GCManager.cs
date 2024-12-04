using System;

namespace NetworkCoreStandard.Utils;

public class GCManager
{
    public static GCManager instance = new GCManager();

    public static GCManager Instance => instance;

    protected int _gcCount = 0;

    public void CollectGarbage()
    {
        _gcCount++;
    }
}
