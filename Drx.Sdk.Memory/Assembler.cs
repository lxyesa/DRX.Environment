using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Drx.Sdk.Memory
{
    /// <summary>
    /// ���ָ�����ͽ�����
    /// ���ڽ����ָ���ַ���ת��Ϊ��Ӧ�Ļ������ֽ�����
    /// Encapsulates assembly-related instruction encoding and parsing logic
    /// </summary>
    public class Assembler
    {
        /// <summary>
        /// ����ָ��ֵ䣬�洢��ָ��Ĳ�����
        /// key: ָ������, value: ��Ӧ�Ļ������ֽ�����
        /// </summary>
        private static readonly Dictionary<string, byte[]> instructionSet = new Dictionary<string, byte[]>
            {
                { "NOP", new byte[] { 0x90 } }
            };

        // Ԥ�����������ʽ
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
        /// x64�Ĵ���������ձ�
        /// key: �Ĵ�������, value: �Ĵ�������(0-7)
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
        /// �����ָ��ת��Ϊ�������ֽ�����
        /// </summary>
        /// <param name="instruction">���ָ���ַ���</param>
        /// <param name="args">ָ�����</param>
        /// <returns>��Ӧ�Ļ������ֽ�����</returns>
        public static byte[] Assemble(string instruction, params object[] args)
        {

            // ���ⲻ��Ҫ���ַ�����ʽ��
            string formattedInstruction = args.Length > 0 ?
                string.Format(instruction, args) :
                instruction;

            // ʹ�� AsSpan() �����ַ���ǰ׺�Ƚ�
            ReadOnlySpan<char> span = formattedInstruction.AsSpan();

            if (instructionSet.TryGetValue(instruction.ToUpperInvariant(), out var bytecode))
            {
                return bytecode;
            }

            // ʹ�� Span<T> �Ż��ַ����Ƚ�
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
        /// ����MOVָ��ı���
        /// ֧�ָ�ʽ: 
        /// 1. MOV destination, value
        /// 2. MOV byte ptr [register+offset], immediate
        /// 3. MOV BYTE PTR [register+hexoffset], hexvalue
        /// </summary>
        private static byte[] HandleMovInstruction(string instruction, object[] args)
        {
            string formattedInstruction = string.Format(instruction, args);

            // ����ƥ���ʽ: MOV BYTE PTR [register+hexoffset], hexvalue
            // ֧����0x��ͷ��16���Ƹ�ʽ�Ͳ���0x��16���Ƹ�ʽ
            var patterns = new[]
            {
            // ��׼��ʽ��mov byte ptr [register+offset],immediate
            @"mov\s+byte\s+ptr\s*\[(\w+)\+([0-9A-Fa-f]+)\]\s*,\s*([0-9A-Fa-f]+)",
            
            // ��д��ʽ��MOV BYTE PTR [register+0xoffset], 0xvalue
            @"MOV\s+BYTE\s+PTR\s*\[(\w+)\+(0x[0-9A-Fa-f]+|\d+)\]\s*,\s*(0x[0-9A-Fa-f]+|\d+)",
            
            // ��ϸ�ʽ��֧�ֲ�ͬ��Сд���
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

                    // ����ƫ������֧��0xǰ׺��ʮ�����ƺ���ͨ����
                    long offset = ParseHexOrDecimal(offsetStr);

                    // ������������֧��0xǰ׺��ʮ�����ƺ���ͨ����
                    byte immediate = (byte)ParseHexOrDecimal(valueStr);

                    // REXǰ׺ - ����64λ�Ĵ���
                    byte rex = 0x48;

                    // ModRM�ֽ�: mod=10(32λλ��), reg=000(/0), rm=�Ĵ�������
                    byte modRM = (byte)(0x80 | registerCode);

                    return new byte[]
                    {
                    rex,           // REXǰ׺
                    0xC6,         // MOV r/m8, imm8 ������
                    modRM,         // ModRM�ֽ�
                    (byte)(offset & 0xFF),
                    (byte)((offset >> 8) & 0xFF),
                    (byte)((offset >> 16) & 0xFF),
                    (byte)((offset >> 24) & 0xFF),
                    immediate
                    };
                }
            }

            // ����ԭ�и�ʽ
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
        /// ����CMPָ��ı���
        /// ֧�����ָ�ʽ:
        /// 1. CMP qword ptr [register+offset],immediate - �Ƚ��ڴ��е�64λֵ��������
        /// 2. CMP operand1,operand2 - ֱ�ӱȽ�����������
        /// </summary>
        private static byte[] HandleCmpInstruction(string instruction, object[] args)
        {
            string formattedInstruction = string.Format(instruction, args);

            // ƥ���ʽ: cmp qword ptr [register+offset],immediate
            var pattern = @"cmp\s+qword\s+ptr\s*\[(\w+)\+([0-9A-Fa-f]+)\]\s*,\s*([0-9A-Fa-f]+)";
            var match = Regex.Match(formattedInstruction, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                // �����Ĵ�����ƫ������������
                string register = match.Groups[1].Value.ToLower();
                string offsetHex = match.Groups[2].Value;
                string immediateHex = match.Groups[3].Value;

                if (!RegisterCodes.TryGetValue(register, out byte registerCode))
                {
                    throw new ArgumentException($"Unsupported register: {register}");
                }

                long offset = Convert.ToInt64(offsetHex, 16);
                byte immediate = Convert.ToByte(immediateHex, 16);

                // REX.Wǰ׺ - ��ʾ64λ������
                byte rex = 0x48;

                // ModRM�ֽ����:
                // mod(2λ)=10 ��ʾ��32λλ��
                // reg(3λ)=111 ��ʾCMPָ��
                // rm(3λ)=�Ĵ�������
                byte modRM = (byte)(0xB8 | registerCode);

                // ���������Ļ���������
                return new byte[]
                {
                        rex,           // REX.Wǰ׺
                        0x83,          // CMPָ�������
                        modRM,         // ModRM�ֽ�
                        (byte)(offset & 0xFF),          // ƫ�������ֽ�
                        (byte)((offset >> 8) & 0xFF),   // ƫ�������ֽ�
                        (byte)((offset >> 16) & 0xFF),  // ƫ�����θ��ֽ�
                        (byte)((offset >> 24) & 0xFF),  // ƫ�������ֽ�
                        immediate      // ������
                };
            }

            // ������ͨCMPָ���ʽ
            if (args.Length != 2)
            {
                throw new ArgumentException("CMP instruction requires exactly 2 arguments.");
            }

            string[] parts = formattedInstruction.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 3 || !parts[0].Equals("CMP", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid CMP instruction format.");
            }

            // ��������������
            long operand1 = Convert.ToInt64(parts[1], 16);
            byte operand2 = Convert.ToByte(parts[2], 16);

            // ������ͨCMPָ��Ļ�����: 3D + 32λ������1 + 8λ������2
            return new byte[] { 0x3D, (byte)(operand1 & 0xFF), (byte)((operand1 >> 8) & 0xFF), (byte)((operand1 >> 16) & 0xFF), (byte)((operand1 >> 24) & 0xFF), operand2 };
        }

        /// <summary>
        /// ����JMPָ��ı���
        /// ֧�ָ�ʽ��JMP address/offset
        /// </summary>
        private static byte[] HandleJmpInstruction(string instruction, object[] args)
        {
            // ʹ�� Span<T> �Ż��ڴ����
            ReadOnlySpan<char> span = string.Format(instruction, args).AsSpan();

            // ����ָ�����ƺͿո�
            int startIndex = span.IndexOf(' ') + 1;
            if (startIndex <= 0)
            {
                throw new ArgumentException("JMP instruction requires exactly 1 argument.");
            }

            // ��ȡ��ַ����
            ReadOnlySpan<char> addressSpan = span.Slice(startIndex).Trim();

            try
            {
                // ������ַ
                long longAddress = ParseHexOrDecimal(addressSpan);

                if (longAddress > int.MaxValue || longAddress < int.MinValue)
                {
                    throw new ArgumentException($"JMP offset {longAddress} is too large for a relative jump");
                }

                int relativeAddress = (int)longAddress;

                // ʹ��ջ�����Ż�С����
                Span<byte> result = stackalloc byte[5];
                result[0] = 0xE9;  // JMP rel32 ������

                // ֱ��д���ֽڣ����������������
                result[1] = (byte)(relativeAddress & 0xFF);
                result[2] = (byte)((relativeAddress >> 8) & 0xFF);
                result[3] = (byte)((relativeAddress >> 16) & 0xFF);
                result[4] = (byte)((relativeAddress >> 24) & 0xFF);

                // �������ս��
                return result.ToArray();
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Invalid JMP target address: {addressSpan.ToString()}", ex);
            }
        }

        /// <summary>
        /// ����DBָ��ı���
        /// ֧�ָ�ʽ��DB byte1, byte2, ...
        /// </summary>
        private static byte[] HandleDbInstruction(string instruction)
        {
            // �Ƴ�ָ��ǰ׺���ָ��ֽ��ַ���
            string[] byteStrings = instruction.Substring(2).Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // ��ÿ���ֽ��ַ���ת��Ϊ�ֽ�
            byte[] bytes = byteStrings.Select(b => Convert.ToByte(b, 16)).ToArray();

            return bytes;
        }

        /// <summary>
        /// ����ʮ�����ƻ�ʮ�����ַ���
        /// </summary>
        private static long ParseHexOrDecimal(ReadOnlySpan<char> value)
        {
            // �Ƴ���β�ո�
            value = value.Trim();

            // ����Ƿ�Ϊ16���Ƹ�ʽ����0x��ͷ��
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToInt64(value.Slice(2).ToString(), 16);
            }

            // ʹ��stackalloc����ѷ���
            Span<char> validHexChars = stackalloc char[] { '0','1','2','3','4','5','6','7','8','9',
                                                              'a','b','c','d','e','f','A','B','C','D','E','F' };

            // ������Ϊʮ�����ƽ���������0xǰ׺��
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

            // �������Ϊʮ���ƽ���
            return long.Parse(value);
        }
    }
}
