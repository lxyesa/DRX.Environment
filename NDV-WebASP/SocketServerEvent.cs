using System;
using System.Net.Sockets;
using NetworkCoreSandard.Handler;

namespace NDV_WebASP;

public class SocketServerEvent : ServerEventHandler
{
    public override void OnClientConnected(Socket clientSocket)
    {
        base.OnClientConnected(clientSocket);
        
        Console.WriteLine($"客户端 {clientSocket.RemoteEndPoint} 连接成功");
    }
}
