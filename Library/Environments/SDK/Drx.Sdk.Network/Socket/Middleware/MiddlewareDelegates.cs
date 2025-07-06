using System.Threading.Tasks;

namespace Drx.Sdk.Network.Socket.Middleware
{
    /// <summary>
    /// Represents a middleware component that is executed when a new client connects.
    /// </summary>
    /// <param name="context">The <see cref="ConnectionContext"/> for the connection.</param>
    /// <returns>A task that represents the asynchronous middleware operation.</returns>
    public delegate Task ConnectionMiddleware(ConnectionContext context);

    /// <summary>
    /// Represents a middleware component that is executed for each message received.
    /// </summary>
    /// <param name="context">The <see cref="MessageContext"/> for the message.</param>
    /// <returns>A task that represents the asynchronous middleware operation.</returns>
    public delegate Task MessageMiddleware(MessageContext context);
} 