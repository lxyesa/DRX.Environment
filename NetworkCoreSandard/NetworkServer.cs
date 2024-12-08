using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;
using System.Collections.Concurrent;
using Microsoft.Extensions.ObjectPool;
using NetworkCoreStandard.Config;
using NetworkCoreStandard.Components;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Buffers;
using NetworkCoreStandard.Utils.Extensions;
using NetworkCoreStandard.Utils.Common;
using System.Diagnostics;

namespace NetworkCoreStandard;

/// <summary>
/// 网络服务器类，处理TCP连接和事件分发
/// </summary>

public class NetworkServer : NetworkObject
{
    #region 基础成员和初始化
    // 基础字段
    protected DRXSocket _socket;
    protected int _port;
    protected string _ip;
    private readonly ConcurrentDictionary<DRXSocket, byte> _clients = new();
    protected ServerConfig _config;
    private readonly ConcurrentQueue<(NetworkPacket packet, DRXSocket socket)> _messageQueue = new();
    private readonly CancellationTokenSource _processingCts = new();
    private int _processorCount = Environment.ProcessorCount;
    private readonly List<Task> _processingTasks = new();
    private readonly ObjectPool<NetworkPacket> _packetPool;
    private readonly ObjectPool<byte[]> _bufferPool;
    private const int BATCH_SIZE = 100;
    private const int BUFFER_SIZE = 8192;

    /// <summary>
    /// 初始化服务器并开始监听指定端口
    /// </summary>
    /// <param name="port">监听端口</param>
    public NetworkServer(ServerConfig config) : base()
    {
        Logger.Log("Server", "服务器已初始化");
        _config = config;
        _ip = config.IP;
        _port = config.Port;
        _socket = new DRXSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // 初始化对象池
        var packetPoolPolicy = new DefaultPooledObjectPolicy<NetworkPacket>();
        _packetPool = new DefaultObjectPool<NetworkPacket>(packetPoolPolicy, 1000);
        
        var bufferPoolPolicy = new ByteArrayPoolPolicy(BUFFER_SIZE);
        _bufferPool = new DefaultObjectPool<byte[]>(bufferPoolPolicy, 1000);
    }

    private class ByteArrayPoolPolicy : IPooledObjectPolicy<byte[]>
    {
        private readonly int _bufferSize;

        public ByteArrayPoolPolicy(int bufferSize)
        {
            _bufferSize = bufferSize;
        }

        public byte[] Create()
        {
            return new byte[_bufferSize];
        }

        public bool Return(byte[] obj)
        {
            return true;
        }
    }

    public NetworkServer() : base()
    {
        _config = new ServerConfig();
        _ip = _config.IP;
        _port = _config.Port;
        _socket = new DRXSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // Initialize object pools
        var packetPoolPolicy = new DefaultPooledObjectPolicy<NetworkPacket>();
        _packetPool = new DefaultObjectPool<NetworkPacket>(packetPoolPolicy, 1000);
        
        var bufferPoolPolicy = new ByteArrayPoolPolicy(BUFFER_SIZE);
        _bufferPool = new DefaultObjectPool<byte[]>(bufferPoolPolicy, 1000);
    }

    public virtual void SetConfig(ServerConfig config)
    {
        _config = config;
        _ip = config.IP;
        _port = config.Port;
    }
    #endregion

    #region 服务器核心操作
    public virtual void Start()
    {
        try
        {
            _socket.Bind(new IPEndPoint(IPAddress.Parse(_ip), _port));
            _socket.Listen(10);
            _ = _socket.BeginAccept(AcceptCallback, null);

            // 启动消息处理线程
            StartMessageProcessors();

            _ = RaiseEventAsync("OnServerStarted", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.ServerStarted,
                message: $"服务器已启动，监听 {_ip}:{_port}"
            ));
            
            ServerTick(); // 启动服务器Tick

            Logger.Log("Server", _config.OnServerStartedTip);
        }
        catch (Exception ex)
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"启动服务器时发生错误: {ex.Message}"
            ));
        }
    }

    public virtual void Stop()
    {
        if (!_socket.IsBound)
        {
            Console.WriteLine($"[{DateTime.Now}] [Warning] 这不是一个错误，而是因为你的服务器实例已被销毁或根本没有启动，所以无法关闭服务器。");
            return;
        }
        try
        {
            _processingCts.Cancel();    // 取消处理任务
            _ = Task.WaitAll(_processingTasks.ToArray(), TimeSpan.FromSeconds(5));  // 等待任务完成
            _socket.Close();
            _clients.Clear();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"关闭服务器时发生错误: {ex.Message}");
        }
    }

    public virtual void Restart(Action<NetworkServer> action)
    {
        if (_socket == null)
        {
            Console.WriteLine($"[{DateTime.Now}] [Warning] 这不是一个错误，而是因为你的服务器实例已被销毁或根本没有启动，所以无法重启服务器。");
            return;
        }
        try
        {
            Stop();
            action?.Invoke(this);
            Start();
        }
        catch (Exception ex)
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"重启服务器时发生错误: {ex.Message}"
                ));

            throw;
        }
    }

    protected virtual void ServerTick(){
        try
        {
            _ = this.DoTickAsync(() => {
                _ = RaiseEventAsync("OnServerTick", new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent
                ));
            }, (int)(1000 / _config.TickRate), "ServerTick");
        }
        catch (Exception ex)
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"服务器Tick时发生错误: {ex.Message}"
            ));

            Console.WriteLine($"服务器Tick时发生错误: {ex.Message}");
        }
    }
    #endregion

    #region 客户端连接管理
    /// <summary>
    /// 验证连接请求
    /// </summary>
    /// <param name="clientSocket"></param>
    /// <returns></returns>
    protected virtual bool ValidateConnection(DRXSocket clientSocket)
    {
        try
        {
            var clientIP = (clientSocket.RemoteEndPoint as IPEndPoint)?.Address.ToString();

            // 检查最大连接数
            if (_clients.Count >= _config.MaxClients)
            {
                _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                    socket: clientSocket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"[{DateTime.Now:yyyy/mm/dd hh:mm:ss}] [ConnectionFailed] 服务器已达到最大连接数 ({_config.MaxClients})，因此 {clientIP} 的连接被拒绝。"
                ));
                return false;
            }

            // 检查IP黑名单
            if (clientIP != null && _config.BlacklistIPs.Contains(clientIP))
            {
                _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                    socket: clientSocket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"[{DateTime.Now:yyyy/mm/dd hh:mm:ss}] [ConnectionFailed] {clientIP} 在IP黑名单中，因此服务器拒绝与其建立连接。"
                ));
                return false;
            }

            // 检查IP白名单(如果启用)
            if (_config.WhitelistIPs.Any() &&
                clientIP != null &&
                !_config.WhitelistIPs.Contains(clientIP))
            {
                _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                    socket: clientSocket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"[{DateTime.Now:yyyy/mm/dd hh:mm:ss}] [ConnectionFailed] {clientIP} 不在IP白名单中，因此服务器拒绝与其建立连接。"
                ));
                return false;
            }

            // 执行自定义验证
            if (_config.CustomValidator != null)
            {
                return _config.CustomValidator(clientSocket);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"验证连接时发生错误: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 处理新的客户端连接
    /// </summary>
    protected virtual async void AcceptCallback(IAsyncResult ar)
    {
        DRXSocket? clientSocket = null;
        try
        {
            Socket baseSocket = _socket.EndAccept(ar);
            clientSocket = baseSocket.TakeOver<DRXSocket>();

            if (!ValidateConnection(clientSocket))
            {
                await RaiseEventAsync("OnConnectionRejected", new NetworkEventArgs(
                    socket: clientSocket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: "连接被拒绝"
                ));
                clientSocket.Close();
                return;
            }

            if (_clients.TryAdd(clientSocket, 1))
            {
                await InitializeClientSocket(clientSocket);
                BeginReceive(clientSocket);
            }
            else
            {
                clientSocket.Close();
                Logger.Log("Server", "无法添加客户端到连接列表");
            }
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "Server", $"处理客户端连接时发生错误: {ex.Message}");
            clientSocket?.Close();
        }
        finally
        {
            ContinueAccepting();
        }
    }

    /// <summary>
    /// 初始化客户端Socket
    /// </summary>
    /// <param name="clientSocket"></param>
    /// <returns></returns>
    private async Task InitializeClientSocket(DRXSocket clientSocket)
    {
        _ = clientSocket.AddComponent<ClientComponent>();

        await RaiseEventAsync("OnClientConnected", new NetworkEventArgs(
            socket: clientSocket,
            eventType: NetworkEventType.ServerClientConnected,
            message: $"客户端 {clientSocket.RemoteEndPoint} 已连接"
        ));
    }

    /// <summary>
    /// 继续接受新的连接
    /// </summary>
    private void ContinueAccepting()
    {
        if (_socket?.IsBound == true)
        {
            try
            {
                _socket.BeginAccept(AcceptCallback, null);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Server", $"继续监听连接时发生错误: {ex.Message}");
            }
        }
    }

    public virtual async Task HandleDisconnectAsync(DRXSocket clientSocket)
    {
        try
        {
            var endpoint = clientSocket.RemoteEndPoint?.ToString() ?? "Unknown";

            // 首先尝试获取客户端信息
            if (_clients.ContainsKey(clientSocket))
            {
                await RaiseEventAsync("OnClientDisconnected", new NetworkEventArgs(
                    socket: clientSocket!,
                    eventType: NetworkEventType.ServerClientDisconnected,
                    message: $"Client {endpoint} disconnected"
                ));

                _ = _clients.TryRemove(clientSocket, out _);
            }

            // 关闭Socket连接
            CloseSocketSafely(clientSocket);
        }
        catch (Exception)   // 异常将被忽略
        {
            // Console.WriteLine($"处理断开连接时发生错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 辅助方法，安全地关闭Socket连接
    /// </summary>
    /// <param name="socket"></param>
    protected virtual void CloseSocketSafely(Socket socket)
    {
        try
        {
            if (socket.Connected)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch { } // 忽略shutdown可能的异常
                finally
                {
                    socket.Close();
                }
            }
        }
        catch { } // 忽略所有关闭过程中的异常
    }

    /// <summary>
    /// 强制断开指定IP的客户端连接
    /// </summary>
    /// <param name="clientIP">客户端IP地址</param>
    /// <returns>是否成功断开连接</returns>
    public virtual bool DisconnectClient(DRXSocket clientSocket)
    {
        try
        {
            _ = HandleDisconnectAsync(clientSocket);
            return true;
        }
        catch (Exception ex)
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                socket: null!,
                eventType: NetworkEventType.HandlerEvent,
                message: $"强制断开客户端连接时发生错误: {ex.Message}"
            ));
            return false;
        }
    }
    #endregion

    #region 消息处理和队列
    /// <summary>
    /// 开始处理消息队列
    /// </summary>
    protected virtual void StartMessageProcessors()
    {
        for (int i = 0; i < _processorCount; i++)
        {
            _processingTasks.Add(Task.Run(async () =>
            {
                while (!_processingCts.Token.IsCancellationRequested)
                {
                    if (_messageQueue.TryDequeue(out var message))
                    {
                        try
                        {
                            await ProcessMessageAsync(message.packet, message.socket);
                        }
                        catch (Exception ex)
                        {
                            await RaiseEventAsync("OnError", new NetworkEventArgs(
                                socket: message.socket,
                                eventType: NetworkEventType.HandlerEvent,
                                message: $"处理消息时发生错误: {ex.Message}"
                            ));
                        }
                    }
                    else
                    {
                        // 队列为空时等待一小段时间
                        await Task.Delay(1, _processingCts.Token);
                    }
                }
            }, _processingCts.Token));
        }
    }

    /// <summary>
    /// 处理接收到的消息，你可以覆盖这个方法以实现自定义的消息处理逻辑，无论如何，你应该在这个方法的最后使用 ReturnPacket 方法将消息对象返回到对象池
    /// </summary>
    /// <param name="packet"></param>
    /// <param name="clientSocket"></param>
    /// <returns></returns>
    protected virtual async Task ProcessMessageAsync(NetworkPacket packet, DRXSocket clientSocket)
    {
        try
        {
            await using var batch = new BatchProcessor(BATCH_SIZE);

            // 处理消息
            await batch.AddAsync(() => RaiseEventAsync("OnDataReceived", new NetworkEventArgs(
                socket: clientSocket,
                eventType: NetworkEventType.DataReceived,
                packet: packet.GetBytes()
            )));

            await batch.ExecuteAsync();
        }
        finally
        {
            // 返回对象到池
           ReturnPacket(packet);
        }
    }

    /// <summary>
    /// 处理接收到的数据
    /// </summary>
    protected virtual void HandleDataReceived(IAsyncResult ar, DRXSocket clientSocket, byte[] buffer)
    {
        try
        {
            int bytesRead = clientSocket.EndReceive(ar);
            if (bytesRead > 0)
            {
                // 解析数据包
                NetworkPacket packet = NetworkPacket.Deserialize(buffer.Take(bytesRead).ToArray());

                // 将消息加入队列而不是直接处理
                _messageQueue.Enqueue((packet, clientSocket));

                // 继续接收数据
                BeginReceive(clientSocket);
            }
            else
            {
                _ = HandleDisconnectAsync(clientSocket);
            }
        }
        catch
        {
            _ = HandleDisconnectAsync(clientSocket);
        }
    }
    #endregion

    #region 异步通信
    /// <summary>
    /// 开始异步接收客户端数据
    /// </summary>
    protected virtual void BeginReceive(DRXSocket clientSocket)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BUFFER_SIZE);
        try
        {
            _ = clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None,
                ar =>
                {
                    try
                    {
                        HandleDataReceived(ar, clientSocket, buffer);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }, null);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
            _ = HandleDisconnectAsync(clientSocket);
        }
    }

    /// <summary>
    /// 向指定客户端发送数据包
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Send(DRXSocket clientSocket, NetworkPacket packet)
    {
        if (!_clients.ContainsKey(clientSocket))
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                    socket: clientSocket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: "客户端未连接"
                ));
            return;
        }

        try
        {
            byte[] data = packet.Serialize();
            var sendBuffer = ArrayPool<byte>.Shared.Rent(data.Length);
            Buffer.BlockCopy(data, 0, sendBuffer, 0, data.Length);

            _ = clientSocket.BeginSend(sendBuffer, 0, data.Length, SocketFlags.None,
                ar =>
                {
                    try
                    {
                        SendCallback(ar);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(sendBuffer);
                    }
                }, clientSocket);
        }
        catch (Exception ex)
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                socket: clientSocket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"发送数据时发生错误: {ex.Message}"
            ));
            _ = HandleDisconnectAsync(clientSocket);
        }
    }

    /// <summary>
    /// 向所有已连接的客户端广播数据包
    /// </summary>
    public virtual async Task BroadcastAsync(NetworkPacket packet)
    {
        var tasks = new List<Task>();
        var deadClients = new ConcurrentBag<DRXSocket>();
        
        byte[] data = packet.Serialize();
        var buffer = ArrayPool<byte>.Shared.Rent(data.Length);
        Buffer.BlockCopy(data, 0, buffer, 0, data.Length);

        try
        {
            foreach (DRXSocket client in _clients.Keys)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        _ = client.BeginSend(buffer, 0, buffer.Length, SocketFlags.None,
                            ar => SendCallback(ar), client);
                    }
                    catch
                    {
                        deadClients.Add(client);
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            
            // 清理断开的连接
            foreach (var client in deadClients)
            {
                _ = HandleDisconnectAsync(client);
            }
        }
    }

    /// <summary>
    /// 异步发送数据的回调
    /// </summary>
    protected virtual void SendCallback(IAsyncResult ar)
    {
        DRXSocket clientSocket = (DRXSocket)ar.AsyncState;
        try
        {
            _ = clientSocket.EndSend(ar);
        }
        catch (Exception ex)
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                socket: clientSocket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"发送数据时发生错误: {ex.Message}"
            ));
            _ = HandleDisconnectAsync(clientSocket);
        }
    }
    #endregion

    #region 工具类和辅助方法
    // 添加批处理处理器
    private class BatchProcessor : IAsyncDisposable
    {
        private readonly int _batchSize;
        private readonly List<Func<Task>> _actions;
        
        public BatchProcessor(int batchSize)
        {
            _batchSize = batchSize;
            _actions = new List<Func<Task>>(batchSize);
        }

        public async Task AddAsync(Func<Task> action)
        {
            _actions.Add(action);
            if (_actions.Count >= _batchSize)
            {
                await ExecuteAsync();
            }
        }

        public async Task ExecuteAsync()
        {
            if (_actions.Count == 0) return;
            
            await Task.WhenAll(_actions.Select(a => a()));
            _actions.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            await ExecuteAsync();
        }
    }

    /// <summary>
    /// 将数据包对象遣返到对象池
    /// </summary>
    /// <param name="packet">数据包</param>
    public virtual void ReturnPacket(NetworkPacket packet)
    {
        _packetPool.Return(packet);
    }
    #region Getters
    /// <summary>
    /// 获取当前连接的客户端数量
    /// </summary>
    /// <returns></returns>
    public virtual HashSet<DRXSocket> GetConnectedSockets()
    {
        return new HashSet<DRXSocket>(_clients.Keys);
    }
    #endregion
    #endregion
}