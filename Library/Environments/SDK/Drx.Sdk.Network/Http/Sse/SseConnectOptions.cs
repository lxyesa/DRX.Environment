using System;
using System.Collections.Generic;
using System.Threading;

namespace Drx.Sdk.Network.Http.Sse
{
    /// <summary>
    /// SSE 连接配置选项
    /// </summary>
    public class SseConnectOptions
    {
        /// <summary>
        /// 自定义请求头
        /// </summary>
        public Dictionary<string, string>? Headers { get; set; }

        /// <summary>
        /// 自动重连策略，null 表示不自动重连
        /// </summary>
        public SseRetryPolicy? RetryPolicy { get; set; }

        /// <summary>
        /// 取消令牌
        /// </summary>
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        /// <summary>
        /// 初始 Last-Event-ID（用于断线续传）
        /// </summary>
        public string? LastEventId { get; set; }
    }
}
