using DRX.Framework.Common.Args;

namespace DRX.Framework.Common;

public class DRXNetworkObject : DrxBehaviour
{
    protected int GCInterval = 5 * 1000 * 60;
    public DRXNetworkObject()
    {
        // AssemblyLoader.LoadEmbeddedAssemblies();
        _ = this.AddTask(() =>
        {
            // 首先发布通知，告诉所有监听者垃圾回收即将执行
            _ = PushEventAsync("OnGC", new NetworkEventArgs(
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
