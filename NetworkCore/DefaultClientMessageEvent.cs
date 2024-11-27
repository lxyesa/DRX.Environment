using System.Net.Sockets;

public class DefaultClientMessageEvent : IClientMessageEvent
{
    public virtual void OnConnected(TcpClient client)
    {
        Console.WriteLine($"已连接到服务器: {client.Client.RemoteEndPoint}");
    }

    public virtual void OnDisconnected(TcpClient client)
    {
        Console.WriteLine($"与服务器断开连接: {client.Client.RemoteEndPoint}");
    }

    public virtual void OnHeartbeatResponse(NetworkPacket packet)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 收到心跳响应");
    }

    public virtual void OnDataReceived(NetworkPacket packet)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 收到数据: {packet.Body}");
    }

    public virtual void OnErrorReceived(NetworkPacket packet)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 收到错误: {packet.Body}");
    }

    public virtual void OnMessageReceived(NetworkPacket packet)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 收到消息: {packet.Body}");
    }

    public virtual void OnResponseReceived(NetworkPacket packet)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 收到响应: {packet.Body}");
    }
}