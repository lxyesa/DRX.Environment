using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Socket.Services
{
    /// <summary>
    /// Defines the contract for a long-running service that hooks into the socket server's lifecycle and events.
    /// </summary>
    public interface ISocketService
    {
        /// <summary>
        /// Asynchronously executes the main logic of the service. This method is called once when the socket server starts.
        /// The service's primary background tasks should be run here.
        /// </summary>
        Task ExecuteAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Triggered asynchronously when a client successfully connects and passes connection middleware.
        /// </summary>
        Task OnClientConnectAsync(SocketServerService server, DrxTcpClient client, CancellationToken cancellationToken);

        /// <summary>
        /// Triggered asynchronously when a client disconnects for any reason.
        /// </summary>
        Task OnClientDisconnectAsync(SocketServerService server, DrxTcpClient client);

        /// <summary>
        /// Triggered asynchronously when the server receives a message from a client, after decryption/integrity checks.
        /// </summary>
        Task OnServerReceiveAsync(SocketServerService server, DrxTcpClient client, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

        /// <summary>
        /// Triggered asynchronously when the server is about to send a message to a client, before encryption/signing.
        /// </summary>
        Task OnServerSendAsync(SocketServerService server, DrxTcpClient client, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

        /// <summary>
        /// 异步触发：当 UDP 数据到达时调用，允许服务返回要发送回远端的字节（返回 null 表示不回复）。
        /// </summary>
        Task<byte[]?> OnUdpReceiveAsync(SocketServerService server, System.Net.IPEndPoint remote, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

        /// <summary>
        /// Synchronously executes the main logic of the service. Called once at server startup.
        /// Avoid long-blocking operations here; prefer the async version for background tasks.
        /// </summary>
        void Execute();

        /// <summary>
        /// Triggered synchronously when a client connects.
        /// </summary>
        void OnClientConnect(SocketServerService server, DrxTcpClient client);

        /// <summary>
        /// Triggered synchronously when a client disconnects.
        /// </summary>
        void OnClientDisconnect(SocketServerService server, DrxTcpClient client);

        /// <summary>
        /// Triggered synchronously when the server receives a message.
        /// </summary>
        void OnServerReceive(SocketServerService server, DrxTcpClient client, ReadOnlyMemory<byte> data);

        /// <summary>
        /// Triggered synchronously when the server is about to send a message.
        /// </summary>
        void OnServerSend(SocketServerService server, DrxTcpClient client, ReadOnlyMemory<byte> data);

        /// <summary>
        /// 同步触发：当 UDP 数据到达时调用，同步返回响应字节或 null 表示不回复。
        /// </summary>
        byte[]? OnUdpReceive(SocketServerService server, System.Net.IPEndPoint remote, ReadOnlyMemory<byte> data);
    }
}