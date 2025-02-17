using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Drx.Sdk.Memory
{
    /// <summary>
    /// 汇编指令编码和解析类
    /// 用于将汇编指令字符串转换为对应的机器码字节序列
    /// Encapsulates assembly-related instruction encoding and parsing logic
    /// </summary>
    public class Assembler
    {
        /// <summary>
        /// 基础指令集字典，存储简单指令的操作码
        /// key: 指令名称, value: 对应的机器码字节数组
        /// </summary>
        private static readonly Dictionary<string, byte[]> instructionSet = new Dictionary<string, byte[]>
            {
                { "NOP", new byte[] { 0x90 } }
            };

        // 预编译的正则表达式
        private static readonly Regex[] MovPatterns = new[]
        {
                new Regex(@"mov\s+byte\s+ptr\s*\[(\w+)\+([0-9A-Fa-f]+)\]\s*,\s*([0-9A-Fa-f]+)", RegexOptions.Compiled),
                new Regex(@"MOV\s+BYTE\s+PTR\s*\[(\w+)\+(0x[0-9A-Fa-f]+|\d+)\]\s*,\s*(0x[0-9A-Fa-f]+|\d+)", RegexOptions.Compiled),
                new Regex(@"(?i)mov\s+byte\s+ptr\s*\[(\w+)\+(0x[0-9A-Fa-f]+|\d+)\]\s*,\s*(0x[0-9A-Fa-f]+|\d+)", RegexOptions.Compiled)
            };

        private static readonly Regex CmpPattern = new Regex(
            @"cmp\s+qword\s+ptr\s*\[(\w+)\+([0-9A-Fa-f]+)\]\s*,\s*([0-9A-Fa-f]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        /// <summary>
        /// x64寄存器编码对照表
        /// key: 寄存器名称, value: 寄存器编码(0-7)
        /// </summary>
        private static readonly Dictionary<string, byte> RegisterCodes = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
            {
                { "rax", 0 },
                { "rcx", 1 },
                { "rdx", 2 },
                { "rbx", 3 },
                { "rsp", 4 },
                { "rbp", 5 },
                { "rsi", 6 },
                { "rdi", 7 }
            };

        /// <summary>
        /// 将汇编指令转换为机器码字节数组
        /// </summary>
        /// <param name="instruction">汇编指令字符串</param>
        /// <param name="args">指令参数</param>
        /// <returns>对应的机器码字节数组</returns>
        public static byte[] Assemble(string instruction, params object[] args)
        {

            // 避免不必要的字符串格式化
            string formattedInstruction = args.Length > 0 ?
                string.Format(instruction, args) :
                instruction;

            // 使用 AsSpan() 进行字符串前缀比较
            ReadOnlySpan<char> span = formattedInstruction.AsSpan();

            if (instructionSet.TryGetValue(instruction.ToUpperInvariant(), out var bytecode))
            {
                return bytecode;
            }

            // 使用 Span<T> 优化字符串比较
            if (span.StartsWith("MOV", StringComparison.OrdinalIgnoreCase))
            {
                return HandleMovInstruction(formattedInstruction, args);
            }
            if (span.StartsWith("CMP", StringComparison.OrdinalIgnoreCase))
            {
                return HandleCmpInstruction(formattedInstruction, args);
            }
            if (span.StartsWith("JMP", StringComparison.OrdinalIgnoreCase))
            {
                return HandleJmpInstruction(formattedInstruction, args);
            }
            if (span.StartsWith("DB", StringComparison.OrdinalIgnoreCase))
            {
                return HandleDbInstruction(formattedInstruction);
            }
            throw new NotSupportedException($"Instruction '{instruction}' is not supported.");
        }

        /// <summary>
        /// 处理MOV指令的编码
        /// 支持格式: 
        /// 1. MOV destination, value
        /// 2. MOV byte ptr [register+offset], immediate
        /// 3. MOV BYTE PTR [register+hexoffset], hexvalue
        /// </summary>
        private static byte[] HandleMovInstruction(string instruction, object[] args)
        {
            string formattedInstruction = string.Format(instruction, args);

            // 新增匹配格式: MOV BYTE PTR [register+hexoffset], hexvalue
            // 支持以0x开头的16进制格式和不带0x的16进制格式
            var patterns = new[]
            {
            // 标准格式：mov byte ptr [register+offset],immediate
            @"mov\s+byte\s+ptr\s*\[(\w+)\+([0-9A-Fa-f]+)\]\s*,\s*([0-9A-Fa-f]+)",
            
            // 大写格式：MOV BYTE PTR [register+0xoffset], 0xvalue
            @"MOV\s+BYTE\s+PTR\s*\[(\w+)\+(0x[0-9A-Fa-f]+|\d+)\]\s*,\s*(0x[0-9A-Fa-f]+|\d+)",
            
            // 混合格式：支持不同大小写组合
            @"(?i)mov\s+byte\s+ptr\s*\[(\w+)\+(0x[0-9A-Fa-f]+|\d+)\]\s*,\s*(0x[0-9A-Fa-f]+|\d+)"
        };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(formattedInstruction, pattern, RegexOptions.None);
                if (match.Success)
                {
                    string register = match.Groups[1].Value.ToLower();
                    string offsetStr = match.Groups[2].Value;
                    string valueStr = match.Groups[3].Value;

                    if (!RegisterCodes.TryGetValue(register, out byte registerCode))
                    {
                        throw new ArgumentException($"Unsupported register: {register}");
                    }

                    // 解析偏移量，支持0x前缀的十六进制和普通数字
                    long offset = ParseHexOrDecimal(offsetStr);

                    // 解析立即数，支持0x前缀的十六进制和普通数字
                    byte immediate = (byte)ParseHexOrDecimal(valueStr);

                    // REX前缀 - 用于64位寄存器
                    byte rex = 0x48;

                    // ModRM字节: mod=10(32位位移), reg=000(/0), rm=寄存器编码
                    byte modRM = (byte)(0x80 | registerCode);

                    return new byte[]
                    {
                    rex,           // REX前缀
                    0xC6,         // MOV r/m8, imm8 操作码
                    modRM,         // ModRM字节
                    (byte)(offset & 0xFF),
                    (byte)((offset >> 8) & 0xFF),
                    (byte)((offset >> 16) & 0xFF),
                    (byte)((offset >> 24) & 0xFF),
                    immediate
                    };
                }
            }

            // 处理原有格式
            if (args.Length != 2)
            {
                throw new ArgumentException("MOV instruction requires exactly 2 arguments.");
            }

            string[] parts = formattedInstruction.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 3 || !parts[0].Equals("MOV", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid MOV instruction format.");
            }

            long destination = Convert.ToInt64(parts[1], 16);
            byte value = Convert.ToByte(parts[2], 16);

            return new byte[] { 0xC7, 0x05, (byte)(destination & 0xFF), (byte)((destination >> 8) & 0xFF),
                            (byte)((destination >> 16) & 0xFF), (byte)((destination >> 24) & 0xFF), value };
        }

        /// <summary>
        /// 处理CMP指令的编码
        /// 支持两种格式:
        /// 1. CMP qword ptr [register+offset],immediate - 比较内存中的64位值与立即数
        /// 2. CMP operand1,operand2 - 直接比较两个操作数
        /// </summary>
        private static byte[] HandleCmpInstruction(string instruction, object[] args)
        {
            string formattedInstruction = string.Format(instruction, args);

            // 匹配格式: cmp qword ptr [register+offset],immediate
            var pattern = @"cmp\s+qword\s+ptr\s*\[(\w+)\+([0-9A-Fa-f]+)\]\s*,\s*([0-9A-Fa-f]+)";
            var match = Regex.Match(formattedInstruction, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                // 解析寄存器、偏移量和立即数
                string register = match.Groups[1].Value.ToLower();
                string offsetHex = match.Groups[2].Value;
                string immediateHex = match.Groups[3].Value;

                if (!RegisterCodes.TryGetValue(register, out byte registerCode))
                {
                    throw new ArgumentException($"Unsupported register: {register}");
                }

                long offset = Convert.ToInt64(offsetHex, 16);
                byte immediate = Convert.ToByte(immediateHex, 16);

                // REX.W前缀 - 表示64位操作数
                byte rex = 0x48;

                // ModRM字节组成:
                // mod(2位)=10 表示有32位位移
                // reg(3位)=111 表示CMP指令
                // rm(3位)=寄存器编码
                byte modRM = (byte)(0xB8 | registerCode);

                // 生成完整的机器码序列
                return new byte[]
                {
                        rex,           // REX.W前缀
                        0x83,          // CMP指令操作码
                        modRM,         // ModRM字节
                        (byte)(offset & 0xFF),          // 偏移量低字节
                        (byte)((offset >> 8) & 0xFF),   // 偏移量次字节
                        (byte)((offset >> 16) & 0xFF),  // 偏移量次高字节
                        (byte)((offset >> 24) & 0xFF),  // 偏移量高字节
                        immediate      // 立即数
                };
            }

            // 处理普通CMP指令格式
            if (args.Length != 2)
            {
                throw new ArgumentException("CMP instruction requires exactly 2 arguments.");
            }

            string[] parts = formattedInstruction.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 3 || !parts[0].Equals("CMP", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid CMP instruction format.");
            }

            // 解析两个操作数
            long operand1 = Convert.ToInt64(parts[1], 16);
            byte operand2 = Convert.ToByte(parts[2], 16);

            // 生成普通CMP指令的机器码: 3D + 32位操作数1 + 8位操作数2
            return new byte[] { 0x3D, (byte)(operand1 & 0xFF), (byte)((operand1 >> 8) & 0xFF), (byte)((operand1 >> 16) & 0xFF), (byte)((operand1 >> 24) & 0xFF), operand2 };
        }

        /// <summary>
        /// 处理JMP指令的编码
        /// 支持格式：JMP address/offset
        /// </summary>
        private static byte[] HandleJmpInstruction(string instruction, object[] args)
        {
            // 使用 Span<T> 优化内存分配
            ReadOnlySpan<char> span = string.Format(instruction, args).AsSpan();

            // 跳过指令名称和空格
            int startIndex = span.IndexOf(' ') + 1;
            if (startIndex <= 0)
            {
                throw new ArgumentException("JMP instruction requires exactly 1 argument.");
            }

            // 提取地址部分
            ReadOnlySpan<char> addressSpan = span.Slice(startIndex).Trim();

            try
            {
                // 解析地址
                long longAddress = ParseHexOrDecimal(addressSpan);

                if (longAddress > int.MaxValue || longAddress < int.MinValue)
                {
                    throw new ArgumentException($"JMP offset {longAddress} is too large for a relative jump");
                }

                int relativeAddress = (int)longAddress;

                // 使用栈分配优化小数组
                Span<byte> result = stackalloc byte[5];
                result[0] = 0xE9;  // JMP rel32 操作码

                // 直接写入字节，避免额外的数组分配
                result[1] = (byte)(relativeAddress & 0xFF);
                result[2] = (byte)((relativeAddress >> 8) & 0xFF);
                result[3] = (byte)((relativeAddress >> 16) & 0xFF);
                result[4] = (byte)((relativeAddress >> 24) & 0xFF);

                // 返回最终结果
                return result.ToArray();
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Invalid JMP target address: {addressSpan.ToString()}", ex);
            }
        }

        /// <summary>
        /// 处理DB指令的编码
        /// 支持格式：DB byte1, byte2, ...
        /// </summary>
        private static byte[] HandleDbInstruction(string instruction)
        {
            // 移除指令前缀并分割字节字符串
            string[] byteStrings = instruction.Substring(2).Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // 将每个字节字符串转换为字节
            byte[] bytes = byteStrings.Select(b => Convert.ToByte(b, 16)).ToArray();

            return bytes;
        }

        /// <summary>
        /// 解析十六进制或十进制字符串
        /// </summary>
        private static long ParseHexOrDecimal(ReadOnlySpan<char> value)
        {
            // 移除首尾空格
            value = value.Trim();

            // 检查是否为16进制格式（以0x开头）
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToInt64(value.Slice(2).ToString(), 16);
            }

            // 使用stackalloc避免堆分配
            Span<char> validHexChars = stackalloc char[] { '0','1','2','3','4','5','6','7','8','9',
                                                              'a','b','c','d','e','f','A','B','C','D','E','F' };

            // 尝试作为十六进制解析（不带0x前缀）
            bool isHex = true;
            foreach (char c in value)
            {
                if (!validHexChars.Contains(c))
                {
                    isHex = false;
                    break;
                }
            }

            if (isHex)
            {
                return Convert.ToInt64(value.ToString(), 16);
            }

            // 最后尝试作为十进制解析
            return long.Parse(value);
        }
    }
}
