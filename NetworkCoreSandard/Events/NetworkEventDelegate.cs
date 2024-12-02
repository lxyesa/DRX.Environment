using System.Net.Sockets;
using NetworkCoreStandard.EventArgs;

namespace NetworkCoreStandard.Events;

/// <summary>
/// 网络事件委托定义类
/// </summary>

public delegate void NetworkEventHandlerDelegate(object sender, NetworkEventArgs e);
public delegate void NetworkErrorHandler(object sender, NetworkError error);

public static class NetworkEventDelegate
{
    // 委托定义

    // 静态转换方法
    public static NetworkEventHandlerDelegate ToEventHandler(Action<object, NetworkEventArgs> action)
    {
        return new NetworkEventHandlerDelegate(action);
    }

    public static NetworkErrorHandler ToErrorHandler(Action<object, NetworkError> action)
    {
        return new NetworkErrorHandler(action);
    }

    public static NetworkEventHandlerDelegate AsEventHandler(this Action<object, NetworkEventArgs> action)
    {
        return new NetworkEventHandlerDelegate(action);
    }

    public static NetworkErrorHandler AsErrorHandler(this Action<object, NetworkError> action)
    {
        return new NetworkErrorHandler(action);
    }

    public static NetworkEventHandlerDelegate ToEventHandler(NetworkEventHandlerDelegate handler)
    {
        return handler;
    }

    public static NetworkErrorHandler ToErrorHandler(NetworkErrorHandler handler)
    {
        return handler;
    }
}