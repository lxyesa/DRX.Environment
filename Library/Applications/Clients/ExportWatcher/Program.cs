#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ExportWatcher
{
    internal static class Program
    {
        // PE 常量/结构
        private const ushort IMAGE_DOS_SIGNATURE = 0x5A4D;    // "MZ"
        private const uint IMAGE_NT_SIGNATURE = 0x00004550;   // "PE\0\0"

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_DOS_HEADER
        {
            public ushort e_magic;
            public ushort e_cblp; public ushort e_cp; public ushort e_crlc; public ushort e_cparhdr;
            public ushort e_minalloc; public ushort e_maxalloc; public ushort e_ss; public ushort e_sp;
            public ushort e_csum; public ushort e_ip; public ushort e_cs; public ushort e_lfarlc; public ushort e_ovno;
            public ushort e_res1; public ushort e_res2; public ushort e_res3; public ushort e_res4;
            public ushort e_oemid; public ushort e_oeminfo;
            public ushort e_res2_0; public ushort e_res2_1; public ushort e_res2_2; public ushort e_res2_3; public ushort e_res2_4;
            public ushort e_res2_5; public ushort e_res2_6; public ushort e_res2_7; public ushort e_res2_8; public ushort e_res2_9;
            public int e_lfanew;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_FILE_HEADER
        {
            public ushort Machine;
            public ushort NumberOfSections;
            public uint TimeDateStamp;
            public uint PointerToSymbolTable;
            public uint NumberOfSymbols;
            public ushort SizeOfOptionalHeader;
            public ushort Characteristics;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_DATA_DIRECTORY
        {
            public uint VirtualAddress;
            public uint Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_OPTIONAL_HEADER64
        {
            public ushort Magic;
            public byte MajorLinkerVersion; public byte MinorLinkerVersion;
            public uint SizeOfCode; public uint SizeOfInitializedData; public uint SizeOfUninitializedData;
            public uint AddressOfEntryPoint; public uint BaseOfCode;
            public ulong ImageBase;
            public uint SectionAlignment; public uint FileAlignment;
            public ushort MajorOperatingSystemVersion; public ushort MinorOperatingSystemVersion;
            public ushort MajorImageVersion; public ushort MinorImageVersion;
            public ushort MajorSubsystemVersion; public ushort MinorSubsystemVersion;
            public uint Win32VersionValue; public uint SizeOfImage; public uint SizeOfHeaders;
            public uint CheckSum; public ushort Subsystem; public ushort DllCharacteristics;
            public ulong SizeOfStackReserve; public ulong SizeOfStackCommit;
            public ulong SizeOfHeapReserve; public ulong SizeOfHeapCommit;
            public uint LoaderFlags; public uint NumberOfRvaAndSizes;

            public IMAGE_DATA_DIRECTORY ExportTable;
            public IMAGE_DATA_DIRECTORY ImportTable;
            public IMAGE_DATA_DIRECTORY ResourceTable;
            public IMAGE_DATA_DIRECTORY ExceptionTable;
            public IMAGE_DATA_DIRECTORY CertificateTable;
            public IMAGE_DATA_DIRECTORY BaseRelocationTable;
            public IMAGE_DATA_DIRECTORY Debug;
            public IMAGE_DATA_DIRECTORY Architecture;
            public IMAGE_DATA_DIRECTORY GlobalPtr;
            public IMAGE_DATA_DIRECTORY TLSTable;
            public IMAGE_DATA_DIRECTORY LoadConfigTable;
            public IMAGE_DATA_DIRECTORY BoundImport;
            public IMAGE_DATA_DIRECTORY IAT;
            public IMAGE_DATA_DIRECTORY DelayImportDescriptor;
            public IMAGE_DATA_DIRECTORY CLRRuntimeHeader;
            public IMAGE_DATA_DIRECTORY Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_NT_HEADERS64
        {
            public uint Signature;
            public IMAGE_FILE_HEADER FileHeader;
            public IMAGE_OPTIONAL_HEADER64 OptionalHeader;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct IMAGE_SECTION_HEADER
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public byte[] Name;
            public uint Misc_VirtualSize;
            public uint VirtualAddress;
            public uint SizeOfRawData;
            public uint PointerToRawData;
            public uint PointerToRelocations;
            public uint PointerToLinenumbers;
            public ushort NumberOfRelocations;
            public ushort NumberOfLinenumbers;
            public uint Characteristics;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_EXPORT_DIRECTORY
        {
            public uint Characteristics;
            public uint TimeDateStamp;
            public ushort MajorVersion;
            public ushort MinorVersion;
            public uint Name;
            public uint Base;
            public uint NumberOfFunctions;
            public uint NumberOfNames;
            public uint AddressOfFunctions;     // RVA array (DWORD)
            public uint AddressOfNames;         // RVA array (DWORD)
            public uint AddressOfNameOrdinals;  // RVA array (WORD)
        }

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "ExportWatcher - 拖拽 DLL 到窗口列出其导出函数";

            string? targetPath = null;

            if (args.Length > 0)
            {
                targetPath = args[0];
            }
            else
            {
                Console.WriteLine("提示：可将目标 DLL 文件拖拽到本窗口后回车，或直接输入完整路径。");
                Console.Write("> ");
                var line = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                    targetPath = line.Trim().Trim('"');
            }

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                Console.WriteLine("未提供 DLL 路径。");
                return 1;
            }

            if (!File.Exists(targetPath))
            {
                Console.WriteLine($"文件不存在：{targetPath}");
                return 2;
            }

            try
            {
                var exports = ReadNativeExports(targetPath);
                if (exports.Count == 0)
                {
                    Console.WriteLine("未发现导出函数（或仅有按序号导出且未启用显示）。");
                }
                else
                {
                    Console.WriteLine($"文件：{targetPath}");
                    Console.WriteLine($"导出函数计数（具名）：{exports.Count}");
                    foreach (var e in exports)
                    {
                        // 启发式：尝试从导出名推断调用约定/参数总字节数或做 C++ 解码
                        var hint = HeuristicSignatureHint(e.Name);
                        if (!string.IsNullOrEmpty(e.Forwarder))
                            hint = string.IsNullOrEmpty(hint) ? $"-> {e.Forwarder}" : $"{hint} -> {e.Forwarder}";
                        if (string.IsNullOrEmpty(hint))
                            Console.WriteLine($"{e.Ordinal,6}  0x{e.Rva:X8}  {e.Name}");
                        else
                            Console.WriteLine($"{e.Ordinal,6}  0x{e.Rva:X8}  {e.Name}  {hint}");
                    }
                }
                Console.WriteLine();
                Console.WriteLine("按回车键退出，或拖拽新的 DLL 到此窗口并回车以继续解析。");
                Console.Write("> ");
                var next = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(next))
                {
                    var maybePath = next.Trim().Trim('"');
                    if (File.Exists(maybePath))
                    {
                        // 简单循环：再次处理新的路径
                        args = new[] { maybePath };
                        return Main(args);
                    }
                }
                return 0;
            }
            catch (BadImageFormatException)
            {
                Console.WriteLine("该文件不是有效的原生 DLL。");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
                return 3;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析失败：{ex.Message}");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
                return 4;
            }
        }

        private static List<ExportItem> ReadNativeExports(string path)
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            var dos = ReadStruct<IMAGE_DOS_HEADER>(br);
            if (dos.e_magic != IMAGE_DOS_SIGNATURE)
                throw new BadImageFormatException("MZ 头无效。");

            fs.Position = dos.e_lfanew;
            var nt = ReadStruct<IMAGE_NT_HEADERS64>(br);
            if (nt.Signature != IMAGE_NT_SIGNATURE)
                throw new BadImageFormatException("PE 头无效。");

            // 节表
            fs.Position = dos.e_lfanew + Marshal.SizeOf<IMAGE_NT_HEADERS64>();
            var sects = new IMAGE_SECTION_HEADER[nt.FileHeader.NumberOfSections];
            for (int i = 0; i < sects.Length; i++)
                sects[i] = ReadStruct<IMAGE_SECTION_HEADER>(br);

            long RvaToFile(uint rva)
            {
                foreach (var s in sects)
                {
                    var start = s.VirtualAddress;
                    var end = s.VirtualAddress + Math.Max(s.SizeOfRawData, s.Misc_VirtualSize);
                    if (rva >= start && rva < end)
                        return s.PointerToRawData + (rva - s.VirtualAddress);
                }
                throw new InvalidDataException($"无法映射 RVA=0x{rva:X}");
            }

            var expDir = nt.OptionalHeader.ExportTable;
            if (expDir.VirtualAddress == 0 || expDir.Size == 0)
                return new List<ExportItem>();

            fs.Position = RvaToFile(expDir.VirtualAddress);
            var exp = ReadStruct<IMAGE_EXPORT_DIRECTORY>(br);

            var result = new List<ExportItem>();

            // 读取数组
            var namesOfs = RvaToFile(exp.AddressOfNames);
            var ordOfs = RvaToFile(exp.AddressOfNameOrdinals);
            var funcsOfs = RvaToFile(exp.AddressOfFunctions);

            fs.Position = namesOfs;
            var nameRvas = new uint[exp.NumberOfNames];
            for (int i = 0; i < nameRvas.Length; i++) nameRvas[i] = br.ReadUInt32();

            fs.Position = ordOfs;
            var ordinalsRel = new ushort[exp.NumberOfNames];
            for (int i = 0; i < ordinalsRel.Length; i++) ordinalsRel[i] = br.ReadUInt16();

            fs.Position = funcsOfs;
            var funcRvas = new uint[exp.NumberOfFunctions];
            for (int i = 0; i < funcRvas.Length; i++) funcRvas[i] = br.ReadUInt32();

            // 遍历具名导出
            for (int i = 0; i < nameRvas.Length; i++)
            {
                var name = ReadAsciiZ(fs, br, RvaToFile(nameRvas[i]));
                var rva = funcRvas[ordinalsRel[i]];
                var ordinal = exp.Base + ordinalsRel[i];

                // 检测转发导出：若 RVA 位于导出表范围内，通常是一个以 "DLL.Func" 的字符串
                bool isForwarder = rva >= expDir.VirtualAddress && rva < expDir.VirtualAddress + expDir.Size;
                string? forwarder = null;
                if (isForwarder)
                {
                    forwarder = ReadAsciiZ(fs, br, RvaToFile(rva));
                }

                result.Add(new ExportItem
                {
                    Name = name,
                    Ordinal = ordinal,
                    Rva = rva,
                    Forwarder = forwarder
                });
            }

            return result;
        }

        private static T ReadStruct<T>(BinaryReader br) where T : struct
        {
            var size = Marshal.SizeOf<T>();
            var span = br.ReadBytes(size);
            var handle = GCHandle.Alloc(span, GCHandleType.Pinned);
            try { return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject()); }
            finally { handle.Free(); }
        }

        private static string ReadAsciiZ(Stream fs, BinaryReader br, long ofs)
        {
            fs.Position = ofs;
            var buf = new List<byte>(64);
            byte b;
            while ((b = br.ReadByte()) != 0) buf.Add(b);
            return Encoding.ASCII.GetString(buf.ToArray());
        }

        private static string HeuristicSignatureHint(string exportName)
        {
            // 目标：
            // 1) x86 __stdcall: Name@N => 估计参数总字节数 N 与可能参数个数 N/4
            // 2) x86 __cdecl: _Name 前导下划线去掉，仅返回空提示
            // 3) C++ msvc 修饰名: ?Func@@... 解码出可读名（仅粗略），类型信息通常缺失
            // 4) x64: 通常无修饰，不提供参数提示
            if (string.IsNullOrEmpty(exportName)) return string.Empty;

            // 简单处理：去除前导下划线（cdecl in x86）
            var name = exportName;
            if (name.Length > 1 && name[0] == '_' && name.IndexOf('@') < 0 && name[1] != '?')
            {
                name = name[1..];
            }

            // __stdcall (x86) Name@N
            var atIdx = name.LastIndexOf('@');
            if (atIdx > 0 && atIdx < name.Length - 1 && char.IsDigit(name[atIdx + 1]))
            {
                var numStr = name[(atIdx + 1)..];
                if (int.TryParse(numStr, out var bytes) && bytes > 0 && bytes % 4 == 0)
                {
                    var estCount = bytes / 4;
                    return $"[stdcall x86 估计参数总字节数={bytes} 约{estCount}个4字节参数]";
                }
                return $"[stdcall x86 估计参数总字节数={numStr}]";
            }

            // MSVC C++ 修饰名（以 '?' 开头）
            if (exportName.Length > 0 && exportName[0] == '?')
            {
                // 仅做极简“可读名”提取：?FuncName@@... => FuncName
                var secondAt = exportName.IndexOf("@@");
                if (secondAt > 1)
                {
                    var maybe = exportName.Substring(1, secondAt - 1);
                    if (!string.IsNullOrWhiteSpace(maybe))
                        return $"[C++ 修饰名，推断函数名={maybe}]";
                }
                return "[C++ 修饰名，建议用 PDB 获取完整参数]";
            }

            // x64 或已是纯 C 名：不给出参数提示
            return string.Empty;
        }

        private sealed class ExportItem
        {
            public required string Name { get; init; }
            public uint Rva { get; init; }
            public uint Ordinal { get; init; }
            public string? Forwarder { get; init; }
            public override string ToString()
            {
                var hint = HeuristicSignatureHint(Name);
                if (!string.IsNullOrEmpty(Forwarder))
                    hint = string.IsNullOrEmpty(hint) ? $"-> {Forwarder}" : $"{hint} -> {Forwarder}";
                if (!string.IsNullOrEmpty(hint))
                    return $"{Ordinal,6}  0x{Rva:X8}  {Name}  {hint}";
                return $"{Ordinal,6}  0x{Rva:X8}  {Name}";
            }
        }
    }
}
