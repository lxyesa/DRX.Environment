using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Legacy.Socket.Services
{
    /// <summary>
    /// A base class for socket services, providing default empty implementations for all interface methods.
    /// Inherit from this class to only override the methods you need.
    /// </summary>
    public abstract class SocketServiceBase : ISocketService
    {
        public virtual Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public virtual Task OnClientConnectAsync(SocketServerService server, DrxTcpClient client, CancellationToken cancellationToken) => Task.CompletedTask;
        public virtual Task OnClientDisconnectAsync(SocketServerService server, DrxTcpClient client) => Task.CompletedTask;
        public virtual Task OnServerReceiveAsync(SocketServerService server, DrxTcpClient client, ReadOnlyMemory<byte> data, CancellationToken cancellationToken) => Task.CompletedTask;
        public virtual Task OnServerSendAsync(SocketServerService server, DrxTcpClient client, ReadOnlyMemory<byte> data, CancellationToken cancellationToken) => Task.CompletedTask;

        public virtual void Execute() { }
        public virtual void OnClientConnect(SocketServerService server, DrxTcpClient client) { }
        public virtual void OnClientDisconnect(SocketServerService server, DrxTcpClient client) { }
        public virtual void OnServerReceive(SocketServerService server, DrxTcpClient client, ReadOnlyMemory<byte> data) { }
        public virtual void OnServerSend(SocketServerService server, DrxTcpClient client, ReadOnlyMemory<byte> data) { }
        public virtual byte[]? OnUdpReceive(SocketServerService server, System.Net.IPEndPoint remote, ReadOnlyMemory<byte> data) => null;
        public virtual Task<byte[]?> OnUdpReceiveAsync(SocketServerService server, System.Net.IPEndPoint remote, ReadOnlyMemory<byte> data, CancellationToken cancellationToken) => Task.FromResult<byte[]?>(null);
    }
}