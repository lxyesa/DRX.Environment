namespace Drx.Sdk.Network.Legacy.Socket.Hosting
{
    /// <summary>
    /// 独立模式运行选项（不依赖 Microsoft.Extensions.*）
    /// </summary>
    public sealed class SocketHostOptions
    {
        /// <summary>监听端口，默认 8463</summary>
        public int Port { get; set; } = 8463;
        /// <summary>可选的 UDP 监听端口，默认 0 表示不启用 UDP</summary>
        public int UdpPort { get; set; } = 0;
    }
}