using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Text;
using Decoder = Iced.Intel.Decoder;

namespace Drx.Sdk.Memory
{    public static class Disassembler
    {
        /// <summary>
        /// 反汇编指定内存地址处的指令
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="address">起始内存地址</param>
        /// <param name="instructionCount">要反汇编的指令数量（默认为10）</param>
        /// <returns>反汇编后的指令列表</returns>
        public static List<DisassembledInstruction> Disassemble(IntPtr processHandle, IntPtr address, int instructionCount = 10)
        {
            // 初始读取大小估计，每条指令平均15字节
            int initialSize = instructionCount * 15;

            // 读取内存
            byte[] codeBytes = MemoryReader.ReadMemory(processHandle, address, initialSize);

            // 创建反汇编结果列表
            var result = new List<DisassembledInstruction>();

            // 创建解码器
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(codeBytes), (ulong)address.ToInt64());

            // 创建格式化器，使用Intel语法
            var formatter = new IntelFormatter();
            var output = new StringOutput();

            // 解码并格式化指令
            for (int i = 0; i < instructionCount; i++)
            {
                // 检查是否已经到达代码末尾
                if (decoder.IP >= (ulong)(address.ToInt64() + codeBytes.Length))
                    break;

                // 解码下一条指令
                var instruction = decoder.Decode();

                // 格式化指令
                output.Reset();
                formatter.Format(instruction, output);

                // 创建反汇编指令对象
                var disasmInstruction = new DisassembledInstruction
                {
                    Address = new IntPtr((long)instruction.IP),
                    ByteLength = instruction.Length,
                    InstructionBytes = GetInstructionBytes(codeBytes, (int)(instruction.IP - (ulong)address.ToInt64()), instruction.Length),
                    Mnemonic = instruction.Mnemonic.ToString(),
                    InstructionText = output.ToString()
                };

                // 添加到结果列表
                result.Add(disasmInstruction);
            }

            return result;
        }

        /// <summary>
        /// 获取指令字节码的十六进制表示
        /// </summary>
        private static string GetInstructionBytes(byte[] code, int offset, int length)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < length && offset + i < code.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(code[offset + i].ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 估计给定地址处一组指令的总大小
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="address">起始内存地址</param>
        /// <param name="instructionCount">要分析的指令数量</param>
        /// <returns>估计的字节大小</returns>
        public static int EstimateInstructionSize(IntPtr processHandle, IntPtr address, int instructionCount = 10)
        {
            // 初始读取大小，确保足够大以容纳预期数量的指令
            int initialSize = instructionCount * 15; // 15是一条x64指令的保守估计最大长度

            // 读取内存
            byte[] codeBytes = MemoryReader.ReadMemory(processHandle, address, initialSize);

            // 创建解码器
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(codeBytes), (ulong)address.ToInt64());

            int totalSize = 0;
            int decodedCount = 0;

            // 解码指定数量的指令
            while (decodedCount < instructionCount &&
                   decoder.IP < (ulong)(address.ToInt64() + codeBytes.Length))
            {
                var instruction = decoder.Decode();
                totalSize += instruction.Length;
                decodedCount++;
            }

            return totalSize;
        }

        /// <summary>
        /// 获取第一条指令的大小
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="address">指令地址</param>
        /// <returns>指令的字节大小</returns>
        public static int GetFirstInstructionSize(IntPtr processHandle, IntPtr address)
        {
            // 读取足够多的字节以覆盖一条指令（最大15字节）
            byte[] codeBytes = MemoryReader.ReadMemory(processHandle, address, 15);

            var decoder = Decoder.Create(64, new ByteArrayCodeReader(codeBytes), (ulong)address.ToInt64());
            var instruction = decoder.Decode();

            return instruction.Length;
        }
    }

    /// <summary>
    /// 反汇编指令信息
    /// </summary>
    public class DisassembledInstruction
    {
        /// <summary>指令地址</summary>
        public IntPtr Address { get; set; }

        /// <summary>指令字节长度</summary>
        public int ByteLength { get; set; }

        /// <summary>指令字节（十六进制表示）</summary>
        public string InstructionBytes { get; set; }

        /// <summary>指令助记符</summary>
        public string Mnemonic { get; set; }

        /// <summary>完整指令文本</summary>
        public string InstructionText { get; set; }

        public override string ToString()
        {
            return $"0x{Address.ToInt64():X16}: {InstructionBytes} - {InstructionText}";
        }
    }

    /// <summary>
    /// 简单的字符串输出格式化器
    /// </summary>
    class StringOutput : Iced.Intel.FormatterOutput
    {
        private readonly StringBuilder sb = new StringBuilder();

        public override void Write(string text, Iced.Intel.FormatterTextKind kind)
        {
            sb.Append(text);
        }

        public void Reset()
        {
            sb.Clear();
        }

        public override string ToString()
        {
            return sb.ToString();
        }
    }
}