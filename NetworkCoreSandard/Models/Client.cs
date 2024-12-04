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
    
    public Client(Socket socket)
    {
        _socket = socket;
        var endpoint = (IPEndPoint)socket.RemoteEndPoint!;
        IP = endpoint.Address.ToString();
        Port = endpoint.Port;
    }
    

    public bool Contains(Socket socket)
    {
        return _socket == socket;
    }

    public Socket GetSocket()
    {
        return _socket;
    }

    ~Client()
    {
        if (_socket.Connected)
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }

        _socket.Dispose();
        Logger.Log("GC", $"{this.GetType().Name}对象被销毁");
    }
}
