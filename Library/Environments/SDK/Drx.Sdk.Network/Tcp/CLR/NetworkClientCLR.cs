using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Drx.Sdk.Shared;
using System.Collections.Concurrent;

namespace Drx.Sdk.Network.Tcp.CLR;

public static class NetworkClientCLR
{
    // 存储回调的字典
    private static readonly ConcurrentDictionary<IntPtr, ConnectedCallback> _connectedCallbacks = new();
    private static readonly ConcurrentDictionary<IntPtr, DisconnectedCallback> _disconnectedCallbacks = new();
    private static readonly ConcurrentDictionary<IntPtr, DataReceivedCallback> _dataReceivedCallbacks = new();
    private static readonly ConcurrentDictionary<IntPtr, ErrorCallback> _errorCallbacks = new();
    private static readonly ConcurrentDictionary<IntPtr, TimeoutCallback> _timeoutCallbacks = new();

    /// <summary>
    /// 创建 NetworkClient 实例
    /// </summary>
    /// <param name="remoteEndPointPtr">指向字符串的指针，如"127.0.0.1:1234"</param>
    /// <param name="protocolType">1 for TCP, 2 for UDP</param>
    /// <returns>PtrToNetworkClient</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [UnmanagedCallersOnly(EntryPoint = "CreateInstance")]
    public static IntPtr CreateInstance(IntPtr remoteEndPointPtr, int protocolType)
    {
        ProtocolType protocolTypeEnum;
        if (remoteEndPointPtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(remoteEndPointPtr));

        var remoteEndPointStr = Marshal.PtrToStringAnsi(remoteEndPointPtr);
        if (string.IsNullOrEmpty(remoteEndPointStr))
            throw new ArgumentException("Invalid remote endpoint string.", nameof(remoteEndPointPtr));

        var remoteEndPoint = System.Net.IPEndPoint.Parse(remoteEndPointStr);

        if (protocolType == 1) // TCP
        {
            protocolTypeEnum = ProtocolType.Tcp;
        }
        else if (protocolType == 2) // UDP
        {
            protocolTypeEnum = ProtocolType.Udp;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(protocolType), "Unsupported protocol type. Use 1 for TCP or 2 for UDP.");
        }
        var client = new NetworkClient(remoteEndPoint, protocolTypeEnum);
        var clientPtr = GCHandle.ToIntPtr(GCHandle.Alloc(client));

        // 订阅事件
        client.OnConnected += sender =>
        {
            if (_connectedCallbacks.TryGetValue(clientPtr, out var cb))
            {
                try { cb(clientPtr, true); } catch { }
            }
        };

        client.OnDisconnected += sender =>
        {
            if (_disconnectedCallbacks.TryGetValue(clientPtr, out var cb))
            {
                try { cb(clientPtr); } catch { }
            }
        };

        client.OnDataReceived += (sender, data, remote) =>
        {
            if (_dataReceivedCallbacks.TryGetValue(clientPtr, out var cb))
            {
                var dataPtr = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, dataPtr, data.Length);
                var remoteStr = remote.ToString();
                var remotePtr = Marshal.StringToHGlobalAnsi(remoteStr);
                try
                {
                    cb(clientPtr, dataPtr, data.Length, remotePtr);
                }
                catch { }
                // 注意：非托管代码负责释放 dataPtr 和 remotePtr
            }
        };

        client.OnError += (sender, ex) =>
        {
            if (_errorCallbacks.TryGetValue(clientPtr, out var cb))
            {
                var msgPtr = Marshal.StringToHGlobalAnsi(ex.Message);
                try
                {
                    cb(clientPtr, msgPtr);
                }
                catch { }
                // 注意：非托管代码负责释放 msgPtr
            }
        };

        client.OnTimeout += (sender, e) =>
        {
            if (_timeoutCallbacks.TryGetValue(clientPtr, out var cb))
            {
                try { cb(clientPtr); } catch { }
            }
        };

        return clientPtr;
    }

    /// <summary>
    /// 释放 NetworkClient 实例
    /// </summary>
    /// <param name="clientPtr">Ptr to NetworkClient</param>
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

        // 清理回调
        _connectedCallbacks.TryRemove(clientPtr, out _);
        _disconnectedCallbacks.TryRemove(clientPtr, out _);
        _dataReceivedCallbacks.TryRemove(clientPtr, out _);
        _errorCallbacks.TryRemove(clientPtr, out _);
        _timeoutCallbacks.TryRemove(clientPtr, out _);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void ConnectedCallback(IntPtr clientPtr, [MarshalAs(UnmanagedType.I1)] bool success);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void DisconnectedCallback(IntPtr clientPtr);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void DataReceivedCallback(IntPtr clientPtr, IntPtr dataPtr, int length, IntPtr remoteEndPointPtr);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void ErrorCallback(IntPtr clientPtr, IntPtr messagePtr);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void TimeoutCallback(IntPtr clientPtr);

    /// <summary>
    /// 异步连接到远程端点，完成后调用回调函数
    /// </summary>
    /// <param name="clientPtr">Ptr to NetworkClient</param>
    [UnmanagedCallersOnly(EntryPoint = "ConnectAsync")]
    public static void ConnectAsync(IntPtr clientPtr, IntPtr callbackPtr)
    {
        if (clientPtr == IntPtr.Zero) throw new ArgumentNullException(nameof(clientPtr));
        Logger.Debug($"[NetworkClientCLR] ConnectAsync called for ");
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
                try { callback(clientPtr, result); } catch { /* 防止回调抛异常跨界 */ }
            }
            catch
            {
                try { callback(clientPtr, false); } catch { }
            }
        });
    }

    [UnmanagedCallersOnly(EntryPoint = "SendAsync")]
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
                var targetStr = Marshal.PtrToStringAnsi(remoteEndPointPtr);
                if (!string.IsNullOrEmpty(targetStr))
                {
                    target = System.Net.IPEndPoint.Parse(targetStr);
                }
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

    /// <summary>
    /// 注册连接事件回调
    /// </summary>
    /// <param name="clientPtr">Ptr to NetworkClient</param>
    /// <param name="callbackPtr">Ptr to callback function</param>
    [UnmanagedCallersOnly(EntryPoint = "RegisterConnectedCallback")]
    public static void RegisterConnectedCallback(IntPtr clientPtr, IntPtr callbackPtr)
    {
        if (callbackPtr == IntPtr.Zero)
        {
            _connectedCallbacks.TryRemove(clientPtr, out _);
            return;
        }
        var callback = Marshal.GetDelegateForFunctionPointer<ConnectedCallback>(callbackPtr);
        _connectedCallbacks[clientPtr] = callback;
    }

    /// <summary>
    /// 注册断开连接事件回调
    /// </summary>
    /// <param name="clientPtr">Ptr to NetworkClient</param>
    /// <param name="callbackPtr">Ptr to callback function</param>
    [UnmanagedCallersOnly(EntryPoint = "RegisterDisconnectedCallback")]
    public static void RegisterDisconnectedCallback(IntPtr clientPtr, IntPtr callbackPtr)
    {
        if (callbackPtr == IntPtr.Zero)
        {
            _disconnectedCallbacks.TryRemove(clientPtr, out _);
            return;
        }
        var callback = Marshal.GetDelegateForFunctionPointer<DisconnectedCallback>(callbackPtr);
        _disconnectedCallbacks[clientPtr] = callback;
    }

    /// <summary>
    /// 注册数据接收事件回调
    /// </summary>
    /// <param name="clientPtr">Ptr to NetworkClient</param>
    /// <param name="callbackPtr">Ptr to callback function</param>
    [UnmanagedCallersOnly(EntryPoint = "RegisterDataReceivedCallback")]
    public static void RegisterDataReceivedCallback(IntPtr clientPtr, IntPtr callbackPtr)
    {
        if (callbackPtr == IntPtr.Zero)
        {
            _dataReceivedCallbacks.TryRemove(clientPtr, out _);
            return;
        }
        var callback = Marshal.GetDelegateForFunctionPointer<DataReceivedCallback>(callbackPtr);
        _dataReceivedCallbacks[clientPtr] = callback;
    }

    /// <summary>
    /// 注册错误事件回调
    /// </summary>
    /// <param name="clientPtr">Ptr to NetworkClient</param>
    /// <param name="callbackPtr">Ptr to callback function</param>
    [UnmanagedCallersOnly(EntryPoint = "RegisterErrorCallback")]
    public static void RegisterErrorCallback(IntPtr clientPtr, IntPtr callbackPtr)
    {
        if (callbackPtr == IntPtr.Zero)
        {
            _errorCallbacks.TryRemove(clientPtr, out _);
            return;
        }
        var callback = Marshal.GetDelegateForFunctionPointer<ErrorCallback>(callbackPtr);
        _errorCallbacks[clientPtr] = callback;
    }

    /// <summary>
    /// 注册超时事件回调
    /// </summary>
    /// <param name="clientPtr">Ptr to NetworkClient</param>
    /// <param name="callbackPtr">Ptr to callback function</param>
    [UnmanagedCallersOnly(EntryPoint = "RegisterTimeoutCallback")]
    public static void RegisterTimeoutCallback(IntPtr clientPtr, IntPtr callbackPtr)
    {
        if (callbackPtr == IntPtr.Zero)
        {
            _timeoutCallbacks.TryRemove(clientPtr, out _);
            return;
        }
        var callback = Marshal.GetDelegateForFunctionPointer<TimeoutCallback>(callbackPtr);
        _timeoutCallbacks[clientPtr] = callback;
    }
}
