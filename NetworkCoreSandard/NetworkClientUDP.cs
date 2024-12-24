using System.Net;
using System.Net.Sockets;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;
using NetworkCoreStandard.Utils.Common;
using NetworkCoreStandard.Utils.Common.Models;
using NetworkCoreStandard.Utils.Extensions;

namespace NetworkCoreStandard;

public class NetworkClientUDP : DRXNetworkObject
{
    protected DRXSocket _socket;
    protected string _serverIP;
    protected int _serverPort;
    protected EndPoint _serverEndPoint;
    protected bool _isRunning;
    private readonly CancellationTokenSource _processingCts;

    public NetworkClientUDP(int serverPort) : base()
    {
        _serverIP = "127.0.0.1";  // 本地回环地址
        _serverPort = serverPort;
        _socket = new DRXSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _serverEndPoint = new IPEndPoint(IPAddress.Parse(_serverIP), _serverPort);
        _processingCts = new CancellationTokenSource();
    }

    public virtual void Start()
    {
        try
        {
            _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            _isRunning = true;

            // 开始接收数据
            BeginReceive();

            _ = PushEventAsync("OnUDPClientStarted", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.ClientConnected,
                message: "UDP客户端已启动"
            ));
        }
        catch (Exception ex)
        {
            _ = PushEventAsync("OnError", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"启动UDP客户端时发生错误: {ex.Message}"
            ));
        }
    }

    protected virtual void BeginReceive()
    {
        if (!_isRunning) return;

        try
        {
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] buffer = new byte[8192];

            _ = _socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None,
                ref remoteEP, ar => HandleDataReceived(ar, buffer, remoteEP), null);
        }
        catch (Exception ex)
        {
            _ = PushEventAsync("OnError", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"开始接收数据时发生错误: {ex.Message}"
            ));
        }
    }

    protected virtual void HandleDataReceived(IAsyncResult ar, byte[] buffer, EndPoint remoteEP)
    {
        try
        {
            int bytesRead = _socket.EndReceiveFrom(ar, ref remoteEP);
            if (bytesRead > 0)
            {
                NetworkPacket packet = NetworkPacket.Deserialize(buffer.Take(bytesRead).ToArray());
                
                _ = PushEventAsync("OnDataReceived", new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.DataReceived,
                    message: $"从 {remoteEP} 接收到数据",
                    packet: packet.GetBytes()
                ));
            }
        }
        catch (Exception ex)
        {
            _ = PushEventAsync("OnError", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"处理接收数据时发生错误: {ex.Message}"
            ));
        }
        finally
        {
            if (_isRunning)
            {
                BeginReceive(); // 继续接收下一个数据包
            }
        }
    }

    public virtual void Send(NetworkPacket packet)
    {
        try
        {
            byte[] data = packet.ToJson().GetBytes();
            _ = _socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, _serverEndPoint,
                ar =>
                {
                    try
                    {
                        _ = _socket.EndSendTo(ar);
                        _ = PushEventAsync("OnDataSent", new NetworkEventArgs(
                            socket: _socket,
                            eventType: NetworkEventType.HandlerEvent,
                            message: "数据包已发送",
                            packet: packet.GetBytes()
                        ));
                    }
                    catch (Exception ex)
                    {
                        _ = PushEventAsync("OnError", new NetworkEventArgs(
                            socket: _socket,
                            eventType: NetworkEventType.HandlerEvent,
                            message: $"发送数据时发生错误: {ex.Message}"
                        ));
                    }
                }, null);
        }
        catch (Exception ex)
        {
            _ = PushEventAsync("OnError", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"准备发送数据时发生错误: {ex.Message}"
            ));
        }
    }

    public virtual void Stop()
    {
        if (!_isRunning) return;

        try
        {
            _isRunning = false;
            _processingCts.Cancel();
            _socket.Close();
            
            _ = PushEventAsync("OnStopped", new NetworkEventArgs(
                socket: _socket,
                eventType: NetworkEventType.ClientDisconnected,
                message: "UDP客户端已停止"
            ));
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "Client", $"停止UDP客户端时发生错误: {ex.Message}");
        }
    }
}