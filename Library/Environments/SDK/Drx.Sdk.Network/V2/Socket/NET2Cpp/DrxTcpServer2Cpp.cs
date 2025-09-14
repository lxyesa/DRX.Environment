using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

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

    [UnmanagedCallersOnly(EntryPoint = "DrxTcpServer_Create")]
    public static IntPtr Create()
    {
        try
        {
            var srv = new Drx.Sdk.Network.V2.Socket.Server.DrxTcpServer();
            var handle = ToHandle(srv);
            _servers[handle] = srv;
            return handle;
        }
        catch
        {
            return IntPtr.Zero;
        }
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
                    return srv.PacketS2C(tcpDirect, data) ? 1 : 0;
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
                            return srv.PacketS2C(tcpObj, data) ? 1 : 0;
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
}