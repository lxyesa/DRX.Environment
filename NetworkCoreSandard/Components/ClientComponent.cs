using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using NetworkCoreStandard.Interface;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard.Components;

public class ClientComponent : IComponent
{
    public int Id { get; set; }
    public string? IP { get; private set; }
    public int Port { get; private set; }
    public int PermissionLevel { get; set; } = 1;
    public object? Owner { get; set; }

    public Socket? GetSocket()
    {
        return Owner as Socket;
    }

    public void SetPermissionLevel(int level)
    {
        PermissionLevel = level;
    }

    public void Start()
    {
        var socket = Owner as Socket;
        if(socket?.RemoteEndPoint is IPEndPoint endpoint)
        {
            IP = endpoint.Address.ToString();
            Port = endpoint.Port;
            Id = GetHashCode();
        }
    }

    public void Awake()
    {

    }
}
