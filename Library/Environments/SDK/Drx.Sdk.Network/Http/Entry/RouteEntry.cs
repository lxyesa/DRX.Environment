using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Configs;

namespace Drx.Sdk.Network.Http.Entry
{
    internal class RouteEntry
    {
        public string Template { get; set; }
        public HttpMethod Method { get; set; }
        public Func<HttpRequest, Task<HttpResponse>> Handler { get; set; }
        public Func<string, Dictionary<string, string>> ExtractParameters { get; set; }
        // 可选的路由级速率限制（默认为0表示无限制）
        public int RateLimitMaxRequests { get; set; }
        public int RateLimitWindowSeconds { get; set; }
        // 可选的路由级触发回调（若设置则在该路由触发限流时优先调用）
        // 签名: (int triggeredCount, HttpRequest req, OverrideContext ctx) -> Task<HttpResponse?>
        public Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? RateLimitCallback { get; set; }
    }
}