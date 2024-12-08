using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Events;
using NetworkCoreStandard.Utils.Common;
using NetworkCoreStandard.Utils.Extensions;

namespace NetworkCoreStandard;

public class NetworkObject : DRXBehaviour
{
    protected NetworkEventBus _eventBus;
    protected int GCInterval = 5 * 1000 * 60;
    public NetworkObject()
    {
        // AssemblyLoader.LoadEmbeddedAssemblies();
        _eventBus = new NetworkEventBus();
        _ = this.DoTickAsync(() =>
        {
            // 首先发布通知，告诉所有监听者垃圾回收即将执行
            _ = RaiseEventAsync("OnGC", new NetworkEventArgs(
                socket: null!,
                eventType: NetworkEventType.HandlerEvent,
                message: "执行垃圾回收"
            ));
            // 执行垃圾回收
            GC.Collect(
                generation: GC.MaxGeneration,
                mode: GCCollectionMode.Forced
            );
        }, GCInterval, "DefaultTickTask");
    }

    public virtual async Task RaiseEventAsync(string eventName, NetworkEventArgs args)
    {
        await _eventBus.RaiseEventAsync(eventName, args);
    }

    public void AddListener(string eventName, EventHandler<NetworkEventArgs> handler)
    {
        _eventBus.AddListener(eventName, handler);
    }
}
