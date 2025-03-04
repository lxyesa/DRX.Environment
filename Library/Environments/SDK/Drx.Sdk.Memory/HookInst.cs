using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Functions;
using Drx.Sdk.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Drx.Sdk.Memory
{
    /// <summary>
    /// 表示一个Hook实例，提供Hook的激活、禁用和代码注入功能
    /// </summary>
    [ScriptClass("HookInst")]
    public class HookInst : IDisposable
    {
        // 常量定义
        private const int JMP_INSTRUCTION_SIZE = 5; // JMP指令的长度

        // 地址变量字典，用于存储和管理内存地址
        private Dictionary<string, IntPtr> addressVariable;

        // 实例属性
        private readonly IntPtr processHandle;
        private IntPtr targetAddress;
        private readonly string hookName;
        private IntPtr allocatedMemory;
        private List<CodeCaveInstruction> instructions;
        private byte[] originalBytes;
        private bool isEnabled;
        private bool disposed;

        // 属性
        public string Name => hookName;
        public bool IsEnabled => isEnabled;
        
        /// <summary>
        /// 创建一个Hook实例
        /// </summary>
        /// <param name="processHandle">目标进程的句柄</param>
        /// <param name="targetAddress">Hook的目标地址</param>
        /// <param name="hookName">Hook实例的名称</param>
        /// <param name="allocatedMemory">分配的内存地址</param>
        public HookInst(IntPtr processHandle, IntPtr targetAddress, string hookName, IntPtr allocatedMemory)
        {
            this.processHandle = processHandle;
            this.targetAddress = targetAddress;
            this.hookName = hookName;
            this.allocatedMemory = allocatedMemory;
            this.instructions = new List<CodeCaveInstruction>();
            this.addressVariable = new Dictionary<string, IntPtr>();
            this.isEnabled = false;
            this.disposed = false;
            
            // 如果目标地址有效，备份原始字节
            if (targetAddress != IntPtr.Zero)
            {
                BackupOriginalBytes();
            }

            // 将 allocatedMemory 添加到地址变量字典，命名为 "inject"
            addressVariable.Add("inject", allocatedMemory);
        }

        /// <summary>
        /// 添加一个地址变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="baseAddress">目标地址</param>
        /// <param name="processName">目标进程名</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public IntPtr AddAddressVariable(string name, IntPtr targetAddress)
        {
            if (addressVariable.ContainsKey(name))
            {
                throw new InvalidOperationException($"地址变量 {name} 已存在");
            }

            var alloc = MemoryWriter.Alloc(targetAddress, 1024, processHandle);
            addressVariable.Add(name, alloc);
            return alloc;
        }

        /// <summary>
        /// 获取地址变量
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public IntPtr GetAddressVariable(string name)
        {
            if (addressVariable.ContainsKey(name))
            {
                return addressVariable[name];
            }
            return IntPtr.Zero;
        }


        /// <summary>
        /// 移除地址变量
        /// </summary>
        /// <param name="name"></param>
        public void RemoveAddressVariable(string name)
        {
            if (addressVariable.ContainsKey(name))
            {
                addressVariable.Remove(name);
            }
        }

        public IntPtr AllocAddressVariable(string name, uint size, IntPtr baseAddress)
        {
            IntPtr address = MemoryWriter.Alloc(baseAddress, size, processHandle);
            return AddAddressVariable(name, address);
        }

        /// <summary>
        /// 设置Hook的目标地址
        /// </summary>
        /// <param name="address">目标地址</param>
        /// <param name="allocSize">分配内存大小</param>
        public void SetTargetAddress(IntPtr address, uint allocSize = 1024)
        {
            if (isEnabled)
            {
                throw new InvalidOperationException("无法在Hook启用时更改目标地址");
            }
            
            targetAddress = address;
            
            // 如果之前已分配内存，先释放
            if (allocatedMemory != IntPtr.Zero)
            {
                MemoryWriter.Free(allocatedMemory, processHandle);
                allocatedMemory = IntPtr.Zero;
            }
            
            // 重新分配内存
            allocatedMemory = MemoryWriter.Alloc(address, allocSize, processHandle);
            
            // 备份原始字节
            BackupOriginalBytes();
        }

        /// <summary>
        /// 添加汇编指令
        /// </summary>
        /// <param name="asmCode">汇编代码</param>
        /// <param name="isJumpBack">是否是跳回原地址的指令</param>
        /// <returns>当前实例，支持链式调用</returns>
        public HookInst AddAsm(string asmCode, bool isJumpBack = false)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(HookInst));

            if (allocatedMemory == IntPtr.Zero)
                throw new InvalidOperationException("必须先设置有效的目标地址");

            // 解析各种格式的地址引用
            asmCode = ParseModuleReferences(asmCode);
            asmCode = ParseAllocReferences(asmCode);
            asmCode = ParseLegacyModuleReferences(asmCode);
            asmCode = ParseLegacyVarReferences(asmCode);

            CodeCaveInstruction instruction = new CodeCaveInstruction
            {
                Index = instructions.Count,
                OriginalAsm = asmCode,
                IsJumpBack = isJumpBack
            };

            instructions.Add(instruction);
            return this;
        }

        /// <summary>
        /// 添加汇编指令，指定目标地址，当内嵌脚本&alloc()无法找到可用地址时，不再掷出异常，而是自动根据目标地址申请内存
        /// </summary>
        /// <param name="asmCode">汇编代码</param>
        /// <param name="targetAddress">目标地址</param>
        /// <returns>当前实例，支持链式调用</returns>
        public HookInst AddAsm(string asmCode, nint targetAddress)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(HookInst));

            if (allocatedMemory == IntPtr.Zero)
                throw new InvalidOperationException("必须先设置有效的目标地址");

            // 解析各种格式的地址引用
            asmCode = ParseModuleReferences(asmCode);
            asmCode = ParseAllocReferences(asmCode);
            asmCode = ParseLegacyModuleReferences(asmCode);
            asmCode = ParseLegacyVarReferences(asmCode);

            CodeCaveInstruction instruction = new CodeCaveInstruction
            {
                Index = instructions.Count,
                OriginalAsm = asmCode,
                IsJumpBack = false
            };

            instructions.Add(instruction);
            return this;
        }

        /// <summary>
        /// 解析 &module(模块名) 格式的地址引用
        /// </summary>
        /// <param name="asmCode">汇编代码</param>
        /// <returns>解析后的汇编代码</returns>
        private string ParseModuleReferences(string asmCode)
        {
            string modulePattern = @"&module\(([^)]+)\)(?:\+([^,\s]+))?";
            var moduleMatches = System.Text.RegularExpressions.Regex.Matches(asmCode, modulePattern);

            foreach (System.Text.RegularExpressions.Match moduleMatch in moduleMatches)
            {
                string moduleName = moduleMatch.Groups[1].Value;
                string offsetStr = moduleMatch.Groups[2].Success ? moduleMatch.Groups[2].Value : "0";

                // 获取模块基地址
                IntPtr moduleBaseAddress = MemoryReader.GetModuleBaseAddress(moduleName, processHandle);
                if (moduleBaseAddress == IntPtr.Zero)
                    throw new InvalidOperationException($"无法获取模块 {moduleName} 的基地址");

                // 解析偏移地址
                long offset = ParseOffset(offsetStr);

                // 计算最终地址
                IntPtr finalAddress = new IntPtr(moduleBaseAddress.ToInt64() + offset);

                // 替换原始指令中的 &module(xxxx.dll)+offset 部分
                asmCode = asmCode.Replace(moduleMatch.Value, $"0x{finalAddress.ToInt64():X}");
            }

            return asmCode;
        }

        /// <summary>
        /// 解析 &alloc(变量名) 格式的地址引用
        /// </summary>
        /// <param name="asmCode">汇编代码</param>
        /// <returns>解析后的汇编代码</returns>
        private string ParseAllocReferences(string asmCode, nint targerAddress = 0x00)
        {
            string allocPattern = @"&alloc\(([^)]+)\)(?:\+([^,\s]+))?";
            var allocMatches = System.Text.RegularExpressions.Regex.Matches(asmCode, allocPattern);

            foreach (System.Text.RegularExpressions.Match allocMatch in allocMatches)
            {
                string varName = allocMatch.Groups[1].Value;
                string offsetStr = allocMatch.Groups[2].Success ? allocMatch.Groups[2].Value : string.Empty;

                // 从地址变量字典中获取地址
                if (!addressVariable.TryGetValue(varName, out IntPtr varAddress))
                    if (targerAddress != 0x00)
                    {
                        varAddress = MemoryWriter.Alloc(targerAddress, 10, processHandle);
                        addressVariable.Add(varName, varAddress);
                    }
                    else { throw new InvalidOperationException($"地址变量 '{varName}' 不存在"); }


                long finalAddress = varAddress.ToInt64();

                // 如果有偏移量，计算最终地址
                if (!string.IsNullOrEmpty(offsetStr))
                {
                    finalAddress += ParseOffset(offsetStr);
                }

                // 替换指令中的变量引用为实际地址
                asmCode = asmCode.Replace(allocMatch.Value, $"0x{finalAddress:X}");
            }

            return asmCode;
        }

        /// <summary>
        /// 解析旧的 {moduleName+offset} 格式（保留向后兼容性）
        /// </summary>
        /// <param name="asmCode">汇编代码</param>
        /// <returns>解析后的汇编代码</returns>
        private string ParseLegacyModuleReferences(string asmCode)
        {
            string oldModulePattern = @"\{([^[\]]+)\+([^{}]+)\}";
            var oldModuleMatch = System.Text.RegularExpressions.Regex.Match(asmCode, oldModulePattern);

            if (oldModuleMatch.Success)
            {
                string moduleName = oldModuleMatch.Groups[1].Value;
                string offsetStr = oldModuleMatch.Groups[2].Value;

                // 获取模块基地址
                IntPtr moduleBaseAddress = MemoryReader.GetModuleBaseAddress(moduleName, processHandle);
                if (moduleBaseAddress == IntPtr.Zero)
                    throw new InvalidOperationException($"无法获取模块 {moduleName} 的基地址");

                // 解析偏移地址
                if (!long.TryParse(offsetStr, System.Globalization.NumberStyles.HexNumber, null, out long offset))
                    throw new InvalidOperationException($"无法解析偏移地址 {offsetStr}");

                // 计算最终地址
                IntPtr finalAddress = new IntPtr(moduleBaseAddress.ToInt64() + offset);

                // 替换原始指令中的 {xxxx.dll+xxx} 部分
                asmCode = asmCode.Replace(oldModuleMatch.Value, $"0x{finalAddress.ToInt64():X}");
            }

            return asmCode;
        }

        /// <summary>
        /// 解析旧的 {[addressVariable]} 或 {[addressVariable]+offset} 格式（保留向后兼容性）
        /// </summary>
        /// <param name="asmCode">汇编代码</param>
        /// <returns>解析后的汇编代码</returns>
        private string ParseLegacyVarReferences(string asmCode)
        {
            string oldVarPattern = @"\{\[([^\]]+)\](?:\+([^{}]+))?\}";
            var oldVarMatch = System.Text.RegularExpressions.Regex.Match(asmCode, oldVarPattern);

            while (oldVarMatch.Success)
            {
                string varName = oldVarMatch.Groups[1].Value;
                string offsetStr = oldVarMatch.Groups[2].Success ? oldVarMatch.Groups[2].Value : string.Empty;

                // 从地址变量字典中获取地址
                if (!addressVariable.TryGetValue(varName, out IntPtr varAddress))
                    throw new InvalidOperationException($"地址变量 '{varName}' 不存在");

                long finalAddress = varAddress.ToInt64();

                // 如果有偏移量，计算最终地址
                if (!string.IsNullOrEmpty(offsetStr))
                {
                    finalAddress += ParseLegacyOffset(offsetStr);
                }

                // 替换指令中的变量引用为实际地址
                asmCode = asmCode.Replace(oldVarMatch.Value, $"0x{finalAddress:X}");

                // 继续查找下一个匹配项
                oldVarMatch = oldVarMatch.NextMatch();
            }

            return asmCode;
        }

        /// <summary>
        /// 解析偏移量字符串（支持多种格式）
        /// </summary>
        /// <param name="offsetStr">偏移量字符串</param>
        /// <returns>解析后的偏移量</returns>
        private long ParseOffset(string offsetStr)
        {
            if (string.IsNullOrEmpty(offsetStr))
                return 0;

            // 尝试解析偏移量（支持十六进制或十进制）
            if (offsetStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                // 以0x开头，处理为十六进制
                if (!long.TryParse(offsetStr.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out long offset))
                    throw new InvalidOperationException($"无法解析偏移量 '{offsetStr}'");
                return offset;
            }
            else if (offsetStr.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            {
                // 以h结尾，处理为十六进制
                if (!long.TryParse(offsetStr.Substring(0, offsetStr.Length - 1),
                    System.Globalization.NumberStyles.HexNumber, null, out long offset))
                    throw new InvalidOperationException($"无法解析十六进制偏移量 '{offsetStr}'");
                return offset;
            }
            else
            {
                // 尝试解析为十进制或十六进制（无前缀）
                if (!long.TryParse(offsetStr, System.Globalization.NumberStyles.HexNumber, null, out long offset) &&
                    !long.TryParse(offsetStr, out offset))
                    throw new InvalidOperationException($"无法解析偏移量 '{offsetStr}'");
                return offset;
            }
        }

        /// <summary>
        /// 解析传统格式的偏移量（为了向后兼容）
        /// </summary>
        /// <param name="offsetStr">偏移量字符串</param>
        /// <returns>解析后的偏移量</returns>
        private long ParseLegacyOffset(string offsetStr)
        {
            if (string.IsNullOrEmpty(offsetStr))
                return 0;

            // 尝试解析偏移量（支持十六进制或十进制）
            if (offsetStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                // 以0x开头，处理为十六进制
                if (!long.TryParse(offsetStr.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out long offset))
                    throw new InvalidOperationException($"无法解析偏移量 '{offsetStr}'");
                return offset;
            }
            else
            {
                // 处理为十进制或尝试十六进制
                bool isHex = offsetStr.EndsWith("h", StringComparison.OrdinalIgnoreCase);
                if (isHex)
                {
                    // 以h结尾，处理为十六进制
                    if (!long.TryParse(offsetStr.Substring(0, offsetStr.Length - 1),
                        System.Globalization.NumberStyles.HexNumber, null, out long offset))
                        throw new InvalidOperationException($"无法解析十六进制偏移量 '{offsetStr}'");
                    return offset;
                }
                else
                {
                    // 尝试解析为十进制或无前缀十六进制
                    if (!long.TryParse(offsetStr, System.Globalization.NumberStyles.HexNumber, null, out long offset) &&
                        !long.TryParse(offsetStr, out offset))
                        throw new InvalidOperationException($"无法解析偏移量 '{offsetStr}'");
                    return offset;
                }
            }
        }

        /// <summary>
        /// 在系统构建的 jmp 指令后面添加汇编指令
        /// </summary>
        /// <param name="asmCode">汇编代码</param>
        /// <returns>当前实例，支持链式调用</returns>
        public HookInst AddJumpAsm(string asmCode)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(HookInst));

            if (allocatedMemory == IntPtr.Zero)
                throw new InvalidOperationException("必须先设置有效的目标地址");

            // 计算当前指令应该放置的地址
            long baseOffset = targetAddress.ToInt64() + JMP_INSTRUCTION_SIZE;
            int offsetFromJmp = 0;

            // 找出已存在的JumpAsm指令，计算新指令的偏移
            var existingAfterJumps = instructions.Where(i => i.IsAfterJump).ToList();
            if (existingAfterJumps.Any())
            {
                // 计算所有已有的跳转后指令的总字节数
                foreach (var instr in existingAfterJumps)
                {
                    if (instr.Bytes == null)
                    {
                        // 如果尚未编译，先尝试编译一下以获取大小
                        instr.Bytes = AssemblerHelper.ToBytes64(instr.OriginalAsm, instr.Address);
                        instr.Size = instr.Bytes != null ? instr.Bytes.Length : 0;
                    }
                    offsetFromJmp += instr.Size;
                }
            }

            // 创建指令对象
            CodeCaveInstruction instruction = new CodeCaveInstruction
            {
                Index = instructions.Count,
                OriginalAsm = asmCode,
                IsJumpBack = false,
                IsAfterJump = true,
                // 新指令地址 = 基地址 + 已有指令总偏移量
                Address = new IntPtr(baseOffset + offsetFromJmp)
            };

            instructions.Add(instruction);
            return this;
        }

        /// <summary>
        /// 启用Hook
        /// </summary>
        /// <returns>操作是否成功</returns>
        public bool Enable()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(HookInst));

            if (targetAddress == IntPtr.Zero || allocatedMemory == IntPtr.Zero)
                throw new InvalidOperationException("目标地址或分配内存无效");

            // 无论如何，先备份原始字节
            BackupOriginalBytes();

            // 如果已启用，则禁用（切换状态）
            if (isEnabled)
            {
                return Disable();
            }

            try
            {
                // 编译指令
                CompileInstructions();
                
                // 写入跳转指令
                WriteJumpToHook();
                
                isEnabled = true;
                return true;
            }
            catch (Exception ex)
            {
                // 发生错误时尝试恢复原始代码
                try
                {
                    RestoreOriginalBytes();
                }
                catch
                {
                    // 恢复失败，忽略
                }
                
                // 重新抛出异常
                throw new InvalidOperationException($"启用Hook失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 禁用Hook
        /// </summary>
        /// <returns>操作是否成功</returns>
        public bool Disable()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(HookInst));

            if (!isEnabled)
                return true; // 已经禁用

            try
            {
                // 恢复原始字节
                RestoreOriginalBytes();
                
                isEnabled = false;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #region Private Methods
        /// <summary>
        /// 备份目标地址的原始字节
        /// </summary>
        private void BackupOriginalBytes()
        {
            // 获取需要备份的基本字节数量
            int targetSize = GetTargetInstructionSize();
            // 确保至少是JMP指令的大小
            targetSize = Math.Max(targetSize, (int)JMP_INSTRUCTION_SIZE);

            // 检查是否有跳转后代码，如果有，计算总大小
            if (HasAfterJumpCode())
            {
                int afterJumpSize = CalculateAfterJumpCodeSize();
                // 备份尺寸需要包括基本尺寸加上跳转后代码的尺寸
                targetSize = Math.Max(targetSize, JMP_INSTRUCTION_SIZE + afterJumpSize);
            }

            // 确保备份足够的字节
            originalBytes = new byte[targetSize];
            int bytesRead;

            if (!Kernel32.ReadProcessMemory(
                processHandle,
                targetAddress,
                originalBytes,
                (uint)originalBytes.Length,
                out bytesRead))
            {
                throw new InvalidOperationException($"无法读取目标内存: {Marshal.GetLastWin32Error()}");
            }

            if (bytesRead != originalBytes.Length)
            {
                throw new InvalidOperationException("无法读取足够的原始字节");
            }
        }


        /// <summary>
        /// 恢复目标地址的原始字节
        /// </summary>
        private void RestoreOriginalBytes()
        {
            if (originalBytes == null || originalBytes.Length == 0)
                return;
                
            uint oldProtect;
            
            // 修改内存保护
            Kernel32.VirtualProtectEx(
                processHandle, 
                targetAddress, 
                (uint)originalBytes.Length, 
                Kernel32.PAGE_EXECUTE_READWRITE, 
                out oldProtect);
                
            // 写回原始字节
            int bytesWritten;
            if (!Kernel32.WriteProcessMemory(
                processHandle, 
                targetAddress, 
                originalBytes, 
                (uint)originalBytes.Length, 
                out bytesWritten))
            {
                throw new InvalidOperationException($"恢复原始字节失败: {Marshal.GetLastWin32Error()}");
            }
            
            // 恢复内存保护
            Kernel32.VirtualProtectEx(
                processHandle, 
                targetAddress, 
                (uint)originalBytes.Length, 
                oldProtect, 
                out _);
                
            // 刷新指令缓存
            Kernel32.FlushInstructionCache(processHandle, targetAddress, (UIntPtr)originalBytes.Length);
        }

        /// <summary>
        /// 编译指令列表
        /// </summary>
        private void CompileInstructions()
        {
            if (instructions.Count == 0)
                return;

            // 检查用户是否已经添加了跳回指令
            bool hasJumpBack = instructions.Any(i => i.IsJumpBack);
            
            // 如果没有明确的跳回指令，自动添加一条
            if (!hasJumpBack)
            {
                AddAsm("", true); // 添加空指令，但标记为跳回指令
            }

            // 当前指令的偏移地址，从分配的内存开始
            IntPtr currentAddress = allocatedMemory;
            
            // 首先计算每条指令的地址和字节码
            foreach (var instruction in instructions)
            {
                // 如果为在入口的JMP后添加的指令，跳过
                if (instruction.IsAfterJump)
                    continue;
                // 设置指令地址
                instruction.Address = currentAddress;

                if (instruction.IsJumpBack)
                {
                    // 如果是跳回原始代码的指令，生成JMP指令跳回到原始代码
                    // 计算需要跳转的目标地址（原始地址 + 原始指令大小）
                    int targetSize = GetTargetInstructionSize();
                    targetSize = Math.Max(targetSize, (int)JMP_INSTRUCTION_SIZE); // 确保至少是JMP指令的大小

                    // 检查是否有跳转后代码
                    if (HasAfterJumpCode())
                    {
                        // 如果有跳转后代码，需要跳过这些代码
                        int afterJumpSize = CalculateAfterJumpCodeSize();
                        targetSize += afterJumpSize;
                    }

                    IntPtr jumpBackTarget = new IntPtr(targetAddress.ToInt64() + targetSize);

                    // 构造跳回指令：JMP [原始地址 + 原始指令大小 + 跳转后代码大小(如果有)]
                    string jumpBackAsm = $"jmp 0x{jumpBackTarget.ToInt64():X}";

                    // 使用AssemblerHelper编译跳回指令
                    instruction.Bytes = AssemblerHelper.ToBytes64(jumpBackAsm, instruction.Address);
                }
                else if (!string.IsNullOrEmpty(instruction.OriginalAsm))
                {
                    // 普通指令，直接编译
                    instruction.Bytes = AssemblerHelper.ToBytes64(instruction.OriginalAsm, instruction.Address);
                }
                else
                {
                    // 空指令，不需要编译
                    instruction.Bytes = new byte[0];
                }
                
                // 更新指令大小
                instruction.Size = instruction.Bytes != null ? instruction.Bytes.Length : 0;
                
                // 更新下一条指令的地址
                currentAddress = new IntPtr(currentAddress.ToInt64() + instruction.Size);
            }
            
            // 将编译后的指令写入分配的内存
            WriteInstructionsToMemory();
        }

        /// <summary>
        /// 将编译好的指令写入分配的内存空间
        /// </summary>
        private void WriteInstructionsToMemory()
        {
            uint oldProtect;
            
            // 修改内存保护
            Kernel32.VirtualProtectEx(
                processHandle, 
                allocatedMemory, 
                GetTotalInstructionSize(), 
                Kernel32.PAGE_EXECUTE_READWRITE, 
                out oldProtect);

            foreach (var instruction in instructions)
            {
                if (instruction.Bytes == null || instruction.Bytes.Length == 0)
                    continue;
                    
                int bytesWritten;
                if (!Kernel32.WriteProcessMemory(
                    processHandle, 
                    instruction.Address, 
                    instruction.Bytes, 
                    (uint)instruction.Bytes.Length, 
                    out bytesWritten))
                {
                    throw new InvalidOperationException($"写入指令失败: {Marshal.GetLastWin32Error()}");
                }
            }
            
            // 恢复内存保护
            Kernel32.VirtualProtectEx(
                processHandle, 
                allocatedMemory, 
                GetTotalInstructionSize(), 
                oldProtect, 
                out _);
                
            // 刷新指令缓存
            Kernel32.FlushInstructionCache(processHandle, allocatedMemory, (UIntPtr)GetTotalInstructionSize());
        }

        /// <summary>
        /// 计算所有指令总大小
        /// </summary>
        private uint GetTotalInstructionSize()
        {
            uint totalSize = 0;
            foreach (var instruction in instructions)
            {
                totalSize += (uint)instruction.Size;
            }
            return totalSize;
        }

        /// <summary>
        /// 获取目标地址的指令字节大小
        /// </summary>
        private int GetTargetInstructionSize()
        {
            int size = 0;
            try
            {
                size = Disassembler.EstimateInstructionSize(processHandle, targetAddress, 1);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"获取目标地址指令大小失败: {ex.Message}");
                System.Console.WriteLine($"在: {ex.StackTrace}");
            }

            return size;
        }

        ///<summary>
        /// 计算要填充的NOP指令数量
        ///</summary>
        private int CalculateNopCount()
        {
            int targetSize = GetTargetInstructionSize();
            int totalSize = (int)JMP_INSTRUCTION_SIZE;
            return targetSize - totalSize;
        }

        /// <summary>
        /// 计算跳转后代码的总大小
        /// </summary>
        private int CalculateAfterJumpCodeSize()
        {
            int totalSize = 0;

            // 找出所有在JMP后添加的指令
            var afterJumpInstructions = instructions.Where(i => i.IsAfterJump).ToList();

            // 编译这些指令并计算总大小
            foreach (var instruction in afterJumpInstructions)
            {
                if (!string.IsNullOrEmpty(instruction.OriginalAsm))
                {
                    // 如果指令字节码尚未编译，先编译
                    if (instruction.Bytes == null)
                    {
                        instruction.Bytes = AssemblerHelper.ToBytes64(instruction.OriginalAsm, instruction.Address);
                        instruction.Size = instruction.Bytes != null ? instruction.Bytes.Length : 0;
                    }
                    totalSize += instruction.Size;
                }
            }

            return totalSize;
        }

        private bool HasAfterJumpCode()
        {
            return instructions.Any(i => i.IsAfterJump);
        }

        private int GetAfterJumpCodeCount()
        {
            return instructions.Count(i => i.IsAfterJump);
        }

        /// <summary>
        /// 写入跳转指令到目标地址
        /// </summary>
        private void WriteJumpToHook()
        {
            // 计算需要填充的NOP数量
            int nopCount = CalculateNopCount();
            // 确保NOP数量不为负数
            nopCount = Math.Max(0, nopCount);

            // 计算总指令大小（JMP + NOP填充）
            int totalInstructionSize = (int)JMP_INSTRUCTION_SIZE;

            // 是否有跳转后代码
            bool hasAfterJumpCode = HasAfterJumpCode();

            // 如果没有跳转后代码，则需要填充NOP
            if (!hasAfterJumpCode)
            {
                totalInstructionSize += nopCount;
            }

            // 创建包含JMP指令和可能的NOP填充的字节数组
            byte[] jumpInstruction = new byte[totalInstructionSize];
            jumpInstruction[0] = 0xE9; // JMP opcode

            // 计算跳转偏移量 = 目标地址 - 当前地址 - 5
            int offset = (int)(allocatedMemory.ToInt64() - targetAddress.ToInt64() - JMP_INSTRUCTION_SIZE);

            // 写入偏移量（小端序）
            jumpInstruction[1] = (byte)(offset & 0xFF);
            jumpInstruction[2] = (byte)((offset >> 8) & 0xFF);
            jumpInstruction[3] = (byte)((offset >> 16) & 0xFF);
            jumpInstruction[4] = (byte)((offset >> 24) & 0xFF);

            // 如果没有跳转后代码，则填充NOP指令
            if (!hasAfterJumpCode)
            {
                // 填充NOP指令（0x90是NOP操作码）
                for (int i = 0; i < nopCount; i++)
                {
                    jumpInstruction[JMP_INSTRUCTION_SIZE + i] = 0x90; // NOP opcode
                }
            }

            uint oldProtect;

            // 修改内存保护 - 使用完整的指令大小(包括原始指令大小)来确保后续写入也受保护
            int protectSize = Math.Max(totalInstructionSize, GetTargetInstructionSize());
            if (hasAfterJumpCode)
            {
                // 如果有跳转后代码，需要确保保护范围足够大
                int afterJumpSize = CalculateAfterJumpCodeSize();
                protectSize = Math.Max(protectSize, JMP_INSTRUCTION_SIZE + afterJumpSize);
            }

            Kernel32.VirtualProtectEx(
                processHandle,
                targetAddress,
                (uint)protectSize,
                Kernel32.PAGE_EXECUTE_READWRITE,
                out oldProtect);

            // 1. 首先写入跳转指令
            int bytesWritten;
            if (!Kernel32.WriteProcessMemory(
                processHandle,
                targetAddress,
                jumpInstruction,
                (uint)jumpInstruction.Length,
                out bytesWritten))
            {
                throw new InvalidOperationException($"写入跳转指令失败: {Marshal.GetLastWin32Error()}");
            }

            // 2. 如果有跳转后代码，在跳转指令写入后再写入
            if (hasAfterJumpCode)
            {
                var afterJumpInstructions = instructions.Where(i => i.IsAfterJump)
                    .OrderBy(i => i.Address.ToInt64()) // 按地址排序，确保顺序正确
                    .ToList();

                // 写入这些指令
                foreach (var instruction in afterJumpInstructions)
                {
                    if (instruction.Bytes == null || instruction.Bytes.Length == 0)
                        continue;

                    if (!Kernel32.WriteProcessMemory(
                        processHandle,
                        instruction.Address,
                        instruction.Bytes,
                        (uint)instruction.Bytes.Length,
                        out bytesWritten))
                    {
                        throw new InvalidOperationException($"写入JMP后指令失败: {Marshal.GetLastWin32Error()}");
                    }
                }
            }

            // 恢复内存保护
            Kernel32.VirtualProtectEx(
                processHandle,
                targetAddress,
                (uint)protectSize,
                oldProtect,
                out _);

            // 刷新指令缓存
            Kernel32.FlushInstructionCache(processHandle, targetAddress, (UIntPtr)protectSize);
        }

        #endregion

        #region IDisposable Implementation
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // 清理托管资源
                    // 如果Hook启用，先禁用
                    if (isEnabled)
                    {
                        try
                        {
                            Disable();
                        }
                        catch
                        {
                            // 忽略异常
                        }
                    }
                    
                    instructions.Clear();
                }

                // 清理非托管资源
                if (allocatedMemory != IntPtr.Zero)
                {
                    try
                    {
                        MemoryWriter.Free(allocatedMemory, processHandle);
                    }
                    catch
                    {
                        // 忽略异常
                    }
                    allocatedMemory = IntPtr.Zero;
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~HookInst()
        {
            Dispose(false);
        }
        #endregion
    }

    /// <summary>
    /// 表示代码洞中的一条指令
    /// </summary>
    [ScriptClass("CCI")]
    public class CodeCaveInstruction
    {
        public int Index { get; set; }          // 指令索引
        public IntPtr Address { get; set; }     // 指令地址
        public byte[] Bytes { get; set; }       // 指令字节
        public int Size { get; set; }           // 指令大小
        public string OriginalAsm { get; set; } // 原始汇编文本
        public bool IsJumpBack { get; set; }    // 是否是跳回原地址的指令
        public bool IsAfterJump { get; set; }   // 是否在入口的JMP后添加
        public IntPtr Offset { get; set; }      // 指针偏移
    }
}
