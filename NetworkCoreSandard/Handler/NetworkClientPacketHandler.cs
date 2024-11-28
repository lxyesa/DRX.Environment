using System;
using System.Net.Sockets;
using System.Threading;
using NetworkCoreStandard.Models;

/// <summary>
/// 网络服务器数据包处理器
/// </summary>

namespace NetworkCoreStandard.Handler;

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
        var messageBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message);
        await _stream.WriteAsync(messageBytes, 0, messageBytes.Length, CancellationToken.None);
    }

    /// <summary>
    /// 接收数据包
    /// </summary>
    /// <returns>接收到的网络包，如果没有数据则返回null</returns>
    public virtual async Task<NetworkPacket?> ReceivePacketAsync()
    {
        if (!_stream.DataAvailable) return null;

        byte[] buffer = new byte[4096];
        int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);
        if (bytesRead == 0) return null;

        byte[] data = new byte[bytesRead];
        Array.Copy(buffer, data, bytesRead);
        return NetworkPacket.Deserialize(data);
    }

    /// <summary>
    /// 发送指定类型的数据包
    /// </summary>
    /// <param name="body">数据包主体</param>
    /// <param name="type">数据包类型</param>
    public virtual async Task SendAsync(NetworkPacket body)
    {
        await SendMessageAsync(body);
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