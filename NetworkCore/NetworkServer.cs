using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

/// <summary>
/// 网络服务器类，用于处理TCP连接和消息传输
/// </summary>
public class NetworkServer
{
    #region 字段
    private readonly Socket _serverSocket;
    private readonly INetworkServerEvent _eventHandler;
    private readonly ClientManager _clientManager;
    private NetworkServerPacketHandler _ntwsPacketHanlder;
    private readonly UserManager _userManager;
    private bool _isRunning;
    private readonly int _port;
    #endregion

    #region 构造函数
    /// <summary>
    /// 初始化网络服务器实例
    /// </summary>
    /// <param name="port">服务器监听端口</param>
    /// <param name="eventHandler">网络事件处理器，如果为null则使用默认处理器</param>
    public NetworkServer(int port, INetworkServerEvent? eventHandler = null)
    {
        _port = port;
        _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _eventHandler = eventHandler ?? new DefaultNetworkServerEvent();
        _clientManager = new ClientManager();
        _ntwsPacketHanlder = new NetworkServerPacketHandler(_serverSocket, this);
        _userManager = new UserManager();  // 初始化用户管理器
        Win32API.AllocConsole();
    }
    #endregion

    #region 公共方法
    /// <summary>
    /// 启动网络服务器
    /// </summary>
    /// <remarks>
    /// 启动服务器将开始监听指定端口，并创建接受连接和心跳检测的后台线程
    /// </remarks>
    public void Start()
    {
        // 绑定端口并开始监听
        _serverSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
        _serverSocket.Listen(100);
        _isRunning = true;
        
        _eventHandler.OnServerStarted(_serverSocket);
        
        // 启动接收连接的线程
        Thread acceptThread = new Thread(AcceptConnections);
        acceptThread.Start();
        
        // 启动心跳检测线程
        Thread heartbeatThread = new Thread(CheckHeartbeats);
        heartbeatThread.Start();
    }

    /// <summary>
    /// 停止网络服务器
    /// </summary>
    /// <remarks>
    /// 停止服务器将断开所有客户端连接并释放相关资源
    /// </remarks>
    public void Stop()
    {
        // 停止服务器运行
        _isRunning = false;
        _clientManager.DisconnectAllClients();
        _serverSocket.Close();
        _eventHandler.OnServerStopped(_serverSocket);
    }


    // Getters
    public NetworkServerPacketHandler GetNetworkServerPacketHandler()
    {
        return _ntwsPacketHanlder;
    }
    public void SetNetworkServerPacketHandler(NetworkServerPacketHandler packetHandler)
    {
        _ntwsPacketHanlder = packetHandler;
    }
    public ClientManager GetClientManager()
    {
        return _clientManager;
    }

    // 添加用户管理器的访问器
    public UserManager GetUserManager()
    {
        return _userManager;
    }
    public Socket GetSocket()
    {
        return _serverSocket;
    }
    #endregion

    #region 私有连接方法
    /// <summary>
    /// 处理新的客户端连接
    /// </summary>
    private void AcceptConnections()
    {
        while (_isRunning)
        {
            try
            {
                // 接受新的客户端连接
                Socket clientSocket = _serverSocket.Accept();
                
                // 检查是否为浏览器连接
                if (IsBrowserConnection(clientSocket))
                {
                    clientSocket.Close();
                    continue;
                }

                // 添加客户端并启动接收消息的线程
                _clientManager.AddClient(clientSocket);
                _eventHandler.OnClientConnected(clientSocket);
                
                Thread receiveThread = new Thread(() => ReceiveMessages(clientSocket));
                receiveThread.Start();
            }
            catch
            {
                if (_isRunning)
                    throw;
            }
        }
    }

    /// <summary>
    /// 检查是否为浏览器发起的HTTP连接
    /// </summary>
    /// <param name="socket">待检查的客户端Socket</param>
    /// <returns>如果是浏览器连接返回true，否则返回false</returns>
    private bool IsBrowserConnection(Socket socket)
    {
        try
        {
            // 接收数据并检查HTTP请求头特征
            byte[] buffer = new byte[1024];
            int received = socket.Receive(buffer, SocketFlags.Peek);
            string data = System.Text.Encoding.ASCII.GetString(buffer, 0, received);
            
            return data.StartsWith("GET") || data.StartsWith("POST") || 
                   data.StartsWith("HEAD") || data.StartsWith("HTTP");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 接收并处理客户端消息
    /// </summary>
    /// <param name="clientSocket">客户端Socket连接</param>
    private void ReceiveMessages(Socket clientSocket)
    {
        byte[] buffer = new byte[4096];
        
        while (_isRunning && clientSocket.Connected)
        {
            try
            {
                // 接收数据并处理
                int received = clientSocket.Receive(buffer);
                if (received > 0)
                {
                    byte[] data = new byte[received];
                    Array.Copy(buffer, data, received);
                    
                    var packet = NetworkPacket.Deserialize(data);
                    HandlePacket(clientSocket, packet);
                    
                    // 如果包不是心跳包则触发消息事件
                    if (packet.Type != PacketType.Heartbeat)
                        _eventHandler.OnClientMessage(clientSocket, data);
                }
            }
            catch
            {
                HandleClientDisconnection(clientSocket);
                break;
            }
        }
    }
    #endregion

    #region 私有数据包处理
    /// <summary>
    /// 处理接收到的网络数据包
    /// </summary>
    /// <param name="clientSocket">客户端Socket连接</param>
    /// <param name="packet">接收到的数据包</param>
    private async void HandlePacket(Socket clientSocket, NetworkPacket packet)
    {
        await _ntwsPacketHanlder.HandlePacketAsync(clientSocket, _serverSocket, packet);   // 这里是外部处理
        switch (packet.Type)                                                    // 这里是内部处理
        {
            case PacketType.Heartbeat:
                // 更新客户端心跳时间
                _clientManager.UpdateClientLastHeartbeat(clientSocket);
                break;
            case PacketType.Message: 
                // 处理消息包
                break;
            case PacketType.Unknown:
                // 未知包
                break;
            case PacketType.Request:
                // 请求包
                break;
            case PacketType.Response:
                // 响应包
                break;
            case PacketType.Command:
                // 命令包
                break;
            case PacketType.Data:
                // 数据包
                break;
            case PacketType.Error:
                // 错误包
                break;
            // 处理其他类型的包...
            default:
                break;
        }
    }

    /// <summary>
    /// 执行心跳检测
    /// </summary>
    private void CheckHeartbeats()      // 心跳检测, 超时断开
    {
        while (_isRunning)
        {
            // 检查并移除不活跃的客户端
            _clientManager.CheckAndRemoveInactiveClients((clientSocket) =>
            {
                HandleClientDisconnection(clientSocket);
            });
            Thread.Sleep(1000); // 每秒检查一次
        }
    }
    #endregion

    #region 私有客户端管理
    /// <summary>
    /// 处理客户端断开连接
    /// </summary>
    /// <param name="clientSocket">断开连接的客户端Socket</param>
    private void HandleClientDisconnection(Socket clientSocket)
    {
        // 移除客户端并关闭连接
        _clientManager.RemoveClient(clientSocket);
        _eventHandler.OnClientDisconnected(clientSocket);
        try
        {
            clientSocket.Close();
        }
        catch { }
    }
    #endregion
}