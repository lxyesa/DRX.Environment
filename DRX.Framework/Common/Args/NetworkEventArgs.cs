using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;

namespace DRX.Framework.Common.Args;

/// <summary>
/// 网络事件类型枚举
/// </summary>
public enum NetworkEventType
{
    HandlerEvent, // 事件处理事件(共用)
}


/// <summary>
/// 网络事件参数类，用于传递网络事件相关信息
/// </summary>
public class NetworkEventArgs : EventArgs, IDisposable
{
    /// <summary>事件发生的Socket对象（可能为Null）</summary>
    public DRXSocket? Socket { get; }

    /// <summary>网络数据包(可选)</summary>
    public byte[]? Packet { get; }

    /// <summary>事件类型</summary>
    public NetworkEventType? EventType { get; }

    /// <summary>事件相关的消息描述</summary>
    public string? Message { get; }

    /// <summary>事件相关的数据</summary>
    public object[]? Data { get; set; }

    /// <summary>
    /// 事件的远程终结点，格式为"IP:Port"
    /// </summary>
    public string? RemoteEndPoint { get; }

    public NetworkEventArgs(DRXSocket? socket = null, NetworkEventType eventType = NetworkEventType.HandlerEvent, string message = "",
        byte[]? packet = null, object[]? data = null)
    {
        Socket = socket;
        EventType = eventType;
        Message = message;
        Packet = packet;
        RemoteEndPoint = socket?.RemoteEndPoint?.ToString();
        Data = data;
    }

    public NetworkEventArgs() : this(null, NetworkEventType.HandlerEvent, string.Empty, null)
    {
    }

    ~NetworkEventArgs()
    {
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
