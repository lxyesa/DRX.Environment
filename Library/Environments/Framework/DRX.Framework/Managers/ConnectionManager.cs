using DRX.Framework.Common;
using System.Collections.Concurrent;

namespace DRX.Framework.Managers;

public class ConnectionManager
{
    private readonly ConcurrentDictionary<DRXSocket, ConnectionInfo> _connections = new();
    
    public bool TryAddConnection(DRXSocket socket)
    {
        return _connections.TryAdd(socket, new ConnectionInfo(socket));
    }
    
    public bool TryRemoveConnection(DRXSocket socket)
    {
        return _connections.TryRemove(socket, out _);
    }
    
    public int ConnectionCount => _connections.Count;
    
    public IEnumerable<DRXSocket> GetAllSockets() => _connections.Keys;
    
    public bool IsConnected(DRXSocket socket) => _connections.ContainsKey(socket);
    
    public void Clear() => _connections.Clear();
}

public class ConnectionInfo
{
    public DRXSocket Socket { get; }
    public DateTime ConnectedTime { get; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    
    public ConnectionInfo(DRXSocket socket)
    {
        Socket = socket;
        ConnectedTime = DateTime.UtcNow;
    }
}
