using System.Net.Sockets;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Models;
using static NetworkCoreStandard.Events.NetworkEventDelegate;

namespace NetworkCoreStandard.Events;

/// <summary>
/// 网络事件管理器，集中处理所有网络相关事件
/// </summary>
public class NetworkEventManager
{
    /// <summary>基础网络事件</summary>
    public event NetworkEventHandlerDelegate? OnNetworkEvent;
    
    /// <summary>错误事件</summary>
    public event NetworkErrorHandler? OnNetworkError;

    #region 通用事件

    /// <summary>
    /// 触发网络错误事件
    /// </summary>
    public virtual void RaiseNetworkError(object sender, NetworkError error)
    {
        OnNetworkError?.Invoke(sender, error);
    }

    /// <summary>
    /// 触发数据包接收事件
    /// </summary>
    public virtual void RaiseDataReceived(object sender, Socket socket, NetworkPacket packet)
    {
        OnNetworkEvent?.Invoke(sender, new NetworkEventArgs(
            socket,
            NetworkEventType.DataReceived,
            $"接收到数据包: {packet.Type} - {packet.Serialize().Length} bytes",
            packet
        ));
    }

    #endregion

    #region  客户端事件

    /// <summary>
    /// 触发客户端连接到服务器事件
    /// </summary>
    public virtual void RaiseClientConnected(object sender, Socket socket, string serverIP, int port)
    {
        OnNetworkEvent?.Invoke(sender, new NetworkEventArgs(
            socket,
            NetworkEventType.ClientConnected,
            $"已连接到服务器 {serverIP}:{port}"
        ));
    }

    /// <summary>
    /// 触发客户端断开连接事件
    /// </summary>
    public virtual void RaiseClientDisconnected(object sender, Socket socket, string serverIP, int port)
    {
        OnNetworkEvent?.Invoke(sender, new NetworkEventArgs(
            socket,
            NetworkEventType.ClientDisconnected,
            $"已断开与服务器 {serverIP}:{port} 的连接"
        ));
    }

    #endregion
    #region 服务器事件

    /// <summary>
    /// 触发服务器启动事件
    /// </summary>
    public virtual void RaiseServerStarted(object sender, string ip, int port)
    {
        OnNetworkEvent?.Invoke(sender, new NetworkEventArgs(
            new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), // 临时Socket对象
            NetworkEventType.ServerStarted,
            $"服务器在 {ip}:{port} 启动成功！"
        ));
    }

    /// <summary>
    /// 触发服务器端的客户端连接事件
    /// </summary>
    public virtual void RaiseServerClientConnected(object sender, Socket clientSocket)
    {
        OnNetworkEvent?.Invoke(sender, new NetworkEventArgs(
            clientSocket,
            NetworkEventType.ServerClientConnected,
            $"客户端 {clientSocket.RemoteEndPoint} 连接到了服务器"
        ));
    }

    /// <summary>
    /// 触发服务器端的客户端断开事件
    /// </summary>
    public virtual void RaiseServerClientDisconnected(object sender, Socket clientSocket)
    {
        OnNetworkEvent?.Invoke(sender, new NetworkEventArgs(
            clientSocket,
            NetworkEventType.ServerClientDisconnected,
            $"客户端 {clientSocket.RemoteEndPoint} 断开了与服务器的连接"
        ));
    }
    #endregion
}