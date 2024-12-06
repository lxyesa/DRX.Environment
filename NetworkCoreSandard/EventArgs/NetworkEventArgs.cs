using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard.EventArgs;

/// <summary>
/// 网络事件类型枚举
/// </summary>
public enum NetworkEventType
{
    // 服务器事件
    ServerStarted,         /* 服务器启动事件 */
    ServerClientConnected, /* 服务器-客户端连接事件 */
    ServerClientDisconnected, /* 服务器-客户端断开事件 */

    // 客户端事件
    ClientConnected,       /* 客户端-连接到服务器事件 */
    ClientDisconnected,    /* 客户端-与服务器断开事件 */

    // 通用事件
    DataReceived,         /* 数据接收事件(共用) */
    HandlerEvent,         /* 事件处理事件(共用) */
}

public enum NetworkErrorType
{
    ClientNetworkError, /* 客户端网络错误 */
    ServerNetworkError, /* 服务器网络错误 */
    DataError,          /* 数据错误 */
}

public enum NetworkErrorCode
{
    ClientNotConnected = 0x00001, /* 客户端未连接 */
    ServerNetworkError = 0x00002, /* 服务器网络错误 */
    ServerNotStarted = 0x00003, /* 服务器未启动 */
    DataError = 0x00004,          /* 数据错误 */
    UnknownError = 0x00005,       /* 未知错误 */
}

public class NetworkError
{
    public NetworkErrorCode Code { get; }
    public string Message { get; }
    public DateTime Timestamp { get; }
    public NetworkErrorType ErrorType { get; }

    public NetworkError(NetworkErrorCode code, string message, NetworkErrorType errorType)
    {
        Code = code;
        Message = message;
        Timestamp = DateTime.Now;
        ErrorType = errorType;
    }
}


/// <summary>
/// 网络事件参数类，用于传递网络事件相关信息
/// </summary>
public class NetworkEventArgs : System.EventArgs , IDisposable
{
    /// <summary>事件发生的Socket对象（可能为Null）</summary>
    public Socket Socket { get; }

    /// <summary>事件发生的时间戳</summary>
    public DateTime Timestamp { get; }

    /// <summary>网络数据包(可选)</summary>
    public NetworkPacket? Packet { get; }

    /// <summary>事件类型</summary>
    public NetworkEventType EventType { get; }

    /// <summary>事件相关的消息描述</summary>
    public string? Message { get; }
    /// <summary>
    /// 事件的发送者
    /// </summary>
    public object? Sender { get; }
    /// <summary>
    /// 事件的远程终结点，格式为"IP:Port"
    /// </summary>
    public string? RemoteEndPoint { get; }

    // 使用 ConcurrentDictionary 替代 Dictionary + lock，提供更好的并发性能
    private ConcurrentDictionary<string, object>? _elements;

    // 使用 Lazy<T> 实现延迟初始化
    private readonly Lazy<ConcurrentDictionary<string, object>> _lazyElements;

    public NetworkEventArgs(Socket socket, NetworkEventType eventType, string message = "", 
        NetworkPacket? packet = null, object? sender = null)
    {
        Socket = socket;
        EventType = eventType;
        Message = message;
        Timestamp = DateTime.Now;
        Packet = packet;
        Sender = sender;
        RemoteEndPoint = socket?.RemoteEndPoint?.ToString();

        // 使用 Lazy<T> 初始化
        _lazyElements = new Lazy<ConcurrentDictionary<string, object>>(
            () => new ConcurrentDictionary<string, object>());
    }

    public ConcurrentDictionary<string, object> Elements => _lazyElements.Value;

    // 使用 TryAdd 避免并发冲突
    public bool TryAddElement(string key, object value)
    {
        if(string.IsNullOrEmpty(key)) return false;
        return Elements.TryAdd(key, value);
    }

    // 使用 TryGetValue 优化获取操作
    public bool TryGetElement<T>(string key, out T? value) where T : class
    {
        value = default;
        if (Elements.TryGetValue(key, out object? obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }
        return false;
    }

    // 为了兼容性保留原方法
    public NetworkEventArgs AddElement(string key, object value)
    {
        Elements.TryAdd(key, value);
        return this;
    }

    public object? GetElement(string key)
    {
        return Elements.TryGetValue(key, out object? value) ? value : null;
    }
    
    // 添加无参构造函数
    public NetworkEventArgs() : this(null!, NetworkEventType.DataReceived, string.Empty)
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