using System;

namespace Drx.Sdk.Network.V2.Socket.Handler;

/// <summary>
/// Client 事件处理接口
/// </summary>
public interface IClientHandler
{
    /// <summary>
    /// 优先级，数值越小优先级越高，被执行的顺序也越靠前
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 数据包最大大小，超过该大小的数据包将断开连接(该值必须是 2 的幂次方)，为 0 表示不限制
    /// </summary>
    int MaxPacketSize { get; }

    /// <summary>
    /// 收到数据时触发，返回值作为回复发送回去；若返回 null 则不发送任何回复
    /// </summary>
    /// <param name="data">客户端发送的数据</param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    bool OnClientReceiveAsync(byte[] data);

    /// <summary>
    /// 发送数据前触发，可修改数据内容后返回
    /// </summary>
    /// <param name="data">待发送的数据</param>
    /// <returns>返回实际发送的数据</returns>
    byte[] OnClientSendAsync(byte[] data);

    /// <summary>
    /// 连接服务器成功时触发(在UDP模式下不可用)
    /// </summary>
    void OnClientConnected();

    /// <summary>
    /// 断开连接(之前)触发(在UDP模式下不可用)
    /// </summary>
    void OnClientDisconnected();

    /// <summary>
    /// 连接断开后触发(在UDP模式下不可用)
    /// </summary>
    void OnClientDisconnectedCompleted();
}
