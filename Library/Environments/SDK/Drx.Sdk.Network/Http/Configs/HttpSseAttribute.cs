using System;

namespace Drx.Sdk.Network.Http.Configs
{
    /// <summary>
    /// SSE（Server-Sent Events）处理方法特性。
    /// 标注此特性的方法会被注册为 SSE 端点，框架自动处理连接生命周期、心跳和客户端追踪。
    /// 方法签名支持以下参数（任意顺序，均可选除 ISseWriter 外）：
    ///   ISseWriter  —— 必须，用于向客户端推送事件
    ///   HttpRequest —— 可选，获取请求上下文（Query、Headers 等）
    ///   CancellationToken —— 可选，客户端断开时触发取消
    ///   DrxHttpServer —— 可选，访问服务器实例
    /// 返回类型应为 Task。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class HttpSseAttribute : Attribute
    {
        /// <summary>
        /// SSE 端点路径
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 自动心跳间隔（秒），0 表示禁用自动心跳。默认 15 秒。
        /// </summary>
        public int HeartbeatSeconds { get; set; } = 15;

        /// <summary>
        /// 可选：路由级速率限制 —— 时间窗口内最大请求数。0 表示不限流。
        /// </summary>
        public int RateLimitMaxRequests { get; set; }

        /// <summary>
        /// 可选：路由级速率限制 —— 时间窗口（秒）。
        /// </summary>
        public int RateLimitWindowSeconds { get; set; }

        public HttpSseAttribute(string path)
        {
            Path = path;
        }
    }
}
