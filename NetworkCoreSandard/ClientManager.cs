using System;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NetworkCoreStandard.Models;

namespace NetworkCoreStandard;

/// <summary>
/// 客户端连接管理器，负责管理所有已连接客户端的状态和生命周期
/// </summary>
public class ClientManager
{
    /// <summary>
    /// 客户端信息内部类，存储每个连接的详细信息
    /// </summary>
    private class ClientInfo
    {
        public Socket Socket { get; set; } = null!;
        public DateTime LastHeartbeat { get; set; }
        public UserInstance? User { get; set; }
    }

    private readonly Dictionary<Socket, ClientInfo> _clients = new();
    private readonly object _lock = new();
    private const int HEARTBEAT_TIMEOUT = 30; // 30秒没有心跳就断开

    /// <summary>
    /// 添加新的客户端连接
    /// </summary>
    /// <param name="clientSocket">客户端Socket连接</param>
    public void AddClient(Socket clientSocket)
    {
        lock (_lock)
        {
            _clients[clientSocket] = new ClientInfo 
            { 
                Socket = clientSocket, 
                LastHeartbeat = DateTime.Now,
                User = new UserInstance() // 默认创建一个游客用户实例
            };
        }
    }

    /// <summary>
    /// 获取指定客户端Socket对应的用户信息
    /// </summary>
    /// <param name="clientSocket">客户端Socket连接</param>
    /// <returns>用户实例，如不存在则返回null</returns>
    public UserInstance? GetUserForClient(Socket clientSocket)
    {
        lock (_lock)
        {
            return _clients.TryGetValue(clientSocket, out ClientInfo? info) ? info.User : null;
        }
    }

    public void UpdateClientLastHeartbeat(Socket clientSocket)
    {
        lock (_lock)
        {
            if (_clients.TryGetValue(clientSocket, out ClientInfo? info))
            {
                info.LastHeartbeat = DateTime.Now;
            }
        }
    }

    public void RemoveClient(Socket clientSocket)
    {
        lock (_lock)
        {
            _clients.Remove(clientSocket);
        }
    }

    /// <summary>
    /// 检查并移除不活跃的客户端连接
    /// </summary>
    /// <param name="onClientRemoved">客户端被移除时的回调函数</param>
    public void CheckAndRemoveInactiveClients(Action<Socket> onClientRemoved)
    {
        lock (_lock)
        {
            var now = DateTime.Now;
            var inactiveClients = _clients.Where(x => 
                (now - x.Value.LastHeartbeat).TotalSeconds > HEARTBEAT_TIMEOUT
            ).ToList();

            foreach (var client in inactiveClients)
            {
                _clients.Remove(client.Key);
                onClientRemoved(client.Key);
            }
        }
    }

    public void DisconnectAllClients()
    {
        lock (_lock)
        {
            foreach (var client in _clients.Keys)
            {
                try
                {
                    
                    client.Close();
                }
                catch { }
            }
            _clients.Clear();
        }
    }
}