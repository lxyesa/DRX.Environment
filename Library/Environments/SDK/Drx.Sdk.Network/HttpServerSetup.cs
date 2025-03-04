using Drx.Sdk.Network;
using Drx.Sdk.Network.Helpers;
using Drx.Sdk.Network.Interfaces;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Drx.Sdk
{
    public class HttpServerSetup
    {
        public async Task ConfigureAsync()
        {
            var server = new HttpServer("http://localhost:5000/");

            // 初始化并注册API已经在HttpServer构造函数中完成

            // 添加中间件示例
            server.AddComponent(new LoggingMiddleware());

            await server.StartAsync();
        }
    }

    /// <summary>
    /// 示例中间件，用于日志记录
    /// </summary>
    public class LoggingMiddleware : IMiddleware
    {
        public Task Invoke(HttpListenerContext context)
        {
            Console.WriteLine($"收到请求: {context.Request.HttpMethod} {context.Request.Url}");
            return Task.CompletedTask;
        }
    }
}
