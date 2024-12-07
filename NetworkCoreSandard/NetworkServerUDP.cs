using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using NetworkCoreStandard;
using NetworkCoreStandard.Config;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;

public class NetworkServerUDP : NetworkObject
{
    protected Socket _socket;
    protected int _port;
    protected string _ip;
    protected ServerConfig _config;
    private readonly ConcurrentDictionary<EndPoint, DateTime> _clientEndpoints;
    private readonly ConcurrentQueue<(NetworkPacket packet, EndPoint endpoint)> _messageQueue;
    private readonly CancellationTokenSource _processingCts;
    private readonly List<Task> _processingTasks;
    private int _processorCount = Environment.ProcessorCount;
    private bool _isRunning;

    public NetworkServerUDP(ServerConfig config) : base()
    {
        _config = config;
        _ip = config.IP;
        _port = config.Port;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _clientEndpoints = new ConcurrentDictionary<EndPoint, DateTime>();
        _messageQueue = new ConcurrentQueue<(NetworkPacket, EndPoint)>();
        _processingCts = new CancellationTokenSource();
        _processingTasks = new List<Task>();
    }

    /// <summary>
    /// 启动UDP服务器
    /// </summary>
    public virtual void Start()
    {
        try
        {
            _socket.Bind(new IPEndPoint(IPAddress.Parse(_ip), _port));
            _isRunning = true;
            
            // 启动消息处理线程
            StartMessageProcessors();
            
            // 开始接收数据
            BeginReceive();

            _ = RaiseEventAsync("OnUDPServerStarted", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.ServerStarted,
                message: $"UDP服务器已启动，监听 {_ip}:{_port}"
            ));

            Logger.Log("Server", _config.OnServerStartedTip);
        }
        catch (Exception ex)
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"启动UDP服务器时发生错误: {ex.Message}"
            ));
        }
    }

    /// <summary>
    /// 开始接收数据
    /// </summary>
    protected virtual void BeginReceive()
    {
        if (!_isRunning) return;

        try
        {
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] buffer = new byte[8192];
            
            _socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, 
                ref remoteEP, ar => HandleDataReceived(ar, buffer, remoteEP), null);
        }
        catch (Exception ex)
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"开始接收数据时发生错误: {ex.Message}"
            ));
        }
    }

    /// <summary>
    /// 处理接收到的数据
    /// </summary>
    /// <param name="ar"></param>
    /// <param name="buffer"></param>
    /// <param name="remoteEP"></param>
    protected virtual void HandleDataReceived(IAsyncResult ar, byte[] buffer, EndPoint remoteEP)
    {
        try
        {
            int bytesRead = _socket.EndReceiveFrom(ar, ref remoteEP);
            if (bytesRead > 0)
            {
                NetworkPacket packet = NetworkPacket.Deserialize(buffer.Take(bytesRead).ToArray());
                _clientEndpoints.AddOrUpdate(remoteEP, DateTime.Now, (_, __) => DateTime.Now);
                _messageQueue.Enqueue((packet, remoteEP));
            }
        }
        catch (Exception ex)
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"处理接收数据时发生错误: {ex.Message}"
            ));
        }
        finally
        {
            BeginReceive(); // 继续接收下一个数据包
        }
    }

    /// <summary>
    /// 启动消息处理线程
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
                        await ProcessMessageAsync(message.packet, message.endpoint);
                    }
                    else
                    {
                        await Task.Delay(1, _processingCts.Token);
                    }
                }
            }, _processingCts.Token));
        }
    }

    /// <summary>
    /// 处理接收到的数据
    /// </summary>
    /// <param name="packet"></param>
    /// <param name="endpoint"></param>
    /// <returns></returns>
    protected virtual async Task ProcessMessageAsync(NetworkPacket packet, EndPoint endpoint)
    {
        await RaiseEventAsync("OnDataReceived", new NetworkEventArgs(
            socket: _socket,
            eventType: NetworkEventType.DataReceived,
            message: $"从 {endpoint} 接收到数据",
            packet: packet
        ).AddElement("endpoint", endpoint)
        .AddElement("clientEndpoints", _clientEndpoints));
    }

    /// <summary>
    /// 发送数据
    /// </summary>
    /// <param name="endpoint"></param>
    /// <param name="packet"></param>
    public virtual void Send(EndPoint endpoint, NetworkPacket packet)
    {
        try
        {
            byte[] data = packet.Serialize();
            _socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, endpoint,
                ar =>
                {
                    try
                    {
                        _socket.EndSendTo(ar);
                    }
                    catch (Exception ex)
                    {
                        _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                            socket: _socket,
                            eventType: NetworkEventType.HandlerEvent,
                            message: $"发送数据时发生错误: {ex.Message}"
                        ));
                    }
                }, null);
        }
        catch (Exception ex)
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"准备发送数据时发生错误: {ex.Message}"
            ));
        }
    }

    /// <summary>
    /// 广播数据
    /// </summary>
    /// <param name="packet"></param>
    /// <param name="port"></param>
    public virtual void Broadcast(NetworkPacket packet, int port)
    {
        try
        {
            byte[] data = packet.Serialize();
            var endpoint = new IPEndPoint(IPAddress.Broadcast, port);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
            
            _socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, endpoint,
                ar =>
                {
                    try
                    {
                        _socket.EndSendTo(ar);
                        _ = RaiseEventAsync("OnBroadcast", new NetworkEventArgs(
                            socket: _socket,
                            eventType: NetworkEventType.HandlerEvent,
                            message: "广播数据包已发送",
                            packet: packet
                        ));
                    }
                    catch (Exception ex)
                    {
                        _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                            socket: _socket,
                            eventType: NetworkEventType.HandlerEvent,
                            message: $"广播数据时发生错误: {ex.Message}"
                        ));
                    }
                }, null);
        }
        catch (Exception ex)
        {
            _ = RaiseEventAsync("OnError", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"准备广播数据时发生错误: {ex.Message}"
            ));
        }
    }

    /// <summary>
    /// 停止UDP服务器
    /// </summary>
    public virtual void Stop()
    {
        if (!_isRunning) return;

        try
        {
            _isRunning = false;
            _processingCts.Cancel();
            Task.WaitAll(_processingTasks.ToArray(), TimeSpan.FromSeconds(5));
            _socket.Close();
            _clientEndpoints.Clear();
            
            _ = RaiseEventAsync("OnServerStopped", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.HandlerEvent,
                message: "UDP服务器已停止"
            ));
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "Server", $"停止UDP服务器时发生错误: {ex.Message}");
        }
    }
}