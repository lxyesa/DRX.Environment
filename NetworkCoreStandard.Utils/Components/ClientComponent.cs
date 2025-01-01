using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using NetworkCoreStandard.Utils;
using NetworkCoreStandard.Utils.Common;
using NetworkCoreStandard.Utils.Interface;

namespace NetworkCoreStandard.Components;

public class ClientComponent : IComponent
{
    public int Id { get; set; }
    public string? IP { get; private set; }
    public int Port { get; private set; }
    public int PermissionLevel { get; set; } = 1;
    public object? Owner { get; set; }
    public DateTime LastActiveTime { get; private set; } = DateTime.Now;

    public DRXSocket? GetSocket()
    {
        return Owner as DRXSocket;
    }

    public void SetPermissionLevel(int level)
    {
        PermissionLevel = level;
    }

    public void Start()
    {
        var socket = Owner as DRXSocket;
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

    public void OnDestroy()
    {
        
    }

    public void Dispose()
    {
        
    }

    public void UpdateLastActiveTime()
    {
        LastActiveTime = DateTime.Now;
    }

    public DateTime GetLastActiveTime()
    {
        return LastActiveTime;
    }
}
