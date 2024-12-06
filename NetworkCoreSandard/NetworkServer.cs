using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Events;
using static NetworkCoreStandard.Events.NetworkEventDelegate;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;
using System.Collections.Concurrent;
using Microsoft.Extensions.ObjectPool;
using NetworkCoreStandard.Interface;
using NetworkCoreStandard.Config;
using NetworkCoreStandard.Extensions;
using NetworkCoreStandard.Components;

namespace NetworkCoreStandard;

/// <summary>
/// 网络服务器类，处理TCP连接和事件分发
/// </summary>

public class NetworkServer : NetworkObject
{
    protected Socket _socket;
    protected int _port;
    protected string _ip;
    private HashSet<Socket> _clients = new();
    protected ServerConfig _config;
    // 添加消息队列
    private readonly ConcurrentQueue<(NetworkPacket packet, Socket socket)> _messageQueue = new();
    // 添加处理线程的取消令牌
    private readonly CancellationTokenSource _processingCts = new();
    // 处理线程数量(可配置)
    private int _processorCount = Environment.ProcessorCount;
    // 处理任务列表
    private readonly List<Task> _processingTasks = new();

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
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

    public NetworkServer() : base()
    {
        _config = new ServerConfig();
        _ip = _config.IP;
        _port = _config.Port;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

    public virtual void SetConfig(ServerConfig config)
    {
        _config = config;
        _ip = config.IP;
        _port = config.Port;
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

    protected virtual bool ValidateConnection(Socket clientSocket)
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

    public virtual async void StartAsync()
    {
        await Task.Run(() => Start());
    }

    public virtual async void StopAsync()
    {
        await Task.Run(() => Stop());
    }

    public virtual async void RestartAsync(Action<NetworkServer> action)
    {
        await Task.Run(() => Restart(action));
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
            Task.WaitAll(_processingTasks.ToArray(), TimeSpan.FromSeconds(5));  // 等待任务完成
            _socket.Close();
            _clients.Clear();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"关闭服务器时发生错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 启动服务器开始监听连接
    /// </summary>
    public virtual void Start()
    {
        try
        {
            _socket.Bind(new IPEndPoint(IPAddress.Parse(_ip), _port));
            _socket.Listen(10);
            _socket.BeginAccept(AcceptCallback, null);

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

    /// <summary>
    /// 处理新的客户端连接
    /// </summary>
    protected virtual async void AcceptCallback(IAsyncResult ar)
    {
        try
        {
            Socket clientSocket = _socket.EndAccept(ar);

            // 验证连接
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

            _clients.Add(clientSocket);
            clientSocket.AddComponent<ClientComponent>();

            await RaiseEventAsync("OnClientConnected", new NetworkEventArgs(
                socket: clientSocket,
                eventType: NetworkEventType.ServerClientConnected,
                message: $"客户端 {clientSocket.RemoteEndPoint} 已连接"
            ));

            BeginReceive(clientSocket);
        }
        catch (Exception ex)
        {
            await RaiseEventAsync("OnError", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"接受客户端连接时发生错误: {ex.Message}"
            ));
        }
        finally
        {
            // 继续监听下一个连接
            _socket.BeginAccept(AcceptCallback, null);
        }
    }

    /// <summary>
    /// 开始异步接收客户端数据
    /// </summary>
    protected virtual void BeginReceive(Socket clientSocket)
    {
        byte[] buffer = new byte[8192]; // 增大缓冲区
        try
        {
            clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None,
                (ar) => HandleDataReceived(ar, clientSocket, buffer), null);
        }
        catch
        {
            HandleDisconnect(clientSocket);
        }
    }

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
    /// 处理接收到的数据
    /// </summary>
    protected virtual void HandleDataReceived(IAsyncResult ar, Socket clientSocket, byte[] buffer)
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
                HandleDisconnect(clientSocket);
            }
        }
        catch
        {
            HandleDisconnect(clientSocket);
        }
    }

    /// <summary>
    /// 处理接收到的数据包
    /// </summary>
    /// <param name="packet"></param>
    /// <param name="clientSocket"></param>
    /// <returns></returns>
    protected virtual async Task ProcessMessageAsync(NetworkPacket packet, Socket clientSocket)
    {
        // 首先触发数据接收事件
        await RaiseEventAsync("OnDataReceived", new NetworkEventArgs(
            socket: clientSocket,
            eventType: NetworkEventType.DataReceived,
            message: string.Empty,
            packet: packet
        ));

        // 然后处理具体的包类型
        switch (packet.Type)
        {
            case (int)PacketType.Command:
                await RaiseEventAsync("OnCommandReceived", new NetworkEventArgs(
                    socket: clientSocket,
                    eventType: NetworkEventType.HandlerEvent,
                    packet: packet
                ));
                break;
        }
    }

    /// <summary>
    /// 向指定客户端发送数据包
    /// </summary>
    public virtual void Send(Socket clientSocket, NetworkPacket packet)
    {
        if (!_clients.Contains(clientSocket))
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
            clientSocket.BeginSend(data, 0, data.Length, SocketFlags.None,
                new AsyncCallback(SendCallback), clientSocket);
        }
        catch (Exception ex)
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                socket: clientSocket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"发送数据时发生错误: {ex.Message}"
            ));
            HandleDisconnect(clientSocket);
        }
    }

    /// <summary>
    /// 向所有已连接的客户端广播数据包
    /// </summary>
    public virtual void Broadcast(NetworkPacket packet)
    {
        List<Socket> deadClients = new List<Socket>();

        foreach (Socket client in _clients)
        {
            try
            {
                byte[] data = packet.Serialize();
                client.BeginSend(data, 0, data.Length, SocketFlags.None,
                    new AsyncCallback(SendCallback), client);
            }
            catch (Exception ex)
            {
                _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                    socket: client,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"广播数据时发生错误: {ex.Message}"
                ));
                Console.WriteLine($"客户端 {((IPEndPoint)client.RemoteEndPoint).Address} 发送数据时发生错误，断开连接。");
                deadClients.Add(client);
            }
        }

        // 清理断开连接的客户端
        foreach (Socket client in deadClients)
        {
            HandleDisconnect(client);
        }
    }

    protected virtual void SendCallback(IAsyncResult ar)
    {
        Socket clientSocket = (Socket)ar.AsyncState;
        try
        {
            clientSocket.EndSend(ar);
        }
        catch (Exception ex)
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                socket: clientSocket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"发送数据时发生错误: {ex.Message}"
            ));
            HandleDisconnect(clientSocket);
        }
    }

    /// <summary>
    /// 处理客户端断开连接的清理工作
    /// </summary>
    public virtual async void HandleDisconnect(Socket clientSocket)
    {
        try
        {
            var endpoint = clientSocket.RemoteEndPoint?.ToString() ?? "Unknown";

            // 首先尝试获取客户端信息
            if (_clients.Contains(clientSocket))
            {
                await RaiseEventAsync("OnClientDisconnected", new NetworkEventArgs(
                    socket: clientSocket!,
                    eventType: NetworkEventType.ServerClientDisconnected,
                    message: $"Client {endpoint} disconnected"
                ));

                _clients.Remove(clientSocket);
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
    public virtual bool DisconnectClient(Socket clientSocket)
    {
        try
        {
            HandleDisconnect(clientSocket);
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




    #region Getters


    /// <summary>
    /// 获取当前连接的客户端数量
    /// </summary>
    /// <returns></returns>
    public virtual HashSet<Socket> GetConnectedSockets()
    {
        return _clients;
    }
    #endregion
}