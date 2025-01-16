using NetworkCoreStandard.Common;
using NetworkCoreStandard.Common.Args;
using NetworkCoreStandard.Common.Enums.Packet;
using NetworkCoreStandard.Common.Models;
using NetworkCoreStandard.Utils;
using System.Windows.Input;

namespace NetworkCoreStandard;

public class NDVClient : DRXClient
{
    // ------------------------------------------------------------- 私有事件
    public event EventHandler<NetworkEventArgs>? OnReceiveMessage; // 接收消息事件
    public event EventHandler<NetworkEventArgs>? OnReceiveCommandResponse; // 接收命令响应事件
    public event EventHandler<NetworkEventArgs>? OnReceiveResponse; // 接收响应事件

    /// <summary>
    /// 初始化网络客户端
    /// </summary>
    public NDVClient(string serverIP, int serverPort, string key) : base(serverIP, serverPort, key)
    {
        OnReceiveCallback += _client_OnReceiveCallback;
    }

    private void _client_OnReceiveCallback(object? sender, NetworkEventArgs e)
    {
        var packet = DRXPacket.Unpack(e.Packet, _key);
        var ph = packet?.Headers;

        switch (ph?[PacketHeaderKey.Type]?.ToString())
        {
            case PacketTypes.Response:
                OnReceiveResponse?.Invoke(sender, e);
                break;
            case PacketTypes.CommandResponse:
                OnReceiveCommandResponse?.Invoke(sender, e);
                break;
            case PacketTypes.Message:
                OnReceiveMessage?.Invoke(sender, e);
                break;
            default:
                break;
        }
    }
}
