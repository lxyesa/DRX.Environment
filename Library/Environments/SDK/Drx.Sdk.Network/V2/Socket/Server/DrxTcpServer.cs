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
using System.Diagnostics;

namespace Drx.Sdk.Network.V2.Socket.Server;

/// <summary>
/// 精简版 V2 TCP 服务器实现，使用 Packetizer 格式收发数据。
/// 目的是提供可用且与 V2 PacketBuilder/Packetizer 配合的基础实现。
/// </summary>
public class DrxTcpServer : NetworkObject
{
    // 委托定义
    public delegate void ClientConnectedHandler(DrxTcpClient client);
    public delegate void ClientDisconnectedHandler(DrxTcpClient client);

    // tick 委托与事件（服务器主循环）
    public delegate void TickHandler(DrxTcpServer self);
    /// <summary>
    /// OnTick 事件：每秒触发 Tick 次，作为服务器主循环的入口。
    /// 参数: self - 当前服务器实例
    /// </summary>
    public event TickHandler? OnTick;

    // 事件声明
    public event ClientConnectedHandler? ClientConnected;
    public event ClientDisconnectedHandler? ClientDisconnected;

    private TcpListener? _listener;
    private readonly List<DrxTcpClient> _clients = new();

    // 消息列队（已迁移到基类）

    // tick 相关（Tick 属性移至基类 NetworkObject）
    /// <summary>
    /// 是否接受临时连接（短连接）。如果为 true，服务器在处理完第一个有效请求后会关闭该连接，行为类似 HTTP 的短连接。
    /// 默认为 false，默认保持长连接（连接生命周期由客户端或服务器主动关闭控制）。
    /// </summary>
    public bool AcceptTemporaryConnections { get; set; } = false;
    private CancellationTokenSource? _tickCts;
    private Task? _tickTask;

    public DrxTcpServer()
    {
        // 基类的队列将延迟初始化，子类可以在构造后直接使用 EnqueueMessage
        // 将基类的 OnNetworkTick 事件转发到旧的 OnTick（向后兼容）
        base.OnNetworkTick += (obj) => { try { OnTick?.Invoke(this); } catch (Exception ex) { Console.WriteLine($"OnNetworkTick handler exception: {ex}"); } };
    }

    /// <summary>
    /// 可以在构造时指定 tick 速率（每秒触发次数），默认 20。
    /// </summary>
    public DrxTcpServer(int tick) : this()
    {
        if (tick <= 0) throw new ArgumentException("Tick must be positive", nameof(tick));
        Tick = tick;
    }

    // 本地的消息处理已迁移到基类 NetworkObject.ProcessQueue

    // TickLoop 实现已移至基类 NetworkObject；子类通过 base.TickLoop 启动

    // 处理器管理相关方法已移至 NetworkObject 基类，保留原逻辑并使用基类实现



    public void Start(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        // 通过基类延迟初始化队列（在入队时自动创建）
        // start tick loop
        _tickCts = new CancellationTokenSource();
        _tickTask = Task.Run(() => base.TickLoop(_tickCts.Token));
        _ = AcceptLoopAsync();
    }

    /// <summary>
    /// 异步启动服务器
    /// </summary>
    public async Task StartAsync(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        // 通过基类延迟初始化队列（在入队时自动创建）
        // start tick loop
        _tickCts = new CancellationTokenSource();
        _tickTask = Task.Run(() => base.TickLoop(_tickCts.Token));
        _ = AcceptLoopAsync();
        await Task.Yield();
    }

    public void Stop()
    {
        try { _listener?.Stop(); } catch (Exception ex) { Console.WriteLine($"Stop() listener stop exception: {ex}"); }
        lock (_sync)
        {
            // 使用统一方法关闭并通知每个客户端
            foreach (var c in _clients.ToList())
            {
                try { RemoveClientAndNotify(c); } catch (Exception ex) { Console.WriteLine($"Stop() RemoveClientAndNotify exception: {ex}"); }
            }
            _clients.Clear();
        }
        // 停止队列处理
        try
        {
            // 通过基类关闭队列
            try { base.ShutdownQueue(); } catch (Exception ex) { Console.WriteLine($"Stop() ShutdownQueue exception: {ex}"); }
        }
        catch (Exception ex) { Console.WriteLine($"Stop() outer exception: {ex}"); }

        // 停止 tick loop
        try
        {
            if (_tickCts != null)
            {
                _tickCts.Cancel();
                _tickTask?.Wait(2000);
            }
        }
        catch (Exception ex) { Console.WriteLine($"Stop() tick stop exception: {ex}"); }
    }

    public bool IsRunning
    {
        get { return _listener != null; }
    }

    public async Task StopAsync()
    {
        try { _listener?.Stop(); } catch (Exception ex) { Console.WriteLine($"StopAsync() listener stop exception: {ex}"); }
        lock (_sync)
        {
            foreach (var c in _clients.ToList())
            {
                try { RemoveClientAndNotify(c); } catch (Exception ex) { Console.WriteLine($"StopAsync() RemoveClientAndNotify exception: {ex}"); }
            }
            _clients.Clear();
        }
        // 停止队列处理
        try
        {
            try { await base.ShutdownQueueAsync(); } catch (Exception ex) { Console.WriteLine($"StopAsync() ShutdownQueueAsync exception: {ex}"); }
        }
        catch (Exception ex) { Console.WriteLine($"StopAsync() outer exception: {ex}"); }

        // 停止 tick loop
        try
        {
            if (_tickCts != null)
            {
                _tickCts.Cancel();
                if (_tickTask != null) await Task.Run(() => _tickTask.Wait(2000));
            }
        }
        catch (Exception ex) { Console.WriteLine($"StopAsync() tick stop exception: {ex}"); }
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
        try { return client?.Client?.RemoteEndPoint as IPEndPoint; } catch (Exception ex) { Console.WriteLine($"GetRemoteEndPoint exception: {ex}"); return null; }
    }

    public IPEndPoint? GetLocalEndPoint(DrxTcpClient client)
    {
        try { return client?.Client?.LocalEndPoint as IPEndPoint; } catch (Exception ex) { Console.WriteLine($"GetLocalEndPoint exception: {ex}"); return null; }
    }

    /// <summary>
    /// 强制断开指定客户端连接
    /// </summary>
    /// <param name="client">要断开的客户端</param>
    public void ForceDisconnect(DrxTcpClient client)
    {
        // 使用统一方法处理移除与断开事件，保证只触发一次
        try { RemoveClientAndNotify(client); } catch (Exception ex) { Console.WriteLine($"ForceDisconnect RemoveClientAndNotify exception: {ex}"); }
    }

    /// <summary>
    /// 统一从客户端列表中移除客户端并触发断开事件（如果尚未触发）。
    /// 此方法线程安全，并保证事件只在客户端确实存在于列表时触发一次。
    /// </summary>
    /// <param name="client">要移除并通知的客户端</param>
    private void RemoveClientAndNotify(DrxTcpClient? client)
    {
        if (client == null) return;

        // 我们改为：在锁内从客户端实例中分离出底层 Socket（避免并发访问），
        // 然后用该 Socket 构造一个副本传给 handlers/event，副本在处理完成后被关闭。
        System.Net.Sockets.Socket? socket = null;
        string? remoteEp = null;
        string? localEp = null;
        bool existed = false;
        lock (_sync)
        {
            existed = _clients.Remove(client);
            try
            {
                socket = client.Client; // 取得底层 socket
                if (socket != null)
                {
                    try { remoteEp = socket.RemoteEndPoint?.ToString(); } catch (Exception ex) { Console.WriteLine($"RemoveClientAndNotify get RemoteEndPoint exception: {ex}"); }
                    try { localEp = socket.LocalEndPoint?.ToString(); } catch (Exception ex) { Console.WriteLine($"RemoveClientAndNotify get LocalEndPoint exception: {ex}"); }
                }
                // 将原对象与 socket 分离，防止后续 Close 导致副本不可用或重复关闭
                try { client.Client = null!; } catch (Exception ex) { Console.WriteLine($"RemoveClientAndNotify clear client.Client exception: {ex}"); }
            }
            catch (Exception ex) { Console.WriteLine($"RemoveClientAndNotify lock block exception: {ex}"); }
        }

        // 如果既不在列表中且没有底层 socket，则不需进一步处理
        if (!existed && socket == null) return;

        // 构造副本（使用分离出的 socket，如果为 null 则构造空副本）
        DrxTcpClient snapshot = (socket != null) ? new DrxTcpClient(socket) : new DrxTcpClient();
        try { if (remoteEp != null) snapshot.SetTag("RemoteEndPoint", remoteEp); } catch (Exception ex) { Console.WriteLine($"RemoveClientAndNotify SetTag RemoteEndPoint exception: {ex}"); }
        try { if (localEp != null) snapshot.SetTag("LocalEndPoint", localEp); } catch (Exception ex) { Console.WriteLine($"RemoveClientAndNotify SetTag LocalEndPoint exception: {ex}"); }

        try
        {
            // 先通知 handlers 正在断开（可在此做出清理动作）
            lock (_sync)
            {
                foreach (var h in _handlers)
                {
                    try { h.OnServerDisconnecting(snapshot); } catch (Exception ex) { Console.WriteLine($"Handler OnServerDisconnecting exception: {ex}"); }
                }
            }

            // 关闭副本的底层 socket，表示连接已真正断开（确保 OnServerDisconnected 在关闭后执行）
            try { snapshot.Close(); } catch (Exception ex) { Console.WriteLine($"snapshot.Close() exception: {ex}"); }

            // 通知 handlers 已断开
            lock (_sync)
            {
                foreach (var h in _handlers)
                {
                    try { h.OnServerDisconnected(snapshot); } catch (Exception ex) { Console.WriteLine($"Handler OnServerDisconnected exception: {ex}"); }
                }
            }

            // 最后触发事件回调
            try { ClientDisconnected?.Invoke(snapshot); } catch (Exception ex) { Console.WriteLine($"ClientDisconnected invoke exception: {ex}"); }
        }
        finally
        {
            try { snapshot.Close(); } catch (Exception ex) { Console.WriteLine($"RemoveClientAndNotify final snapshot.Close() exception: {ex}"); }
        }
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
                    catch (Exception ex) { Console.WriteLine($"PacketS2C handler OnServerRawSendAsync exception: {ex}"); }
                }
            }
            if (!proceed) { callback?.Invoke(false); return false; }

            var packet = base.Packetize(rawToSend);
            stream.Write(packet, 0, packet.Length);
            callback?.Invoke(true);
            return true;
        }
        catch (Exception ex) { Console.WriteLine($"PacketS2C exception: {ex}"); callback?.Invoke(false); return false; }
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
                        catch (Exception ex) { Console.WriteLine($"PacketS2AllC handler OnServerRawSendAsync exception: {ex}"); }
                    }
                    if (!proceed) continue;

                    var packet = base.Packetize(rawToSend);
                    c.GetStream().Write(packet, 0, packet.Length);
                }
                catch (Exception ex) { Console.WriteLine($"PacketS2AllC send exception: {ex}"); }
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
            catch (Exception ex) { Console.WriteLine($"AcceptLoopAsync AcceptSocketAsync exception: {ex}"); break; }
            if (socket == null) break;
            var client = new DrxTcpClient(socket);
            // 根据服务器配置决定该连接是否为一次性连接（短连接）
            bool isTemp = AcceptTemporaryConnections;
            if (!isTemp)
            {
                // 只有非临时连接才加入服务器连接列表并触发连接事件
                lock (_sync) { _clients.Add(client); }
                // 触发客户端连接事件
                ClientConnected?.Invoke(client);
                // 为长连接启动一个监控任务，以便更可靠地检测远端主动关闭
                _ = MonitorClientAsync(client);
            }
            _ = HandleClientAsync(client, isTemp);
        }
    }

    /// <summary>
    /// 监控客户端连接状态（用于更可靠检测远端主动断开）。
    /// 使用 Socket.Poll + Available 的组合：当可读且 Available==0 时视为对端已关闭连接。
    /// 发现断开后会调用 RemoveClientAndNotify 保证事件被触发且仅触发一次。
    /// </summary>
    private async Task MonitorClientAsync(DrxTcpClient client)
    {
        try
        {
            while (true)
            {
                try
                {
                    var sock = client?.Client;
                    if (sock == null) break;
                    // 更可靠的检测：使用 Peek 读取 1 字节；若返回 0 或抛出异常视为连接已关闭
                    try
                    {
                        var buffer = new byte[1];
                        int rc = sock.Receive(buffer, 0, 1, SocketFlags.Peek);
                        if (rc == 0)
                        {
                            RemoveClientAndNotify(client);
                            break;
                        }
                    }
                    catch (SocketException)
                    {
                        // 异常通常表示对端已经断开或 socket 出现错误
                        RemoveClientAndNotify(client);
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        RemoveClientAndNotify(client);
                        break;
                    }

                    // 作为补充，再检查 Connected（判空以避免分析警告）
                    if (client == null || !client.Connected)
                    {
                        RemoveClientAndNotify(client);
                        break;
                    }
                }
                catch (Exception ex) { Console.WriteLine($"MonitorClientAsync inner exception: {ex}"); }

                await Task.Delay(1000);
            }
        }
        catch (Exception ex) { Console.WriteLine($"MonitorClientAsync outer exception: {ex}"); }
    }

    private async Task HandleClientAsync(DrxTcpClient client, bool isTemporary)
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
                            catch (Exception ex) { Console.WriteLine($"HandleClientAsync handler OnServerRawReceiveAsync exception: {ex}"); }
                        }
                    }

                    if (!proceed) continue; // 中断本次循环，丢弃该数据

                    var payload = base.UnpackPacket(raw);

                    // 入队或直接处理（一次性连接不入队，立即同步调用 handlers 并允许发送响应）
                    try
                    {
                        if (isTemporary)
                        {
                            // 一次性连接：同步调用 handlers，让它们可以选择发送响应
                            lock (_sync)
                            {
                                foreach (var h in _handlers)
                                {
                                    try
                                    {
                                        bool shouldRespond = false;
                                        try { shouldRespond = h.OnServerReceiveAsync(payload, client); } catch (Exception ex) { Console.WriteLine($"HandleClientAsync handler OnServerReceiveAsync exception: {ex}"); }
                                        if (shouldRespond)
                                        {
                                            // 让 handler 修改发送内容（OnServerSendAsync）并发送
                                            try
                                            {
                                                var response = h.OnServerSendAsync(Array.Empty<byte>(), client);
                                                if (response != null && response.Length > 0)
                                                {
                                                    // 直接调用发送，不触发加入 clients 列表的限制
                                                    try { PacketS2C(client, response); } catch (Exception ex) { Console.WriteLine($"HandleClientAsync PacketS2C exception: {ex}"); }
                                                }
                                            }
                                            catch (Exception ex) { Console.WriteLine($"HandleClientAsync handler OnServerSendAsync exception: {ex}"); }
                                        }
                                    }
                                    catch (Exception ex) { Console.WriteLine($"HandleClientAsync handler loop exception: {ex}"); }
                                }
                            }
                            // 一次性连接在处理完首个请求后关闭（不继续循环）
                            break;
                        }
                        else
                        {
                            // 非临时连接：尝试入队到基类队列，失败时降级同步调用 handlers
                            if (!base.EnqueueMessage(payload, client))
                            {
                                lock (_sync)
                                {
                                    foreach (var h in _handlers) try { h.OnServerReceiveAsync(payload, client); } catch (Exception ex) { Console.WriteLine($"HandleClientAsync fallback OnServerReceiveAsync exception: {ex}"); }
                                }
                            }
                        }
                    }
                    catch (Exception ex) { Console.WriteLine($"HandleClientAsync enqueue/process exception: {ex}"); /* 忽略入队/处理异常 */ }
                }
                catch (Exception ex) { Console.WriteLine($"HandleClientAsync unpack/parse exception: {ex}"); /* 忽略无法解析的数据 */ }
            }
        }
        catch (Exception ex) { Console.WriteLine($"HandleClientAsync outer exception: {ex}"); }
        finally
        {
            try { client.Close(); } catch (Exception ex) { Console.WriteLine($"finally client.Close() exception: {ex}"); }
            // 对于临时连接（一次性连接），它们没有加入到 _clients 列表中，
            // 因此需要直接触发断开事件；非临时连接使用统一方法移除并通知。
            if (isTemporary)
            {
                // 临时连接没有加入到 _clients，因此我们需要构造一个副本并交给 handlers/event
                DrxTcpClient snapshot = null;
                try
                {
                    var sock = client?.Client;
                    string? re = null; string? le = null;
                    try { if (sock != null) { re = sock.RemoteEndPoint?.ToString(); le = sock.LocalEndPoint?.ToString(); } } catch (Exception ex) { Console.WriteLine($"temporary disconnect get endpoints exception: {ex}"); }
                    if (sock != null) snapshot = new DrxTcpClient(sock);
                    else snapshot = new DrxTcpClient();

                    try { if (re != null) snapshot.SetTag("RemoteEndPoint", re); } catch (Exception ex) { Console.WriteLine($"temporary snapshot SetTag RemoteEndPoint exception: {ex}"); }
                    try { if (le != null) snapshot.SetTag("LocalEndPoint", le); } catch (Exception ex) { Console.WriteLine($"temporary snapshot SetTag LocalEndPoint exception: {ex}"); }

                    // handlers OnServerDisconnecting
                    lock (_sync)
                    {
                        foreach (var h in _handlers) try { h.OnServerDisconnecting(snapshot); } catch (Exception ex) { Console.WriteLine($"temporary handler OnServerDisconnecting exception: {ex}"); }
                    }

                    try { snapshot.Close(); } catch (Exception ex) { Console.WriteLine($"temporary snapshot.Close() exception: {ex}"); }

                    lock (_sync)
                    {
                        foreach (var h in _handlers) try { h.OnServerDisconnected(snapshot); } catch (Exception ex) { Console.WriteLine($"temporary handler OnServerDisconnected exception: {ex}"); }
                    }

                    try { ClientDisconnected?.Invoke(snapshot); } catch (Exception ex) { Console.WriteLine($"temporary ClientDisconnected invoke exception: {ex}"); }
                }
                catch (Exception ex) { Console.WriteLine($"temporary disconnect outer exception: {ex}"); }
                finally { try { snapshot?.Close(); } catch (Exception ex) { Console.WriteLine($"temporary final snapshot.Close() exception: {ex}"); } }
            }
            else
            {
                try { RemoveClientAndNotify(client); } catch (Exception ex) { Console.WriteLine($"finally RemoveClientAndNotify exception: {ex}"); }
            }
        }
    }
}
