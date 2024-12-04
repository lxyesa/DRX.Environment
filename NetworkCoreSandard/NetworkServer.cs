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

namespace NetworkCoreStandard;

/// <summary>
/// 网络服务器类，处理TCP连接和事件分发
/// </summary>
public class NetworkServer : NetworkObject
{
    protected Socket _socket;
    protected int _port;
    protected string _ip;
    private ConcurrentDictionary<Socket, Client> _clientsBySocket = new();
    private ConcurrentDictionary<int, Client> _clientsByID = new();
    protected ConnectionConfig _config;

    /// <summary>
    /// 初始化服务器并开始监听指定端口
    /// </summary>
    /// <param name="port">监听端口</param>
    public NetworkServer(ConnectionConfig config) : base()
    {
        _config = config;
        _ip = config.IP;
        _port = config.Port;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }


    protected virtual void ServerTick(){
        try
        {
            _ = DoTickAsync(() => {
                _ = RaiseEventAsync("OnServerTick", new NetworkEventArgs(
                    socket: _socket,
                    models: _clientsBySocket.Values.Cast<ModelObject>().ToList(),
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
            if (_clientsBySocket.Count >= _config.MaxClients)
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
            _socket.Close();
            _clientsBySocket.Clear();
            _clientsByID.Clear();
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

            _ = RaiseEventAsync("OnServerStarted", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.ServerStarted,
                message: $"服务器已启动，监听 {_ip}:{_port}"
            ));
            
            ServerTick(); // 启动服务器Tick
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

            var client = new Client(clientSocket);
            _clientsBySocket[clientSocket] = client;
            _clientsByID[client.Id] = client;

            await RaiseEventAsync("OnClientConnected", new NetworkEventArgs(
                socket: clientSocket,
                model: client,
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

                _ = RaiseEventAsync("OnDataReceived", new NetworkEventArgs(
                       socket: clientSocket,
                       model: _clientsBySocket[clientSocket],
                       eventType: NetworkEventType.DataReceived,
                       message: $"",
                       packet: packet
                   ));

                NetworkPacketParse(packet, clientSocket);

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

    private void NetworkPacketParse(NetworkPacket packet, Socket clientSocket)
    {
        if (packet.Type == (int)PacketType.Command)
        {
            _ = RaiseEventAsync("OnCommandReceived", new NetworkEventArgs(
                socket: clientSocket,
                model: _clientsBySocket[clientSocket],
                eventType: NetworkEventType.HandlerEvent,
                message: $"",
                packet: packet
            ));
        }
    }

    /// <summary>
    /// 向指定客户端发送数据包
    /// </summary>
    public virtual void Send(Socket clientSocket, NetworkPacket packet)
    {
        if (!_clientsBySocket.ContainsKey(clientSocket))
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                    socket: clientSocket,
                    model: _clientsBySocket[clientSocket],
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
                model: _clientsBySocket[clientSocket],
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

        foreach (Socket client in _clientsBySocket.Keys)
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
                    model: _clientsBySocket[client],
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
            if (_clientsBySocket.TryGetValue(clientSocket, out Client? client))
            {
                await RaiseEventAsync("OnClientDisconnected", new NetworkEventArgs(
                    socket: clientSocket!,
                    model: client,
                    eventType: NetworkEventType.ServerClientDisconnected,
                    message: $"Client {endpoint} disconnected"
                ));

                // 移除客户端记录
                _clientsByID.TryRemove(client.Id, out _);
                _clientsBySocket.TryRemove(clientSocket, out _);
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
    private void CloseSocketSafely(Socket socket)
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
    public virtual bool DisconnectClient(int clientID)
    {
        try
        {
            if (_clientsByID.TryGetValue(clientID, out Client client))
            {
                HandleDisconnect(client.GetSocket());
                return true;
            }

            return false;
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

    public ModelObject GetClientBySocket(Socket socket)
    {
        return _clientsBySocket[socket];
    }

    public ModelObject GetClientByID(int id)
    {
        return _clientsByID[id];
    }

    public List<ModelObject> GetClients()
    {
        return _clientsBySocket.Values.Cast<ModelObject>().ToList();
    }

    public bool HasClient(Socket socket)
    {
        return _clientsBySocket.ContainsKey(socket);
    }

    #endregion
}