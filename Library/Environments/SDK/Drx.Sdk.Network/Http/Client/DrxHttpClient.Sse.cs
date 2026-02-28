using System;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http.Sse;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpClient SSE 部分：提供客户端 SSE 流连接、类型化事件订阅和自动重连功能
    /// </summary>
    public partial class DrxHttpClient
    {
        /// <summary>
        /// 连接到 SSE 端点并返回 SseStream 实例。
        /// SseStream 支持事件订阅、类型化反序列化和自动重连。
        /// </summary>
        /// <param name="url">SSE 端点地址（可以是绝对 URL 或相对路径）</param>
        /// <param name="options">连接选项（自定义头、重连策略等），null 使用默认配置</param>
        /// <returns>已启动的 SseStream 实例</returns>
        /// <example>
        /// <code>
        /// // 基本用法
        /// await using var sse = await client.ConnectSseAsync("/api/logs/stream");
        /// sse.OnMessage += (s, e) => Console.WriteLine(e.Data);
        ///
        /// // 类型化订阅 + 自动重连
        /// await using var sse = await client.ConnectSseAsync("/api/events", new SseConnectOptions
        /// {
        ///     Headers = new() { ["Authorization"] = "Bearer token" },
        ///     RetryPolicy = SseRetryPolicy.ExponentialBackoff()
        /// });
        /// sse.OnEvent&lt;MyEvent&gt;("update", e => Console.WriteLine(e.Name));
        /// </code>
        /// </example>
        public async Task<SseStream> ConnectSseAsync(string url, SseConnectOptions? options = null)
        {
            var stream = new SseStream(_httpClient, url, options);
            await stream.StartAsync().ConfigureAwait(false);
            return stream;
        }

        /// <summary>
        /// 连接到 SSE 端点，使用查询参数传递认证令牌（适用于 EventSource 不支持自定义头的场景）
        /// </summary>
        /// <param name="url">SSE 端点地址</param>
        /// <param name="token">认证令牌，将作为 ?token=xxx 附加到 URL</param>
        /// <param name="options">其他连接选项</param>
        public async Task<SseStream> ConnectSseWithTokenAsync(string url, string token, SseConnectOptions? options = null)
        {
            var separator = url.Contains('?') ? "&" : "?";
            var tokenUrl = $"{url}{separator}token={Uri.EscapeDataString(token)}";
            return await ConnectSseAsync(tokenUrl, options).ConfigureAwait(false);
        }
    }
}
