using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Win32;

namespace Drx.Sdk.Memory;

[ScriptClass("MemoryWriter")]
public class MemoryWriter
{
    /// <summary>
    /// 在目标进程中目标地址附近并且可用的内存区域申请内存
    /// </summary>
    /// <param name="processName">目标进程的名称</param>
    /// <param name="targetAddress">目标地址</param>
    /// <param name="size">申请的内存大小</param>
    /// <returns>分配的内存地址</returns>
    public static IntPtr Alloc(IntPtr targetAddress, uint size, string processName)
    {
        var process = Process.GetProcessesByName(processName);
        var processHandle = IntPtr.Zero;
        if ( process != null)
        {
            processHandle = process[0].Handle;
        }

        if (processHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"无法获取进程句柄");
        }

        // 获取系统信息
        Kernel32.SYSTEM_INFO sysInfo = new Kernel32.SYSTEM_INFO();
        Kernel32.GetSystemInfo(ref sysInfo);

        // 尝试在目标地址直接分配内存
        IntPtr allocatedAddress = Kernel32.VirtualAllocEx(
            processHandle,
            targetAddress,
            size,
            Kernel32.MEM_COMMIT | Kernel32.MEM_RESERVE,
            Kernel32.PAGE_EXECUTE_READWRITE);

        if (allocatedAddress != IntPtr.Zero)
        {
            return allocatedAddress;
        }

        // 如果在目标地址分配失败，尝试在附近区域分配
        // 以目标地址为中心，逐渐扩大搜索范围
        for (long offset = sysInfo.allocationGranularity; offset < 0x7FFFFFFF; offset += sysInfo.allocationGranularity)
        {
            // 尝试在目标地址上方分配
            allocatedAddress = Kernel32.VirtualAllocEx(
                processHandle,
                new IntPtr(targetAddress.ToInt64() + offset),
                size,
                Kernel32.MEM_COMMIT | Kernel32.MEM_RESERVE,
                Kernel32.PAGE_EXECUTE_READWRITE);

            if (allocatedAddress != IntPtr.Zero)
            {
                return allocatedAddress;
            }

            // 尝试在目标地址下方分配
            allocatedAddress = Kernel32.VirtualAllocEx(
                processHandle,
                new IntPtr(targetAddress.ToInt64() - offset),
                size,
                Kernel32.MEM_COMMIT | Kernel32.MEM_RESERVE,
                Kernel32.PAGE_EXECUTE_READWRITE);

            if (allocatedAddress != IntPtr.Zero)
            {
                return allocatedAddress;
            }
        }

        throw new OutOfMemoryException("无法在目标进程中分配内存");
    }

    public static IntPtr Alloc(IntPtr targetAddress, uint size, IntPtr processHandle)
    {
        if (processHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"无法获取进程句柄");
        }

        // 获取系统信息
        Kernel32.SYSTEM_INFO sysInfo = new Kernel32.SYSTEM_INFO();
        Kernel32.GetSystemInfo(ref sysInfo);

        // 尝试在目标地址直接分配内存
        IntPtr allocatedAddress = Kernel32.VirtualAllocEx(
            processHandle,
            targetAddress,
            size,
            Kernel32.MEM_COMMIT | Kernel32.MEM_RESERVE,
            Kernel32.PAGE_EXECUTE_READWRITE);

        if (allocatedAddress != IntPtr.Zero)
        {
            return allocatedAddress;
        }

        // 如果在目标地址分配失败，尝试在附近区域分配
        // 以目标地址为中心，逐渐扩大搜索范围
        for (long offset = sysInfo.allocationGranularity; offset < 0x7FFFFFFF; offset += sysInfo.allocationGranularity)
        {
            // 尝试在目标地址上方分配
            allocatedAddress = Kernel32.VirtualAllocEx(
                processHandle,
                new IntPtr(targetAddress.ToInt64() + offset),
                size,
                Kernel32.MEM_COMMIT | Kernel32.MEM_RESERVE,
                Kernel32.PAGE_EXECUTE_READWRITE);

            if (allocatedAddress != IntPtr.Zero)
            {
                return allocatedAddress;
            }

            // 尝试在目标地址下方分配
            allocatedAddress = Kernel32.VirtualAllocEx(
                processHandle,
                new IntPtr(targetAddress.ToInt64() - offset),
                size,
                Kernel32.MEM_COMMIT | Kernel32.MEM_RESERVE,
                Kernel32.PAGE_EXECUTE_READWRITE);

            if (allocatedAddress != IntPtr.Zero)
            {
                return allocatedAddress;
            }
        }

        throw new OutOfMemoryException("无法在目标进程中分配内存");
    }

    /// <summary>
    /// 在当前进程中分配内存
    /// </summary>
    /// <param name="size">申请的内存大小</param>
    /// <returns>分配的内存地址</returns>
    public static IntPtr Alloc(uint size)
    {
        IntPtr allocatedAddress = Kernel32.VirtualAlloc(
            IntPtr.Zero,
            size,
            Kernel32.MEM_COMMIT | Kernel32.MEM_RESERVE,
            Kernel32.PAGE_EXECUTE_READWRITE);

        if (allocatedAddress == IntPtr.Zero)
        {
            throw new OutOfMemoryException("无法在当前进程中分配内存");
        }

        return allocatedAddress;
    }

    public static bool Free(IntPtr address, IntPtr processHandle)
    {
        return Kernel32.VirtualFreeEx(processHandle, address, 0, Kernel32.MEM_RELEASE);
    }

    /// <summary>
    /// 高性能的内存写入方法，将数据写入目标进程的指定内存地址
    /// </summary>
    /// <param name="processHandle">目标进程的句柄</param>
    /// <param name="address">要写入的内存地址</param>
    /// <param name="buffer">要写入的数据缓冲区</param>
    /// <returns>是否成功写入内存</returns>
    public static bool WriteMemory(IntPtr processHandle, IntPtr address, byte[] buffer)
    {
        if (processHandle == IntPtr.Zero)
            throw new ArgumentException("进程句柄不能为空", nameof(processHandle));
        
        if (address == IntPtr.Zero)
            throw new ArgumentException("内存地址不能为零", nameof(address));
        
        if (buffer == null || buffer.Length == 0)
            throw new ArgumentException("数据缓冲区不能为空或长度为0", nameof(buffer));
            
        int bytesWritten = 0;
        
        // 首先检查是否需要修改内存保护
        uint oldProtect = 0;
        bool protectChanged = Kernel32.VirtualProtectEx(
            processHandle, 
            address, 
            (uint)buffer.Length, 
            Kernel32.PAGE_EXECUTE_READWRITE, 
            out oldProtect);
            
        // 执行内存写入操作
        bool result = Kernel32.WriteProcessMemory(
            processHandle, 
            address, 
            buffer, 
            (uint)buffer.Length, 
            out bytesWritten);
            
        // 如果之前修改了内存保护，则还原
        if (protectChanged)
        {
            Kernel32.VirtualProtectEx(processHandle, address, (uint)buffer.Length, oldProtect, out _);
        }
        
        // 刷新指令缓存，确保指令修改能立即生效
        Kernel32.FlushInstructionCache(processHandle, address, new UIntPtr((uint)buffer.Length));
        
        return result && bytesWritten == buffer.Length;
    }

    public static bool WriteMemory(string processName, IntPtr address, byte[] buffer)
    {
        var handle = GetProcessHandle(processName);
        return WriteMemory(handle, address, buffer);
    }

    /// <summary>
    /// 将十六进制格式的字符串写入目标进程内存
    /// </summary>
    /// <param name="processName">目标进程名称</param>
    /// <param name="address">要写入的内存地址</param>
    /// <param name="hexString">十六进制格式的字符串，例如："90 90 EB 05 B8 FF FF FF FF"</param>
    /// <returns>是否成功写入内存</returns>
    public static bool WriteMemory(string processName, IntPtr address, string hexString)
    {
        if (string.IsNullOrWhiteSpace(hexString))
            throw new ArgumentException("十六进制字符串不能为空", nameof(hexString));

        // 解析十六进制字符串为字节数组
        string[] parts = hexString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        byte[] buffer = new byte[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "**" || parts[i] == "??")
            {
                buffer[i] = 0; // 对于通配符，使用0填充
            }
            else
            {
                buffer[i] = Convert.ToByte(parts[i], 16);
            }
        }

        // 调用原有的WriteMemory方法写入字节数组
        var handle = GetProcessHandle(processName);
        return WriteMemory(handle, address, buffer);
    }

    /// <summary>
    /// 将十六进制格式的字符串写入目标进程内存
    /// </summary>
    /// <param name="processHandle">目标进程句柄</param>
    /// <param name="address">要写入的内存地址</param>
    /// <param name="hexString">十六进制格式的字符串，例如："90 90 EB 05 B8 FF FF FF FF"</param>
    /// <returns>是否成功写入内存</returns>
    public static bool WriteMemory(IntPtr processHandle, IntPtr address, string hexString)
    {
        if (string.IsNullOrWhiteSpace(hexString))
            throw new ArgumentException("十六进制字符串不能为空", nameof(hexString));

        // 解析十六进制字符串为字节数组
        string[] parts = hexString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        byte[] buffer = new byte[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "**" || parts[i] == "??")
            {
                buffer[i] = 0; // 对于通配符，使用0填充
            }
            else
            {
                buffer[i] = Convert.ToByte(parts[i], 16);
            }
        }

        // 调用原有的WriteMemory方法写入字节数组
        return WriteMemory(processHandle, address, buffer);
    }

    private static IntPtr GetProcessHandle(string processName)
    {
        var process = Process.GetProcessesByName(processName);
        var processHandle = IntPtr.Zero;
        if (process != null)
        {
            processHandle = process[0].Handle;
        }
        if (processHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"无法获取进程句柄");
        }
        return processHandle;
    }

    /// <summary>
    /// 高性能的异步内存写入方法
    /// </summary>
    /// <param name="processHandle">目标进程的句柄</param>
    /// <param name="address">要写入的内存地址</param>
    /// <param name="buffer">要写入的数据缓冲区</param>
    /// <returns>表示异步操作的任务，完成时返回是否成功写入内存</returns>
    public static async Task<bool> WriteMemoryAsync(IntPtr processHandle, IntPtr address, byte[] buffer)
    {
        return await Task.Run(() => WriteMemory(processHandle, address, buffer));
    }
    
    /// <summary>
    /// 写入特定类型的数据到目标进程内存
    /// </summary>
    /// <typeparam name="T">要写入的数据类型</typeparam>
    /// <param name="processHandle">目标进程的句柄</param>
    /// <param name="address">要写入的内存地址</param>
    /// <param name="value">要写入的值</param>
    /// <returns>是否成功写入内存</returns>
    public static bool WriteMemory<T>(IntPtr processHandle, IntPtr address, T value) where T : struct
    {
        int size = System.Runtime.InteropServices.Marshal.SizeOf(value);
        byte[] buffer = new byte[size];
        
        IntPtr ptr = IntPtr.Zero;
        try
        {
            // 分配托管内存并将结构体复制到其中
            ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
            System.Runtime.InteropServices.Marshal.StructureToPtr(value, ptr, false);
            
            // 从托管内存复制到字节数组
            System.Runtime.InteropServices.Marshal.Copy(ptr, buffer, 0, size);
            
            // 写入到目标进程
            return WriteMemory(processHandle, address, buffer);
        }
        finally
        {
            // 释放托管内存
            if (ptr != IntPtr.Zero)
                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
        }
    }
    
    /// <summary>
    /// 异步写入特定类型的数据到目标进程内存
    /// </summary>
    /// <typeparam name="T">要写入的数据类型</typeparam>
    /// <param name="processHandle">目标进程的句柄</param>
    /// <param name="address">要写入的内存地址</param>
    /// <param name="value">要写入的值</param>
    /// <returns>表示异步操作的任务，完成时返回是否成功写入内存</returns>
    public static async Task<bool> WriteMemoryAsync<T>(IntPtr processHandle, IntPtr address, T value) where T : struct
    {
        return await Task.Run(() => WriteMemory(processHandle, address, value));
    }
}
