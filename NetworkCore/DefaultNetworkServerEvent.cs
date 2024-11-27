
using System.Net.Sockets;

public class DefaultNetworkServerEvent : INetworkServerEvent
{
    public virtual void OnServerStarted(Socket serverSocket)
    {
        Console.WriteLine($"{DateTime.Now:yyyy/mm/dd hh:mm:ss:ff} 服务器已启动");
    }
    public virtual void OnServerStopped(Socket serverSocket)
    {
        Console.WriteLine($"{DateTime.Now:yyyy/mm/dd hh:mm:ss:ff} 服务器已停止");
    }
    public virtual void OnServerTick(Socket serverSocket)
    {
        Console.WriteLine($"{DateTime.Now:yyyy/mm/dd hh:mm:ss:ff} 服务器正在运行...");
    }
    public virtual void OnClientMessage(Socket clientSocket, byte[] message)
    {
        //Console.WriteLine($"{DateTime.Now:yyyy/mm/dd hh:mm:ss:ff} 客户端 {clientSocket.RemoteEndPoint} 发送消息: {System.Text.Encoding.UTF8.GetString(message)}");
    }
    public virtual void OnClientDisconnected(Socket clientSocket)
    {
        try
        {
            Console.WriteLine($"{DateTime.Now:yyyy/mm/dd hh:mm:ss:ff} 客户端 {clientSocket.RemoteEndPoint} 断开连接");
        }
        catch
        {
            Console.WriteLine($"{DateTime.Now:yyyy/mm/dd hh:mm:ss:ff} 客户端断开连接");
        }
    }
    public virtual void OnClientConnected(Socket clientSocket)
    {
        Console.WriteLine($"{DateTime.Now:yyyy/mm/dd hh:mm:ss:ff} 客户端 {clientSocket.RemoteEndPoint} 连接成功");
    }
}