using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Drx.Sdk.Native;
using Drx.Sdk.Script;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Interfaces;

namespace Drx.Sdk.Memory;

[ScriptClass("MemorySearcher")]
public static class MemorySearcher
{
    // 内存保护标志常量
    private const uint MEM_COMMIT = 0x1000;
    private const uint PAGE_READWRITE = 0x04;
    private const uint PAGE_READONLY = 0x02;
    private const uint PAGE_EXECUTE_READ = 0x20;

    /// <summary>
    /// 通过进程名获取进程句柄
    /// </summary>
    /// <param name="processName">进程名，可包含.exe后缀或不包含</param>
    /// <returns>进程句柄和进程对象</returns>
    private static (IntPtr handle, Process process) GetProcessHandleByName(string processName)
    {
        // 如果进程名包含.exe后缀，则移除它
        if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            processName = processName.Substring(0, processName.Length - 4);
        }
        
        // 获取所有指定名称的进程
        Process[] processes = Process.GetProcessesByName(processName);
        
        if (processes.Length == 0)
        {
            throw new ArgumentException($"无法找到进程：{processName}");
        }
        
        // 使用第一个找到的进程
        Process process = processes[0];
        return (process.Handle, process);
    }

    /// <summary>
    /// 获取当前进程的句柄
    /// </summary>
    private static IntPtr GetCurrentProcessHandle()
    {
        return Kernel32.GetCurrentProcess();
    }

    #region 字节搜索方法
    /// <summary>
    /// 在指定进程的整个内存空间中搜索字节模式
    /// </summary>
    /// <param name="processHandle">进程句柄</param>
    /// <param name="pattern">要搜索的字节模式</param>
    /// <returns>匹配该模式的内存地址集合</returns>
    public static IEnumerable<IntPtr> SearchBytes(IntPtr processHandle, byte[] pattern)
    {
        var systemInfo = new Kernel32.SYSTEM_INFO();
        Kernel32.GetSystemInfo(ref systemInfo);

        var currentAddress = systemInfo.minimumApplicationAddress;
        var maxAddress = systemInfo.maximumApplicationAddress;

        Console.WriteLine($"开始搜索模式，长度为 {pattern.Length} 字节");
        Console.WriteLine($"搜索范围: 0x{currentAddress.ToInt64():X16} - 0x{maxAddress.ToInt64():X16}");

        return SearchMemoryRange(processHandle, currentAddress, maxAddress, pattern);
    }
    
    /// <summary>
    /// 在指定进程名的进程的整个内存空间中搜索字节模式
    /// </summary>
    /// <param name="processName">进程名</param>
    /// <param name="pattern">要搜索的字节模式</param>
    /// <returns>匹配该模式的内存地址集合</returns>
    public static IEnumerable<IntPtr> SearchBytes(string processName, byte[] pattern)
    {
        var (handle, _) = GetProcessHandleByName(processName);
        return SearchBytes(handle, pattern);
    }
    
    /// <summary>
    /// 在当前进程的内存空间中搜索字节模式
    /// </summary>
    /// <param name="pattern">要搜索的字节模式</param>
    /// <returns>匹配该模式的内存地址集合</returns>
    public static IEnumerable<IntPtr> SearchBytes(byte[] pattern)
    {
        var handle = GetCurrentProcessHandle();
        return SearchBytes(handle, pattern);
    }
    #endregion
    
    #region 模块内字节搜索方法
    /// <summary>
    /// 在指定进程的特定模块中搜索字节模式
    /// </summary>
    /// <param name="processHandle">进程句柄</param>
    /// <param name="processId">进程ID</param>
    /// <param name="pattern">要搜索的字节模式</param>
    /// <param name="moduleName">模块名称</param>
    /// <returns>匹配该模式的内存地址集合</returns>
    public static IEnumerable<IntPtr> SearchBytesInModule(IntPtr processHandle, int processId, byte[] pattern, string moduleName)
    {
        // 获取目标进程中的模块
        System.Diagnostics.Process? sysProcess = null;
        try
        {
            // 获取目标进程的系统Process对象
            sysProcess = System.Diagnostics.Process.GetProcessById(processId);

            // 查找指定名称的模块
            System.Diagnostics.ProcessModule? module = null;
            foreach (System.Diagnostics.ProcessModule mod in sysProcess.Modules)
            {
                if (string.Equals(mod.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
                {
                    module = mod;
                    break;
                }
            }

            if (module == null)
            {
                Console.WriteLine($"无法找到模块: {moduleName}");
                yield return ThrowNoResultsException();
                yield break;
            }

            IntPtr startAddress = module.BaseAddress;
            int moduleSize = module.ModuleMemorySize;
            IntPtr endAddress = new IntPtr(startAddress.ToInt64() + moduleSize);

#if DEBUG
            Console.WriteLine($"开始在模块 {moduleName} 中搜索");
            Console.WriteLine($"模块基址: 0x{startAddress.ToInt64():X16}");
            Console.WriteLine($"模块大小: 0x{moduleSize:X8} ({moduleSize} 字节)");
            Console.WriteLine($"搜索范围: 0x{startAddress.ToInt64():X16} - 0x{endAddress.ToInt64():X16}");
#endif

            foreach (var result in SearchMemoryRange(processHandle, startAddress, endAddress, pattern))
            {
                yield return result;
            }
        }
        finally
        {
            sysProcess?.Dispose();
        }
    }
    
    /// <summary>
    /// 在指定进程的特定模块中搜索字节模式
    /// </summary>
    /// <param name="processName">进程名</param>
    /// <param name="pattern">要搜索的字节模式</param>
    /// <param name="moduleName">模块名称</param>
    /// <returns>匹配该模式的内存地址集合</returns>
    public static IEnumerable<IntPtr> SearchBytesInModule(string processName, byte[] pattern, string moduleName)
    {
        var (handle, process) = GetProcessHandleByName(processName);
        return SearchBytesInModule(handle, process.Id, pattern, moduleName);
    }
    
    /// <summary>
    /// 在当前进程的特定模块中搜索字节模式
    /// </summary>
    /// <param name="pattern">要搜索的字节模式</param>
    /// <param name="moduleName">模块名称</param>
    /// <returns>匹配该模式的内存地址集合</returns>
    public static IEnumerable<IntPtr> SearchBytesInModule(byte[] pattern, string moduleName)
    {
        var handle = GetCurrentProcessHandle();
        int processId = Process.GetCurrentProcess().Id;
        return SearchBytesInModule(handle, processId, pattern, moduleName);
    }
    #endregion

    /// <summary>
    /// 在指定内存范围内搜索字节模式
    /// </summary>
    private static IEnumerable<IntPtr> SearchMemoryRange(IntPtr processHandle, IntPtr startAddress, IntPtr endAddress, byte[] pattern)
    {
        bool foundAnyResult = false;
        int regionsSearched = 0;
        var currentAddress = startAddress;

        while (currentAddress.ToInt64() < endAddress.ToInt64())
        {
            var memInfo = new Kernel32.MEMORY_BASIC_INFORMATION();
            if (Kernel32.VirtualQueryEx(processHandle, currentAddress, ref memInfo, (uint)Marshal.SizeOf<Kernel32.MEMORY_BASIC_INFORMATION>()) == 0)
            {
                Debug.WriteLine($"无法查询内存区域: 0x{currentAddress.ToInt64():X16}");
                break;
            }

            // 检查这个内存区域是否在搜索范围内
            if (memInfo.BaseAddress.ToInt64() >= endAddress.ToInt64())
                break;

#if DEBUG
            Console.WriteLine($"搜索内存区域: 0x{memInfo.BaseAddress.ToInt64():X16} - 0x{memInfo.BaseAddress.ToInt64() + memInfo.RegionSize.ToInt64():X16}, 保护: 0x{memInfo.Protect:X}");
#endif

            // 检查内存区域是否可读
            if (IsMemoryRegionReadable(memInfo))
            {
                regionsSearched++;

                foreach (var result in SearchMemoryRegion(processHandle, memInfo, pattern))
                {
                    foundAnyResult = true;
                    yield return result;
                }
            }

            currentAddress = new IntPtr(memInfo.BaseAddress.ToInt64() + memInfo.RegionSize.ToInt64());
        }

#if DEBUG
        Console.WriteLine($"搜索完成，共搜索 {regionsSearched} 个内存区域");
#endif

        if (!foundAnyResult)
        {
            yield return ThrowNoResultsException();
        }
    }

    /// <summary>
    /// 判断内存区域是否可读
    /// </summary>
    private static bool IsMemoryRegionReadable(Kernel32.MEMORY_BASIC_INFORMATION memInfo)
    {
        return memInfo.State == MEM_COMMIT &&
               (memInfo.Protect == PAGE_READWRITE ||
                memInfo.Protect == PAGE_READONLY ||
                memInfo.Protect == PAGE_EXECUTE_READ);
    }

    /// <summary>
    /// 在单个内存区域中搜索字节模式
    /// </summary>
    private static IEnumerable<IntPtr> SearchMemoryRegion(IntPtr processHandle, Kernel32.MEMORY_BASIC_INFORMATION memInfo, byte[] pattern)
    {
        List<IntPtr> results = new List<IntPtr>();
        try
        {
            var buffer = MemoryReader.ReadMemory(processHandle, memInfo.BaseAddress, memInfo.RegionSize.ToInt32());

            for (var i = 0; i <= buffer.Length - pattern.Length; i++)
            {
                if (PatternMatchesAt(buffer, i, pattern))
                {
                    var resultAddress = new IntPtr(memInfo.BaseAddress.ToInt64() + i);
                    Console.WriteLine($"找到匹配: 0x{resultAddress.ToInt64():X16}");
                    results.Add(resultAddress);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取内存区域失败，地址: 0x{memInfo.BaseAddress.ToInt64():X16}: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// 检查字节缓冲区中的指定位置是否与模式匹配
    /// </summary>
    private static bool PatternMatchesAt(byte[] buffer, int offset, byte[] pattern)
    {
        for (var j = 0; j < pattern.Length; j++)
        {
            if (buffer[offset + j] != pattern[j])
            {
                return false;
            }
        }
        return true;
    }

    private static IntPtr ThrowNoResultsException()
    {
        throw new InvalidOperationException("未找到匹配的字节模式。");
    }

    /// <summary>
    /// 将字符串模式转换为字节数组和掩码数组
    /// </summary>
    /// <param name="pattern">形如 "48 8B F0 74 ** 48 8B 44 24 48" 的字符串模式</param>
    /// <returns>字节数组和掩码数组的元组，掩码数组中 true 表示需要匹配，false 表示通配符</returns>
    private static (byte[] bytes, bool[] masks) ParsePatternString(string pattern)
    {
        string[] parts = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        byte[] bytes = new byte[parts.Length];
        bool[] masks = new bool[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "**" || parts[i] == "??")
            {
                bytes[i] = 0;
                masks[i] = false; // 不需要匹配
            }
            else
            {
                bytes[i] = Convert.ToByte(parts[i], 16);
                masks[i] = true; // 需要匹配
            }
        }

        return (bytes, masks);
    }

    /// <summary>
    /// 使用带掩码的模式匹配算法检查是否匹配
    /// </summary>
    private static bool PatternMatchesWithMask(byte[] buffer, int offset, byte[] pattern, bool[] masks)
    {
        for (var j = 0; j < pattern.Length; j++)
        {
            // 如果掩码为true，则需要匹配；如果为false，表示通配符，跳过匹配
            if (masks[j] && buffer[offset + j] != pattern[j])
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 在内存区域中搜索带掩码的字节模式
    /// </summary>
    private static IEnumerable<IntPtr> SearchMemoryRegionWithMask(IntPtr processHandle, Kernel32.MEMORY_BASIC_INFORMATION memInfo, byte[] pattern, bool[] masks)
    {
        List<IntPtr> results = new List<IntPtr>();
        try
        {
            var buffer = MemoryReader.ReadMemory(processHandle, memInfo.BaseAddress, memInfo.RegionSize.ToInt32());

            for (var i = 0; i <= buffer.Length - pattern.Length; i++)
            {
                if (PatternMatchesWithMask(buffer, i, pattern, masks))
                {
                    var resultAddress = new IntPtr(memInfo.BaseAddress.ToInt64() + i);
                    Console.WriteLine($"找到匹配: 0x{resultAddress.ToInt64():X16}");
                    results.Add(resultAddress);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取内存区域失败，地址: 0x{memInfo.BaseAddress.ToInt64():X16}: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// 在指定内存范围内搜索带掩码的字节模式
    /// </summary>
    private static IEnumerable<IntPtr> SearchMemoryRangeWithMask(IntPtr processHandle, IntPtr startAddress, IntPtr endAddress, byte[] pattern, bool[] masks)
    {
        bool foundAnyResult = false;
        int regionsSearched = 0;
        var currentAddress = startAddress;

        while (currentAddress.ToInt64() < endAddress.ToInt64())
        {
            var memInfo = new Kernel32.MEMORY_BASIC_INFORMATION();
            if (Kernel32.VirtualQueryEx(processHandle, currentAddress, ref memInfo, (uint)Marshal.SizeOf<Kernel32.MEMORY_BASIC_INFORMATION>()) == 0)
            {
                Debug.WriteLine($"无法查询内存区域: 0x{currentAddress.ToInt64():X16}");
                break;
            }

            // 检查这个内存区域是否在搜索范围内
            if (memInfo.BaseAddress.ToInt64() >= endAddress.ToInt64())
                break;

#if DEBUG
            Console.WriteLine($"搜索内存区域: 0x{memInfo.BaseAddress.ToInt64():X16} - 0x{memInfo.BaseAddress.ToInt64() + memInfo.RegionSize.ToInt64():X16}, 保护: 0x{memInfo.Protect:X}");
#endif

            // 检查内存区域是否可读
            if (IsMemoryRegionReadable(memInfo))
            {
                regionsSearched++;

                foreach (var result in SearchMemoryRegionWithMask(processHandle, memInfo, pattern, masks))
                {
                    foundAnyResult = true;
                    yield return result;
                }
            }

            currentAddress = new IntPtr(memInfo.BaseAddress.ToInt64() + memInfo.RegionSize.ToInt64());
        }

#if DEBUG
        Console.WriteLine($"搜索完成，共搜索 {regionsSearched} 个内存区域");
#endif

        if (!foundAnyResult)
        {
            yield return ThrowNoResultsException();
        }
    }

    #region 字符串模式搜索方法
    /// <summary>
    /// 在指定进程的整个内存空间中搜索字符串模式
    /// </summary>
    /// <param name="processHandle">进程句柄</param>
    /// <param name="patternString">要搜索的字符串模式，例如 "48 8B F0 74 ** 48 8B 44 24 48"</param>
    /// <returns>匹配该模式的内存地址集合</returns>
    public static IEnumerable<IntPtr> SearchPattern(IntPtr processHandle, string patternString)
    {
        var (pattern, masks) = ParsePatternString(patternString);

        var systemInfo = new Kernel32.SYSTEM_INFO();
        Kernel32.GetSystemInfo(ref systemInfo);

        var currentAddress = systemInfo.minimumApplicationAddress;
        var maxAddress = systemInfo.maximumApplicationAddress;

        Console.WriteLine($"开始搜索模式，长度为 {pattern.Length} 字节");
        Console.WriteLine($"搜索范围: 0x{currentAddress.ToInt64():X16} - 0x{maxAddress.ToInt64():X16}");

        return SearchMemoryRangeWithMask(processHandle, currentAddress, maxAddress, pattern, masks);
    }

    /// <summary>
    /// 在指定进程名的进程的整个内存空间中搜索字符串模式
    /// </summary>
    /// <param name="processName">进程名</param>
    /// <param name="patternString">要搜索的字符串模式，例如 "48 8B F0 74 ** 48 8B 44 24 48"</param>
    /// <returns>匹配该模式的内存地址集合</returns>
    public static IEnumerable<IntPtr> SearchPattern(string processName, string patternString)
    {
        var (handle, _) = GetProcessHandleByName(processName);
        return SearchPattern(handle, patternString);
    }

    /// <summary>
    /// 在当前进程的内存空间中搜索字符串模式
    /// </summary>
    /// <param name="patternString">要搜索的字符串模式，例如 "48 8B F0 74 ** 48 8B 44 24 48"</param>
    /// <returns>匹配该模式的内存地址集合</returns>
    public static IEnumerable<IntPtr> SearchPattern(string patternString)
    {
        var handle = GetCurrentProcessHandle();
        return SearchPattern(handle, patternString);
    }
    #endregion

    #region 模块内字符串模式搜索方法
    /// <summary>
    /// 在指定进程的特定模块中搜索字符串模式
    /// </summary>
    /// <param name="processHandle">进程句柄</param>
    /// <param name="processId">进程ID</param>
    /// <param name="patternString">要搜索的字符串模式，例如 "48 8B F0 74 ** 48 8B 44 24 48"</param>
    /// <param name="moduleName">模块名称</param>
    /// <returns>匹配该模式的内存地址集合</returns>
    public static IEnumerable<IntPtr> SearchPatternInModule(IntPtr processHandle, int processId, string patternString, string moduleName)
    {
        var (pattern, masks) = ParsePatternString(patternString);

        // 获取目标进程中的模块
        System.Diagnostics.Process? sysProcess = null;
        try
        {
            // 获取目标进程的系统Process对象
            sysProcess = System.Diagnostics.Process.GetProcessById(processId);

            // 查找指定名称的模块
            System.Diagnostics.ProcessModule? module = null;
            foreach (System.Diagnostics.ProcessModule mod in sysProcess.Modules)
            {
                if (string.Equals(mod.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
                {
                    module = mod;
                    break;
                }
            }

            if (module == null)
            {
                Console.WriteLine($"无法找到模块: {moduleName}");
                yield return ThrowNoResultsException();
                yield break;
            }

            IntPtr startAddress = module.BaseAddress;
            int moduleSize = module.ModuleMemorySize;
            IntPtr endAddress = new IntPtr(startAddress.ToInt64() + moduleSize);

#if DEBUG
            Console.WriteLine($"开始在模块 {moduleName} 中搜索");
            Console.WriteLine($"模块基址: 0x{startAddress.ToInt64():X16}");
            Console.WriteLine($"模块大小: 0x{moduleSize:X8} ({moduleSize} 字节)");
            Console.WriteLine($"搜索范围: 0x{startAddress.ToInt64():X16} - 0x{endAddress.ToInt64():X16}");
#endif

            foreach (var result in SearchMemoryRangeWithMask(processHandle, startAddress, endAddress, pattern, masks))
            {
                yield return result;
            }
        }
        finally
        {
            sysProcess?.Dispose();
        }
    }

    /// <summary>
    /// 在指定进程的特定模块中搜索字符串模式
    /// </summary>
    /// <param name="processName">进程名</param>
    /// <param name="patternString">要搜索的字符串模式，例如 "48 8B F0 74 ** 48 8B 44 24 48"</param>
    /// <param name="moduleName">模块名称</param>
    /// <returns>匹配该模式的内存地址集合</returns>
    public static IEnumerable<IntPtr> SearchPatternInModule(string processName, string patternString, string moduleName)
    {
        var (handle, process) = GetProcessHandleByName(processName);
        return SearchPatternInModule(handle, process.Id, patternString, moduleName);
    }

    /// <summary>
    /// 在当前进程的特定模块中搜索字符串模式
    /// </summary>
    /// <param name="patternString">要搜索的字符串模式，例如 "48 8B F0 74 ** 48 8B 44 24 48"</param>
    /// <param name="moduleName">模块名称</param>
    /// <returns>匹配该模式的内存地址集合</returns>
    public static IEnumerable<IntPtr> SearchPatternInModule(string patternString, string moduleName)
    {
        var handle = GetCurrentProcessHandle();
        int processId = Process.GetCurrentProcess().Id;
        return SearchPatternInModule(handle, processId, patternString, moduleName);
    }
    #endregion


    // -------------------------------------------------------------- 针对简易使用

    // 为脚本提供搜索方法，接受进程名
    public static IntPtr[] Search(string processName, byte[] pattern)
    {
        try
        {
            return new List<IntPtr>(SearchBytes(processName, pattern)).ToArray();
        }
        catch (InvalidOperationException ex) when (ex.Message == "未找到匹配的字节模式。")
        {
            throw; // 向上传递异常
        }
    }
    
    // 在当前进程中搜索
    public static IntPtr[] Search(byte[] pattern)
    {
        try
        {
            return new List<IntPtr>(SearchBytes(pattern)).ToArray();
        }
        catch (InvalidOperationException ex) when (ex.Message == "未找到匹配的字节模式。")
        {
            throw; // 向上传递异常
        }
    }
    
    // 为脚本提供在模块中搜索的方法，接受进程名
    public static IntPtr[] SearchInModule(string processName, byte[] pattern, string moduleName)
    {
        try
        {
            return new List<IntPtr>(SearchBytesInModule(processName, pattern, moduleName)).ToArray();
        }
        catch (InvalidOperationException ex) when (ex.Message == "未找到匹配的字节模式。")
        {
            throw; // 向上传递异常
        }
    }
    
    // 在当前进程的模块中搜索
    public static IntPtr[] SearchInModule(byte[] pattern, string moduleName)
    {
        try
        {
            return new List<IntPtr>(SearchBytesInModule(pattern, moduleName)).ToArray();
        }
        catch (InvalidOperationException ex) when (ex.Message == "未找到匹配的字节模式。")
        {
            throw; // 向上传递异常
        }
    }

    public static IntPtr[] Search(string processName, string patternString)
    {
        try
        {
            return new List<IntPtr>(SearchPattern(processName, patternString)).ToArray();
        }
        catch (InvalidOperationException ex) when (ex.Message == "未找到匹配的字节模式。")
        {
            throw; // 向上传递异常
        }
    }

    // 在当前进程中搜索字符串模式
    public static IntPtr[] Search(string patternString)
    {
        try
        {
            return new List<IntPtr>(SearchPattern(patternString)).ToArray();
        }
        catch (InvalidOperationException ex) when (ex.Message == "未找到匹配的字节模式。")
        {
            throw; // 向上传递异常
        }
    }

    // 为脚本提供在模块中搜索字符串模式的方法，接受进程名
    public static IntPtr[]? SearchInModule(string processName, string patternString, string moduleName)
    {
        try
        {
            return new List<IntPtr>(SearchPatternInModule(processName, patternString, moduleName)).ToArray();
        }
        catch (InvalidOperationException ex) when (ex.Message == "未找到匹配的字节模式。")
        {
            throw; // 向上传递异常
        }
    }

    // 在当前进程的模块中搜索字符串模式
    public static IntPtr[] SearchInModule(string patternString, string moduleName)
    {
        try
        {
            return new List<IntPtr>(SearchPatternInModule(patternString, moduleName)).ToArray();
        }
        catch (InvalidOperationException ex) when (ex.Message == "未找到匹配的字节模式。")
        {
            throw; // 向上传递异常
        }
    }
}
