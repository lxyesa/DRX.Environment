using System;
using System.Net;
using Drx.Sdk.Network.V2.Web;

class Program
{
    static async Task Main()
    {
        var server = new DrxHttpServer(new[] { "http://localhost:8080/" });

        // 添加全局中间件
        server.AddMiddleware(async ctx =>
        {
            Console.WriteLine($"全局中间件: {ctx.Request.Url}");
            // 添加自定义头
            ctx.Response.AddHeader("X-Middleware", "global");
        });

        // 添加路由中间件
        server.AddMiddleware(async ctx =>
        {
            Console.WriteLine($"路由中间件: {ctx.Request.Url}");
            ctx.Response.AddHeader("X-Route-Middleware", "api");
        }, "/api");

        // 添加路由
        server.AddRoute(HttpMethod.Get, "/api/test", (req) => new HttpResponse(200, "Hello from API"));
        server.AddRoute(HttpMethod.Get, "/test", (req) => new HttpResponse(200, "Hello from root"));

        await server.StartAsync();

        Console.WriteLine("服务器启动，按任意键停止...");
        Console.ReadKey();

        server.Stop();
    }
}