using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http.Extensions;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Drx.Sdk.Network.V2.Web.Core;
using Drx.Sdk.Network.V2.Web.Http;

namespace Drx.Sdk.Network.V2.Web.Asp
{
    /// <summary>
    /// DrxHttpAspServer - 一个用于快速启动 ASP.NET Core HTTP 服务的轻量封装类。
    /// 说明：该类封装了 WebApplication 的启动/停止流程，并允许外部通过委托注册路由或中间件。
    /// </summary>
    public class DrxHttpAspServer : IDisposable
    {
        private IHost? _host;
        private readonly int _port;
        private readonly Action<IApplicationBuilder>? _configureApp;
        private readonly Func<Drx.Sdk.Network.V2.Web.Http.HttpRequest, Task<Drx.Sdk.Network.V2.Web.Http.HttpResponse>>? _requestHandler;
        private readonly ILoggerFactory? _loggerFactory;

        /// <summary>
        /// 创建一个新的 DrxHttpAspServer 实例。
        /// </summary>
        /// <param name="port">监听端口（例如 5000）。</param>
        /// <param name="configureApp">用于注册路由/中间件的委托，传入 IApplicationBuilder。</param>
        /// <param name="loggerFactory">可选的日志工厂，用于服务器内部日志。</param>
        /// <summary>
        /// 构造函数：可传入一个基于框架 HttpRequest/HttpResponse 的处理委托（优先），或传入 configureApp 进行自定义路由注册。
        /// </summary>
        /// <param name="port">监听端口。</param>
        /// <param name="configureApp">用于注册路由/中间件的委托（可选）。</param>
        /// <param name="requestHandler">框架层请求处理器，签名为 Func&lt;HttpRequest, Task&lt;HttpResponse&gt;&gt;（可选）。</param>
        /// <param name="loggerFactory">可选日志工厂。</param>
        public DrxHttpAspServer(int port = 5000, Action<IApplicationBuilder>? configureApp = null, Func<Drx.Sdk.Network.V2.Web.Http.HttpRequest, Task<Drx.Sdk.Network.V2.Web.Http.HttpResponse>>? requestHandler = null, ILoggerFactory? loggerFactory = null)
        {
            _port = port;
            _configureApp = configureApp;
            _requestHandler = requestHandler;
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// 异步启动服务器。
        /// </summary>
        /// <returns>启动完成的任务。</returns>
        /// <exception cref="InvalidOperationException">当服务器已启动时抛出。</exception>
        public async Task StartAsync()
        {
            if (_host != null)
                throw new InvalidOperationException("服务器已在运行。请先 StopAsync() 再重新 StartAsync().");

            var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();

            // 使用指定的日志工厂（如果提供）
            if (_loggerFactory != null)
            {
                builder.Logging.ClearProviders();
                // 将外部 ILoggerFactory 的提供者适配到 WebApplication 的 logging 系统
                builder.Logging.AddProvider(new LoggerFactoryProviderAdapter(_loggerFactory));
            }

            builder.WebHost.UseUrls($"http://0.0.0.0:{_port}");

            var app = builder.Build();

            // 默认响应用于根路径，方便快速测试
            app.MapGet("/", async ctx =>
            {
                await ctx.Response.WriteAsync($"DrxHttpAspServer listening on port {_port}");
            });

            // 如果提供了框架级的 requestHandler，则注册一个 catch-all 路由，把 HttpContext 转换为框架 HttpRequest 并应用 HttpResponse
            if (_requestHandler != null)
            {
                app.Map("/{**catchall}", async ctx =>
                {
                    var req = await ToFrameworkRequestAsync(ctx);
                    Drx.Sdk.Network.V2.Web.HttpResponse resp = null;
                    try
                    {
                        resp = await _requestHandler(req).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // 处理异常，返回 500
                        resp = new Drx.Sdk.Network.V2.Web.HttpResponse(500, ex.ToString());
                    }

                    await ApplyFrameworkResponseAsync(ctx, resp).ConfigureAwait(false);
                });
            }

            // 调用传统的用户路由/中间件注册（在 requestHandler 之后仍然有效）
            _configureApp?.Invoke(app);

            _host = app;
            await _host.StartAsync();
        }

        private static async Task<Drx.Sdk.Network.V2.Web.Http.HttpRequest> ToFrameworkRequestAsync(HttpContext ctx)
        {
            var fr = new Drx.Sdk.Network.V2.Web.HttpRequest();
            fr.Method = ctx.Request.Method;
            fr.Path = ctx.Request.Path + ctx.Request.QueryString;
            fr.Url = ctx.Request.GetDisplayUrl();

            // Headers
            var headers = new System.Collections.Specialized.NameValueCollection();
            foreach (var h in ctx.Request.Headers)
            {
                headers.Add(h.Key, string.Join(",", h.Value.ToArray()));
            }
            fr.Headers = headers;

            // Query
            var q = new System.Collections.Specialized.NameValueCollection();
            foreach (var p in ctx.Request.Query)
            {
                q.Add(p.Key, p.Value);
            }
            fr.Query = q;

            // Body
            try
            {
                ctx.Request.EnableBuffering();
                using var reader = new System.IO.StreamReader(ctx.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                fr.Body = body;
                fr.BodyJson = body;
                try { ((IDictionary<string, object>)fr.Content)["Text"] = body; } catch { }
                ctx.Request.Body.Position = 0;
            }
            catch { }

            // Client address
            try
            {
                var ip = ctx.Connection.RemoteIpAddress;
                var port = ctx.Connection.RemotePort;
                if (ip != null)
                {
                    fr.RemoteEndPoint = new IPEndPoint(ip, port);
                }
                else
                {
                    fr.RemoteEndPoint = new IPEndPoint(IPAddress.None, 0);
                }
                fr.ClientAddress = Drx.Sdk.Network.V2.Web.HttpRequest.Address.FromEndPoint(fr.RemoteEndPoint, fr.Headers);
            }
            catch { }

            return fr;
        }

        private static async Task ApplyFrameworkResponseAsync(HttpContext ctx, Drx.Sdk.Network.V2.Web.Http.HttpResponse resp)
        {
            if (resp == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsync("null response from handler").ConfigureAwait(false);
                return;
            }

            ctx.Response.StatusCode = resp.StatusCode;

            // Headers
            try
            {
                foreach (string key in resp.Headers)
                {
                    var val = resp.Headers[key];
                    if (!string.IsNullOrEmpty(key))
                    {
                        ctx.Response.Headers[key] = val;
                    }
                }
            }
            catch { }

            // Body: 优先 FileStream -> BodyBytes -> Body -> BodyObject (序列化为 JSON)
            if (resp.FileStream != null)
            {
                try
                {
                    var stream = resp.FileStream;
                    // 如果响应中未指定 Content-Length 且流可寻址，则尝试设置
                    try
                    {
                        if (stream.CanSeek && !ctx.Response.Headers.ContainsKey("Content-Length"))
                        {
                            ctx.Response.ContentLength = stream.Length;
                        }
                    }
                    catch { }

                    const int defaultBuffer = 64 * 1024; // 64KB
                    var buffer = new byte[defaultBuffer];
                    int read;
                    var bandwidthKb = resp.BandwidthLimitKb; // 0 表示无限制
                    double bytesPerSecond = bandwidthKb > 0 ? bandwidthKb * 1024.0 : double.PositiveInfinity;

                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        await ctx.Response.Body.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                        await ctx.Response.Body.FlushAsync().ConfigureAwait(false);

                        if (bandwidthKb > 0 && bytesPerSecond > 0 && double.IsFinite(bytesPerSecond))
                        {
                            // 简单限速：按当前块大小计算需要等待的毫秒数
                            var waitMs = (int)Math.Ceiling(read * 1000.0 / bytesPerSecond);
                            if (waitMs > 0)
                            {
                                await Task.Delay(waitMs).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch
                {
                    // 忽略流写出时的异常
                }
            }
            else if (resp.BodyBytes != null && resp.BodyBytes.Length > 0)
            {
                await ctx.Response.Body.WriteAsync(resp.BodyBytes, 0, resp.BodyBytes.Length).ConfigureAwait(false);
            }
            else if (!string.IsNullOrEmpty(resp.Body))
            {
                await ctx.Response.WriteAsync(resp.Body).ConfigureAwait(false);
            }
            else if (resp.BodyObject != null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(resp.BodyObject);
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(json).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 异步停止服务器。
        /// </summary>
        /// <returns>停止完成的任务。</returns>
        public async Task StopAsync()
        {
            if (_host == null)
                return;

            try
            {
                await _host.StopAsync();
            }
            finally
            {
                _host.Dispose();
                _host = null;
            }
        }

        /// <summary>
        /// 释放资源。
        /// </summary>
        public void Dispose()
        {
            if (_host != null)
            {
                try
                {
                    _host.StopAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    // 忽略停止过程中的异常，确保释放
                }
                _host.Dispose();
                _host = null;
            }
        }
    }

    /// <summary>
    /// 将外部的 ILoggerFactory 适配为一个简单的 ILoggerProvider，以便把外部日志工厂的日志接入到 WebApplication 的 logging 系统。
    /// 注意：此适配器非常轻量，仅将 ILoggerFactory 的 CreateLogger 用于返回 ILogger，不对生命周期做管理。
    /// </summary>
    internal class LoggerFactoryProviderAdapter : ILoggerProvider
    {
        private readonly ILoggerFactory _factory;

        public LoggerFactoryProviderAdapter(ILoggerFactory factory)
        {
            _factory = factory;
        }

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        {
            return _factory.CreateLogger(categoryName);
        }

        public void Dispose()
        {
            // 不释放外部工厂
        }
    }
}
