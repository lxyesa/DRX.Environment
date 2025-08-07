namespace Drx.Sdk.Network.Socket.Hosting
{
    /// <summary>
    /// 独立模式运行选项（不依赖 Microsoft.Extensions.*）
    /// </summary>
    public sealed class SocketHostOptions
    {
        /// <summary>监听端口，默认 8463</summary>
        public int Port { get; set; } = 8463;
    }
}