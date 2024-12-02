using System;
using System.Runtime.CompilerServices;

namespace NetworkCoreStandard.Utils;

public static class DllInitializer
{
    // 静态构造函数方式
    static DllInitializer()
    {
        Initialize();
    }

#if NET5_0_OR_GREATER
    [ModuleInitializer] 
#endif
    internal static void Initialize()
    {
        try
        {
            // 在这里添加初始化代码
            Utils.AssemblyLoader.LoadEmbeddedAssemblies();

            // 可以添加其他初始化逻辑
            System.Diagnostics.Debug.WriteLine("NetworkCoreStandard DLL 已加载");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DLL初始化失败: {ex.Message}");
        }
    }
}
