using System;
using System.Collections.Generic;
using Drx.Sdk.Network.V2.Socket.Client;
using Drx.Sdk.Network.V2.Socket.Server;
using Drx.Sdk.Shared.Serialization;

namespace Drx.Sdk.Network.V2.Socket.Handler;

/// <summary>
/// 命令处理器，负责处理特定的命令逻辑（服务器端）。
/// </summary>
public class CommandHandlerServer : DefaultServerHandler
{
    private readonly DrxTcpServer? _tcpServer;
    private readonly DrxUdpServer? _udpServer;
    private readonly Dictionary<string, Func<DrxTcpClient?, DrxUdpClient?, DrxSerializationData, Task>> _commandHandlers = new();

    public delegate Task<bool> CommandReceivedEventHandler(string command, byte[] data, DrxTcpClient? tcpClient, DrxUdpClient? udpClient);
    public event CommandReceivedEventHandler? CommandReceived;

    public CommandHandlerServer(DrxTcpServer server)
    {
        _tcpServer = server ?? throw new ArgumentNullException(nameof(server));
    }

    public CommandHandlerServer(DrxUdpServer server)
    {
        _udpServer = server ?? throw new ArgumentNullException(nameof(server));
    }

    public override int GetPriority()
    {
        return base.GetPriority();
    }

    public override bool OnServerReceiveAsync(byte[] data, DrxTcpClient client)
    {
        var deserialized = DrxSerializationData.Deserialize(data);
        deserialized.TryGetString("command", out var command);

        if (string.IsNullOrEmpty(command))
        {
            // 无命令，忽略
            return false;
        }

        if (_commandHandlers.TryGetValue(command, out var handler))
        {
            handler(client, null, deserialized).GetAwaiter().GetResult();
        }

        var result = CommandReceived?.Invoke(command, data!, client, null);
        return result?.Result ?? false;
    }

    /// <summary>
    /// 注册命令及其处理函数。
    /// </summary>
    /// <param name="command">命令名称</param>
    /// <param name="handler">处理函数，参数为TCP客户端、UDP客户端和序列化数据</param>
    public void RegisterCommand(string command, Func<DrxTcpClient?, DrxUdpClient?, DrxSerializationData, Task> handler)
    {
        if (string.IsNullOrEmpty(command))
        {
            throw new ArgumentException("命令名称不能为空", nameof(command));
        }
        _commandHandlers[command] = handler ?? throw new ArgumentNullException(nameof(handler));
    }
}
