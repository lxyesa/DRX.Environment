using System;
using System.Threading;

namespace Drx.Sdk.Network.Http.Sse
{
    /// <summary>
    /// 描述一个活跃的 SSE 客户端连接，用于服务端追踪和广播
    /// </summary>
    public class SseClientInfo
    {
        /// <summary>
        /// 客户端唯一标识（与 ISseWriter.ClientId 一致）
        /// </summary>
        public string ClientId { get; init; } = "";

        /// <summary>
        /// 连接的 SSE 端点路径
        /// </summary>
        public string Path { get; init; } = "";

        /// <summary>
        /// 该客户端对应的 SSE 写入器
        /// </summary>
        public ISseWriter Writer { get; init; } = null!;

        /// <summary>
        /// 客户端远程地址
        /// </summary>
        public string? RemoteAddress { get; init; }

        /// <summary>
        /// 连接建立时间
        /// </summary>
        public DateTime ConnectedAt { get; init; } = DateTime.Now;

        /// <summary>
        /// 用于取消该客户端连接的 Token（内部使用）
        /// </summary>
        internal CancellationTokenSource Cts { get; init; } = new();
    }
}
