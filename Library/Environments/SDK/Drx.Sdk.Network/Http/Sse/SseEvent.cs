using System;

namespace Drx.Sdk.Network.Http.Sse
{
    /// <summary>
    /// 客户端接收到的 SSE 事件
    /// </summary>
    public class SseEvent
    {
        /// <summary>
        /// 事件 ID（来自 id: 字段）
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// 事件类型（来自 event: 字段），默认为 "message"
        /// </summary>
        public string EventName { get; set; } = "message";

        /// <summary>
        /// 事件数据（来自 data: 字段）
        /// </summary>
        public string Data { get; set; } = "";

        /// <summary>
        /// 服务端建议的重连间隔（毫秒），来自 retry: 字段，null 表示未指定
        /// </summary>
        public int? Retry { get; set; }
    }
}
