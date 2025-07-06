using Keystone;
using System.Runtime.InteropServices;
using Architecture = System.Runtime.InteropServices.Architecture;

namespace Drx.Sdk.Memory.Utils;

internal class DllLoaderUtility
{
    private static string? _customDllPath;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    /// <summary>
    /// 设置Keystone库的自定义加载路径
    /// </summary>
    /// <param name="path">库文件所在的目录路径</param>
    public static void SetKeystoneDllPath(string path)
    {
        _customDllPath = path;
        PreloadKeystoneDll();
    }

    private static void PreloadKeystoneDll()
    {
        if (string.IsNullOrEmpty(_customDllPath))
            return;

        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            _ => throw new PlatformNotSupportedException($"不支持的架构: {RuntimeInformation.ProcessArchitecture}")
        };

        // 尝试第一种路径格式 (架构文件夹/keystone.dll)
        var dllPath = Path.Combine(_customDllPath, architecture, "keystone.dll");
        if (File.Exists(dllPath))
        {
            LoadLibrary(dllPath);
            return;
        }

        // 尝试第二种路径格式 (keystone.架构.dll)
        var alternateDllPath = Path.Combine(_customDllPath, $"keystone.{architecture}.dll");
        if (File.Exists(alternateDllPath))
            LoadLibrary(alternateDllPath);
    }

    /// <summary>
    /// 初始化Keystone库，确保正确加载
    /// </summary>
    public static void InitializeKeystone()
    {
        try
        {
            // 验证库是否正确加载
            using var engine = new Engine(Keystone.Architecture.X86, Mode.X32);
            engine.Assemble("nop", 0);
        }
        catch (DllNotFoundException ex)
        {
            PreloadKeystoneDll();
        }
    }
}