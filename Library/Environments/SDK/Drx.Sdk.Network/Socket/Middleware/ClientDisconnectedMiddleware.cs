using System;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Socket.Middleware
{
    /// <summary>
    /// 客户端断开连接中间件，支持自定义断开处理逻辑
    /// </summary>
    public class ClientDisconnectedMiddleware
    {
        private readonly Action<Drx.Sdk.Network.Socket.SocketServerService, Drx.Sdk.Network.Socket.DrxTcpClient> _handler;

        public ClientDisconnectedMiddleware(Action<Drx.Sdk.Network.Socket.SocketServerService, Drx.Sdk.Network.Socket.DrxTcpClient> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// 触发断开处理逻辑
        /// </summary>
        public Task InvokeAsync(Drx.Sdk.Network.Socket.SocketServerService server, Drx.Sdk.Network.Socket.DrxTcpClient client)
        {
            _handler?.Invoke(server, client);
            return Task.CompletedTask;
        }
    }
}
