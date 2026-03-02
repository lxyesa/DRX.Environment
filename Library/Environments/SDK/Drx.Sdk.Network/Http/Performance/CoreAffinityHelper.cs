using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// CPU 核心亲和性辅助工具：提供跨平台的线程-核心绑定能力。
    /// 核心亲和性（Core Affinity）将线程固定到特定 CPU 核心，减少上下文切换和缓存失效。
    /// 
    /// 支持平台：
    ///   - Windows: 通过 SetThreadAffinityMask Win32 API
    ///   - Linux: 通过 sched_setaffinity syscall（需要 glibc）
    ///   - 其他平台: 静默回退（不绑定，不报错）
    /// 
    /// 使用场景：
    ///   - Worker 线程池中的每个 worker 绑定到不同核心
    ///   - Accept 线程绑定到专用核心减少中断
    ///   - Ticker 线程绑定到低负载核心
    /// </summary>
    public static class CoreAffinityHelper
    {
        #region Platform Detection

        /// <summary>
        /// 获取逻辑处理器数量（包含超线程）
        /// </summary>
        public static int LogicalProcessorCount => Environment.ProcessorCount;

        /// <summary>
        /// 获取推荐 Worker 数量（物理核心数的近似值）。
        /// 超线程核心通常共享执行单元和缓存，绑定到逻辑核心对 CPU 密集型任务收益有限，
        /// 因此推荐使用物理核心数作为 Worker 数量。
        /// 当无法精确检测时，使用逻辑处理器数 / 2（常见的 SMT 比率）作为下限估计。
        /// </summary>
        public static int RecommendedWorkerCount
        {
            get
            {
                var logical = Environment.ProcessorCount;
                // 大多数现代 CPU 是 2-way SMT（超线程），物理核心 ≈ 逻辑核心 / 2
                // 最少保留 1 个 worker
                return Math.Max(1, logical / 2);
            }
        }

        #endregion

        #region Windows P/Invoke

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessAffinityMask(IntPtr hProcess, out IntPtr lpProcessAffinityMask, out IntPtr lpSystemAffinityMask);

        #endregion

        #region Linux P/Invoke

        private const int __CPU_SETSIZE = 1024;
        private const int __NCPUBITS = 64; // sizeof(ulong) * 8

        [DllImport("libc", SetLastError = true, EntryPoint = "sched_setaffinity")]
        private static extern int sched_setaffinity_linux(int pid, IntPtr cpusetsize, IntPtr mask);

        #endregion

        /// <summary>
        /// 将当前线程绑定到指定的 CPU 核心。
        /// 核心索引从 0 开始，对应逻辑处理器编号。
        /// 如果核心索引超出范围或平台不支持，静默忽略。
        /// </summary>
        /// <param name="coreIndex">目标核心索引（0-based）</param>
        /// <returns>true 表示成功绑定，false 表示绑定失败或不支持</returns>
        public static bool SetCurrentThreadAffinity(int coreIndex)
        {
            if (coreIndex < 0 || coreIndex >= Environment.ProcessorCount)
                return false;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return SetAffinityWindows(coreIndex);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return SetAffinityLinux(coreIndex);
                }
                // macOS 不支持线程级亲和性（只支持 affinity tag，不是强绑定）
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warn($"设置 CPU 亲和性失败 (core={coreIndex}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 将当前线程绑定到一组 CPU 核心（允许在多个核心间调度）。
        /// </summary>
        /// <param name="coreIndices">核心索引数组</param>
        /// <returns>true 表示成功</returns>
        public static bool SetCurrentThreadAffinityMask(int[] coreIndices)
        {
            if (coreIndices == null || coreIndices.Length == 0)
                return false;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    long mask = 0;
                    foreach (var idx in coreIndices)
                    {
                        if (idx >= 0 && idx < 64) // Windows 亲和性掩码最大 64 位
                            mask |= 1L << idx;
                    }
                    if (mask == 0) return false;

                    var hThread = GetCurrentThread();
                    var result = SetThreadAffinityMask(hThread, new IntPtr(mask));
                    return result != IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"设置 CPU 亲和性掩码失败: {ex.Message}");
            }
            return false;
        }

        private static bool SetAffinityWindows(int coreIndex)
        {
            var hThread = GetCurrentThread();
            var mask = new IntPtr(1L << coreIndex);
            var result = SetThreadAffinityMask(hThread, mask);
            if (result == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                Logger.Warn($"SetThreadAffinityMask 失败, error={error}, core={coreIndex}");
                return false;
            }
            return true;
        }

        private static unsafe bool SetAffinityLinux(int coreIndex)
        {
            // cpu_set_t 通常是 1024 位 = 128 字节
            int cpuSetSize = __CPU_SETSIZE / 8;
            var cpuSet = stackalloc byte[cpuSetSize];
            new Span<byte>(cpuSet, cpuSetSize).Clear();

            // 设置目标核心对应的位
            int byteIndex = coreIndex / 8;
            int bitIndex = coreIndex % 8;
            if (byteIndex < cpuSetSize)
            {
                cpuSet[byteIndex] = (byte)(1 << bitIndex);
            }

            int result = sched_setaffinity_linux(0, new IntPtr(cpuSetSize), new IntPtr(cpuSet));
            return result == 0;
        }

        /// <summary>
        /// 获取当前进程可用的 CPU 亲和性掩码（仅 Windows）。
        /// 返回值的每个位对应一个可用的逻辑处理器。
        /// 其他平台返回 -1 表示不可用。
        /// </summary>
        public static long GetProcessAffinityMask()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return -1;

            try
            {
                var hProcess = Process.GetCurrentProcess().Handle;
                if (GetProcessAffinityMask(hProcess, out var processMask, out _))
                    return processMask.ToInt64();
            }
            catch { }
            return -1;
        }

        /// <summary>
        /// 检查指定的核心索引在当前进程的亲和性掩码中是否可用。
        /// </summary>
        public static bool IsCoreAvailable(int coreIndex)
        {
            if (coreIndex < 0 || coreIndex >= Environment.ProcessorCount)
                return false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var mask = GetProcessAffinityMask();
                if (mask <= 0) return true; // 无法检测时假定可用
                return (mask & (1L << coreIndex)) != 0;
            }

            // Linux/macOS 默认假定所有核心可用
            return true;
        }
    }
}
