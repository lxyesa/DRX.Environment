using System;
using System.Collections.Concurrent;
using System.IO.Pipes;

namespace NetworkStandard.Pip;

public class PipeConnectionPool
{
    private readonly ConcurrentDictionary<string, NamedPipeServerStream> _connections = new();
    private readonly int _maxConnections;
    
    public PipeConnectionPool(int maxConnections)
    {
        _maxConnections = maxConnections;
    }

    public bool TryAdd(string clientId, NamedPipeServerStream connection)
    {
        if (_connections.Count >= _maxConnections)
            return false;
            
        return _connections.TryAdd(clientId, connection);
    }

    public bool TryRemove(string clientId)
    {
        return _connections.TryRemove(clientId, out _);
    }

    public bool TryGetConnection(string clientId, out NamedPipeServerStream? connection)
    {
        return _connections.TryGetValue(clientId, out connection);
    }

    public IEnumerable<NamedPipeServerStream> GetAllConnections()
    {
        return _connections.Values;
    }
}