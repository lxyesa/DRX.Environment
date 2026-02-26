using System;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Legacy.Socket.Middleware
{
    /// <summary>
    /// 客户端断开连接中间件，支持自定义断开处理逻辑
    /// </summary>
    public class ClientDisconnectedMiddleware
    {
        private readonly Action<Drx.Sdk.Network.Legacy.Socket.SocketServerService, Drx.Sdk.Network.Legacy.Socket.DrxTcpClient> _handler;

        public ClientDisconnectedMiddleware(Action<Drx.Sdk.Network.Legacy.Socket.SocketServerService, Drx.Sdk.Network.Legacy.Socket.DrxTcpClient> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// 触发断开处理逻辑
        /// </summary>
        public Task InvokeAsync(Drx.Sdk.Network.Legacy.Socket.SocketServerService server, Drx.Sdk.Network.Legacy.Socket.DrxTcpClient client)
        {
            _handler?.Invoke(server, client);
            return Task.CompletedTask;
        }
    }
}
