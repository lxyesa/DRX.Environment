using System.Net.Sockets;
using System.Threading;

namespace Drx.Sdk.Network.Legacy.Socket.Middleware
{
    /// <summary>
    /// Represents the context for a received message, passed through the message middleware pipeline.
    /// </summary>
    public class MessageContext
    {
        /// <summary>
        /// The instance of the socket server service.
        /// </summary>
        public SocketServerService Server { get; }

        /// <summary>
        /// The TCP client that sent the message.
        /// </summary>
        public DrxTcpClient Client { get; }

        /// <summary>
        /// The raw message data received from the client. Can be modified by middleware.
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// The cancellation token for the connection.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Gets or sets whether the message has been handled.
        /// If set to true by a middleware, subsequent middleware and the final command handler will be skipped.
        /// </summary>
        public bool IsHandled { get; set; } = false;

        public MessageContext(SocketServerService server, DrxTcpClient client, byte[] data, CancellationToken cancellationToken)
        {
            Server = server;
            Client = client;
            Data = data;
            CancellationToken = cancellationToken;
        }
    }
} 