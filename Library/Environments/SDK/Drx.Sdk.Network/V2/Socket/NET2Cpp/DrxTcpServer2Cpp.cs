using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Drx.Sdk.Network.V2.Socket.Client;
using Drx.Sdk.Network.V2.Socket.Handler;

namespace Drx.Sdk.Network.V2.Socket.NET2Cpp;

/// <summary>
/// DrxTcpServer 的 UnmanagedCallersOnly 版本，供 C++/CLI 调用的简单包装。
/// 该实现管理托管的 DrxTcpServer 实例并以 IntPtr 句柄返回给本地调用方。
/// 所有导出方法均为 static，并使用简单的 IntPtr/长度 约定传递数据（前 4 字节为长度）。
/// </summary>
public static class DrxTcpServer2Cpp
{
    private static readonly ConcurrentDictionary<IntPtr, Drx.Sdk.Network.V2.Socket.Server.DrxTcpServer> _servers = new();
    private static long _nextId = 1;

    private static byte[] ReadBytesWithLength(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return Array.Empty<byte>();
        int len = Marshal.ReadInt32(ptr);
        if (len <= 0) return Array.Empty<byte>();
        var buf = new byte[len];
        Marshal.Copy(ptr + 4, buf, 0, len);
        return buf;
    }

    private static IntPtr ToHandle(object obj)
    {
        var id = System.Threading.Interlocked.Increment(ref _nextId);
        var handle = new IntPtr(id);
        return handle;
    }

    // 本地回调的托管委托定义（C 调用约定）
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void NativeServerReceiveCallback(IntPtr clientPtr, IntPtr dataPtr);

    // 存储服务器对应的本地 handler 字典（serverHandle -> (name -> handler)）
    private static readonly ConcurrentDictionary<IntPtr, Dictionary<string, NativeServerHandler>> _nativeHandlers = new();

    // 将本地回调封装为 IServerHandler
    private class NativeServerHandler : IServerHandler
    {
        public int Priority { get; }
        public int MaxPacketSize => 0;
        private readonly NativeServerReceiveCallback _cb;
        public NativeServerReceiveCallback Callback => _cb;

        public NativeServerHandler(NativeServerReceiveCallback cb, int priority)
        {
            _cb = cb;
            Priority = priority;
        }

        public bool OnServerReceiveAsync(byte[] data, DrxTcpClient client)
        {
            try
            {
                // 将数据打包为 [len(4)] + payload，并传入本地回调
                var payload = data ?? Array.Empty<byte>();
                int len = payload.Length;
                IntPtr ptr = Marshal.AllocCoTaskMem(4 + len);
                Marshal.WriteInt32(ptr, len);
                if (len > 0) Marshal.Copy(payload, 0, ptr + 4, len);

                // 为 client 创建临时 GCHandle，传入指针，调用后立即释放
                var gch = GCHandle.Alloc(client, GCHandleType.Normal);
                try
                {
                    _cb(GCHandle.ToIntPtr(gch), ptr);
                }
                finally
                {
                    gch.Free();
                    try { Marshal.FreeCoTaskMem(ptr); } catch { }
                }
                // 我们默认返回 true（表示可以发送回复），C++ 侧若需控制可扩展
                return true;
            }
            catch { return false; }
        }

        public byte[] OnServerSendAsync(byte[] data, DrxTcpClient client) => data ?? Array.Empty<byte>();
        public void OnServerConnected() { }
        public void OnServerDisconnecting(DrxTcpClient client) { }
        public void OnServerDisconnected(DrxTcpClient client) { }
        public bool OnServerRawReceiveAsync(byte[] rawData, DrxTcpClient client, out byte[]? modifiedData) { modifiedData = rawData; return true; }
        public bool OnServerRawSendAsync(byte[] rawData, DrxTcpClient client, out byte[]? modifiedData) { modifiedData = rawData; return true; }
    }


    [UnmanagedCallersOnly(EntryPoint = "DrxTcpServer_Release")]
    public static void Release(IntPtr serverHandle)
    {
        if (serverHandle == IntPtr.Zero) return;
        if (_servers.TryRemove(serverHandle, out var srv))
        {
            try { srv.Stop(); } catch { }
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "DrxTcpServer_Start")]
    public static int Start(IntPtr serverHandle, int port)
    {
        if (serverHandle == IntPtr.Zero) return 0;
        if (!_servers.TryGetValue(serverHandle, out var srv)) return 0;
        try
        {
            srv.Start(port);
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "DrxTcpServer_Stop")]
    public static int Stop(IntPtr serverHandle)
    {
        if (serverHandle == IntPtr.Zero) return 0;
        if (!_servers.TryGetValue(serverHandle, out var srv)) return 0;
        try
        {
            srv.Stop();
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "DrxTcpServer_SendToClient")]
    public static int SendToClient(IntPtr serverHandle, IntPtr clientPtr, IntPtr dataPtr)
    {
        if (serverHandle == IntPtr.Zero || clientPtr == IntPtr.Zero) return 0;
        if (!_servers.TryGetValue(serverHandle, out var srv)) return 0;
        try
        {
            var data = ReadBytesWithLength(dataPtr);

            // clientPtr 约定：为 GCHandle.ToIntPtr(gch) 所得到的指针，指向一个托管对象
            try
            {
                var gch = System.Runtime.InteropServices.GCHandle.FromIntPtr(clientPtr);
                var target = gch.Target;
                if (target == null) return 0;

                // 如果直接传入的是 TcpClient
                if (target is System.Net.Sockets.TcpClient tcpDirect)
                {
                    try { return srv.PacketS2C(tcpDirect.ToDrxClient(), data) ? 1 : 0; } catch { }
                }

                // 如果是 V2 的 DrxTcpClient（托管包装），尝试通过反射取得其私有字段 _tcp
                var t = target.GetType();
                if (t.FullName == "Drx.Sdk.Network.V2.Socket.Client.DrxTcpClient")
                {
                    var field = t.GetField("_tcp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        var tcpObj = field.GetValue(target) as System.Net.Sockets.TcpClient;
                        if (tcpObj != null)
                        {
                            try { return srv.PacketS2C(tcpObj.ToDrxClient(), data) ? 1 : 0; } catch { }
                        }
                    }
                }
            }
            catch { }

            // 最后退回到按远端地址字符串匹配（兼容旧逻辑）
            try
            {
                string? clientStr = Marshal.PtrToStringUTF8(clientPtr);
                if (!string.IsNullOrEmpty(clientStr))
                {
                    var clients = srv.GetClients();
                    foreach (var c in clients)
                    {
                        try
                        {
                            var remote = srv.GetRemoteEndPoint(c);
                            if (remote != null)
                            {
                                var s = $"{remote.Address}:{remote.Port}";
                                if (s == clientStr)
                                {
                                    return srv.PacketS2C(c, data) ? 1 : 0;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "DrxTcpServer_SendToAll")]
    public static int SendToAll(IntPtr serverHandle, IntPtr dataPtr)
    {
        if (serverHandle == IntPtr.Zero) return 0;
        if (!_servers.TryGetValue(serverHandle, out var srv)) return 0;
        try
        {
            var data = ReadBytesWithLength(dataPtr);
            srv.PacketS2AllC(data);
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    // 注册/注销本地接收回调
    [UnmanagedCallersOnly(EntryPoint = "DrxTcpServer_RegisterReceiveCallback")]
    public static int RegisterReceiveCallback(IntPtr serverHandle, IntPtr namePtr, IntPtr callbackPtr, int priority)
    {
        if (serverHandle == IntPtr.Zero || callbackPtr == IntPtr.Zero) return 0;
        if (!_servers.TryGetValue(serverHandle, out var srv)) return 0;
        try
        {
            var cb = Marshal.GetDelegateForFunctionPointer<NativeServerReceiveCallback>(callbackPtr);
            var h = new NativeServerHandler(cb, priority);
            // 读取 name，如果为空则生成唯一名字
            string name = string.Empty;
            try { name = Marshal.PtrToStringUTF8(namePtr) ?? string.Empty; } catch { name = string.Empty; }
            if (string.IsNullOrEmpty(name)) name = "native" + Guid.NewGuid().ToString("N");
            srv.RegisterHandler(name, h);
            // 保存以便后续注销：按 serverHandle -> name 映射
            var dict = _nativeHandlers.GetOrAdd(serverHandle, _ => new Dictionary<string, NativeServerHandler>());
            lock (dict)
            {
                dict[name] = h;
            }
            return 1;
        }
        catch { return 0; }
    }

    [UnmanagedCallersOnly(EntryPoint = "DrxTcpServer_UnregisterReceiveCallback")]
    public static int UnregisterReceiveCallback(IntPtr serverHandle, IntPtr namePtr, IntPtr callbackPtr)
    {
        if (serverHandle == IntPtr.Zero || callbackPtr == IntPtr.Zero) return 0;
        if (!_servers.TryGetValue(serverHandle, out var srv)) return 0;
        try
        {
            var cb = Marshal.GetDelegateForFunctionPointer<NativeServerReceiveCallback>(callbackPtr);
            // 优先按提供的 name 注销（如果 namePtr 非空且能解析）
            string name = string.Empty;
            try { name = Marshal.PtrToStringUTF8(namePtr) ?? string.Empty; } catch { name = string.Empty; }
            if (_nativeHandlers.TryGetValue(serverHandle, out var dict))
            {
                lock (dict)
                {
                    if (!string.IsNullOrEmpty(name))
                    {
                        if (dict.TryGetValue(name, out var h))
                        {
                            dict.Remove(name);
                            try { srv.UnregisterHandler(name, h); } catch { }
                            return 1;
                        }
                        return 0;
                    }
                    // 若未提供 name，则通过 delegate 匹配查找并移除第一个匹配项
                    string? foundKey = null;
                    NativeServerHandler? foundHandler = null;
                    foreach (var kv in dict)
                    {
                        var h = kv.Value;
                        if (h != null && h.Callback != null && Delegate.Equals(h.Callback, cb)) { foundKey = kv.Key; foundHandler = h; break; }
                    }
                    if (foundKey != null && foundHandler != null)
                    {
                        dict.Remove(foundKey);
                        try { srv.UnregisterHandler(foundKey, foundHandler); } catch { }
                        return 1;
                    }
                }
            }
            return 0;
        }
        catch { return 0; }
    }
}