using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.V2.Socket.Client;

/// <summary>
/// 简单的 V2 TCP 客户端实现，继承自 `TcpClient`。
/// 目的是让服务器端能直接用接收到的 `Socket` 包装为 `DrxTcpClient` 实例。
/// 保持原有公有 API（标签、分组、处理器、PacketC2SAsync）兼容。
/// </summary>
public class DrxTcpClient : TcpClient
{
    private readonly object _sync = new();
    private readonly Dictionary<string, object?> _tags = new();
    private string _group = "default";
    private readonly List<Drx.Sdk.Network.V2.Socket.Handler.IClientHandler> _handlers = new();

    public DrxTcpClient() : base() { }

    /// <summary>
    /// 使用已连接的 Socket 构造客户端（服务器端接受后包装用）
    /// </summary>
    public DrxTcpClient(System.Net.Sockets.Socket socket) : base()
    {
        try
        {
            this.Client = socket;
        }
        catch { }
    }

    /// <summary>是否已连接（来自基类）</summary>
    public new bool Connected => Client != null && Client.Connected;

    /// <summary>客户端分组</summary>
    public string Group => _group;

    public void SetTag(string key, object? value)
    {
        lock (_sync) { _tags[key] = value; }
    }

    public object? GetTag(string key)
    {
        lock (_sync) { return _tags.TryGetValue(key, out var v) ? v : null; }
    }

    public void SetGroup(string group)
    {
        if (string.IsNullOrEmpty(group)) throw new ArgumentNullException(nameof(group));
        _group = group;
    }

    /// <summary>
    /// 异步连接到服务器，超时返回 false
    /// </summary>
    public async Task<bool> ConnectAsync(string ip, int port, int timeout = 5000)
    {
        try
        {
            var connectTask = base.ConnectAsync(ip, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(timeout));
            return completed == connectTask && Connected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>关闭连接</summary>
    public new void Close()
    {
        try { base.Close(); } catch { }
    }

    public void RegisterHandler(string name, Drx.Sdk.Network.V2.Socket.Handler.IClientHandler handler)
    {
        if (handler == null) return;
        lock (_sync)
        {
            _handlers.Add(handler);
            _handlers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }
    }

    public void UnregisterHandler(string name, Drx.Sdk.Network.V2.Socket.Handler.IClientHandler handler)
    {
        lock (_sync) { _handlers.Remove(handler); }
    }

    public void UnregisterAllHandlers()
    {
        lock (_sync) { _handlers.Clear(); }
    }

    /// <summary>
    /// 发送数据到服务器（使用 Packetizer 打包）。可选接收一次性响应。
    /// </summary>
    public async Task PacketC2SAsync(byte[] data, Action<byte[]>? onResponse = null, int timeout = 5000)
    {
        if (!Connected) throw new InvalidOperationException("Not connected");
        var stream = GetStream();

        // 在打包/加密之前触发 Raw 发送事件，允许 handler 修改或拦截发送
        byte[] rawToSend = data;
        lock (_sync)
        {
            foreach (var h in _handlers)
            {
                try
                {
                    if (!h.OnClientRawSendAsync(rawToSend, out var outData))
                    {
                        // 取消发送
                        return;
                    }
                    if (outData != null) rawToSend = outData;
                }
                catch { }
            }
        }

        // 使用 Packetizer 打包并发送
        var packet = Drx.Sdk.Network.V2.Socket.Packet.Packetizer.Pack(rawToSend);
        await stream.WriteAsync(packet, 0, packet.Length);

        if (onResponse != null && timeout >= 0)
        {
            var buf = new byte[8192];
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                var readTask = stream.ReadAsync(buf, 0, buf.Length, cts.Token);
                var bytes = await readTask;
                if (bytes > 0)
                {
                    var respRaw = new byte[bytes];
                    Array.Copy(buf, 0, respRaw, 0, bytes);

                    // 接收到 Raw（解包前）事件，允许 handler 修改或拦截响应
                    byte[] processed = respRaw;
                    lock (_sync)
                    {
                        foreach (var h in _handlers)
                        {
                            try
                            {
                                if (!h.OnClientRawReceiveAsync(processed, out var outData))
                                {
                                    // 中止后续处理（不调用 onResponse）
                                    processed = Array.Empty<byte>();
                                    break;
                                }
                                if (outData != null) processed = outData;
                            }
                            catch { }
                        }
                    }

                    if (processed != null && processed.Length > 0)
                    {
                        var resp = Drx.Sdk.Network.V2.Socket.Packet.Packetizer.Unpack(processed);
                        onResponse(resp);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }
    }
}