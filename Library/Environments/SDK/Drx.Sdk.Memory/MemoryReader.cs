using Drx.Sdk.Native;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Drx.Sdk.Memory;

public static class MemoryReader
{
    // 使用ThreadStatic确保线程安全的共享缓冲区
    [ThreadStatic]
    private static byte[] _smallBuffer;
    
    [ThreadStatic]
    private static ArrayPool<byte> _arrayPool;
    
    // 初始化线程本地存储的缓冲区和对象池
    private static void EnsureInitialized()
    {
        if (_smallBuffer == null)
            _smallBuffer = new byte[256];
            
        if (_arrayPool == null)
            _arrayPool = ArrayPool<byte>.Shared;
    }

    /// <summary>
    /// 高性能读取内存块
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe byte[] ReadMemory(IntPtr processHandle, IntPtr address, int size)
    {
        EnsureInitialized();
        
        // 对于小尺寸读取，使用预分配的缓冲区
        if (size <= 256)
        {
            fixed (byte* bufferPtr = _smallBuffer)
            {
                // 在ReadProcessMemory调用后获取实际读取的字节数
                if (!Kernel32.ReadProcessMemory(processHandle, address, (IntPtr)bufferPtr, size, out IntPtr bytesRead))
                {
                    throw new InvalidOperationException($"读取内存失败: {Marshal.GetLastWin32Error()}");
                }

                // 根据实际读取的字节数创建结果数组
                var result = new byte[bytesRead.ToInt64()];
                if (bytesRead.ToInt64() > 0)
                {
                    Buffer.MemoryCopy(bufferPtr, Unsafe.AsPointer(ref result[0]), result.Length, bytesRead.ToInt64());
                }
                return result;
            }
        }
        else
        {
            var buffer = _arrayPool.Rent(size);
            try
            {
                fixed (byte* bufferPtr = buffer)
                {
                    if (!Kernel32.ReadProcessMemory(processHandle, address, (IntPtr)bufferPtr, size, out IntPtr bytesRead))
                    {
                        throw new InvalidOperationException($"读取内存失败: {Marshal.GetLastWin32Error()}");
                    }
                    
                    var result = new byte[bytesRead.ToInt64()];
                    if (bytesRead.ToInt64() > 0)
                    {
                        Buffer.MemoryCopy(bufferPtr, Unsafe.AsPointer(ref result[0]), result.Length, bytesRead.ToInt64());
                    }
                    return result;
                }
            }
            finally
            {
                _arrayPool.Return(buffer);
            }
        }
    }

    /// <summary>
    /// 高性能读取值类型
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T Read<T>(IntPtr processHandle, IntPtr address) where T : unmanaged
    {
        // 直接使用栈分配内存，避免堆分配
        T result = default;
        if (!Kernel32.ReadProcessMemory(processHandle, address, (IntPtr)(&result), sizeof(T), out IntPtr bytesRead) || 
            bytesRead.ToInt32() != sizeof(T))
        {
            throw new InvalidOperationException($"读取内存失败: {Marshal.GetLastWin32Error()}");
        }
        return result;
    }

    /// <summary>
    /// 高性能读取数组
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T[] ReadArray<T>(IntPtr processHandle, IntPtr address, int count) where T : unmanaged
    {
        int elementSize = sizeof(T);
        int totalSize = elementSize * count;
        
        // 直接创建结果数组并固定它
        T[] result = new T[count];
        
        fixed (T* resultPtr = result)
        {
            if (!Kernel32.ReadProcessMemory(processHandle, address, (IntPtr)resultPtr, totalSize, out IntPtr bytesRead))
            {
                throw new InvalidOperationException($"读取内存失败: {Marshal.GetLastWin32Error()}");
            }
            
            // 如果读取字节数不足，调整数组大小
            int actualCount = (int)bytesRead.ToInt64() / elementSize;
            if (actualCount < count)
            {
                T[] trimmedResult = new T[actualCount];
                Array.Copy(result, trimmedResult, actualCount);
                return trimmedResult;
            }
        }
        
        return result;
    }

    /// <summary>
    /// 无需分配内存的原始读取方法
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool TryReadInto<T>(IntPtr processHandle, IntPtr address, ref T destination) where T : unmanaged
    {
        fixed (T* destPtr = &destination)
        {
            return Kernel32.ReadProcessMemory(processHandle, address, (IntPtr)destPtr, sizeof(T), out _);
        }
    }

    /// <summary>
    /// 无需分配内存的数组内原始读取方法
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool TryReadIntoArray<T>(IntPtr processHandle, IntPtr address, T[] destination, int destinationOffset, int count) where T : unmanaged
    {
        int elementSize = sizeof(T);
        int bytesToRead = elementSize * count;
        
        fixed (T* destPtr = &destination[destinationOffset])
        {
            return Kernel32.ReadProcessMemory(processHandle, address, (IntPtr)destPtr, bytesToRead, out _);
        }
    }

    /// <summary>
    /// 高性能非托管读取
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T ReadUnmanaged<T>(IntPtr processHandle, IntPtr address) where T : unmanaged
    {
        // 直接使用栈上内存，无需分配/释放
        T result;
        if (!Kernel32.ReadProcessMemory(processHandle, address, (IntPtr)(&result), sizeof(T), out _))
        {
            throw new InvalidOperationException($"读取内存失败: {Marshal.GetLastWin32Error()}");
        }
        return result;
    }

    /// <summary>
    /// 读取字符串（自动检测编码类型）
    /// </summary>
    public static unsafe string ReadString(IntPtr processHandle, IntPtr address, int maxLength = 1024, bool isWide = false)
    {
        // 读取实际数据
        var buffer = ReadMemory(processHandle, address, maxLength * (isWide ? 2 : 1));
        if (buffer.Length == 0) return string.Empty;

        // 确定字符串结束位置
        int length = 0;
        if (isWide)
        {
            // Unicode字符串
            while (length < buffer.Length - 1 && (buffer[length] != 0 || buffer[length + 1] != 0))
            {
                length += 2;
            }
            return System.Text.Encoding.Unicode.GetString(buffer, 0, length);
        }
        else
        {
            // ANSI字符串
            while (length < buffer.Length && buffer[length] != 0)
            {
                length++;
            }
            return System.Text.Encoding.Default.GetString(buffer, 0, length);
        }
    }
    
    /// <summary>
    /// 优化的内存读取，支持指定目的地址，避免分配
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool ReadMemoryInto(IntPtr processHandle, IntPtr sourceAddress, IntPtr destinationAddress, int size)
    {
        return Kernel32.ReadProcessMemory(processHandle, sourceAddress, destinationAddress, size, out _);
    }

    /// <summary>
    /// 批量读取多个地址
    /// </summary>
    public static unsafe T[] ReadMultiple<T>(IntPtr processHandle, IntPtr[] addresses) where T : unmanaged
    {
        T[] results = new T[addresses.Length];
        fixed (T* resultsPtr = results)
        {
            for (int i = 0; i < addresses.Length; i++)
            {
                if (!Kernel32.ReadProcessMemory(processHandle, addresses[i], (IntPtr)(resultsPtr + i), sizeof(T), out _))
                {
                    throw new InvalidOperationException($"读取内存失败于地址 {addresses[i].ToInt64():X}: {Marshal.GetLastWin32Error()}");
                }
            }
        }
        return results;
    }

    /// <summary>
    /// 检查内存区域是否可读
    /// </summary>
    public unsafe static bool IsReadable(IntPtr processHandle, IntPtr address, int size)
    {
        EnsureInitialized();
        
        try
        {
            if (size <= 0)
                return false;
                
            // 尝试读取1字节检查可读性
            fixed (byte* bufferPtr = _smallBuffer)
            {
                return Kernel32.ReadProcessMemory(processHandle, address, (IntPtr)bufferPtr, 1, out _);
            }
        }
        catch
        {
            return false;
        }
    }

    public unsafe static IntPtr GetModuleBaseAddress(string moduleName, nint pHandle)
    {
        if (string.IsNullOrEmpty(moduleName))
            throw new ArgumentNullException(nameof(moduleName));

        // 使用psapi.dll中的EnumProcessModules和GetModuleInformation函数
        try
        {
            // 获取进程中所有模块
            const int MAX_MODULE_COUNT = 1024;
            IntPtr[] moduleHandles = new IntPtr[MAX_MODULE_COUNT];
            uint bytesNeeded;

            // 尝试枚举进程模块
            if (!Kernel32.EnumProcessModules(pHandle, moduleHandles,
                (uint)(moduleHandles.Length * IntPtr.Size), out bytesNeeded))
            {
                throw new InvalidOperationException($"无法枚举进程模块: {Marshal.GetLastWin32Error()}");
            }

            // 计算实际的模块数量
            int moduleCount = (int)(bytesNeeded / IntPtr.Size);

            // 为每个模块获取信息
            for (int i = 0; i < moduleCount; i++)
            {
                // 获取模块名称
                StringBuilder moduleNameBuffer = new StringBuilder(260); // MAX_PATH
                if (Kernel32.GetModuleFileNameEx(pHandle, moduleHandles[i], moduleNameBuffer, (uint)moduleNameBuffer.Capacity) > 0)
                {
                    string currentModuleName = Path.GetFileName(moduleNameBuffer.ToString());

                    // 模块名匹配（不区分大小写）
                    if (string.Equals(currentModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
                    {
                        // 获取模块信息
                        Kernel32.MODULEINFO moduleInfo = new Kernel32.MODULEINFO();
                        if (Kernel32.GetModuleInformation(pHandle, moduleHandles[i],
                            out moduleInfo, (uint)Marshal.SizeOf<Kernel32.MODULEINFO>()))
                        {
                            return moduleInfo.lpBaseOfDll;
                        }
                    }
                }
            }

            // 若未找到精确匹配，尝试部分匹配
            for (int i = 0; i < moduleCount; i++)
            {
                StringBuilder moduleNameBuffer = new StringBuilder(260);
                if (Kernel32.GetModuleFileNameEx(pHandle, moduleHandles[i], moduleNameBuffer, (uint)moduleNameBuffer.Capacity) > 0)
                {
                    string currentModuleName = Path.GetFileName(moduleNameBuffer.ToString());

                    // 如果模块名包含目标名称（不区分大小写）
                    if (currentModuleName.IndexOf(moduleName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Kernel32.MODULEINFO moduleInfo = new Kernel32.MODULEINFO();
                        if (Kernel32.GetModuleInformation(pHandle, moduleHandles[i],
                            out moduleInfo, (uint)Marshal.SizeOf<Kernel32.MODULEINFO>()))
                        {
                            return moduleInfo.lpBaseOfDll;
                        }
                    }
                }
            }

            // 如果在外部进程中找不到，则尝试在当前进程中查找（兜底方案）
            IntPtr moduleHandle;
            if (!Kernel32.GetModuleHandleEx(0, moduleName, out moduleHandle))
            {
                throw new InvalidOperationException($"无法获取模块 {moduleName} 的基地址: {Marshal.GetLastWin32Error()}");
            }

            return moduleHandle;
        }
        catch (Exception ex) when (!(ex is ArgumentNullException || ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"获取模块基地址时发生错误: {ex.Message}", ex);
        }
    }

    // =====================================================
    // 导出给ClearScript的方法
    // =====================================================
    #region Script Methods
    public static int ReadInt32(IntPtr processHandle, IntPtr address)
    {
        return Read<int>(processHandle, address);
    }

    public static float ReadFloat(IntPtr processHandle, IntPtr address)
    {
        return Read<float>(processHandle, address);
    }

    public static double ReadDouble(IntPtr processHandle, IntPtr address)
    {
        return Read<double>(processHandle, address);
    }

    public static long ReadInt64(IntPtr processHandle, IntPtr address)
    {
        return Read<long>(processHandle, address);
    }

    public static short ReadInt16(IntPtr processHandle, IntPtr address)
    {
        return Read<short>(processHandle, address);
    }

    public static byte ReadByte(IntPtr processHandle, IntPtr address)
    {
        return Read<byte>(processHandle, address);
    }

    public static int[] ReadInt32Array(IntPtr processHandle, IntPtr address, int count)
    {
        return ReadArray<int>(processHandle, address, count);
    }

    public static float[] ReadFloatArray(IntPtr processHandle, IntPtr address, int count)
    {
        return ReadArray<float>(processHandle, address, count);
    }

    public static double[] ReadDoubleArray(IntPtr processHandle, IntPtr address, int count)
    {
        return ReadArray<double>(processHandle, address, count);
    }

    public static byte[] ReadByteArray(IntPtr processHandle, IntPtr address, int count)
    {
        return ReadArray<byte>(processHandle, address, count);
    }
    
    public static string ReadAnsiString(IntPtr processHandle, IntPtr address, int maxLength = 1024)
    {
        return ReadString(processHandle, address, maxLength, false);
    }
    
    public static string ReadUnicodeString(IntPtr processHandle, IntPtr address, int maxLength = 1024)
    {
        return ReadString(processHandle, address, maxLength, true);
    }
    #endregion
}