using System;
using System.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.V2.Socket.Client;

/// <summary>
/// 简单的 V2 TCP 客户端实现，使用 Packetizer 打包/解包数据。
/// 该实现保持精简，作为仓库中更完整实现的可替代实现。
/// </summary>
public class DrxTcpClient
{
    private TcpClient _tcp = new();
    private readonly object _sync = new();
    private readonly Dictionary<string, object?> _tags = new();
    private string _group = "default";
    private readonly List<Drx.Sdk.Network.V2.Socket.Handler.IClientHandler> _handlers = new();

    public DrxTcpClient() { }

    /// <summary>是否已连接</summary>
    public bool Connected => _tcp?.Connected ?? false;

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
            var connectTask = _tcp.ConnectAsync(ip, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(timeout));
            return completed == connectTask && _tcp.Connected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>关闭连接</summary>
    public void Close()
    {
        try { _tcp?.Close(); } catch { }
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
        var stream = _tcp.GetStream();

        // 使用 Packetizer 打包并发送
        var packet = Drx.Sdk.Network.V2.Socket.Packet.Packetizer.Pack(data);
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
                    var resp = Drx.Sdk.Network.V2.Socket.Packet.Packetizer.Unpack(respRaw);
                    onResponse(resp);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }
    }
}