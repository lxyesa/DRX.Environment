using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NetworkCommonLibrary.EventHandlers;
using NetworkCoreStandard.Models;

namespace NetworkCommonLibrary;

public class NetworkUtils : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly NetworkClientEventHandler _eventHandler;
    private bool _isConnected;
    private Task? _receiveTask;
    private Task? _heartbeatTask;
    private int _heartbeatInterval = 30000; // 30秒发送一次心跳包
    private readonly CancellationTokenSource _cancellationTokenSource;

    public NetworkUtils(NetworkClientEventHandler eventHandler, int heartbeatInterval = 30000)
    {
        _eventHandler = eventHandler;
        _cancellationTokenSource = new CancellationTokenSource();
        _heartbeatInterval = heartbeatInterval;
    }

    public async Task ConnectAsync(string host, int port)
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
            _isConnected = true;

            _eventHandler.OnConnected(_client);
            
            // 启动接收消息的任务
            _receiveTask = StartReceiveLoop(_cancellationTokenSource.Token);
            // 启动心跳包任务
            _heartbeatTask = StartHeartbeatLoop(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            throw new Exception($"连接服务器失败: {ex.Message}");
        }
    }

    private async Task StartReceiveLoop(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested && _isConnected)
        {
            try
            {
                if (_stream == null) break;
                
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead == 0)
                {
                    _eventHandler.OnDisconnected(_client!, "服务器关闭了连接");
                    break;
                }

                string jsonData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var packet = JsonSerializer.Deserialize<NetworkPacket>(jsonData);
                
                if (packet != null)
                {
                    _eventHandler.OnPacketReceived(_client!, packet);
                }
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                _eventHandler.OnDisconnected(_client!, "客户端主动取消连接");
                break;
            }
            catch (Exception ex)
            {
                _eventHandler.OnDisconnected(_client!, $"连接异常：{ex.Message}");
                _isConnected = false;
                break;
            }
        }
    }

    private async Task StartHeartbeatLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isConnected)
        {
            try
            {
                var heartbeatPacket = new NetworkPacket
                {
                    Header = "Heartbeat",
                    Type = 3,
                    Body = DateTime.Now.ToString("o") // Use the round-trip date/time pattern
                };

                await SendPacketAsync(heartbeatPacket);
                _eventHandler.OnHeartbeatSent(_client!, heartbeatPacket);
                
                await Task.Delay(_heartbeatInterval, cancellationToken); // 30秒发送一次心跳包
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task SendPacketAsync(NetworkPacket packet)
    {
        if (!_isConnected || _stream == null)
            throw new InvalidOperationException("未连接到服务器");

        var json = JsonSerializer.Serialize(packet);
        var data = Encoding.UTF8.GetBytes(json);
        await _stream.WriteAsync(data, 0, data.Length);
        
        // 触发发送数据包事件
        _eventHandler.OnPacketSent(_client!, packet);
    }

    public void Disconnect()
    {
        if (_isConnected && _client != null)
        {
            _eventHandler.OnDisconnected(_client, "客户端主动断开连接");
        }
        
        _isConnected = false;
        _cancellationTokenSource.Cancel();
        
        _stream?.Close();
        _client?.Close();
        
        _stream = null;
        _client = null;
    }

    public void Dispose()
    {
        Disconnect();
        _cancellationTokenSource.Dispose();
    }
}
