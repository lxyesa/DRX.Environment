using System;
using Drx.Sdk.Network.V2.Socket.Client;

namespace Drx.Sdk.Network.V2.Socket.Handler;

public abstract class DefaultServerHandler : IServerHandler
{
    public int Priority => int.MaxValue;

    public int MaxPacketSize => 0;

    public virtual bool OnServerReceiveAsync(byte[] data, Client.DrxTcpClient client)
    {
        return false;
    }

    public virtual byte[] OnServerSendAsync(byte[] data, Client.DrxTcpClient client)
    {
        return data;
    }

    public virtual void OnServerConnected()
    {
    }

    public virtual void OnServerDisconnecting(Client.DrxTcpClient client)
    {
    }

    public virtual void OnServerDisconnected(Client.DrxTcpClient client)
    {
    }

    public virtual bool OnServerRawReceiveAsync(byte[] rawData, DrxTcpClient client, out byte[]? modifiedData)
    {
        modifiedData = rawData;
        return true;
    }

    public virtual bool OnServerRawSendAsync(byte[] rawData, DrxTcpClient client, out byte[]? modifiedData)
    {
        modifiedData = rawData;
        return true;
    }
}
