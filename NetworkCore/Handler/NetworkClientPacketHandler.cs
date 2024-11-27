using System;
using System.Net.Sockets;

/// <summary>
/// 网络服务器数据包处理器
/// </summary>
public class NetworkClientPacketHandler
{
    private readonly NetworkStream _stream;

    /// <summary>
    /// 初始化网络包处理器
    /// </summary>
    /// <param name="stream">网络流</param>
    public NetworkClientPacketHandler(NetworkStream stream)
    {
        _stream = stream;
    }

    /// <summary>
    /// 发送网络消息包
    /// </summary>
    /// <param name="message">要发送的网络包</param>
    public virtual async Task SendMessageAsync(NetworkPacket message)
    {
        var messageBytes = message.Serialize();
        await _stream.WriteAsync(messageBytes);
    }

    /// <summary>
    /// 发送数据包
    /// </summary>
    /// <param name="data">要发送的数据对象</param>
    [Obsolete("请使用 SendAsync 方法替代此方法")]
    public virtual async Task SendDataAsync(object data)
    {
        var packet = new NetworkPacket(
            header: string.Empty,
            body: data,
            key: Guid.NewGuid().ToString(),
            type: PacketType.Data
        );
        await SendMessageAsync(packet);
    }

    /// <summary>
    /// 发送心跳包
    /// </summary>
    public virtual async Task SendHeartbeatAsync()
    {
        var heartbeatPacket = new NetworkPacket(
            header: string.Empty,
            body: DateTime.Now,
            key: string.Empty,
            type: PacketType.Heartbeat
        );
        await SendMessageAsync(heartbeatPacket);
    }

    /// <summary>
    /// 发送聊天消息
    /// </summary>
    /// <param name="message">聊天消息内容</param>
    [Obsolete("请使用 SendAsync 方法替代此方法")]
    public virtual async Task SendChatMessageAsync(string message)
    {
        var messagePacket = new NetworkPacket(
            header: string.Empty,
            body: message,
            key: Guid.NewGuid().ToString(),
            type: PacketType.Message
        );
        await SendMessageAsync(messagePacket);
    }

    /// <summary>
    /// 接收数据包
    /// </summary>
    /// <returns>接收到的网络包，如果没有数据则返回null</returns>
    public virtual async Task<NetworkPacket?> ReceivePacketAsync()
    {
        if (!_stream.DataAvailable) return null;

        byte[] buffer = new byte[4096];
        int bytesRead = await _stream.ReadAsync(buffer);
        if (bytesRead == 0) return null;

        byte[] data = new byte[bytesRead];
        Array.Copy(buffer, data, bytesRead);
        return NetworkPacket.Deserialize(data);
    }

    /// <summary>
    /// 发送请求数据包
    /// </summary>
    /// <param name="requestData">请求数据主体</param>
    public virtual async Task SendRequestAsync(INetworkPacketBody requestData)
    {
        var requestPacket = new NetworkPacket(
            header: string.Empty,
            body: requestData,
            key: Guid.NewGuid().ToString(),
            type: PacketType.Request
        );
        await SendMessageAsync(requestPacket);
    }

    /// <summary>
    /// 发送带有请求头的请求数据包
    /// </summary>
    /// <param name="requestData">请求数据主体</param>
    /// <param name="header">请求头信息</param>
    public virtual async Task SendRequestAsync(INetworkPacketBody requestData, string header)
    {
        var requestPacket = new NetworkPacket(
            header: header,
            body: requestData,
            key: Guid.NewGuid().ToString(),
            type: PacketType.Request
        );
        await SendMessageAsync(requestPacket);
    }

    /// <summary>
    /// 发送指定类型的数据包
    /// </summary>
    /// <param name="body">数据包主体</param>
    /// <param name="type">数据包类型</param>
    public virtual async Task SendAsync(INetworkPacketBody body, PacketType type)
    {
        var packet = new NetworkPacket(
            header: string.Empty,
            body: body,
            key: Guid.NewGuid().ToString(),
            type: type
        );
        await SendMessageAsync(packet);
    }

    /// <summary>
    /// 处理接收到的数据包
    /// </summary>
    /// <param name="packet">要处理的网络包</param>
    public virtual async Task HandlePacketAsync(NetworkPacket packet)
    {
        await Task.CompletedTask;
    }
}