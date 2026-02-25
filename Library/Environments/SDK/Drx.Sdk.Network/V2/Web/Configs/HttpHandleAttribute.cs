using System;

namespace Drx.Sdk.Network.V2.Web.Configs
{
    /// <summary>
    /// HTTP处理方法特性
    /// 支持通过属性标注该方法为原始（Raw）处理，或用于流式上传/下载场景。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HttpHandleAttribute : Attribute
    {
        /// <summary>
        /// 请求路径
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// HTTP 方法字符串（例如 "GET"、"POST"）
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// 标记该处理器为原始处理（Raw），方法可以直接接收 HttpListenerContext进行流式处理
        /// </summary>
        public bool Raw { get; set; }

        /// <summary>
        /// 标记该处理器用于流式上传（服务器接收大文件流）
        /// 相当于 Raw 的语义扩展，用于可读性和过滤
        /// </summary>
        public bool StreamUpload { get; set; }

        /// <summary>
        /// 标记该处理器用于流式下载（服务器直接写入响应流）
        /// 相当于 Raw 的语义扩展
        /// </summary>
        public bool StreamDownload { get; set; }

        /// <summary>
        /// 可选：为该处理器设置路由级速率限制（最大请求数）。
        /// 默认 0 表示不启用路由级限流。
        /// 使用示例： [HttpHandle("/api/foo", "GET", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60)]
        /// </summary>
        public int RateLimitMaxRequests { get; set; }

        /// <summary>
        /// 可选：路由级速率限制的时间窗口，单位为秒。
        /// 与 RateLimitMaxRequests 一起使用表示在该时间窗内最多允许的请求数。
        /// </summary>
        public int RateLimitWindowSeconds { get; set; }

        /// <summary>
        /// 可选：指定用于路由级速率触发回调的方法名（字符串）。
        /// 若指定，`RegisterHandlersFromAssembly` 会尝试在声明此属性的类型或通过 RateLimitCallbackType 指定的类型中查找此静态方法并绑定为回调。
        /// </summary>
        public string? RateLimitCallbackMethodName { get; set; }

        /// <summary>
        /// 可选：指定回调方法所在的类型（当回调方法不在声明该路由的方法所在类型中时使用）。
        /// </summary>
        public Type? RateLimitCallbackType { get; set; }

        /// <summary>
        /// 新的构造重载，允许通过字符串直接指定回调方法名（在同一类型内查找）。
        /// 使用示例： [HttpHandle("/api/hello", "GET", "TestRateLimit")]（将在当前定义类型中查找静态方法 TestRateLimit）
        /// </summary>
        /// <param name="path"></param>
        /// <param name="method"></param>
        /// <param name="rateLimitCallbackMethodName"></param>
        public HttpHandleAttribute(string path, string method, string rateLimitCallbackMethodName)
        {
            Path = path;
            Method = method;
            RateLimitCallbackMethodName = rateLimitCallbackMethodName;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="path">请求路径</param>
        /// <param name="method">HTTP 方法字符串</param>
        public HttpHandleAttribute(string path, string method)
        {
            Path = path;
            Method = method;
        }
    }
}
