using System.Diagnostics.CodeAnalysis;

namespace KaxSocket
{
    // 该文件用于向 ILLink/Trimmer 声明需要保留的类型（通过在方法上使用 DynamicDependency）
    // 把需要保留的 handler 类型列在下面即可，Trimmer 会根据这些注解保留相应的元数据。
    internal static class PreserveHandlers
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, "KaxSocket.Handlers.DLTBModPackerHttp", "KaxSocket")]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, "KaxSocket.Handlers.KaxHttp", "KaxSocket")]
        private static void Preserve() { /* 仅供 trimmer 识别，故意保持空 */ }
    }
}
