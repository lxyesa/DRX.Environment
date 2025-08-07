using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.Security;
using Drx.Sdk.Network.Socket;
using DRX.Framework;

public static class DrxTcpClientExport
{
    // 回调签名：原生可调用函数：void (*cb)(const uint8_t* data, int len)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void PacketResponseCallback(byte* data, int length);

    // 最简 AES 加解密器（仅当显式设置时启用，避免外部依赖）
    private sealed class AesEncryptor : IPacketEncryptor
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public AesEncryptor(byte[] key, byte[] iv)
        {
            _key = key;
            _iv = iv;
        }

        public byte[] Encrypt(byte[] data)
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = System.Security.Cryptography.CipherMode.CBC;
            aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
            using var enc = aes.CreateEncryptor();
            Logger.Info($"[AesEncryptor] Encrypting {data.Length} bytes");
            return enc.TransformFinalBlock(data, 0, data.Length);
        }

        public byte[] Decrypt(byte[] data)
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = System.Security.Cryptography.CipherMode.CBC;
            aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
            using var dec = aes.CreateDecryptor();
            return dec.TransformFinalBlock(data, 0, data.Length);
        }
    }

    // 句柄封送
    private static IntPtr ToPtr(object obj)
    {
        var h = GCHandle.Alloc(obj, GCHandleType.Normal);
        return GCHandle.ToIntPtr(h);
    }
    private static T FromPtr<T>(IntPtr ptr) where T : class
    {
        if (ptr == IntPtr.Zero) return null;
        var h = GCHandle.FromIntPtr(ptr);
        return h.Target as T;
    }
    private static void FreePtr(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return;
        var h = GCHandle.FromIntPtr(ptr);
        if (h.IsAllocated) h.Free();
    }

    // UTF8 辅助（unsafe 指针读取）
    private static unsafe string BytesToString(byte* data, int len)
    {
        if (data == null || len <= 0) return string.Empty;
        return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(data, len));
    }

    // Create(): 返回 DrxTcpClient 句柄
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_Create")]
    public static IntPtr Create()
    {
        Console.WriteLine("[DrxTcpClientExport] Drx_Create called");
        var client = new DrxTcpClient();
        Console.WriteLine("[DrxTcpClientExport] Drx_Create => handle pinned");
        return ToPtr(client);
    }

    // Create(key, iv): 启用 AES（指针）
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_CreateWithAes")]
    public static unsafe IntPtr CreateWithAes(byte* key, int keyLen, byte* iv, int ivLen)
    {
        var client = new DrxTcpClient();
        if (key != null && keyLen > 0 && iv != null && ivLen > 0)
        {
            var k = new byte[keyLen];
            var v = new byte[ivLen];
            new ReadOnlySpan<byte>(key, keyLen).CopyTo(k);
            new ReadOnlySpan<byte>(iv, ivLen).CopyTo(v);
            client.SetEncryptor(new AesEncryptor(k, v));
        }
        return ToPtr(client);
    }

    // Connect(client, host, port)
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_Connect")]
    public static unsafe bool Connect(IntPtr clientPtr, byte* hostUtf8, int hostLen, int port)
    {
        Console.WriteLine($"[DrxTcpClientExport] Drx_Connect called handle={clientPtr}, port={port}");
        var client = FromPtr<DrxTcpClient>(clientPtr);
        if (client == null)
        {
            Console.WriteLine("[DrxTcpClientExport] Drx_Connect failed: clientPtr invalid");
            return false;
        }
        try
        {
            var host = BytesToString(hostUtf8, hostLen);
            Console.WriteLine($"[DrxTcpClientExport] Drx_Connect host='{host}' len={hostLen}");
            client.Connect(host, port);
            Console.WriteLine("[DrxTcpClientExport] Drx_Connect success");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DrxTcpClientExport] Drx_Connect exception: {ex}");
            return false;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "TS")]
    public static void TS()
    {

    }

    // SetAesEncryptor(client, key, iv)（unsafe 指针版）
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_SetAesEncryptor")]
    public static unsafe void SetAesEncryptor(IntPtr clientPtr, byte* key, int keyLen, byte* iv, int ivLen)
    {
        var client = FromPtr<DrxTcpClient>(clientPtr);
        if (client == null) return;
        if (key == null || keyLen <= 0 || iv == null || ivLen <= 0) return;
        var k = new byte[keyLen];
        var v = new byte[ivLen];
        new ReadOnlySpan<byte>(key, keyLen).CopyTo(k);
        new ReadOnlySpan<byte>(iv, ivLen).CopyTo(v);
        client.SetEncryptor(new AesEncryptor(k, v));
    }

    // SendPacket(client, jsonData, callback, timeoutMilliseconds)
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_SendPacket")]
    public static unsafe bool SendPacket(IntPtr clientPtr, byte* jsonData, int jsonLen, IntPtr callbackPtr, int timeoutMilliseconds)
    {
        Console.WriteLine($"[DrxTcpClientExport] Drx_SendPacket called handle={clientPtr}, len={jsonLen}, timeoutMs={timeoutMilliseconds}, cb={(callbackPtr!=IntPtr.Zero)}");
        var client = FromPtr<DrxTcpClient>(clientPtr);
        if (client == null || jsonData == null || jsonLen <= 0)
        {
            Console.WriteLine("[DrxTcpClientExport] Drx_SendPacket invalid args: " +
                              $"client={(client!=null)}, jsonData={(jsonData!=null)}, jsonLen={jsonLen}");
            return false;
        }

        Action<DrxTcpClient, byte[]> onResp = null;
        if (callbackPtr != IntPtr.Zero)
        {
            var cb = Marshal.GetDelegateForFunctionPointer<PacketResponseCallback>(callbackPtr);
            onResp = (c, bytes) =>
            {
                try
                {
                    if (bytes == null)
                    {
                        Console.WriteLine("[DrxTcpClientExport] Response callback: null bytes");
                        cb(null, 0);
                    }
                    else
                    {
                        Console.WriteLine($"[DrxTcpClientExport] Response callback: {bytes.Length} bytes");
                        fixed (byte* p = bytes)
                        {
                            cb(p, bytes.Length);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DrxTcpClientExport] Response callback exception: {ex}");
                }
            };
        }

        try
        {
            var payload = new byte[jsonLen];
            new ReadOnlySpan<byte>(jsonData, jsonLen).CopyTo(payload);

            // 打印前 256 字节便于诊断（UTF-8 安全截断）
            var previewLen = Math.Min(jsonLen, 256);
            string preview = string.Empty;
            try { preview = Encoding.UTF8.GetString(payload, 0, previewLen); } catch { preview = "(non-utf8)"; }
            Console.WriteLine($"[DrxTcpClientExport] Payload preview({previewLen}/{jsonLen}): {preview}");

            var timeout = timeoutMilliseconds > 0 ? TimeSpan.FromMilliseconds(timeoutMilliseconds) : TimeSpan.FromSeconds(30);
            var cts = new CancellationTokenSource(timeout);

            Console.WriteLine("[DrxTcpClientExport] SendPacketAsync start");
            // 避免在 unsafe 上下文直接 await：改为在任务中执行并在此同步等待
            Task.Run(() => client.SendPacketAsync(payload, onResp, timeout, cts.Token))
                .Wait(timeoutMilliseconds > 0 ? timeoutMilliseconds + 500 : 31000);
            Console.WriteLine("[DrxTcpClientExport] SendPacketAsync completed (Wait returned)");
            return true;
        }
        catch (AggregateException aex)
        {
            Console.WriteLine($"[DrxTcpClientExport] Drx_SendPacket AggregateException: {aex.Flatten()}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DrxTcpClientExport] Drx_SendPacket exception: {ex}");
            return false;
        }
    }

    // Disconnect(client)
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_Disconnect")]
    public static void Disconnect(IntPtr clientPtr)
    {
        Console.WriteLine("[DrxTcpClientExport] Drx_Disconnect called");
        var client = FromPtr<DrxTcpClient>(clientPtr);
        if (client == null)
        {
            Console.WriteLine("[DrxTcpClientExport] Drx_Disconnect: clientPtr invalid");
            return;
        }
        try
        {
            if (client.Connected)
            {
                try { client.GetStream().Close(); } catch (Exception ex) { Console.WriteLine($"[DrxTcpClientExport] Drx_Disconnect stream close ex: {ex.Message}"); }
                client.Close();
                Console.WriteLine("[DrxTcpClientExport] Drx_Disconnect closed");
            }
            else
            {
                Console.WriteLine("[DrxTcpClientExport] Drx_Disconnect: already not connected");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DrxTcpClientExport] Drx_Disconnect exception: {ex}");
        }
    }

    // Destroy(client)
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_Destroy")]
    public static void Destroy(IntPtr clientPtr)
    {
        Console.WriteLine("[DrxTcpClientExport] Drx_Destroy called");
        var client = FromPtr<DrxTcpClient>(clientPtr);
        try
        {
            if (client != null)
            {
                try { client.Dispose(); Console.WriteLine("[DrxTcpClientExport] Drx_Destroy disposed"); } catch (Exception ex) { Console.WriteLine($"[DrxTcpClientExport] Drx_Destroy dispose ex: {ex.Message}"); }
            }
        }
        finally
        {
            FreePtr(clientPtr);
            Console.WriteLine("[DrxTcpClientExport] Drx_Destroy handle freed");
        }
    }

    // JSON 构造/读取：基于 JsonObject 句柄
    
    // 新增：从原始 UTF-8 JSON 文本创建 JsonNode 句柄
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_JsonCreateFromBytes")]
    public static unsafe IntPtr JsonCreateFromBytes(byte* jsonBytes, int jsonLen)
    {
        try
        {
            if (jsonBytes == null || jsonLen <= 0) return IntPtr.Zero;
            var json = BytesToString(jsonBytes, jsonLen);
            var node = JsonNode.Parse(json) as JsonObject;
            if (node == null) return IntPtr.Zero;
            return ToPtr(node);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }
    
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_JsonCreate")]
    public static IntPtr JsonCreate()
    {
        var obj = new JsonObject();
        return ToPtr(obj);
    }
    
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_JsonPushString")]
    public static unsafe IntPtr JsonPushString(IntPtr jsonPtr, byte* keyBytes, int keyLen, byte* valBytes, int valLen)
    {
        var obj = FromPtr<JsonObject>(jsonPtr);
        if (obj == null) return IntPtr.Zero;
        var key = BytesToString(keyBytes, keyLen);
        var val = BytesToString(valBytes, valLen);
        obj[key] = val;
        return jsonPtr;
    }
    
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_JsonPushNumber")]
    public static unsafe IntPtr JsonPushNumber(IntPtr jsonPtr, byte* keyBytes, int keyLen, double value)
    {
        var obj = FromPtr<JsonObject>(jsonPtr);
        if (obj == null) return IntPtr.Zero;
        var key = BytesToString(keyBytes, keyLen);
        obj[key] = value;
        return jsonPtr;
    }
    
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_JsonPushBoolean")]
    public static unsafe IntPtr JsonPushBoolean(IntPtr jsonPtr, byte* keyBytes, int keyLen, byte value)
    {
        var obj = FromPtr<JsonObject>(jsonPtr);
        if (obj == null) return IntPtr.Zero;
        var key = BytesToString(keyBytes, keyLen);
        obj[key] = value != 0;
        return jsonPtr;
    }
    
    // 复合对象：将 childPtr 指向的 JsonObject 作为值挂入 parent[key]
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_JsonPushCompound")]
    public static unsafe IntPtr JsonPushCompound(IntPtr jsonPtr, byte* keyBytes, int keyLen, IntPtr childPtr)
    {
        var obj = FromPtr<JsonObject>(jsonPtr);
        var child = FromPtr<JsonObject>(childPtr);
        if (obj == null || child == null) return IntPtr.Zero;
        var key = BytesToString(keyBytes, keyLen);
        obj[key] = child;
        return jsonPtr;
    }
    
    // 新增：读取 String 到调用方缓冲区，返回写入的 UTF-8 字节数（不足则截断）
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_JsonReadString")]
    public static unsafe int JsonReadString(IntPtr jsonPtr, byte* keyBytes, int keyLen, IntPtr outBuffer, int outCapacity)
    {
        var obj = FromPtr<JsonObject>(jsonPtr);
        if (obj == null || keyBytes == null || keyLen <= 0 || outBuffer == IntPtr.Zero || outCapacity <= 0) return 0;
        var key = BytesToString(keyBytes, keyLen);
        try
        {
            if (!obj.TryGetPropertyValue(key, out var node) || node == null) return 0;
            // 兼容值类型：优先按字符串，其次 ToJsonString 去引号
            string str;
            if (node is JsonValue jv && jv.TryGetValue<string>(out var s)) str = s;
            else if (node is JsonArray or JsonObject) str = node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            else str = node.ToJsonString(new JsonSerializerOptions { WriteIndented = false }).Trim('"');
            var bytes = Encoding.UTF8.GetBytes(str);
            var n = Math.Min(bytes.Length, outCapacity);
            Marshal.Copy(bytes, 0, outBuffer, n);
            return n;
        }
        catch
        {
            return 0;
        }
    }
    
    // 新增：读取 Boolean。返回值为 0/1；successPtr 非空时写入 0/1 指示成功
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_JsonReadBoolean")]
    public static unsafe byte JsonReadBoolean(IntPtr jsonPtr, byte* keyBytes, int keyLen, byte* successPtr)
    {
        if (successPtr != null) *successPtr = 0;
        var obj = FromPtr<JsonObject>(jsonPtr);
        if (obj == null || keyBytes == null || keyLen <= 0) return 0;
        var key = BytesToString(keyBytes, keyLen);
        try
        {
            if (!obj.TryGetPropertyValue(key, out var node) || node == null) return 0;
            if (node is JsonValue jv)
            {
                if (jv.TryGetValue<bool>(out var b)) { if (successPtr != null) *successPtr = 1; return (byte)(b ? 1 : 0); }
                if (jv.TryGetValue<string>(out var s))
                {
                    if (bool.TryParse(s, out var pb)) { if (successPtr != null) *successPtr = 1; return (byte)(pb ? 1 : 0); }
                    if (s == "1") { if (successPtr != null) *successPtr = 1; return 1; }
                    if (s == "0") { if (successPtr != null) *successPtr = 1; return 0; }
                }
                if (jv.TryGetValue<double>(out var num)) { if (successPtr != null) *successPtr = 1; return (byte)(num != 0 ? 1 : 0); }
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }
    
    // 新增：读取 Number（double）。返回数值；successPtr 非空时写入 0/1 指示成功
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_JsonReadNumber")]
    public static unsafe double JsonReadNumber(IntPtr jsonPtr, byte* keyBytes, int keyLen, byte* successPtr)
    {
        if (successPtr != null) *successPtr = 0;
        var obj = FromPtr<JsonObject>(jsonPtr);
        if (obj == null || keyBytes == null || keyLen <= 0) return 0;
        var key = BytesToString(keyBytes, keyLen);
        try
        {
            if (!obj.TryGetPropertyValue(key, out var node) || node == null) return 0;
            if (node is JsonValue jv)
            {
                if (jv.TryGetValue<double>(out var d)) { if (successPtr != null) *successPtr = 1; return d; }
                if (jv.TryGetValue<int>(out var i)) { if (successPtr != null) *successPtr = 1; return i; }
                if (jv.TryGetValue<long>(out var l)) { if (successPtr != null) *successPtr = 1; return l; }
                if (jv.TryGetValue<string>(out var s) && double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pd))
                { if (successPtr != null) *successPtr = 1; return pd; }
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }
    
    // 新增：读取子对象（Compound）。若 key 对应非对象或不存在，返回 IntPtr.Zero
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_JsonReadCompound")]
    public static unsafe IntPtr JsonReadCompound(IntPtr jsonPtr, byte* keyBytes, int keyLen)
    {
        var obj = FromPtr<JsonObject>(jsonPtr);
        if (obj == null || keyBytes == null || keyLen <= 0) return IntPtr.Zero;
        var key = BytesToString(keyBytes, keyLen);
        try
        {
            if (!obj.TryGetPropertyValue(key, out var node) || node == null) return IntPtr.Zero;
            if (node is JsonObject childObj)
            {
                // 返回新的句柄；由调用方调用 Drx_JsonDestroy 释放
                return ToPtr(childObj);
            }
            return IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }
    
    // 将 JSON 对象序列化到调用方提供的缓冲区，返回写入的字节数
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_JsonSerialize")]
    public static int JsonSerialize(IntPtr jsonPtr, IntPtr outBuffer, int outCapacity)
    {
        var obj = FromPtr<JsonObject>(jsonPtr);
        if (obj == null || outBuffer == IntPtr.Zero || outCapacity <= 0) return 0;
        var json = obj.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        var bytes = Encoding.UTF8.GetBytes(json);
        var n = Math.Min(bytes.Length, outCapacity);
        Marshal.Copy(bytes, 0, outBuffer, n);
        return n;
    }
    
    // 释放 JSON 句柄
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "Drx_JsonDestroy")]
    public static void JsonDestroy(IntPtr jsonPtr)
    {
        FreePtr(jsonPtr);
    }

    // 类级静态导出表与固定句柄，确保原生侧长期可用
    private static nint[] s_exportTable = Array.Empty<nint>();
    private static GCHandle s_pinnedHandle;
    private static IntPtr s_tablePtr = IntPtr.Zero;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe IntPtr GetExportsPinnedPointer()
    {
        if (s_tablePtr != IntPtr.Zero)
            return s_tablePtr;

        // 构建函数指针表（顺序固定）
        static delegate* unmanaged[Cdecl]<IntPtr>                                 p_Create()               => &Create;
        static delegate* unmanaged[Cdecl]<byte*, int, byte*, int, IntPtr>         p_CreateWithAes()        => &CreateWithAes;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, int, bool>          p_Connect()              => &Connect;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, int, void>   p_SetAesEncryptor()      => &SetAesEncryptor;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, IntPtr, int, bool>  p_SendPacket()           => &SendPacket;
        static delegate* unmanaged[Cdecl]<IntPtr, void>                           p_Disconnect()           => &Disconnect;
        static delegate* unmanaged[Cdecl]<IntPtr, void>                           p_Destroy()              => &Destroy;
    
        static delegate* unmanaged[Cdecl]<byte*, int, IntPtr>                     p_JsonCreateFromBytes()  => &JsonCreateFromBytes;
        static delegate* unmanaged[Cdecl]<IntPtr>                                 p_JsonCreate()           => &JsonCreate;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, int, IntPtr> p_JsonPushString()       => &JsonPushString;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, double, IntPtr>     p_JsonPushNumber()       => &JsonPushNumber;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte, IntPtr>       p_JsonPushBoolean()      => &JsonPushBoolean;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, IntPtr, IntPtr>     p_JsonPushCompound()     => &JsonPushCompound;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, IntPtr, int, int>   p_JsonReadString()       => &JsonReadString;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, byte>        p_JsonReadBoolean()      => &JsonReadBoolean;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, double>      p_JsonReadNumber()       => &JsonReadNumber;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, IntPtr>             p_JsonReadCompound()     => &JsonReadCompound;
        static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int>               p_JsonSerialize()        => &JsonSerialize;
        static delegate* unmanaged[Cdecl]<IntPtr, void>                           p_JsonDestroy()          => &JsonDestroy;
    
        s_exportTable = new nint[]
        {
            (nint)p_Create(),
            (nint)p_CreateWithAes(),
            (nint)p_Connect(),
            (nint)p_SetAesEncryptor(),
            (nint)p_SendPacket(),
            (nint)p_Disconnect(),
            (nint)p_Destroy(),
    
            (nint)p_JsonCreateFromBytes(),
            (nint)p_JsonCreate(),
            (nint)p_JsonPushString(),
            (nint)p_JsonPushNumber(),
            (nint)p_JsonPushBoolean(),
            (nint)p_JsonPushCompound(),
            (nint)p_JsonReadString(),
            (nint)p_JsonReadBoolean(),
            (nint)p_JsonReadNumber(),
            (nint)p_JsonReadCompound(),
            (nint)p_JsonSerialize(),
            (nint)p_JsonDestroy(),
        };

        // 固定数组，获取长期有效首地址
        if (s_pinnedHandle.IsAllocated) s_pinnedHandle.Free();
        s_pinnedHandle = GCHandle.Alloc(s_exportTable, GCHandleType.Pinned);
        s_tablePtr = s_pinnedHandle.AddrOfPinnedObject();
        return s_tablePtr;
    }

    // 稳定导出入口：返回函数指针表地址（nint 数组首指针）
    // 目的：dumpbin /exports 能看到固定名 GetDrxTcpClientExports，原生侧通过该表获取所有函数指针
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "GetDrxTcpClientExports")]
    public static unsafe IntPtr GetDrxTcpClientExports()
    {
        // 收集所有导出函数的本机指针（按固定顺序）
        static delegate* unmanaged[Cdecl]<IntPtr>                                 p_Create()               => &Create;
        static delegate* unmanaged[Cdecl]<byte*, int, byte*, int, IntPtr>         p_CreateWithAes()        => &CreateWithAes;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, int, bool>          p_Connect()              => &Connect;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, int, void>   p_SetAesEncryptor()      => &SetAesEncryptor;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, IntPtr, int, bool>  p_SendPacket()           => &SendPacket;
        static delegate* unmanaged[Cdecl]<IntPtr, void>                           p_Disconnect()           => &Disconnect;
        static delegate* unmanaged[Cdecl]<IntPtr, void>                           p_Destroy()              => &Destroy;
    
        static delegate* unmanaged[Cdecl]<byte*, int, IntPtr>                     p_JsonCreateFromBytes()  => &JsonCreateFromBytes;
        static delegate* unmanaged[Cdecl]<IntPtr>                                 p_JsonCreate()           => &JsonCreate;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, int, IntPtr> p_JsonPushString()       => &JsonPushString;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, double, IntPtr>     p_JsonPushNumber()       => &JsonPushNumber;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte, IntPtr>       p_JsonPushBoolean()      => &JsonPushBoolean;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, IntPtr, IntPtr>     p_JsonPushCompound()     => &JsonPushCompound;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, IntPtr, int, int>   p_JsonReadString()       => &JsonReadString;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, byte>        p_JsonReadBoolean()      => &JsonReadBoolean;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, double>      p_JsonReadNumber()       => &JsonReadNumber;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, IntPtr>             p_JsonReadCompound()     => &JsonReadCompound;
        static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int>               p_JsonSerialize()        => &JsonSerialize;
        static delegate* unmanaged[Cdecl]<IntPtr, void>                           p_JsonDestroy()          => &JsonDestroy;
    
        // 索引更新说明：
        // 0..6 为 TCP 客户端；其后 JSON：
        // 7:JsonCreateFromBytes, 8:JsonCreate, 9:JsonPushString, 10:JsonPushNumber, 11:JsonPushBoolean,
        // 12:JsonPushCompound, 13:JsonReadString, 14:JsonReadBoolean, 15:JsonReadNumber, 16:JsonReadCompound,
        // 17:JsonSerialize, 18:JsonDestroy
        return GetExportsPinnedPointer();
    }
}