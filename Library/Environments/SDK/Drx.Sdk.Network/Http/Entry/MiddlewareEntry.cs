using System;
using System.Net;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http.Protocol;

namespace Drx.Sdk.Network.Http.Entry
{
    internal class MiddlewareEntry
    {
        public Func<HttpListenerContext, Task> Handler { get; set; }
        public string? Path { get; set; } // null for global
        public int Priority { get; set; }
        public bool OverrideGlobal { get; set; }
        public int AddOrder { get; set; }
        // 可选：基于 HttpRequest 的中间件实现，签名为 (HttpRequest, Func<HttpRequest, Task<HttpResponse?>>) -> Task<HttpResponse?>
        public Func<HttpRequest, Func<HttpRequest, Task<HttpResponse?>>, Task<HttpResponse?>>? RequestMiddleware { get; set; }
    }
}