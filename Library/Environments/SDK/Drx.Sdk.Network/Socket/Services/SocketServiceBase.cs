using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Socket.Services
{
    /// <summary>
    /// A base class for socket services, providing default empty implementations for all interface methods.
    /// Inherit from this class to only override the methods you need.
    /// </summary>
    public abstract class SocketServiceBase : ISocketService
    {
        public virtual Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public virtual Task OnClientConnectAsync(SocketServerService server, TcpClient client, CancellationToken cancellationToken) => Task.CompletedTask;
        public virtual Task OnClientDisconnectAsync(SocketServerService server, TcpClient client) => Task.CompletedTask;
        public virtual Task OnServerReceiveAsync(SocketServerService server, TcpClient client, ReadOnlyMemory<byte> data, CancellationToken cancellationToken) => Task.CompletedTask;
        public virtual Task OnServerSendAsync(SocketServerService server, TcpClient client, ReadOnlyMemory<byte> data, CancellationToken cancellationToken) => Task.CompletedTask;
        
        public virtual void Execute() { }
        public virtual void OnClientConnect(SocketServerService server, TcpClient client) { }
        public virtual void OnClientDisconnect(SocketServerService server, TcpClient client) { }
        public virtual void OnServerReceive(SocketServerService server, TcpClient client, ReadOnlyMemory<byte> data) { }
        public virtual void OnServerSend(SocketServerService server, TcpClient client, ReadOnlyMemory<byte> data) { }
    }
} 