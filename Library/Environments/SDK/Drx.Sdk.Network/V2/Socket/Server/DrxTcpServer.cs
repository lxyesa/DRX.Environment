using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Drx.Sdk.Network.V2.Socket.Client;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.V2.Socket.Handler;
using Drx.Sdk.Shared;
using System.Linq;

namespace Drx.Sdk.Network.V2.Socket.Server;

/// <summary>
/// 精简版 V2 TCP 服务器实现，使用 Packetizer 格式收发数据。
/// 目的是提供可用且与 V2 PacketBuilder/Packetizer 配合的基础实现。
/// </summary>
public class DrxTcpServer
{
    // 委托定义
    public delegate void ClientConnectedHandler(DrxTcpClient client);
    public delegate void ClientDisconnectedHandler(DrxTcpClient client);

    // 事件声明
    public event ClientConnectedHandler? ClientConnected;
    public event ClientDisconnectedHandler? ClientDisconnected;

    private TcpListener? _listener;
    private readonly List<DrxTcpClient> _clients = new();
    private readonly object _sync = new();
    private readonly List<Drx.Sdk.Network.V2.Socket.Handler.IServerHandler> _handlers = new();

    // 消息列队与处理
    private readonly System.Collections.Concurrent.BlockingCollection<(byte[] Payload, DrxTcpClient Client)> _messageQueue;
    private CancellationTokenSource? _queueCts;
    private Task? _queueTask;
    /// <summary>队列上限，默认 1000</summary>
    public int QueueCapacity { get; set; } = 1000;

    public DrxTcpServer()
    {
        _messageQueue = new System.Collections.Concurrent.BlockingCollection<(byte[] Payload, DrxTcpClient Client)>(
            new System.Collections.Concurrent.ConcurrentQueue<(byte[] Payload, DrxTcpClient Client)>(), QueueCapacity);
    }

    private void ProcessQueue(CancellationToken token)
    {
        try
        {
            foreach (var item in _messageQueue.GetConsumingEnumerable(token))
            {
                // 按序处理，每个消息依次调用 handlers
                try
                {
                    lock (_sync)
                    {
                        foreach (var h in _handlers)
                        {
                            try { h.OnServerReceiveAsync(item.Payload, item.Client); } catch { }
                        }
                    }
                }
                catch { }
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    /// <summary>
    /// 注册一个处理器实例，优先级高的在前。
    /// </summary>
    /// <typeparam name="T">处理器类型</typeparam>
    /// <param name="args">构造函数参数</param>
    public void RegisterHandler<T>(params object[] args) where T : IServerHandler
    {
        try
        {
            var obj = Activator.CreateInstance(typeof(T), args) as IServerHandler;
            if (obj == null) throw new ArgumentException("Handler must implement IServerHandler");

            lock (_sync)
            {
                // 避免重复注册同类型 handler，在有相同类型（不包括子类）的情况下，掷出警告并移除旧的
                var existing = _handlers.FindAll(h => h.GetType() == typeof(T));
                if (existing.Count > 0)
                {
                    Logger.Warn($"Handler of type {typeof(T).FullName} is already registered. Replacing the old one.");
                    foreach (var e in existing) _handlers.Remove(e);
                }

                // 按优先级插入，优先级高的在前
                int index = _handlers.FindIndex(h => h.GetPriority() < obj.GetPriority());
                if (index >= 0) _handlers.Insert(index, obj);
                else _handlers.Add(obj);
            }
        }
        catch (MissingMethodException ex)
        {
            var constructors = typeof(T).GetConstructors();
            var descriptions = constructors.Select(c => $"({string.Join(", ", c.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
            var message = $"Constructor not found or parameters do not match. Available constructors: {string.Join("; ", descriptions)}";
            throw new ArgumentException(message, ex);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to register handler: " + ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 注销指定类型的处理器实例。
    /// </summary>
    /// <typeparam name="T">处理器类型</typeparam>
    public void UnregisterHandler<T>() where T : IServerHandler
    {
        lock (_sync)
        {
            _handlers.RemoveAll(h => h is T);
        }
    }

    /// <summary>
    /// 获取所有注册的处理器实例。
    /// </summary>
    /// <returns>所有注册的处理器实例列表</returns>
    public List<IServerHandler> GetHandlers()
    {
        lock (_sync) { return new List<IServerHandler>(_handlers); }
    }

    /// <summary>
    /// 获取指定类型的处理器实例，若不存在则返回 null。
    /// </summary>
    /// <typeparam name="T">处理器类型</typeparam>
    /// <returns>指定类型的处理器实例，若不存在则返回 null。</returns>
    public T GetHandler<T>() where T : class, IServerHandler
    {
        lock (_sync)
        {
            return _handlers.OfType<T>().FirstOrDefault()!;
        }
    }

    /// <summary>
    /// 注销所有处理器实例。
    /// </summary>
    public void UnregisterAllHandlers()
    {
        lock (_sync) { _handlers.Clear(); }
    }



    public void Start(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _queueCts = new CancellationTokenSource();
        _queueTask = Task.Run(() => ProcessQueue(_queueCts.Token));
        _ = AcceptLoopAsync();
    }

    /// <summary>
    /// 异步启动服务器
    /// </summary>
    public async Task StartAsync(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _queueCts = new CancellationTokenSource();
        _queueTask = Task.Run(() => ProcessQueue(_queueCts.Token));
        _ = AcceptLoopAsync();
        await Task.Yield();
    }

    public void Stop()
    {
        try { _listener?.Stop(); } catch { }
        lock (_sync)
        {
            foreach (var c in _clients) try { c.Close(); } catch { }
            _clients.Clear();
        }
        // 停止队列处理
        try
        {
            if (_queueCts != null)
            {
                _queueCts.Cancel();
                // 完成添加，促使 BlockingCollection 完结
                try { _messageQueue.CompleteAdding(); } catch { }
                _queueTask?.Wait(2000);
            }
        }
        catch { }
    }

    public bool IsRunning
    {
        get { return _listener != null; }
    }

    public async Task StopAsync()
    {
        try { _listener?.Stop(); } catch { }
        lock (_sync)
        {
            foreach (var c in _clients) try { c.Close(); } catch { }
            _clients.Clear();
        }
        // 停止队列处理
        try
        {
            if (_queueCts != null)
            {
                _queueCts.Cancel();
                // 完成添加，促使 BlockingCollection 完结
                try { _messageQueue.CompleteAdding(); } catch { }
                if (_queueTask != null) await Task.Run(() => _queueTask.Wait(2000));
            }
        }
        catch { }
        await Task.Yield();
    }

    public int ClientCount
    {
        get { lock (_sync) { return _clients.Count; } }
    }

    public List<DrxTcpClient> GetClients()
    {
        lock (_sync) { return new List<DrxTcpClient>(_clients); }
    }

    public IPEndPoint? GetRemoteEndPoint(DrxTcpClient client)
    {
        try { return client?.Client?.RemoteEndPoint as IPEndPoint; } catch { return null; }
    }

    public IPEndPoint? GetLocalEndPoint(DrxTcpClient client)
    {
        try { return client?.Client?.LocalEndPoint as IPEndPoint; } catch { return null; }
    }

    public void ForceDisconnect(DrxTcpClient client)
    {
        try { client?.Close(); } catch { }
        lock (_sync) { _clients.Remove(client!); }
    }

    /// <summary>
    /// 发送数据到指定客户端（使用 Packetizer 打包）
    /// </summary>
    public bool PacketS2C(DrxTcpClient client, byte[] data, Action<bool>? callback = null, int timeout = 5000)
    {
        if (client == null || !client.Connected) return false;
        try
        {
            var stream = client.GetStream();

            // 在打包/加密之前触发 Raw 发送事件，允许 handler 修改或拦截发送
            byte[] rawToSend = data;
            bool proceed = true;
            lock (_sync)
            {
                foreach (var h in _handlers)
                {
                    try
                    {
                        if (!h.OnServerRawSendAsync(rawToSend, client, out var outData))
                        {
                            proceed = false; break;
                        }
                        if (outData != null) rawToSend = outData;
                    }
                    catch { }
                }
            }
            if (!proceed) { callback?.Invoke(false); return false; }

            var packet = Drx.Sdk.Network.V2.Socket.Packet.Packetizer.Pack(rawToSend);
            stream.Write(packet, 0, packet.Length);
            callback?.Invoke(true);
            return true;
        }
        catch { callback?.Invoke(false); return false; }
    }

    /// <summary>
    /// 广播数据到所有客户端
    /// </summary>
    public void PacketS2AllC(byte[] data)
    {
        lock (_sync)
        {
            foreach (var c in _clients)
            {
                try
                {
                    // 每个客户端在发送前都允许 handler 修改或拦截原始数据
                    byte[] rawToSend = data;
                    bool proceed = true;
                    foreach (var h in _handlers)
                    {
                        try
                        {
                            if (!h.OnServerRawSendAsync(rawToSend, c, out var outData))
                            {
                                proceed = false; break;
                            }
                            if (outData != null) rawToSend = outData;
                        }
                        catch { }
                    }
                    if (!proceed) continue;

                    var packet = Drx.Sdk.Network.V2.Socket.Packet.Packetizer.Pack(rawToSend);
                    c.GetStream().Write(packet, 0, packet.Length);
                }
                catch { }
            }
        }
    }

    private async Task AcceptLoopAsync()
    {
        if (_listener == null) return;
        while (true)
        {
            System.Net.Sockets.Socket? socket = null;
            try { socket = await _listener.AcceptSocketAsync(); }
            catch { break; }
            if (socket == null) break;
            var client = new DrxTcpClient(socket);
            lock (_sync) { _clients.Add(client); }
            // 触发客户端连接事件
            ClientConnected?.Invoke(client);
            _ = HandleClientAsync(client);
        }
    }

    private async Task HandleClientAsync(DrxTcpClient client)
    {
        var stream = client.GetStream();
        var buf = new byte[8192];
        try
        {
            while (client.Connected)
            {
                int read = await stream.ReadAsync(buf, 0, buf.Length);
                if (read <= 0) break;
                var raw = new byte[read];
                Array.Copy(buf, 0, raw, 0, read);
                try
                {
                    // 先触发 Raw 接收事件，允许 handler 修改原始数据或中断处理
                    bool proceed = true;
                    lock (_sync)
                    {
                        foreach (var h in _handlers)
                        {
                            try
                            {
                                // 如果 handler 返回 false，则停止后续处理
                                if (!h.OnServerRawReceiveAsync(raw, client, out var outData))
                                {
                                    proceed = false;
                                    break;
                                }
                                if (outData != null) raw = outData; // 使用最后一个非空修改结果
                            }
                            catch { }
                        }
                    }

                    if (!proceed) continue; // 中断本次循环，丢弃该数据

                    var payload = Drx.Sdk.Network.V2.Socket.Packet.Packetizer.Unpack(raw);

                    // 入队，由后台队列任务处理
                    try
                    {
                        // 若队列已关闭或达到容量限制，TryAdd 返回 false
                        if (!_messageQueue.TryAdd((payload, client)))
                        {
                            // 若无法入队，则立即调用 handlers（降级策略），并继续
                            lock (_sync)
                            {
                                foreach (var h in _handlers) try { h.OnServerReceiveAsync(payload, client); } catch { }
                            }
                        }
                    }
                    catch { /* 忽略入队异常 */ }
                }
                catch { /* 忽略无法解析的数据 */ }
            }
        }
        catch { }
        finally
        {
            try { client.Close(); } catch { }
            lock (_sync) { if (client != null) { _clients.Remove(client!); } }
            // 触发客户端断开事件
            if (client != null)
            {
                ClientDisconnected?.Invoke(client);
            }
        }
    }
}
