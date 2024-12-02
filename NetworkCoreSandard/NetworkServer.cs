using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Events;
using static NetworkCoreStandard.Events.NetworkEventDelegate;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard;

/// <summary>
/// 网络服务器类，处理TCP连接和事件分发
/// </summary>
public class NetworkServer : NetworkObject
{
    protected Socket _socket;
    protected int _port;
    protected string _ip;
    protected HashSet<Socket> _clients = new HashSet<Socket>();

    /// <summary>
    /// 初始化服务器并开始监听指定端口
    /// </summary>
    /// <param name="port">监听端口</param>
    public NetworkServer(int port)
    {
        AssemblyLoader.LoadEmbeddedAssemblies();
        _ip = "0.0.0.0";
        _port = port;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

    /// <summary>
    /// 初始化服务器并开始监听指定IP和端口
    /// </summary>
    /// <param name="ip">监听IP地址</param>
    /// <param name="port">监听端口</param>
    public NetworkServer(string ip, int port)
    {
        _ip = ip;
        _port = port;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
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

            _ = RaiseEventAsync("OnServerStarted", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.ServerStarted,
                message: $"服务器已启动，监听 {_ip}:{_port}"
            ));
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
    protected virtual void AcceptCallback(IAsyncResult ar)
    {
        try
        {
            Socket clientSocket = _socket.EndAccept(ar);
            _clients.Add(clientSocket);

            _ = RaiseEventAsync("OnClientConnected", new NetworkEventArgs(
                    socket: clientSocket,
                    eventType: NetworkEventType.ServerClientConnected
                ));

            BeginReceive(clientSocket);
            _socket.BeginAccept(AcceptCallback, null);
        }
        catch (Exception ex)
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"处理客户端连接时发生错误: {ex.Message}"
                ));
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
                       eventType: NetworkEventType.DataReceived,
                       message: $"",
                       packet: packet
                   ));
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

        foreach (var client in _clients)
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
        foreach (var client in deadClients)
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
    protected virtual void HandleDisconnect(Socket clientSocket)
    {
        if (_clients.Remove(clientSocket))
        {
            _ = RaiseEventAsync("OnClientDisconnected", new NetworkEventArgs(
                socket: clientSocket,
                eventType: NetworkEventType.ServerClientDisconnected
            ));
            clientSocket.Close();
        }
    }

    /// <summary>
    /// 强制断开指定IP的客户端连接
    /// </summary>
    /// <param name="clientIP">客户端IP地址</param>
    /// <returns>是否成功断开连接</returns>
    public virtual bool DisconnectClient(string clientIP)
    {
        try
        {
            // 查找匹配IP的客户端Socket
            var clientSocket = _clients.FirstOrDefault(socket =>
                ((IPEndPoint)socket.RemoteEndPoint).Address.ToString() == clientIP);

            if (clientSocket != null)
            {
                // 使用已有的断开连接处理方法
                HandleDisconnect(clientSocket);
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
}