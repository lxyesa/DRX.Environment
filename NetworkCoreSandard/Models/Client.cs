using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard.Models;

public class Client : ModelObject
{
    private Socket _socket = null!;
    public int Id { get; set; }
    public string IP { get; private set; }
    public int Port { get; private set; }
    public int PermissionLevel { get; set; } = 1;
    
    public Client(Socket socket)
    {
        _socket = socket;
        var endpoint = (IPEndPoint)socket.RemoteEndPoint!;
        IP = endpoint.Address.ToString();
        Port = endpoint.Port;
        Id = GetHashCode();
    }
    

    public bool Contains(Socket socket)
    {
        return _socket == socket;
    }

    public Socket GetSocket()
    {
        return _socket;
    }

    public void SetPermissionLevel(int level)
    {
        PermissionLevel = level;
    }

    ~Client()
    {
        
    }
}
