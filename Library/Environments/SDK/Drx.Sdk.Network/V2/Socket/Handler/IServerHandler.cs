using System;
using System.Net.Sockets;

namespace Drx.Sdk.Network.V2.Socket.Handler;

/// <summary>
/// Server 事件处理接口
/// </summary>
public interface IServerHandler
{
    /// <summary>
    /// 优先级，数值越小优先级越高，被执行的顺序也越靠前
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 数据包最大大小，超过该大小的数据包将断开连接(该值必须是 2 的幂次方)，为 0 表示不限制
    /// <para>在UDP与TCP下均可用</para>
    /// </summary>
    int MaxPacketSize { get; }

    /// <summary>
    /// 收到数据时触发，返回值作为回复发送回去；若返回 null 则不发送任何回复
    /// </summary>
    /// <param name="data">数据内容</param>
    /// <param name="client">发送数据的客户端</param>
    /// <returns></returns>
    bool OnServerReceiveAsync(byte[] data, TcpClient client);

    /// <summary>
    /// 发送数据前触发，可修改数据内容后返回
    /// </summary>
    /// <param name="data">待发送的数据</param>
    /// <param name="client">发送数据的客户端</param>
    /// <returns>返回实际发送的数据</returns>
    byte[] OnServerSendAsync(byte[] data, TcpClient client);

    /// <summary>
    /// 客户端连接时触发，在UDP模式下无效
    /// </summary>
    void OnServerConnected();

    /// <summary>
    /// 在客户端断开(之前)触发，在UDP模式下无效
    /// </summary>
    void OnServerDisconnecting(TcpClient client);

    /// <summary>
    /// 在客户端断开后触发，在UDP模式下无效
    /// </summary>
    /// <param name="client">断开的客户端(为了避免空引用，该参数实际上是断开客户端的副本，原对象已被回收)</param>
    /// <returns></returns>
    void OnServerDisconnected(TcpClient client);
}
