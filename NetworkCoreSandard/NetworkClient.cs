using System.Net.Sockets;
using NetworkCoreSandard.Handler;
using NetworkCoreSandard.Interface;
using NetworkCoreStandard.Models;

/// <summary>
/// 网络客户端类，用于处理TCP连接和消息传输
/// </summary>
namespace NetworkCoreSandard;
public class NetworkClient : IDisposable
{
    #region 字段
    private readonly TcpClient _client; // TCP客户端实例
    private NetworkClientPacketHandler _packetHandler = null!;   // 消息处理器
    private readonly IClientMessageEvent _eventHandler;     // 客户端消息事件处理器
    private readonly CancellationTokenSource _cancellationTokenSource;  // 取消令牌
    private bool _isRunning;
    #endregion

    #region 构造函数
    /// <summary>
    /// 初始化网络客户端实例
    /// </summary>
    /// <param name="host">服务器主机地址</param>
    /// <param name="port">服务器端口</param>
    /// <param name="eventHandler">客户端消息事件处理器，如果为null则使用默认处理器</param>
    public NetworkClient(string host, int port, IClientMessageEvent? eventHandler = null)
    {
        _client = new TcpClient();
        _eventHandler = eventHandler ?? new ClientEventHandler();
        _cancellationTokenSource = new CancellationTokenSource();
    }
    #endregion

    #region 公共方法
    /// <summary>
    /// 异步连接到服务器
    /// </summary>
    /// <param name="host">服务器主机地址</param>
    /// <param name="port">服务器端口</param>
    public async Task ConnectAsync(string host, int port)
    {
        await _client.ConnectAsync(host, port);
        _packetHandler = new NetworkClientPacketHandler(_client.GetStream());
        _eventHandler.OnConnected(_client);
        StartMessageLoop();
        StartHeartbeatLoop();
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _isRunning = false;
        _cancellationTokenSource.Cancel();
        _client.Dispose();
    }

    // Getters
    public TcpClient GetClient() => _client;
    public NetworkClientPacketHandler GetPacketHandler() => _packetHandler;
    #endregion

    #region 私有方法
    /// <summary>
    /// 启动消息接收循环
    /// </summary>
    private void StartMessageLoop()
    {
        _isRunning = true;
        Task.Run(async () =>
        {
            try
            {
                while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var packet = await _packetHandler.ReceivePacketAsync();
                    if (packet != null)
                    {
                        HandlePacket(packet);
                    }
                    await Task.Delay(100);
                }
            }
            catch (Exception)
            {
                HandleDisconnection();
            }
        }, _cancellationTokenSource.Token);
    }

    /// <summary>
    /// 启动心跳包发送循环
    /// </summary>
    private void StartHeartbeatLoop()
    {
        Task.Run(async () =>
        {
            try
            {
                while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await _packetHandler.SendAsync(new NetworkPacket
                    {
                        Header = "Heartbeat",
                        Type = (int)PacketType.Heartbeat,
                        Body = DateTime.Now.ToString("o")
                    });
                    await Task.Delay(2000);
                }
            }
            catch (Exception)
            {
                HandleDisconnection();
            }
        }, _cancellationTokenSource.Token);
    }

    /// <summary>
    /// 处理接收到的数据包
    /// </summary>
    /// <param name="packet">接收到的数据包</param>
    private async void HandlePacket(NetworkPacket packet)
    {
        await _packetHandler.HandlePacketAsync(packet);
        switch (packet.Type)    // 根据数据包类型调用不同的事件处理方法。
        {
            case (int)PacketType.Response:
                _eventHandler.OnResponseReceived(packet);
                break;
            case (int)PacketType.Error:
                _eventHandler.OnErrorReceived(packet);
                break;
            case (int)PacketType.Data:
                _eventHandler.OnDataReceived(packet);
                break;
            case (int)PacketType.Message:
                _eventHandler.OnMessageReceived(packet);
                break;
            case (int)PacketType.Heartbeat:
                _eventHandler.OnHeartbeatResponse(packet);
                break;
        }
    }

    /// <summary>
    /// 处理客户端断开连接
    /// </summary>
    private void HandleDisconnection()
    {
        _isRunning = false;
        _eventHandler.OnDisconnected(_client);
    }
    #endregion
}