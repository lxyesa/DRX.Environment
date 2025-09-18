using System;
using System.Net.Sockets;
using Drx.Sdk.Network.V2.Socket.Client;

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
    /// 收到数据时触发（解包/解密之后），返回值作为回复发送回去；若返回 false 则不发送任何回复
    /// </summary>
    /// <param name="data">数据内容</param>
    /// <param name="client">发送数据的客户端（TCP 为 DrxTcpClient，UDP 为 DrxUdpClient）</param>
    /// <returns>是否发送回复</returns>
    bool OnServerReceiveAsync(byte[] data, DrxTcpClient client);
    bool OnServerReceiveAsync(byte[] data, DrxUdpClient client);

    /// <summary>
    /// 发送数据前触发（在打包/加密之前），可修改数据内容后返回
    /// </summary>
    /// <param name="data">待发送的数据</param>
    /// <param name="client">发送数据的客户端（TCP 为 DrxTcpClient，UDP 为 DrxUdpClient）</param>
    /// <returns>返回实际发送的数据</returns>
    byte[] OnServerSendAsync(byte[] data, DrxTcpClient client);
    byte[] OnServerSendAsync(byte[] data, DrxUdpClient client);

    /// <summary>
    /// 客户端连接时触发，在UDP模式下无效（TCP 专用）
    /// </summary>
    void OnServerConnected();

    /// <summary>
    /// 在客户端断开(之前)触发，在UDP模式下无效
    /// </summary>
    void OnServerDisconnecting(DrxTcpClient client);
    void OnServerDisconnecting(DrxUdpClient client);

    /// <summary>
    /// 在客户端断开后触发，在UDP模式下无效
    /// </summary>
    /// <param name="client">断开的客户端(为了避免空引用，该参数实际上是断开客户端的副本，原对象已被回收)</param>
    void OnServerDisconnected(DrxTcpClient client);
    void OnServerDisconnected(DrxUdpClient client);

    // 追加：Raw 事件（在解包/解密与打包/加密之前触发）
    // 接收到 Raw（解密与解包前）数据时触发（比 OnServerReceiveAsync 更底层）
    // 参数: byte[] rawData - 接收到的原始数据
    // 参数: DrxTcpClient client - 发送数据的客户端
    // 参数(输出): out byte[]? modifiedData - 可修改该参数以改变后续处理的数据内容
    // 返回值: bool - 是否继续处理该数据（返回 false 则停止后续处理）
    bool OnServerRawReceiveAsync(byte[] rawData, DrxTcpClient client, out byte[]? modifiedData);
    bool OnServerRawReceiveAsync(byte[] rawData, DrxUdpClient client, out byte[]? modifiedData);

    // 发送 Raw 数据前触发（比 OnServerSendAsync 更底层）
    // 参数: byte[] rawData - 待发送的原始数据
    // 参数: DrxTcpClient client - 发送数据的客户端
    // 参数(输出): out byte[]? modifiedData - 可修改该参数以改变实际发送的数据内容
    // 返回值: bool - 是否继续发送该数据（返回 false 则不发送）
    bool OnServerRawSendAsync(byte[] rawData, DrxTcpClient client, out byte[]? modifiedData);
    bool OnServerRawSendAsync(byte[] rawData, DrxUdpClient client, out byte[]? modifiedData);

    /// <summary>
    /// 获取优先级
    /// </summary>
    /// <returns></returns>
    int GetPriority();
}
