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
    
    public virtual bool OnServerReceiveAsync(byte[] data, Client.DrxUdpClient client)
    {
        // 默认为不处理，子类可覆盖
        return OnServerReceiveAsync(data, (Client.DrxTcpClient?)null!);
    }

    public virtual byte[] OnServerSendAsync(byte[] data, Client.DrxTcpClient client)
    {
        return data;
    }

    public virtual byte[] OnServerSendAsync(byte[] data, Client.DrxUdpClient client)
    {
        return OnServerSendAsync(data, (Client.DrxTcpClient?)null!);
    }

    public virtual void OnServerConnected()
    {
    }

    public virtual void OnServerDisconnecting(Client.DrxTcpClient client)
    {
    }

    public virtual void OnServerDisconnecting(Client.DrxUdpClient client)
    {
        OnServerDisconnecting((Client.DrxTcpClient?)null!);
    }

    public virtual void OnServerDisconnected(Client.DrxTcpClient client)
    {
    }

    public virtual void OnServerDisconnected(Client.DrxUdpClient client)
    {
        OnServerDisconnected((Client.DrxTcpClient?)null!);
    }

    public virtual bool OnServerRawReceiveAsync(byte[] rawData, DrxTcpClient client, out byte[]? modifiedData)
    {
        modifiedData = rawData;
        return true;
    }

    public virtual bool OnServerRawReceiveAsync(byte[] rawData, Client.DrxUdpClient client, out byte[]? modifiedData)
    {
        return OnServerRawReceiveAsync(rawData, (DrxTcpClient?)null!, out modifiedData);
    }

    public virtual bool OnServerRawSendAsync(byte[] rawData, DrxTcpClient client, out byte[]? modifiedData)
    {
        modifiedData = rawData;
        return true;
    }

    public virtual bool OnServerRawSendAsync(byte[] rawData, Client.DrxUdpClient client, out byte[]? modifiedData)
    {
        return OnServerRawSendAsync(rawData, (DrxTcpClient?)null!, out modifiedData);
    }

    public virtual int GetPriority()
    {
        return Priority;
    }
}
