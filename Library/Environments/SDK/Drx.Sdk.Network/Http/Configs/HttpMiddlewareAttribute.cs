using System;

namespace Drx.Sdk.Network.Http.Configs
{
    /// <summary>
    /// HTTP中间件特性
    /// 支持通过属性标注该方法为中间件处理。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HttpMiddlewareAttribute : Attribute
    {
        /// <summary>
        /// 请求路径前缀，为 null 或空表示全局中间件
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// 优先级，-1 表示使用默认
        /// </summary>
        public int Priority { get; set; } = -1;

        /// <summary>
        /// 是否覆盖全局优先级
        /// </summary>
        public bool OverrideGlobal { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="path">路径前缀，可为 null</param>
        public HttpMiddlewareAttribute(string? path = null)
        {
            Path = path;
        }
    }
}
