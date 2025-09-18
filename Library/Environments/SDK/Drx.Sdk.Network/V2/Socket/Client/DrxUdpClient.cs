using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.V2.Socket.Handler;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.V2.Socket.Client;

/// <summary>
/// 简单的 V2 UDP 客户端实现，尽量与 `DrxTcpClient` 的接口保持一致。
/// 由于 UDP 为无连接协议，某些 TCP 专用的事件/方法在 UDP 上语义不同或不可用。
/// </summary>
public class DrxUdpClient : UdpClient
{
    private readonly object _sync = new();
    private readonly Dictionary<string, object?> _tags = new();
    private string _group = "default";
    private readonly List<IClientHandler> _handlers = new();

    public DrxUdpClient() : base() { }

    /// <summary>
    /// 使用已创建的 Socket 构造（若有需要）
    /// </summary>
    public DrxUdpClient(System.Net.Sockets.Socket socket) : base()
    {
        try { this.Client = socket; } catch { }
    }

    /// <summary>
    /// 使用本地端点构造（绑定到本地端口）
    /// </summary>
    public DrxUdpClient(IPEndPoint localEP) : base(localEP) { }

    /// <summary>
    /// 对于 UDP，若底层 Socket 的 Connected 为 true，则表示已使用 Connect 绑定了远端地址。
    /// 与 TCP 的语义不同，但保留该属性以与 DrxTcpClient 接口兼容。
    /// </summary>
    public bool Connected => Client != null && Client.Connected;

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
    /// 注册一个处理器实例，优先级高的在前。
    /// </summary>
    public void RegisterHandler<T>(params object[] args) where T : IClientHandler
    {
        try
        {
            var obj = Activator.CreateInstance(typeof(T), args) as IClientHandler;
            if (obj is not IClientHandler) throw new ArgumentException("Handler must implement IClientHandler");

            lock (_sync)
            {
                var existing = _handlers.FindAll(h => h.GetType() == typeof(T));
                if (existing.Count > 0)
                {
                    Logger.Warn($"Handler of type {typeof(T).FullName} is already registered. Replacing the old one.");
                    foreach (var e in existing) _handlers.Remove(e);
                }

                int index = _handlers.FindIndex(h => h.GetPriority() < obj.GetPriority());
                if (index >= 0) _handlers.Insert(index, obj);
                else _handlers.Add(obj);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to register handler: " + ex.Message);
        }
    }

    public void UnregisterHandler<T>() where T : IClientHandler
    {
        lock (_sync) { _handlers.RemoveAll(h => h is T); }
    }

    public List<IClientHandler> GetHandlers()
    {
        lock (_sync) { return new List<IClientHandler>(_handlers); }
    }

    public T GetHandler<T>() where T : class, IClientHandler
    {
        lock (_sync) { return _handlers.OfType<T>().FirstOrDefault()!; }
    }

    public void UnregisterAllHandlers()
    {
        lock (_sync) { _handlers.Clear(); }
    }

    /// <summary>
    /// 发送数据到服务器（使用 Packetizer 打包）。如果已使用 Connect 绑定远端，则直接发送。
    /// 可选接收一次性响应（等待一次 UDP 响应），超时则返回。
    /// </summary>
    public async Task PacketC2SAsync(byte[] data, Action<byte[]>? onResponse = null, int timeout = 5000)
    {
        // 对于未 Connect 的 UdpClient，不能使用无端点的 SendAsync；要求先 Connect。
        if (!Connected) throw new InvalidOperationException("Not connected");

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
        try
        {
            await this.SendAsync(packet, packet.Length);
        }
        catch { }

        if (onResponse != null && timeout >= 0)
        {
            var buf = new byte[8192];
            var receiveTask = this.ReceiveAsync();
            var delayTask = Task.Delay(timeout);
            var completed = await Task.WhenAny(receiveTask, delayTask);
            if (completed == receiveTask)
            {
                try
                {
                    var res = receiveTask.Result; // UdpReceiveResult
                    var respRaw = res.Buffer;

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
                catch { }
            }
        }
    }
}
