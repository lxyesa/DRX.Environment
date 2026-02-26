using System;
using Drx.Sdk.Network.Http.Protocol;

namespace Drx.Sdk.Network.Http.Configs
{
    /// <summary>
    /// 当路由级回调需要放弃自定义处理并让框架执行默认限流行为时，可调用此对象的 Default() 返回值（返回 null 表示使用默认行为）。
    /// 包含一些上下文信息，供回调参考或记录（只读）。
    /// </summary>
    public class OverrideContext
    {
        public string RouteKey { get; }
        public int MaxRequests { get; }
        public int WindowSeconds { get; }
        public DateTime TimestampUtc { get; }

        public OverrideContext(string routeKey, int maxRequests, int windowSeconds)
        {
            RouteKey = routeKey;
            MaxRequests = maxRequests;
            WindowSeconds = windowSeconds;
            TimestampUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// 回调可以返回此方法的返回值以告知框架使用默认行为（即返回 429）。
        /// 返回值类型为 HttpResponse?（null 表示默认行为）。
        /// </summary>
        public HttpResponse? Default() => null;
    }
}
