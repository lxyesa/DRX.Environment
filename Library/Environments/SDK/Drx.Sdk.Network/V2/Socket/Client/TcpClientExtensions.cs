using System.Net.Sockets;

namespace Drx.Sdk.Network.V2.Socket.Client
{
    /// <summary>
    /// TcpClient 扩展方法，提供从基类转换为 DrxTcpClient 的便捷方法。
    /// 注：该方法不会复制除底层 Socket 之外的状态，仅用于将现有连接包装为 DrxTcpClient。
    /// </summary>
    public static class TcpClientExtensions
    {
        /// <summary>
        /// 将 `TcpClient` 包装为 `DrxTcpClient`。如果已经是 `DrxTcpClient` 则直接返回原对象。
        /// </summary>
        public static DrxTcpClient ToDrxClient(this TcpClient client)
        {
            if (client == null) throw new System.ArgumentNullException(nameof(client));
            if (client is DrxTcpClient d) return d;
            return new DrxTcpClient(client.Client);
        }
    }
}
