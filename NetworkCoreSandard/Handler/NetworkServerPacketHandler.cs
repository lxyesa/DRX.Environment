using System;
using System.Net.Sockets;
using NetworkCoreStandard.Models;
using System.Threading;

/// <summary>
/// 网络服务器数据包处理器
/// </summary>
/// 

namespace NetworkCoreSandard.Handler;

public class NetworkServerPacketHandler
{
    protected readonly Socket serverSocket;
    protected readonly NetworkServer server;

    /// <summary>
    /// 初始化服务器数据包处理器
    /// </summary>
    /// <param name="serverSocket">服务器套接字</param>
    /// <param name="server">网络服务器实例</param>
    public NetworkServerPacketHandler(Socket serverSocket, NetworkServer server)
    {
        this.serverSocket = serverSocket;
        this.server = server;
    }

    /// <summary>
    /// 发送网络消息包
    /// </summary>
    /// <param name="clientSocket">客户端套接字</param>
    /// <param name="message">要发送的消息包</param>
    public virtual async Task SendMessageAsync(Socket clientSocket, NetworkPacket message)
    {
        byte[] serializedData = message.Serialize();
        var segment = new ArraySegment<byte>(serializedData);
        await clientSocket.SendAsync(segment, SocketFlags.None);
    }

    /// <summary>
    /// 发送指定类型的数据包
    /// </summary>
    /// <param name="clientSocket">客户端套接字</param>
    /// <param name="body">数据包主体</param>
    /// <param name="type">数据包类型</param>
    /// <param name="key">数据包键值</param>
    public virtual async Task SendAsync(Socket clientSocket, NetworkPacket body, PacketType type, string key = "")
    {
        await SendMessageAsync(clientSocket, body);
    }

    

    /// <summary>
    /// 处理接收到的数据包
    /// </summary>
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
    public virtual async Task HandlePacketAsync(Socket clientSocket, Socket serverSocket, NetworkPacket packet)
#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
    {
        // 这里可以处理接收到的数据包
    }
}