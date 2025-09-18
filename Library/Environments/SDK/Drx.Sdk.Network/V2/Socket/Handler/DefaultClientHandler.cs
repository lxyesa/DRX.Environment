using System;

namespace Drx.Sdk.Network.V2.Socket.Handler;

public abstract class DefaultClientHandler : IClientHandler
{
    public virtual int Priority => int.MaxValue;

    public virtual int MaxPacketSize => 0;

    public virtual bool OnClientReceiveAsync(byte[] data)
    {
        return false;
    }

    public virtual bool OnClientReceiveAsync(byte[] data, Drx.Sdk.Network.V2.Socket.Client.DrxUdpClient client)
    {
        // 默认转发到无参数版本以保持兼容
        return OnClientReceiveAsync(data);
    }

    public virtual byte[] OnClientSendAsync(byte[] data)
    {
        return data;
    }

    public virtual void OnClientConnected()
    {
    }

    public virtual void OnClientDisconnected()
    {
    }

    public virtual void OnClientDisconnectedCompleted()
    {
    }

    public virtual bool OnClientRawReceiveAsync(byte[] rawData, out byte[]? modifiedData)
    {
        modifiedData = rawData;
        return true;
    }

    public virtual bool OnClientRawReceiveAsync(byte[] rawData, Drx.Sdk.Network.V2.Socket.Client.DrxUdpClient client, out byte[]? modifiedData)
    {
        return OnClientRawReceiveAsync(rawData, out modifiedData);
    }

    public virtual bool OnClientRawSendAsync(byte[] rawData, out byte[]? modifiedData)
    {
        modifiedData = rawData;
        return true;
    }

    public virtual bool OnClientRawSendAsync(byte[] rawData, Drx.Sdk.Network.V2.Socket.Client.DrxUdpClient client, out byte[]? modifiedData)
    {
        return OnClientRawSendAsync(rawData, out modifiedData);
    }

    public virtual int GetPriority()
    {
        return Priority;
    }
}
