using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Drx.Sdk.Memory
{

    /// <summary>
    /// 提供内存Hook功能的类，支持近跳转指令和原始代码恢复
    /// </summary>
    public class AutoHook(IntPtr processHandle) : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer,
            uint nSize,
            out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            uint nSize,
            out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll")]
        private static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            uint dwSize,
            uint dwFreeType);

        // 内存分配和进程访问的常量
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint MEM_RELEASE = 0x8000;

        private bool disposed = false;
        private IntPtr processHandle = processHandle;
        private List<byte[]> codeCaves = new List<byte[]>();
        private IntPtr allocatedCodeCaveAddress;
        private uint totalCodeCaveSize = 0;
        private byte[] originalInstructions;
        private IntPtr targetAddress;
        private uint originalSize;
        private string processName;

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

        /// <summary>
        /// 在目标进程中目标地址附近并且可用的内存区域申请内存
        /// </summary>
        /// <param name="processName">目标进程的名称</param>
        /// <param name="targetAddress">目标地址</param>
        /// <param name="size">申请的内存大小</param>
        /// <returns>分配的内存地址</returns>
        public IntPtr Alloc(IntPtr targetAddress, uint size)
        {
            if (processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"无法获取进程句柄");
            }

            // 获取系统信息
            SYSTEM_INFO sysInfo = new SYSTEM_INFO();
            GetSystemInfo(ref sysInfo);

            // 尝试在目标地址直接分配内存
            IntPtr allocatedAddress = VirtualAllocEx(
                processHandle,
                targetAddress,
                size,
                MEM_COMMIT | MEM_RESERVE,
                PAGE_EXECUTE_READWRITE);

            if (allocatedAddress != IntPtr.Zero)
            {
                return allocatedAddress;
            }

            // 如果在目标地址分配失败，尝试在附近区域分配
            // 以目标地址为中心，逐渐扩大搜索范围
            for (long offset = sysInfo.allocationGranularity; offset < 0x7FFFFFFF; offset += sysInfo.allocationGranularity)
            {
                // 尝试在目标地址上方分配
                allocatedAddress = VirtualAllocEx(
                    processHandle,
                    new IntPtr(targetAddress.ToInt64() + offset),
                    size,
                    MEM_COMMIT | MEM_RESERVE,
                    PAGE_EXECUTE_READWRITE);

                if (allocatedAddress != IntPtr.Zero)
                {
                    return allocatedAddress;
                }

                // 尝试在目标地址下方分配
                allocatedAddress = VirtualAllocEx(
                    processHandle,
                    new IntPtr(targetAddress.ToInt64() - offset),
                    size,
                    MEM_COMMIT | MEM_RESERVE,
                    PAGE_EXECUTE_READWRITE);

                if (allocatedAddress != IntPtr.Zero)
                {
                    return allocatedAddress;
                }
            }

            throw new OutOfMemoryException("无法在目标进程中分配内存");
        }

        /// <summary>
        /// 创建Hook
        /// </summary>
        /// <param name="processName">目标进程名</param>
        /// <param name="targetAddress">目标地址</param>
        /// <param name="originalInstructionSize">原始指令字节数量</param>
        public void CreateHook(IntPtr targetAddress, uint originalInstructionSize)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AutoHook));
            }

            if (string.IsNullOrEmpty(processName))
            {
                throw new ArgumentException("进程名不能为空", nameof(processName));
            }

            if (targetAddress == IntPtr.Zero)
            {
                throw new ArgumentException("目标地址不能为零", nameof(targetAddress));
            }

            if (originalInstructionSize < 5)
            {
                throw new ArgumentException("原始指令字节数量必须至少为5字节", nameof(originalInstructionSize));
            }

            // 确保进程句柄有效
            if (processHandle == IntPtr.Zero)
            {
                if (processHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"无法获取进程 {processName} 的句柄");
                }
            }

            // 1. 合并所有代码洞指令
            byte[] codeCaveInstructions = GetCodeCaveInstructions();
            if (codeCaveInstructions.Length == 0)
            {
                throw new InvalidOperationException("没有添加任何代码洞指令");
            }

            // 2. 读取原始指令前验证进程句柄
            int error = Marshal.GetLastWin32Error();
            if (!IsProcessValid(processHandle))
            {
                if (processHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"进程句柄无效，无法重新获取: {error}");
                }
            }

            // 读取原始指令
            originalInstructions = new byte[originalInstructionSize];
            this.targetAddress = targetAddress;
            this.originalSize = originalInstructionSize;
            int bytesRead;
            if (!ReadProcessMemory(processHandle, targetAddress, originalInstructions, originalInstructionSize, out bytesRead))
            {
                error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"读取原始指令失败: {error}, 进程句柄: {processHandle}, 目标地址: {targetAddress}");
            }

            // 3. 分配代码洞内存并写入指令
            // 计算需要的总空间：代码洞指令 + 跳回指令(5字节)
            uint totalSize = (uint)(codeCaveInstructions.Length + 5);
            allocatedCodeCaveAddress = Alloc(targetAddress, totalSize);

            // 写入代码洞指令
            int bytesWritten;
            if (!WriteProcessMemory(processHandle, allocatedCodeCaveAddress, codeCaveInstructions, (uint)codeCaveInstructions.Length, out bytesWritten))
            {
                throw new InvalidOperationException($"写入代码洞指令失败: {Marshal.GetLastWin32Error()}");
            }

            // 4. 创建并写入从目标地址到代码洞的跳转指令
            byte[] jumpToCodeCave = CreateJumpInstruction(targetAddress, allocatedCodeCaveAddress);
            if (!WriteProcessMemory(processHandle, targetAddress, jumpToCodeCave, 5, out bytesWritten))
            {
                throw new InvalidOperationException($"写入跳转指令失败: {Marshal.GetLastWin32Error()}");
            }

            // 如果原始指令大于5字节，用NOP填充剩余空间
            if (originalInstructionSize > 5)
            {
                byte[] nops = Enumerable.Repeat((byte)0x90, (int)(originalInstructionSize - 5)).ToArray();
                if (!WriteProcessMemory(processHandle, targetAddress + 5, nops, (uint)(originalInstructionSize - 5), out bytesWritten))
                {
                    throw new InvalidOperationException($"填充NOP指令失败: {Marshal.GetLastWin32Error()}");
                }
            }

            // 5. 创建并写入从代码洞返回的跳转指令
            IntPtr returnAddress = new IntPtr(targetAddress.ToInt64() + originalInstructionSize);
            byte[] jumpBack = CreateJumpInstruction(allocatedCodeCaveAddress + (int)codeCaveInstructions.Length, returnAddress);
            if (!WriteProcessMemory(processHandle, allocatedCodeCaveAddress + codeCaveInstructions.Length, jumpBack, 5, out bytesWritten))
            {
                throw new InvalidOperationException($"写入返回跳转指令失败: {Marshal.GetLastWin32Error()}");
            }
        }

        /// <summary>
        /// 创建相对跳转指令
        /// </summary>
        private byte[] CreateJumpInstruction(IntPtr from, IntPtr to)
        {
            // 计算相对地址
            long relativeAddress = to.ToInt64() - (from.ToInt64() + 5);

            // 构造JMP指令字符串
            string jmpInstruction = $"JMP {relativeAddress:X}";

            // 使用Assembler生成机器码
            return Assembler.Assemble(jmpInstruction);
        }

        /// <summary>
        /// 恢复原始代码
        /// </summary>
        public void RestoreOriginalCode()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AutoHook));
            }

            if (originalInstructions == null || originalInstructions.Length == 0)
            {
                throw new InvalidOperationException("没有可恢复的原始代码");
            }

            // 确保进程句柄有效
            if (!IsProcessValid(processHandle))
            {
                if (processHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"无法重新获取进程句柄");
                }
            }

            try
            {
                // 写回原始指令
                int bytesWritten;
                if (!WriteProcessMemory(processHandle, targetAddress, originalInstructions, originalSize, out bytesWritten))
                {
                    throw new InvalidOperationException($"恢复原始代码失败: {Marshal.GetLastWin32Error()}");
                }

                // 清理代码洞
                ClearCodeCaves();
                originalInstructions = null;
                targetAddress = IntPtr.Zero;
                originalSize = 0;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"恢复原始代码时发生错误: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 验证进程句柄是否有效
        /// </summary>
        private bool IsProcessValid(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                return false;

            try
            {
                int exitCode;
                [DllImport("kernel32.dll")]
                static extern bool GetExitCodeProcess(IntPtr hProcess, out int lpExitCode);

                return GetExitCodeProcess(handle, out exitCode) && exitCode == 259; // STILL_ACTIVE = 259
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 添加自定义汇编指令
        /// </summary>
        /// <param name="instruction">汇编指令字符串</param>
        public void AddAsmInstruction(string instruction)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AutoHook));
            }

            if (string.IsNullOrWhiteSpace(instruction))
            {
                throw new ArgumentException("指令不能为空", nameof(instruction));
            }

            try
            {
                // 直接使用Assembler生成机器码
                byte[] machineCode = Assembler.Assemble(instruction);
                AddCodeCave(machineCode);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"解析汇编指令失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 添加代码洞指令
        /// </summary>
        /// <param name="instructions">指令字节序列</param>
        public void AddCodeCave(params byte[] instructions)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AutoHook));
            }

            if (instructions == null || instructions.Length == 0)
            {
                throw new ArgumentException("指令序列不能为空", nameof(instructions));
            }

            // 将指令添加到代码洞列表
            codeCaves.Add(instructions);

            // 更新总大小
            totalCodeCaveSize += (uint)instructions.Length;
        }

        /// <summary>
        /// 获取所有已添加的代码洞指令
        /// </summary>
        /// <returns>所有指令的字节数组</returns>
        public byte[] GetCodeCaveInstructions()
        {
            if (codeCaves.Count == 0)
            {
                return Array.Empty<byte>();
            }

            // 计算总大小并创建结果数组
            byte[] result = new byte[totalCodeCaveSize];
            int offset = 0;

            // 合并所有代码洞指令
            foreach (byte[] cave in codeCaves)
            {
                Buffer.BlockCopy(cave, 0, result, offset, cave.Length);
                offset += cave.Length;
            }

            return result;
        }

        /// <summary>
        /// 清除所有已添加的代码洞指令
        /// </summary>
        public void ClearCodeCaves()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AutoHook));
            }

            try
            {
                // 如果有已分配的代码洞内存，释放它
                if (allocatedCodeCaveAddress != IntPtr.Zero)
                {
                    // 确保进程句柄有效
                    if (!IsProcessValid(processHandle))
                    {
                        if (processHandle == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("无法获取进程句柄以释放内存");
                        }
                    }

                    // 释放内存
                    // 注意：使用MEM_RELEASE时，dwSize必须为0
                    if (!VirtualFreeEx(processHandle, allocatedCodeCaveAddress, 0, MEM_RELEASE))
                    {
                        throw new InvalidOperationException(
                            $"释放代码洞内存失败: {Marshal.GetLastWin32Error()}");
                    }
                }
            }
            finally
            {
                // 无论是否成功释放内存，都清理相关状态
                codeCaves.Clear();
                totalCodeCaveSize = 0;
                allocatedCodeCaveAddress = IntPtr.Zero;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // 清理托管资源
                }

                // 清理非托管资源
                if (processHandle != IntPtr.Zero)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        [DllImport("kernel32.dll")]
                        static extern bool CloseHandle(IntPtr hObject);
                        
                        CloseHandle(processHandle);
                    }
                    processHandle = IntPtr.Zero;
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~AutoHook()
        {
            Dispose(false);
        }
    }
}
