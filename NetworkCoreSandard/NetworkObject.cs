using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Utils.Common;
using NetworkCoreStandard.Utils.Extensions;

namespace NetworkCoreStandard;

public class NetworkObject : DRXBehaviour
{
    protected int GCInterval = 5 * 1000 * 60;
    public NetworkObject()
    {
        // AssemblyLoader.LoadEmbeddedAssemblies();
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
}
