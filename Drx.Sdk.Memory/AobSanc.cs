using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Numerics;
using System.Buffers;
using System.Collections.Concurrent;

namespace Drx.Sdk.Memory
{
    public static class AobSanc
    {
        #region Cache
        private static readonly ConcurrentDictionary<string, (DateTime Timestamp, IntPtr Address)> _cache
            = new ConcurrentDictionary<string, (DateTime, IntPtr)>();

        private const int CACHE_TIMEOUT_SECONDS = 5;

        private static string GenerateCacheKey(string processName, byte[] signature, string? moduleName)
            => $"{processName}_{moduleName}_{BitConverter.ToString(signature)}";

        private static IntPtr GetCachedAddress(string key)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                if ((DateTime.UtcNow - cached.Timestamp).TotalSeconds < CACHE_TIMEOUT_SECONDS)
                {
                    return cached.Address;
                }
                _cache.TryRemove(key, out _);
            }
            return IntPtr.Zero;
        }

        private static void CacheAddress(string key, IntPtr address)
        {
            _cache.AddOrUpdate(key,
                (DateTime.UtcNow, address),
                (_, _) => (DateTime.UtcNow, address));
        }
        #endregion

        /// <summary>
        /// 在指定进程和模块中扫描字节特征码
        /// </summary>
        public static IntPtr Sanc(string processName, byte[] signature, string? moduleName = null)
        {
            if (string.IsNullOrEmpty(processName))
                throw new ArgumentNullException(nameof(processName));
            if (signature == null)
                throw new ArgumentNullException(nameof(signature));
            if (signature.Length == 0)
                throw new ArgumentException("Signature cannot be empty", nameof(signature));

            // 检查缓存
            string cacheKey = GenerateCacheKey(processName, signature, moduleName);
            var cachedAddress = GetCachedAddress(cacheKey);
            if (cachedAddress != IntPtr.Zero)
                return cachedAddress;

            try
            {
                using var process = Process.GetProcessesByName(processName).FirstOrDefault();
                if (process == null)
                    return IntPtr.Zero;

                ProcessModule? module;
                if (string.IsNullOrEmpty(moduleName))
                {
                    module = process.MainModule;
                }
                else
                {
                    module = process.Modules.Cast<ProcessModule>()
                        .FirstOrDefault(m => m.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
                }

                if (module == null)
                    return IntPtr.Zero;

                using (module)
                {
                    var result = ScanProcessMemory(process, module, signature);
                    if (result != IntPtr.Zero)
                    {
                        CacheAddress(cacheKey, result);
                    }
                    return result;
                }
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private static IntPtr ScanProcessMemory(Process process, ProcessModule module, byte[] pattern)
        {
            if (module.BaseAddress == IntPtr.Zero)
                return IntPtr.Zero;

            try
            {
                var memoryRegions = GetMemoryRegions(process, module);

                // 使用并行处理搜索内存区域
                return memoryRegions.AsParallel()
                    .Select(region => ScanRegion(process.Handle, region.Address, region.Size, pattern))
                    .FirstOrDefault(addr => addr != IntPtr.Zero);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private static List<(IntPtr Address, int Size)> GetMemoryRegions(Process process, ProcessModule module)
        {
            var regions = new List<(IntPtr Address, int Size)>();
            IntPtr current = module.BaseAddress;
            IntPtr maxAddress = IntPtr.Add(module.BaseAddress, module.ModuleMemorySize);

            while (current.ToInt64() < maxAddress.ToInt64())
            {
                var memInfo = new MEMORY_BASIC_INFORMATION();
                int queryResult = VirtualQueryEx(
                    process.Handle,
                    current,
                    ref memInfo,
                    (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)));

                if (queryResult == 0)
                    break;

                if (memInfo.State == MEM_COMMIT &&
                    (memInfo.Protect == PAGE_READWRITE ||
                     memInfo.Protect == PAGE_READONLY ||
                     memInfo.Protect == PAGE_EXECUTE_READ))
                {
                    regions.Add((memInfo.BaseAddress, (int)memInfo.RegionSize));
                }

                current = IntPtr.Add(current, (int)memInfo.RegionSize);
            }

            return regions;
        }

        private static IntPtr ScanRegion(IntPtr processHandle, IntPtr baseAddress, int size, byte[] pattern)
        {
            // 使用ArrayPool减少内存分配
            byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
            try
            {
                if (!ReadProcessMemory(processHandle, baseAddress, buffer, size, out int bytesRead))
                    return IntPtr.Zero;

                // 根据CPU特性选择搜索方法
                if (Vector.IsHardwareAccelerated && pattern.Length >= Vector<byte>.Count)
                {
                    return ScanBufferSIMD(buffer, pattern, baseAddress, bytesRead);
                }
                else
                {
                    return ScanBufferBoyerMoore(buffer, pattern, baseAddress, bytesRead);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static IntPtr ScanBufferSIMD(byte[] buffer, byte[] pattern, IntPtr baseAddress, int bufferSize)
        {
            int vectorSize = Vector<byte>.Count;
            int patternLength = pattern.Length;
            int searchSpace = bufferSize - patternLength;

            // 创建第一个字节的向量
            byte[] firstByteArray = Enumerable.Repeat(pattern[0], vectorSize).ToArray();
            var firstByteVector = new Vector<byte>(firstByteArray);

            unsafe
            {
                fixed (byte* pBuffer = buffer)
                {
                    for (int i = 0; i <= searchSpace; i += vectorSize)
                    {
                        // 创建缓冲区的向量
                        var bufferSpan = new ReadOnlySpan<byte>(pBuffer + i, vectorSize);
                        var bufferVector = new Vector<byte>(bufferSpan);

                        // 比较向量
                        var comparison = Vector.Equals(bufferVector, firstByteVector);

                        // 获取比较结果的掩码
                        byte* pComparison = (byte*)&comparison;
                        uint mask = 0;
                        for (int j = 0; j < vectorSize; j++)
                        {
                            if (pComparison[j] != 0)
                            {
                                mask |= 1u << j;
                            }
                        }

                        // 检查每个匹配位置
                        while (mask != 0)
                        {
                            int index = BitOperations.TrailingZeroCount(mask);
                            if (ComparePattern(buffer, i + index, pattern, bufferSize))
                            {
                                return IntPtr.Add(baseAddress, i + index);
                            }
                            mask &= ~(1u << index);
                        }
                    }
                }
            }

            return IntPtr.Zero;
        }

        private static IntPtr ScanBufferBoyerMoore(byte[] buffer, byte[] pattern, IntPtr baseAddress, int bufferSize)
        {
            var skipTable = new int[256];
            for (int i = 0; i < 256; i++)
                skipTable[i] = pattern.Length;

            for (int i = 0; i < pattern.Length - 1; i++)
                skipTable[pattern[i]] = pattern.Length - 1 - i;

            int pos = 0;
            while (pos <= bufferSize - pattern.Length)
            {
                int j = pattern.Length - 1;
                while (j >= 0 && pattern[j] == buffer[pos + j])
                    j--;

                if (j < 0)
                    return IntPtr.Add(baseAddress, pos);

                pos += Math.Max(1, j - skipTable[buffer[pos + j]]);
            }

            return IntPtr.Zero;
        }

        private static bool ComparePattern(byte[] buffer, int offset, byte[] pattern, int bufferSize)
        {
            if (offset + pattern.Length > bufferSize)
                return false;

            for (int i = 1; i < pattern.Length; i++)
            {
                if (buffer[offset + i] != pattern[i])
                    return false;
            }
            return true;
        }

        #region Native Methods and Structures
        [DllImport("kernel32.dll")]
        private static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll")]
        private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress,
            ref MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
            byte[] buffer, int size, out int lpNumberOfBytesRead);

        private const int MEM_COMMIT = 0x1000;
        private const int PAGE_READWRITE = 0x04;
        private const int PAGE_READONLY = 0x02;
        private const int PAGE_EXECUTE_READ = 0x20;

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_INFO
        {
            public ushort processorArchitecture;
            private ushort reserved;
            public uint pageSize;
            public IntPtr minimumApplicationAddress;
            public IntPtr maximumApplicationAddress;
            public IntPtr activeProcessorMask;
            public uint numberOfProcessors;
            public uint processorType;
            public uint allocationGranularity;
            public ushort processorLevel;
            public ushort processorRevision;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }
        #endregion
    }
}
