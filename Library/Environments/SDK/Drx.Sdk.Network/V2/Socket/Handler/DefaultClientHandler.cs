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

    public virtual bool OnClientRawSendAsync(byte[] rawData, out byte[]? modifiedData)
    {
        modifiedData = rawData;
        return true;
    }
}
