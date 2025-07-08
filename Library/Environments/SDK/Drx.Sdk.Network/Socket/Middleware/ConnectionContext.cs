using System.Net.Sockets;
using System.Threading;

namespace Drx.Sdk.Network.Socket.Middleware
{
    /// <summary>
    /// Represents the context for a new client connection, passed through the connection middleware pipeline.
    /// </summary>
    public class ConnectionContext
    {
        /// <summary>
        /// The instance of the socket server service.
        /// </summary>
        public SocketServerService Server { get; }
        
        /// <summary>
        /// The connected TCP client.
        /// </summary>
        public DrxTcpClient Client { get; }
        
        /// <summary>
        /// The cancellation token for the connection.
        /// </summary>
        public CancellationToken CancellationToken { get; }
        
        /// <summary>
        /// Gets or sets whether the connection is rejected.
        /// If set to true by any middleware, the connection will be terminated.
        /// </summary>
        public bool IsRejected { get; set; } = false;

        public ConnectionContext(SocketServerService server, DrxTcpClient client, CancellationToken cancellationToken)
        {
            Server = server;
            Client = client;
            CancellationToken = cancellationToken;
        }
    }
} 