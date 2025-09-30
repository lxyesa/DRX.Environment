using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Drx.Sdk.Network.V2.Socket;

public class NetworkServer : IDisposable
{
    // -------------------- 配置项 --------------------
    private readonly IPEndPoint _localEndPoint;
    private readonly bool _enableTcp;
    private readonly bool _enableUdp;
    /// <summary>
    /// 客户端被认为是僵尸（长时间无活动）的时间，默认 60 秒
    /// </summary>
    public TimeSpan InactivityTimeout { get; set; } = TimeSpan.FromSeconds(60);
    /// <summary>
    /// 僵尸扫描周期，默认 30 秒
    /// </summary>
    public TimeSpan ZombieScanInterval { get; set; } = TimeSpan.FromSeconds(30);

    // -------------------- 内部字段 --------------------
    private TcpListener? _tcpListener;
    private System.Net.Sockets.Socket? _udpSocket;

    private readonly ConcurrentDictionary<string, TcpClientState> _tcpClients = new();
    private readonly ConcurrentDictionary<string, UdpClientState> _udpClients = new();

    // 接收队列：统一把收到的数据包放进队列，供上层消费或事件触发
    private readonly ConcurrentQueue<ReceivedPacket> _receiveQueue = new();

    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private Task? _udpReceiveTask;
    private Task? _zombieScanTask;

    private readonly byte[] _bufferPrototype = new byte[8192];

    // -------------------- 事件 & 委托 --------------------
    public delegate void ClientConnectedHandler(string clientId, IPEndPoint remote);
    public delegate void ClientDisconnectedHandler(string clientId, IPEndPoint remote);
    public delegate void DataReceivedHandler(string clientId, IPEndPoint remote, byte[] data);
    public delegate void ErrorHandler(Exception ex);

    public event ClientConnectedHandler? OnClientConnected;
    public event ClientDisconnectedHandler? OnClientDisconnected;
    public event DataReceivedHandler? OnDataReceived;
    public event ErrorHandler? OnError;

    // -------------------- 构造 --------------------
    public NetworkServer(IPEndPoint localEndPoint, bool enableTcp = true, bool enableUdp = true)
    {
        _localEndPoint = localEndPoint ?? throw new ArgumentNullException(nameof(localEndPoint));
        _enableTcp = enableTcp;
        _enableUdp = enableUdp;
    }

    // -------------------- 启动/停止 --------------------
    public async Task StartAsync()
    {
        if (_cts != null)
            throw new InvalidOperationException("Server already started");

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (_enableTcp)
        {
            _tcpListener = new TcpListener(_localEndPoint);
            _tcpListener.Start();
            _acceptLoopTask = Task.Run(() => AcceptLoopAsync(token), token);
        }

        if (_enableUdp)
        {
            // 用原始 Socket 来更灵活地处理 UDP
            _udpSocket = new System.Net.Sockets.Socket(_localEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            _udpSocket.Bind(_localEndPoint);
            _udpReceiveTask = Task.Run(() => UdpReceiveLoopAsync(token), token);
        }

        _zombieScanTask = Task.Run(() => ZombieScanLoopAsync(token), token);

        await Task.CompletedTask;
    }

    public void Stop()
    {
        if (_cts == null)
            return;

        try
        {
            _cts.Cancel();
        }
        catch { }

        try
        {
            _tcpListener?.Stop();
        }
        catch { }

        try
        {
            _udpSocket?.Close();
        }
        catch { }

        // 断开并清理客户端
        foreach (var kv in _tcpClients)
        {
            TryCloseTcpClient(kv.Key, kv.Value);
        }
        _tcpClients.Clear();

        _udpClients.Clear();

        _cts = null;
    }

    // -------------------- TCP 处理 --------------------
    private async Task AcceptLoopAsync(CancellationToken token)
    {
        Debug.WriteLine("TCP accept loop started");
        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient tcpClient;
                try
                {
                    tcpClient = await _tcpListener!.AcceptTcpClientAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex);
                    await Task.Delay(100, token).ConfigureAwait(false);
                    continue;
                }

                _ = Task.Run(() => HandleNewTcpClientAsync(tcpClient, token), token);
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
        Debug.WriteLine("TCP accept loop stopped");
    }

    private async Task HandleNewTcpClientAsync(TcpClient tcpClient, CancellationToken token)
    {
        var remote = (tcpClient.Client.RemoteEndPoint as IPEndPoint) ?? new IPEndPoint(IPAddress.Any, 0);
        var id = Guid.NewGuid().ToString();

        var state = new TcpClientState
        {
            Id = id,
            Client = tcpClient,
            RemoteEndPoint = remote,
            LastSeen = DateTime.UtcNow,
            ReceiveCts = CancellationTokenSource.CreateLinkedTokenSource(token)
        };

        if (!_tcpClients.TryAdd(id, state))
        {
            // 不能加就关掉
            TryCloseTcpClient(id, state);
            return;
        }

        OnClientConnected?.Invoke(id, remote);

        try
        {
            using var stream = tcpClient.GetStream();
            var buffer = new byte[Math.Max(1024, _bufferPrototype.Length)];

            while (!state.ReceiveCts.IsCancellationRequested && tcpClient.Connected)
            {
                int read = 0;
                try
                {
                    // ReadAsync 会在连接断开时返回 0
                    read = await stream.ReadAsync(buffer, 0, buffer.Length, state.ReceiveCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex);
                    break;
                }

                if (read == 0)
                {
                    // 客户端关闭连接
                    break;
                }

                state.LastSeen = DateTime.UtcNow;

                var data = new byte[read];
                Array.Copy(buffer, 0, data, 0, read);

                // 入队并触发事件（事件可能会阻塞调用方，尽量在事件处理里不要做长时间阻塞）
                var packet = new ReceivedPacket
                {
                    ClientId = id,
                    Remote = state.RemoteEndPoint,
                    Data = data,
                    IsUdp = false
                };
                _receiveQueue.Enqueue(packet);
                OnDataReceived?.Invoke(id, state.RemoteEndPoint, data);
            }
        }
        finally
        {
            // 清理
            _tcpClients.TryRemove(id, out var _);
            TryCloseTcpClient(id, state);
            OnClientDisconnected?.Invoke(id, state.RemoteEndPoint);
        }
    }

    private void TryCloseTcpClient(string id, TcpClientState state)
    {
        try
        {
            try
            {
                state.ReceiveCts?.Cancel();
            }
            catch { }

            try
            {
                state.Client?.Close();
            }
            catch { }

            try
            {
                state.ReceiveCts?.Dispose();
            }
            catch { }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }

    // -------------------- UDP 处理 --------------------
    private async Task UdpReceiveLoopAsync(CancellationToken token)
    {
        Debug.WriteLine("UDP receive loop started");

        var receiveBuffer = new byte[65536];

        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await _udpSocket!.ReceiveMessageFromAsync(new ArraySegment<byte>(receiveBuffer), SocketFlags.None, new IPEndPoint(IPAddress.Any, 0)).ConfigureAwait(false);
                var remoteEp = result.RemoteEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.Any, 0);
                int received = result.ReceivedBytes;

                var data = new byte[received];
                Array.Copy(receiveBuffer, 0, data, 0, received);

                var key = remoteEp.ToString();
                var state = _udpClients.GetOrAdd(key, _ => new UdpClientState { RemoteEndPoint = remoteEp, LastSeen = DateTime.UtcNow });
                state.LastSeen = DateTime.UtcNow;

                var packet = new ReceivedPacket
                {
                    ClientId = key,
                    Remote = remoteEp,
                    Data = data,
                    IsUdp = true
                };
                _receiveQueue.Enqueue(packet);
                OnDataReceived?.Invoke(key, remoteEp, data);
            }
            catch (SocketException se)
            {
                // 当 socket 关闭时可能会抛异常，按需要通知
                OnError?.Invoke(se);
                await Task.Delay(50, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                await Task.Delay(50, token).ConfigureAwait(false);
            }
        }

        Debug.WriteLine("UDP receive loop stopped");
    }

    // -------------------- 僵尸扫描 --------------------
    private async Task ZombieScanLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // TCP 客户端
                foreach (var kv in _tcpClients)
                {
                    var id = kv.Key;
                    var state = kv.Value;
                    if (now - state.LastSeen > InactivityTimeout)
                    {
                        // 认定为僵尸，先尝试优雅断开
                        OnError?.Invoke(new TimeoutException($"TCP client {id} seems inactive and will be disconnected."));
                        if (_tcpClients.TryRemove(id, out var removed))
                        {
                            TryCloseTcpClient(id, removed);
                            OnClientDisconnected?.Invoke(id, removed.RemoteEndPoint);
                        }
                    }
                }

                // UDP 客户端（按 endpoint 识别）
                foreach (var kv in _udpClients)
                {
                    var key = kv.Key;
                    var state = kv.Value;
                    if (now - state.LastSeen > InactivityTimeout)
                    {
                        _udpClients.TryRemove(key, out var _);
                        // 对于 UDP 我们也触发断开事件以便上层清理
                        OnClientDisconnected?.Invoke(key, state.RemoteEndPoint);
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }

            try
            {
                await Task.Delay(ZombieScanInterval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // -------------------- 发送 --------------------
    public void SendToTcpClient(string clientId, byte[] data)
    {
        if (!_tcpClients.TryGetValue(clientId, out var state))
            throw new KeyNotFoundException("Tcp client not found");

        try
        {
            var stream = state.Client.GetStream();
            stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }

    public void BroadcastTcp(byte[] data)
    {
        foreach (var kv in _tcpClients)
        {
            SendToTcpClient(kv.Key, data);
        }
    }

    public void SendToUdp(IPEndPoint remote, byte[] data)
    {
        try
        {
            _udpSocket?.SendTo(data, remote);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }

    public void BroadcastUdp(byte[] data)
    {
        foreach (var kv in _udpClients)
        {
            SendToUdp(kv.Value.RemoteEndPoint, data);
        }
    }

    // -------------------- 公共查询 --------------------
    public IEnumerable<string> GetTcpClientIds() => _tcpClients.Keys;
    public IEnumerable<string> GetUdpClientKeys() => _udpClients.Keys;

    public bool IsTcpClientConnected(string clientId)
    {
        if (!_tcpClients.TryGetValue(clientId, out var state)) return false;
        try
        {
            return state.Client.Connected;
        }
        catch { return false; }
    }

    // 从队列里尝试取出一条消息
    public bool TryDequeue(out ReceivedPacket packet)
    {
        return _receiveQueue.TryDequeue(out packet!);
    }

    // -------------------- 清理与释放 --------------------
    public void Dispose()
    {
        Stop();
    }

    // -------------------- 内部类型 --------------------
    public class TcpClientState
    {
        public string Id = string.Empty;
        public TcpClient Client = null!;
        public IPEndPoint RemoteEndPoint = null!;
        public DateTime LastSeen;
        public CancellationTokenSource? ReceiveCts;
    }

    public class UdpClientState
    {
        public IPEndPoint RemoteEndPoint = null!;
        public DateTime LastSeen;
    }

    public class ReceivedPacket
    {
        public string ClientId = string.Empty; // TCP 用 clientId，UDP 用 endpoint.ToString()
        public IPEndPoint Remote = null!;
        public byte[] Data = null!;
        public bool IsUdp = false;
    }
}