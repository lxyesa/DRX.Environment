using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Drx.Sdk.Network.V2.Socket.NET2Cpp;

/// <summary>
/// 提供给本地（C++ / C++/CLI 等）调用的托管包装，使用 UnmanagedCallersOnly 导出函数。
/// 本文件提供最小的占位实现：以字节数组为载体的打包/解包、AES 协议占位、以及一个非常简单的 PacketBuilder 实现。
/// 注意：该实现为安全的托管包装，返回的指针均由托管端通过非托管内存分配（CoTaskMem）分配，
/// 并需通过 FreePointer 释放。所有接口使用简单的 IntPtr/长度/基本类型，便于从本地调用。
///
/// 设计要点（假设）：
/// - 所有导出方法为 static，并使用 UnmanagedCallersOnly(EntryPoint=...)
/// - 传入的数据指针被视为指向内存块：先读取长度 (int, 4 bytes little-endian)，随后是内容。
/// - 打包/解包操作仅做简单的封装/解封示例（前置 4 字节长度），真实逻辑应替换为项目内 Packetizer/PacketBuilder。
/// - AES 相关函数为占位（简单拷贝），并提供生成 key/iv 文件的简易实现。
///
/// 注：若需要将来用真实实现替换，建议将内部实现抽象成接口并在此处调用。
/// </summary>
public static class Packet2Cpp
{
    // 简单的非托管分配/释放约定：使用 Marshal.AllocCoTaskMem / FreeCoTaskMem

    // 用于管理 PacketBuilder 实例的句柄池（简单实现）
    private static readonly ConcurrentDictionary<IntPtr, SimplePacketBuilder> _builders = new();
    private static long _nextBuilderId = 1;

    // Helper: 从 IntPtr 读取带长度的字节数组（长度为前四字节 int32）
    private static byte[] ReadBytesWithLength(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return Array.Empty<byte>();
        // 先读长度
        int len = Marshal.ReadInt32(ptr);
        if (len <= 0) return Array.Empty<byte>();
        var data = new byte[len];
        IntPtr src = ptr + 4;
        Marshal.Copy(src, data, 0, len);
        return data;
    }

    // Helper: 为字节数组分配非托管内存并返回指针（前4字节为长度）
    private static IntPtr AllocBytesWithLength(byte[] data)
    {
        int len = data?.Length ?? 0;
        IntPtr ptr = Marshal.AllocCoTaskMem(4 + len);
        Marshal.WriteInt32(ptr, len);
        if (data != null && len > 0)
        {
            Marshal.Copy(data, 0, ptr + 4, len);
        }
        return ptr;
    }

    // Helper: 导出函数名常量（便于本地调用）
    private const CallingConvention CALL_CONV = CallingConvention.Cdecl; // 注：UnmanagedCallersOnly 不允许直接指定调用约定，这里仅作说明

    // ========== 基础 Pack / Unpack ===========

    [UnmanagedCallersOnly(EntryPoint = "Packet_Pack")]
    public static IntPtr Pack(IntPtr dataPtr)
    {
        try
        {
            var data = ReadBytesWithLength(dataPtr);
            // 占位打包逻辑：在前面加上 "PKT" 标识（示例）
            var outBuf = new byte[3 + data.Length];
            outBuf[0] = (byte)'P';
            outBuf[1] = (byte)'K';
            outBuf[2] = (byte)'T';
            Buffer.BlockCopy(data, 0, outBuf, 3, data.Length);
            return AllocBytesWithLength(outBuf);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "Packet_PackWithAes")]
    public static IntPtr PackWithAes(IntPtr dataPtr, IntPtr aesKeyPtr, IntPtr aesIvPtr)
    {
        try
        {
            var data = ReadBytesWithLength(dataPtr);
            // 占位：不做真实加解密，仅将 key/iv 长度信息附加用于示例
            var key = ReadBytesWithLength(aesKeyPtr);
            var iv = ReadBytesWithLength(aesIvPtr);
            var header = new byte[7];
            // 标识并写入 key/iv 长度（示例）
            header[0] = (byte)'A';
            header[1] = (byte)'E';
            header[2] = (byte)'S';
            header[3] = (byte)(key.Length & 0xFF);
            header[4] = (byte)((key.Length >> 8) & 0xFF);
            header[5] = (byte)(iv.Length & 0xFF);
            header[6] = (byte)((iv.Length >> 8) & 0xFF);

            var outBuf = new byte[header.Length + data.Length];
            Buffer.BlockCopy(header, 0, outBuf, 0, header.Length);
            Buffer.BlockCopy(data, 0, outBuf, header.Length, data.Length);
            return AllocBytesWithLength(outBuf);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "Packet_Unpack")]
    public static IntPtr Unpack(IntPtr packedPtr)
    {
        try
        {
            var packed = ReadBytesWithLength(packedPtr);
            if (packed.Length >= 3 && packed[0] == (byte)'P' && packed[1] == (byte)'K' && packed[2] == (byte)'T')
            {
                var outBuf = new byte[packed.Length - 3];
                Buffer.BlockCopy(packed, 3, outBuf, 0, outBuf.Length);
                return AllocBytesWithLength(outBuf);
            }
            // 如果不是上面的格式，直接返回原始数据
            return AllocBytesWithLength(packed);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "Packet_UnpackWithAes")]
    public static IntPtr UnpackWithAes(IntPtr packedPtr, IntPtr aesKeyPtr, IntPtr aesIvPtr)
    {
        try
        {
            var packed = ReadBytesWithLength(packedPtr);
            // 如果以 AES 标识开头，跳过前 7 字节示例头
            if (packed.Length >= 3 && packed[0] == (byte)'A' && packed[1] == (byte)'E' && packed[2] == (byte)'S')
            {
                int headerLen = 7;
                var outBuf = new byte[Math.Max(0, packed.Length - headerLen)];
                Buffer.BlockCopy(packed, headerLen, outBuf, 0, outBuf.Length);
                return AllocBytesWithLength(outBuf);
            }
            return AllocBytesWithLength(packed);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    // 生成 AES Key/IV 并写入文件（占位实现）
    [UnmanagedCallersOnly(EntryPoint = "Packet_GenerateAesKeyToFile")]
    public static int GenerateAesKeyToFile(int keyLen, int ivLen, IntPtr keyPathPtr, IntPtr ivPathPtr)
    {
        try
        {
            string keyPath = Marshal.PtrToStringUTF8(keyPathPtr) ?? string.Empty;
            string ivPath = Marshal.PtrToStringUTF8(ivPathPtr) ?? string.Empty;
            var key = new byte[Math.Max(0, keyLen)];
            var iv = new byte[Math.Max(0, ivLen)];
            Random.Shared.NextBytes(key);
            Random.Shared.NextBytes(iv);
            System.IO.File.WriteAllBytes(keyPath, key);
            System.IO.File.WriteAllBytes(ivPath, iv);
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "Packet_FreePointer")]
    public static void FreePointer(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return;
        try
        {
            Marshal.FreeCoTaskMem(ptr);
        }
        catch
        {
            // ignore
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "Packet_DumpPointer")]
    public static int DumpPointer(IntPtr ptr)
    {
        try
        {
            var data = ReadBytesWithLength(ptr);
            // 简单输出到控制台以便调试（本地调用端会在本地捕获）
            System.Console.WriteLine($"[Packet_DumpPointer] len={data.Length}");
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    // ========== PacketBuilder 简单实现 ===========

    private class SimplePacketBuilder
    {
        private readonly ArrayBufferWriter<byte> _writer = new();

        public void AddBytes(byte[] data)
        {
            if (data == null) return;
            _writer.Write(data);
        }

        public void Clear() => _writer.Clear();

        public byte[] Build() => _writer.WrittenSpan.ToArray();
    }

    [UnmanagedCallersOnly(EntryPoint = "PacketBuilder_Create")]
    public static IntPtr PacketBuilder_Create()
    {
        var builder = new SimplePacketBuilder();
        var id = System.Threading.Interlocked.Increment(ref _nextBuilderId);
        var handle = new IntPtr(id);
        _builders[handle] = builder;
        return handle;
    }

    [UnmanagedCallersOnly(EntryPoint = "PacketBuilder_Release")]
    public static void PacketBuilder_Release(IntPtr builderHandle)
    {
        if (builderHandle == IntPtr.Zero) return;
        _builders.TryRemove(builderHandle, out _);
    }

    [UnmanagedCallersOnly(EntryPoint = "PacketBuilder_AddBytes")]
    public static int PacketBuilder_AddBytes(IntPtr builderHandle, IntPtr dataPtr)
    {
        if (builderHandle == IntPtr.Zero) return 0;
        if (!_builders.TryGetValue(builderHandle, out var builder)) return 0;
        var data = ReadBytesWithLength(dataPtr);
        builder.AddBytes(data);
        return 1;
    }

    [UnmanagedCallersOnly(EntryPoint = "PacketBuilder_Build")]
    public static IntPtr PacketBuilder_Build(IntPtr builderHandle)
    {
        if (builderHandle == IntPtr.Zero) return IntPtr.Zero;
        if (!_builders.TryGetValue(builderHandle, out var builder)) return IntPtr.Zero;
        var built = builder.Build();
        return AllocBytesWithLength(built);
    }

    [UnmanagedCallersOnly(EntryPoint = "PacketBuilder_Clear")]
    public static int PacketBuilder_Clear(IntPtr builderHandle)
    {
        if (builderHandle == IntPtr.Zero) return 0;
        if (!_builders.TryGetValue(builderHandle, out var builder)) return 0;
        builder.Clear();
        return 1;
    }

    [UnmanagedCallersOnly(EntryPoint = "PacketBuilder_Dump")]
    public static int PacketBuilder_Dump(IntPtr builderHandle)
    {
        if (builderHandle == IntPtr.Zero) return 0;
        if (!_builders.TryGetValue(builderHandle, out var builder)) return 0;
        var built = builder.Build();
        System.Console.WriteLine($"[PacketBuilder_Dump] len={built.Length}");
        return 1;
    }
}
