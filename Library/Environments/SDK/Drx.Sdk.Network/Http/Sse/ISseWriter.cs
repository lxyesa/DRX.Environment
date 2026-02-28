using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Http.Sse
{
    /// <summary>
    /// SSE 写入器接口，用于向已连接的客户端推送 Server-Sent Events。
    /// 每个 SSE 连接对应一个 ISseWriter 实例，由框架创建并注入到处理方法中。
    /// </summary>
    public interface ISseWriter
    {
        /// <summary>
        /// 当前客户端的唯一标识
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// 客户端重连时传入的 Last-Event-ID，首次连接为 null
        /// </summary>
        string? LastEventId { get; }

        /// <summary>
        /// 连接是否仍然活跃
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 当前已发送的事件 ID（自增序号）
        /// </summary>
        long CurrentEventId { get; }

        /// <summary>
        /// 发送一条 SSE 事件（自动附带递增 id）
        /// </summary>
        /// <param name="eventName">事件名称（对应 SSE 的 event: 字段），null 则为默认 "message"</param>
        /// <param name="data">事件数据（对应 SSE 的 data: 字段）</param>
        Task SendAsync(string? eventName, string data);

        /// <summary>
        /// 发送一条 SSE 事件，数据自动序列化为 JSON
        /// </summary>
        Task SendJsonAsync<T>(string? eventName, T data);

        /// <summary>
        /// 批量发送多条 SSE 事件（单次写入，减少 flush 次数）
        /// </summary>
        Task SendBatchAsync(IEnumerable<(string? EventName, string Data)> events);

        /// <summary>
        /// 发送心跳注释行（: heartbeat），防止连接超时
        /// </summary>
        Task PingAsync();

        /// <summary>
        /// 拒绝连接并关闭。仅在 SSE 头未发送前有效。
        /// 调用后 IsConnected 变为 false，后续 SendAsync 将抛异常。
        /// </summary>
        /// <param name="statusCode">HTTP 状态码</param>
        /// <param name="reason">可选的错误描述</param>
        Task RejectAsync(int statusCode, string? reason = null);

        /// <summary>
        /// 通知客户端建议的重连间隔（毫秒），对应 SSE 的 retry: 字段
        /// </summary>
        Task SetRetryAsync(int milliseconds);
    }
}
