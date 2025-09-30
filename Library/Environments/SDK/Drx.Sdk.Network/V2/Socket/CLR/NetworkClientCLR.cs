using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.V2.Socket.CLR;

public static class NetworkClientCLR
{
    [UnmanagedCallersOnly(EntryPoint = "CreateInstance")]
    public static IntPtr CreateInstance(IntPtr remoteEndPointPtr, int protocolType)
    {
        var remoteEndPoint = MarshalHelper.PtrToIPEndPoint(remoteEndPointPtr);
        var client = new NetworkClient(remoteEndPoint, (ProtocolType)protocolType);
        return GCHandle.ToIntPtr(GCHandle.Alloc(client));
    }

    [UnmanagedCallersOnly(EntryPoint = "Dispose")]
    public static void Dispose(IntPtr clientPtr)
    {
        if (clientPtr == IntPtr.Zero) return;
        var handle = GCHandle.FromIntPtr(clientPtr);
        if (handle.Target is NetworkClient client)
        {
            client.Dispose();
        }
        handle.Free();
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void ConnectedCallback([MarshalAs(UnmanagedType.I1)] bool success);

    [UnmanagedCallersOnly(EntryPoint = "ConnectAsync")]
    public static void ConnectAsync(IntPtr clientPtr, IntPtr callbackPtr)
    {
        if (clientPtr == IntPtr.Zero) throw new ArgumentNullException(nameof(clientPtr));
        var handle = GCHandle.FromIntPtr(clientPtr);
        if (handle.Target is not NetworkClient client) throw new InvalidCastException("Pointer target is not a NetworkClient.");

        // 若：网络协议为UDP，则直接向远程端点发送空包以模拟建立连接。
        if (client.GetProtocolType() == ProtocolType.Udp && !client.Connected)
        {
            _ = client.SendAsync(Array.Empty<byte>(), client.GetRemoteEndPoint());
        }

        if (callbackPtr == IntPtr.Zero) throw new ArgumentNullException(nameof(callbackPtr));

        // 将 native 函数指针转换为托管委托
        var callback = Marshal.GetDelegateForFunctionPointer<ConnectedCallback>(callbackPtr);

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await client.ConnectAsync();
                try { callback(result); } catch { /* 防止回调抛异常跨界 */ }
            }
            catch
            {
                try { callback(false); } catch { }
            }
        });
    }

    public static void SendAsync(IntPtr clientPtr, IntPtr dataPtr, int length, IntPtr remoteEndPointPtr)
    {
        if (clientPtr == IntPtr.Zero) throw new ArgumentNullException(nameof(clientPtr));

        GCHandle handle;
        try
        {
            handle = GCHandle.FromIntPtr(clientPtr);
        }
        catch (Exception ex)
        {
            DebugWriteSafe(ex);
            return;
        }

        if (handle.Target is not NetworkClient client)
        {
            DebugWriteSafe(new InvalidCastException("Pointer target is not a NetworkClient."));
            return;
        }

        byte[] data;
        if (length <= 0 || dataPtr == IntPtr.Zero)
        {
            data = Array.Empty<byte>();
        }
        else
        {
            try
            {
                data = new byte[length];
                Marshal.Copy(dataPtr, data, 0, length);
            }
            catch (Exception ex)
            {
                DebugWriteSafe(ex);
                return;
            }
        }

        System.Net.IPEndPoint? target = null;
        if (remoteEndPointPtr != IntPtr.Zero)
        {
            try
            {
                target = MarshalHelper.PtrToIPEndPoint(remoteEndPointPtr);
            }
            catch (Exception ex)
            {
                DebugWriteSafe(ex);
                // continue with null target
            }
        }

        // Fire-and-forget send to avoid blocking native caller. Errors are swallowed but logged.
        _ = Task.Run(async () =>
        {
            try
            {
                // Use the client's SendAsync overload that accepts an optional target.
                await client.SendAsync(data, target).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DebugWriteSafe(ex);
            }
        });
    }

    private static void DebugWriteSafe(Exception ex)
    {
        try { System.Diagnostics.Debug.WriteLine(ex); } catch { }
    }
}
