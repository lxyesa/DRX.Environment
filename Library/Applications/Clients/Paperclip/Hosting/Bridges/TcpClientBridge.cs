// Copyright (c) DRX SDK — Paperclip TCP/UDP 客户端桥接层
// 职责：将 NetworkClient 的核心能力导出到 JS/TS 脚本
// 关键依赖：Drx.Sdk.Network.Tcp.NetworkClient

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Drx.Sdk.Network.Tcp;

namespace DrxPaperclip.Hosting;

/// <summary>
/// TCP/UDP 客户端脚本桥接层，提供连接、发送、断开等静态 API。
/// </summary>
public static class TcpClientBridge
{
    /// <summary>
    /// 创建 TCP 客户端。
    /// </summary>
    public static NetworkClient createTcp(string host, int port)
    {
        var ep = new IPEndPoint(ResolveAddress(host), port);
        return new NetworkClient(ep, ProtocolType.Tcp);
    }

    /// <summary>
    /// 创建 UDP 客户端。
    /// </summary>
    public static NetworkClient createUdp(string host, int port)
    {
        var ep = new IPEndPoint(ResolveAddress(host), port);
        return new NetworkClient(ep, ProtocolType.Udp);
    }

    /// <summary>
    /// 异步连接到远端（TCP 模式下建立连接，UDP 模式下设置默认目标）。
    /// </summary>
    public static Task<bool> connect(NetworkClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.ConnectAsync();
    }

    /// <summary>
    /// 断开连接。
    /// </summary>
    public static void disconnect(NetworkClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.Disconnect();
    }

    /// <summary>
    /// 发送字节数据。
    /// </summary>
    public static void sendBytes(NetworkClient client, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.Send(data);
    }

    /// <summary>
    /// 发送文本（UTF-8 编码）。
    /// </summary>
    public static void sendText(NetworkClient client, string text)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.Send(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// 异步发送字节数据。
    /// </summary>
    public static Task<bool> sendBytesAsync(NetworkClient client, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.SendAsync(data);
    }

    /// <summary>
    /// 异步发送文本（UTF-8 编码）。
    /// </summary>
    public static Task<bool> sendTextAsync(NetworkClient client, string text)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.SendAsync(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// 获取连接状态。
    /// </summary>
    public static bool isConnected(NetworkClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.Connected;
    }

    /// <summary>
    /// 设置连接超时（秒）。
    /// </summary>
    public static void setTimeout(NetworkClient client, float seconds)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.Timeout = seconds;
    }

    /// <summary>
    /// 释放客户端资源。
    /// </summary>
    public static void dispose(NetworkClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.Dispose();
    }

    private static IPAddress ResolveAddress(string host)
    {
        if (IPAddress.TryParse(host, out var address))
            return address;

        var addresses = Dns.GetHostAddresses(host);
        if (addresses.Length == 0)
            throw new ArgumentException($"无法解析主机名: {host}", nameof(host));
        return addresses[0];
    }
}
