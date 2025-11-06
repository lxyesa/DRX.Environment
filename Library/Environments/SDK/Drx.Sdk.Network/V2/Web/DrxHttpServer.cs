using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Collections.Specialized;
using Drx.Sdk.Shared;
using System.Net.Http;
using System.Threading.Channels;
using System.Threading;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Drx.Sdk.Network.V2.Web
{
    public class DrxHttpServer : IAsyncDisposable
    {
        private HttpListener _listener;
        private readonly List<(string Prefix, string RootDir)> _fileRoutes = new();
        private readonly List<RouteEntry> _routes = new();
        // raw route entries 包含可选的速率限制字段
        private readonly System.Collections.Generic.List<(string Template, Func<HttpListenerContext, Task> Handler, int RateLimitMaxRequests, int RateLimitWindowSeconds)> _rawRoutes = new();
        private readonly string? _staticFileRoot;

        private CancellationTokenSource _cts;
        private readonly Channel<HttpListenerContext> _requestChannel;
        private readonly SemaphoreSlim _semaphore;
        private const int MaxConcurrentRequests = 100; // 最大并发请求数
        private readonly MessageQueue<HttpListenerContext> _messageQueue;
        private readonly ThreadPoolManager _threadPool;
        // 每条消息至少处理耗时（毫秒）。若为 0 则不强制延迟。
        private volatile int _perMessageProcessingDelayMs = 0;

        // 请求拦截机制：基于IP的速率限制（全局）
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Generic.Queue<DateTime>> _ipRequestHistory = new();
        private int _rateLimitMaxRequests = 0; // 0 表示无限制
        private TimeSpan _rateLimitWindow = TimeSpan.Zero;
        private readonly object _rateLimitLock = new object();

        // 路由级速率限制：按 (ip + routeKey) 跟踪请求时间戳
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Generic.Queue<DateTime>> _ipRouteRequestHistory = new();

        private class RouteEntry
        {
            public string Template { get; set; }
            public HttpMethod Method { get; set; }
            public Func<HttpRequest, Task<HttpResponse>> Handler { get; set; }
            public Func<string, Dictionary<string, string>> ExtractParameters { get; set; }
            // 可选的路由级速率限制（默认为0表示无限制）
            public int RateLimitMaxRequests { get; set; }
            public int RateLimitWindowSeconds { get; set; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="prefixes">监听前缀，如 "http://localhost:8080/"</param>
        /// <param name="staticFileRoot">静态文件根目录（可为 null）</param>
        public DrxHttpServer(IEnumerable<string> prefixes, string? staticFileRoot = null)
        {
            _listener = new HttpListener();
            foreach (var prefix in prefixes)
            {
                _listener.Prefixes.Add(prefix);
            }
            _staticFileRoot = staticFileRoot;
            _fileRoutes = new List<(string Prefix, string RootDir)>();
            _rawRoutes = new System.Collections.Generic.List<(string Template, Func<HttpListenerContext, Task> Handler, int RateLimitMaxRequests, int RateLimitWindowSeconds)>();
            _requestChannel = Channel.CreateBounded<HttpListenerContext>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });
            _semaphore = new SemaphoreSlim(MaxConcurrentRequests);
            _messageQueue = new MessageQueue<HttpListenerContext>(1000);
            _threadPool = new ThreadPoolManager(Environment.ProcessorCount);
        }

        /// <summary>
        /// 添加原始路由（raw handler），处理方法直接接收 HttpListenerContext，适合流式上传/下载场景。
        /// </summary>
        /// <param name="path">路径前缀</param>
        /// <param name="handler">处理委托，接收 HttpListenerContext</param>
        public void AddRawRoute(string path, Func<HttpListenerContext, Task> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0)
        {
            if (string.IsNullOrEmpty(path) || handler == null) return;
            if (!path.StartsWith("/")) path = "/" + path;
            _raw_routes_add(path, handler, rateLimitMaxRequests, rateLimitWindowSeconds);
            Logger.Info($"添加原始路由: {path}");
        }

        private void _raw_routes_add(string path, Func<HttpListenerContext, Task> handler, int rateLimitMaxRequests, int rateLimitWindowSeconds)
        {
            _raw_routes_internal_add(path, handler, rateLimitMaxRequests, rateLimitWindowSeconds);
        }

        private void _raw_routes_internal_add(string path, Func<HttpListenerContext, Task> handler, int rateLimitMaxRequests, int rateLimitWindowSeconds)
        {
            _rawRoutes.Add((path, handler, rateLimitMaxRequests, rateLimitWindowSeconds));
        }

        /// <summary>
        /// 添加流式上传路由。处理方法接收 HttpRequest 并可通过 HttpRequest.UploadFile.Stream读取上传数据流。
        /// 与 Raw 不同，处理方法无需声明 HttpListenerContext；只需声明 (HttpRequest) -> HttpResponse 或 Task<HttpResponse> 即可。
        /// </summary>
        /// <param name="path">路径前缀</param>
        /// <param name="handler">处理委托</param>
        public void AddStreamUploadRoute(string path, Func<HttpRequest, Task<HttpResponse>> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0)
        {
            if (string.IsNullOrEmpty(path) || handler == null) return;
            if (!path.StartsWith("/")) path = "/" + path;

            // 将其包装为 raw handler，内部构造 HttpRequest 并将 HttpListenerRequest.InputStream作为 UploadFile.Stream
            Func<HttpListenerContext, Task> rawHandler = async ctx =>
            {
                try
                {
                    var listenerReq = ctx.Request;
                    var req = new HttpRequest
                    {
                        Method = listenerReq.HttpMethod,
                        Path = listenerReq.Url?.AbsolutePath ?? "/",
                        Url = listenerReq.Url?.ToString(),
                        Query = listenerReq.QueryString,
                        Headers = listenerReq.Headers,
                        RemoteEndPoint = listenerReq.RemoteEndPoint,
                        ListenerContext = ctx
                    };

                    // 不在此处读取整个请求体，直接将流暴露给处理方法
                    req.UploadFile = new HttpRequest.UploadFileDescriptor
                    {
                        Stream = listenerReq.InputStream,
                        FileName = listenerReq.Headers["X-File-Name"] ?? Path.GetFileName(req.Path),
                        FieldName = "file",
                        Progress = null,
                        CancellationToken = CancellationToken.None
                    };

                    HttpResponse? resp;
                    try
                    {
                        resp = await handler(req).ConfigureAwait(false) ?? new HttpResponse(500, "Internal Server Error");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"执行 StreamUpload处理方法时发生错误: {ex.InnerException?.Message ?? ex.Message}\n{ex.InnerException?.StackTrace ?? ex.StackTrace}");
                        resp = new HttpResponse(500, $"Internal Server Error: {ex.InnerException?.Message ?? ex.Message}");
                    }

                    SendResponse(ctx.Response, resp ?? new HttpResponse(500, "Internal Server Error"));
                }
                catch (Exception ex)
                {
                    Logger.Error($"StreamUpload raw handler 错误: {ex}");
                    try { ctx.Response.StatusCode = 500; using var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8, leaveOpen: false); sw.Write("Internal Server Error"); } catch { }
                }
            };

            _raw_routes_add(path, rawHandler, rateLimitMaxRequests, rateLimitWindowSeconds);
            Logger.Info($"添加流式上传路由: {path}");
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                _cts = new CancellationTokenSource();
                _listener.Start();
                Logger.Info("HttpServer 已启动");
                var processingTasks = new Task[Environment.ProcessorCount];
                for (int i = 0; i < processingTasks.Length; i++)
                {
                    processingTasks[i] = Task.Run(() => ProcessRequestsAsync(_cts.Token), _cts.Token);
                }

                await Task.WhenAll(
                    ListenAsync(_cts.Token),
                    Task.WhenAll(processingTasks)
                );
            }
            catch (Exception ex)
            {
                Logger.Error($"启动 HttpServer 时发生错误: {ex}");
                throw;
            }
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener.Stop();
                _semaphore.Dispose();
                // 停止并释放新增的队列与线程池
                try { _messageQueue?.Complete(); } catch { }
                try { _threadPool?.Dispose(); } catch { }
                Logger.Info("HttpServer 已停止");
            }
            catch (Exception ex)
            {
                Logger.Error($"停止 HttpServer 时发生错误: {ex}");
            }
        }

        /// <summary>
        /// 添加路由
        /// </summary>
        /// <param name="method">HTTP 方法</param>
        /// <param name="path">路径</param>
        /// <param name="handler">处理委托</param>
        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, HttpResponse> handler)
        {
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = async (request) => await Task.FromResult(handler(request)),
                    ExtractParameters = CreateParameterExtractor(path)
                };
                _routes.Add(route);
                Logger.Info($"添加路由: {method} {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加路由 {method} {path} 时发生错误: {ex}");
            }
        }

        // 同步路由的可选速率限制重载
        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, HttpResponse> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0)
        {
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = async (request) => await Task.FromResult(handler(request)),
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = rateLimitMaxRequests,
                    RateLimitWindowSeconds = rateLimitWindowSeconds
                };
                _routes.Add(route);
                Logger.Info($"添加路由: {method} {path} (rate={rateLimitMaxRequests}/{rateLimitWindowSeconds}s)");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加路由 {method} {path} 时发生错误: {ex}");
            }
        }

        /// <summary>
        /// 添加异步路由
        /// </summary>
        /// <param name="method">HTTP 方法</param>
        /// <param name="path">路径</param>
        /// <param name="handler">异步处理委托</param>
        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, Task<HttpResponse>> handler)
        {
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = handler,
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = 0,
                    RateLimitWindowSeconds = 0
                };
                _routes.Add(route);
                Logger.Info($"添加异步路由: {method} {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加异步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        // 异步路由的可选速率限制重载
        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, Task<HttpResponse>> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0)
        {
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = handler,
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = rateLimitMaxRequests,
                    RateLimitWindowSeconds = rateLimitWindowSeconds
                };
                _routes.Add(route);
                Logger.Info($"添加异步路由: {method} {path} (rate={rateLimitMaxRequests}/{rateLimitWindowSeconds}s)");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加异步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        /// <summary>
        /// 设置每条消息的最小处理延迟（毫秒）。
        /// 若设置为 500，则表示每条消息从开始处理到最终完成至少需要 500 毫秒，
        /// 用于平滑短时高并发以减轻 CPU 峰值压力。
        /// 传入小于等于 0 的值将关闭该功能（默认关闭）。
        /// </summary>
        /// <param name="ms">最小处理耗时，单位毫秒</param>
        public void SetPerMessageProcessingDelay(int ms)
        {
            try
            {
                if (ms <= 0) ms = 0;
                _perMessageProcessingDelayMs = ms;
                Logger.Info($"设置每消息最小处理延迟: {ms} ms");
            }
            catch (Exception ex)
            {
                Logger.Error($"SetPerMessageProcessingDelay 失败: {ex}");
            }
        }

        /// <summary>
        /// 获取当前每条消息的最小处理延迟（毫秒）。
        /// </summary>
        /// <returns>延迟毫秒数（0 表示未设置）</returns>
        public int GetPerMessageProcessingDelay()
        {
            return _perMessageProcessingDelayMs;
        }

        /// <summary>
        /// 设置基于IP的请求速率限制。
        /// 每个IP在指定时间窗口内最多允许的请求数。若超出则返回429 Too Many Requests。
        /// </summary>
        /// <param name="maxRequests">最大请求数，0表示无限制</param>
        /// <param name="timeValue">时间值</param>
        /// <param name="timeUnit">时间单位，支持 "seconds", "minutes", "hours", "days"</param>
        public void SetRateLimit(int maxRequests, int timeValue, string timeUnit)
        {
            if (maxRequests < 0) maxRequests = 0;
            if (timeValue < 0) timeValue = 0;
            TimeSpan window;
            switch (timeUnit.ToLower())
            {
                case "seconds":
                    window = TimeSpan.FromSeconds(timeValue);
                    break;
                case "minutes":
                    window = TimeSpan.FromMinutes(timeValue);
                    break;
                case "hours":
                    window = TimeSpan.FromHours(timeValue);
                    break;
                case "days":
                    window = TimeSpan.FromDays(timeValue);
                    break;
                default:
                    throw new ArgumentException("无效的时间单位。支持: seconds, minutes, hours, days", nameof(timeUnit));
            }
            lock (_rateLimitLock)
            {
                _rateLimitMaxRequests = maxRequests;
                _rateLimitWindow = window;
            }
            Logger.Info($"设置速率限制: 每{timeValue} {timeUnit} 最多 {maxRequests} 个请求");
        }

        /// <summary>
        /// 获取当前速率限制设置。
        /// </summary>
        /// <returns>元组 (maxRequests, window)</returns>
        public (int maxRequests, TimeSpan window) GetRateLimit()
        {
            lock (_rateLimitLock)
            {
                return (_rateLimitMaxRequests, _rateLimitWindow);
            }
        }

        /// <summary>
        /// 从程序集中注册带有 HttpHandle 特性的方法
        /// </summary>
        /// <param name="assembly">要扫描的程序集</param>
        /// <param name="server">HttpServer 实例</param>
        public static void RegisterHandlersFromAssembly(Assembly assembly, DrxHttpServer server)
        {
            try
            {
                var methods = assembly.GetTypes()
                    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    .Where(m => m.GetCustomAttributes(typeof(HttpHandleAttribute), false).Length > 0)
                    .ToList();

                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes<HttpHandleAttribute>();
                    foreach (var attr in attributes)
                    {
                        // 如果标注为 Raw / Stream 下载, 注册为原始处理器（接收 HttpListenerContext）
                        // 注意：StreamUpload 将使用专门的装饰器，允许处理方法签名为 (HttpRequest) -> HttpResponse 或 Task<HttpResponse>
                        if (attr.Raw || attr.StreamDownload)
                        {
                            // 方法签名需接受 HttpListenerContext
                            var parameters = method.GetParameters();
                            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(HttpListenerContext))
                            {
                                var returnType = method.ReturnType;
                                if (returnType == typeof(void))
                                {
                                    Func<HttpListenerContext, Task> handler = ctx => { method.Invoke(null, new object[] { ctx }); return Task.CompletedTask; };
                                    server.AddRawRoute(attr.Path, handler, attr.RateLimitMaxRequests, attr.RateLimitWindowSeconds);
                                }
                                else if (returnType == typeof(Task))
                                {
                                    Func<HttpListenerContext, Task> handler = ctx => (Task)method.Invoke(null, new object[] { ctx });
                                    server.AddRawRoute(attr.Path, handler, attr.RateLimitMaxRequests, attr.RateLimitWindowSeconds);
                                }
                                else if (returnType == typeof(HttpResponse))
                                {
                                    Func<HttpListenerContext, Task> handler = async ctx =>
                                    {
                                        try
                                        {
                                            var resp = (HttpResponse)method.Invoke(null, new object[] { ctx })!;
                                            server.SendResponse(ctx.Response, resp ?? new HttpResponse(500, "Internal Server Error"));
                                        }
                                        catch (TargetInvocationException tie)
                                        {
                                            Logger.Error($"执行原始路由方法 {method.Name} 时发生错误: {tie.InnerException?.Message ?? tie.Message}\n{tie.InnerException?.StackTrace ?? tie.StackTrace}");
                                            try { ctx.Response.StatusCode = 500; using var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8, leaveOpen: false); sw.Write($"Internal Server Error: {tie.InnerException?.Message ?? tie.Message}"); } catch { }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Error($"执行原始路由方法 {method.Name} 时发生错误: {ex}");
                                            try { ctx.Response.StatusCode = 500; using var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8, leaveOpen: false); sw.Write("Internal Server Error"); } catch { }
                                        }
                                        await Task.CompletedTask;
                                    };
                                    server.AddRawRoute(attr.Path, handler);
                                }
                                else if (returnType == typeof(Task<HttpResponse>))
                                {
                                    Func<HttpListenerContext, Task> handler = async ctx =>
                                    {
                                        try
                                        {
                                            var task = (Task<HttpResponse>)method.Invoke(null, new object[] { ctx })!;
                                            var resp = await task.ConfigureAwait(false) ?? new HttpResponse(500, "Internal Server Error");
                                            server.SendResponse(ctx.Response, resp);
                                        }
                                        catch (TargetInvocationException tie)
                                        {
                                            Logger.Error($"执行原始路由方法 {method.Name} 时发生错误: {tie.InnerException?.Message ?? tie.Message}\n{tie.InnerException?.StackTrace ?? tie.StackTrace}");
                                            try { ctx.Response.StatusCode = 500; using var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8, leaveOpen: false); sw.Write($"Internal Server Error: {tie.InnerException?.Message ?? tie.Message}"); } catch { }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Error($"执行原始路由方法 {method.Name} 时发生错误: {ex}");
                                            try { ctx.Response.StatusCode = 500; using var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8, leaveOpen: false); sw.Write("Internal Server Error"); } catch { }
                                        }
                                    };
                                    server.AddRawRoute(attr.Path, handler);
                                }
                                else
                                {
                                    Logger.Warn($"不能注册原始路由: 方法 {method.Name} 返回类型不受支持: {returnType}");
                                }
                            }
                            else
                            {
                                Logger.Warn($"标注为 Raw/Stream 的方法 {method.Name} 必须接受一个 HttpListenerContext 参数");
                            }
                        }
                        else if (attr.StreamUpload)
                        {
                            // StreamUpload 路由会自动将 HttpListenerContext 的请求流传递给处理方法，签名可为 (HttpRequest) -> HttpResponse/Task<HttpResponse>
                            var handler = CreateHandlerDelegate(method);
                            if (handler != null)
                            {
                                // 使用专用注册器将 handler 包装为 raw handler，传入 HttpRequest.Stream 和 ListenerContext
                                server.AddStreamUploadRoute(attr.Path, handler, attr.RateLimitMaxRequests, attr.RateLimitWindowSeconds);
                            }
                            else
                            {
                                Logger.Warn($"不能注册 StreamUpload 路由: 方法 {method.Name} 的签名或返回类型不受支持");
                            }
                        }
                        else
                        {
                            var httpMethod = ParseHttpMethod(attr.Method);
                            if (httpMethod != null)
                            {
                                var handler = CreateHandlerDelegate(method);
                                if (handler != null)
                                {
                                    server.AddRoute(httpMethod, attr.Path, handler, attr.RateLimitMaxRequests, attr.RateLimitWindowSeconds);
                                }
                            }
                            else
                            {
                                Logger.Warn($"无效的 HTTP 方法: {attr.Method}");
                            }
                        }
                    }
                }

                Logger.Info($"从程序集 {assembly.FullName} 注册了 {methods.Count} 个 HTTP处理方法");
            }
            catch (Exception ex)
            {
                Logger.Error($"注册 HTTP处理方法时发生错误: {ex}");
            }
        }

        private static Func<HttpRequest, Task<HttpResponse>> CreateHandlerDelegate(MethodInfo method)
        {
            try
            {
                var parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(HttpRequest))
                {
                    Logger.Warn($"方法 {method.Name} 的签名不正确，应为 (HttpRequest) -> HttpResponse");
                    return null;
                }

                var returnType = method.ReturnType;
                if (returnType != typeof(HttpResponse) && returnType != typeof(Task<HttpResponse>))
                {
                    Logger.Warn($"方法 {method.Name} 的返回类型不正确，应为 HttpResponse 或 Task<HttpResponse>");
                    return null;
                }

                return async (HttpRequest request) =>
                {
                    try
                    {
                        if (returnType == typeof(HttpResponse))
                        {
                            return (HttpResponse)method.Invoke(null, new object[] { request })!;
                        }
                        else
                        {
                            return await (Task<HttpResponse>)method.Invoke(null, new object[] { request })!;
                        }
                    }
                    catch (TargetInvocationException tie)
                    {
                        Logger.Error($"执行 HTTP处理方法 {method.Name} 时发生错误: {tie.InnerException?.Message ?? tie.Message}\n{tie.InnerException?.StackTrace ?? tie.StackTrace}");
                        return new HttpResponse(500, $"Internal Server Error: {tie.InnerException?.Message ?? tie.Message}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"执行 HTTP处理方法 {method.Name} 时发生错误: {ex}");
                        return new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                    }
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"创建处理委托时发生错误: {ex}");
                return null;
            }
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    await _requestChannel.Writer.WriteAsync(context);
                    try { _ = _message_queue_write(context); } catch { }
                }
                catch (HttpListenerException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"接受请求时发生错误: {ex}");
                }
            }
            _requestChannel.Writer.Complete();
            _messageQueue.Complete();
        }

        private ValueTask _message_queue_write(HttpListenerContext context) => _messageQueue.WriteAsync(context);

        private async Task ProcessRequestsAsync(CancellationToken token)
        {
            await foreach (var context in _requestChannel.Reader.ReadAllAsync(token))
            {
                await _semaphore.WaitAsync(token);
                _threadPool?.QueueWork(async () =>
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        await HandleRequestAsync(context).ConfigureAwait(false);
                    }
                    finally
                    {
                        // 强制每条消息至少等待指定的最小处理时间，以降低短时高并发对 CPU 的冲击
                        try
                        {
                            var delayMs = _perMessageProcessingDelayMs; // 读取为本地值以避免竞态
                            if (delayMs > 0)
                            {
                                var elapsed = (int)sw.ElapsedMilliseconds;
                                if (elapsed < delayMs)
                                {
                                    var toWait = delayMs - elapsed;
                                    if (toWait > 0)
                                    {
                                        await Task.Delay(toWait).ConfigureAwait(false);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // 延迟等待不应影响请求清理；仅记录警告
                            Logger.Warn($"应用每消息最小处理延迟时发生错误: {ex.Message}");
                        }

                        _semaphore.Release();
                    }
                });
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                var clientIP = context.Request.RemoteEndPoint?.Address.ToString();

                // 优先尝试以流方式服务文件下载（支持大文件与 Range）
                if (TryServeFileStream(context))
                {
                    return; // 已直接响应（流异步在后台执行，不阻塞本线程）
                }
                // 尝试原始路由（raw handlers），这些处理器可以直接操作 HttpListenerContext 用于流式上传/下载
                var rawPath = context.Request.Url?.AbsolutePath ?? "/";
                foreach (var (Template, Handler, RateLimitMaxRequests, RateLimitWindowSeconds) in _raw_routes_reader())
                {
                    if (rawPath.StartsWith(Template, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            // 路由级速率限制优先
                            if (!string.IsNullOrEmpty(clientIP) && RateLimitMaxRequests > 0 && RateLimitWindowSeconds > 0)
                            {
                                var routeKey = $"RAW:{Template}";
                                if (IsRateLimitExceededForRoute(clientIP, routeKey, RateLimitMaxRequests, RateLimitWindowSeconds))
                                {
                                    SendResponse(context.Response, new HttpResponse(429, "Too Many Requests"));
                                    return;
                                }
                            }

                            // 若没有路由级超限，则检查全局限流（如果设置）
                            if (!string.IsNullOrEmpty(clientIP) && RateLimitMaxRequests <= 0 && IsRateLimitExceeded(clientIP))
                            {
                                SendResponse(context.Response, new HttpResponse(429, "Too Many Requests"));
                                return;
                            }

                            await Handler(context).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Raw handler 错误: {ex}");
                            try { context.Response.StatusCode = 500; using var sw = new StreamWriter(context.Response.OutputStream, Encoding.UTF8, leaveOpen: false); sw.Write("Internal Server Error"); } catch { }
                        }
                        return;
                    }
                }

                var request = await ParseRequestAsync(context.Request);
                HttpResponse response;

                var method = ParseHttpMethod(request.Method);
                if (method != null)
                {
                    foreach (var route in _routes)
                    {
                        if (route.Method == method)
                        {
                            var parameters = route.ExtractParameters(request.Path);
                            if (parameters != null)
                            {
                                request.PathParameters = parameters;
                                // 路由级速率限制优先
                                if (!string.IsNullOrEmpty(clientIP) && route.RateLimitMaxRequests > 0 && route.RateLimitWindowSeconds > 0)
                                {
                                    var routeKey = $"ROUTE:{route.Method}:{route.Template}";
                                    if (IsRateLimitExceededForRoute(clientIP, routeKey, route.RateLimitMaxRequests, route.RateLimitWindowSeconds))
                                    {
                                        response = new HttpResponse(429, "Too Many Requests");
                                        goto respond;
                                    }
                                }

                                // 若未命中路由级限制，则检查全局限流
                                if (!string.IsNullOrEmpty(clientIP) && (route.RateLimitMaxRequests == 0) && IsRateLimitExceeded(clientIP))
                                {
                                    response = new HttpResponse(429, "Too Many Requests");
                                    goto respond;
                                }

                                response = await route.Handler(request);
                                goto respond;
                            }
                        }
                    }
                }

                // 如果没有匹配的路由，尝试静态文件
                if (_staticFileRoot != null && TryServeStaticFile(request.Path, out var fileResponse))
                {
                    response = fileResponse ?? new HttpResponse(500, "Internal Server Error");
                }
                else
                {
                    response = new HttpResponse(404, "Not Found");
                }

            respond:
                SendResponse(context.Response, response);
            }
            catch (Exception ex)
            {
                Logger.Error($"处理请求时发生错误: {ex}");
                var errorResponse = new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                SendResponse(context.Response, errorResponse);
            }
        }

        private IEnumerable<(string Template, Func<HttpListenerContext, Task> Handler, int RateLimitMaxRequests, int RateLimitWindowSeconds)> _raw_routes_reader() => _rawRoutes;

        /// <summary>
        /// 添加文件路由，将 URL 前缀映射到本地目录。例如 AddFileRoute("/download/", "C:\\wwwroot")
        /// 当请求以该前缀开头时，后续路径会被映射到本地目录并尝试以流方式返回文件。
        /// </summary>
        public void AddFileRoute(string urlPrefix, string rootDirectory)
        {
            if (string.IsNullOrEmpty(urlPrefix) || string.IsNullOrEmpty(rootDirectory)) return;
            if (!urlPrefix.StartsWith("/")) urlPrefix = "/" + urlPrefix;
            if (!urlPrefix.EndsWith("/")) urlPrefix += "/";
            _file_routes_add(urlPrefix, rootDirectory);
            Logger.Info($"添加文件路由: {urlPrefix} -> {rootDirectory}");
        }

        private void _file_routes_add(string urlPrefix, string rootDirectory) => _fileRoutes.Add((urlPrefix, rootDirectory));

        /// <summary>
        /// 尝试以流方式服务文件（支持 Range），如果处理则直接写入 context.Response 并返回 true。
        /// 注意：实际的流写入将在后台异步执行，不会阻塞请求处理线程。
        /// </summary>
        private bool TryServeFileStream(HttpListenerContext context)
        {
            try
            {
                var req = context.Request;
                var path = req.Url?.AbsolutePath ?? "/";

                foreach (var (Prefix, RootDir) in _fileRoutes)
                {
                    if (!path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) continue;

                    var rel = path.Substring(Prefix.Length);
                    // 防止路径穿越
                    rel = rel.Replace('/', Path.DirectorySeparatorChar);
                    if (rel.Contains(".."))
                    {
                        context.Response.StatusCode = 400;
                        context.Response.StatusDescription = "Bad Request";
                        context.Response.OutputStream.Close();
                        return true;
                    }

                    var filePath = Path.Combine(RootDir, rel);
                    if (!File.Exists(filePath))
                    {
                        context.Response.StatusCode = 404;
                        context.Response.StatusDescription = "Not Found";
                        context.Response.OutputStream.Close();
                        return true;
                    }

                    var fileInfo = new FileInfo(filePath);
                    long totalLength = fileInfo.Length;
                    var rangeHeader = req.Headers["Range"];
                    long start = 0, end = totalLength - 1;
                    bool isPartial = false;

                    if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                    {
                        // 支持单个范围，例如: bytes=123-
                        var rng = rangeHeader.Substring("bytes=".Length);
                        var parts = rng.Split('-');
                        if (long.TryParse(parts[0], out var s)) start = s;
                        if (parts.Length > 1 && long.TryParse(parts[1], out var e)) end = e;
                        if (start < 0) start = 0;
                        if (end >= totalLength) end = totalLength - 1;
                        if (start <= end) isPartial = true;
                    }

                    var resp = context.Response;
                    resp.AddHeader("Accept-Ranges", "bytes");
                    resp.ContentType = GetMimeType(Path.GetExtension(filePath));
                    resp.SendChunked = false;

                    if (isPartial)
                    {
                        resp.StatusCode = 206;
                        resp.StatusDescription = "Partial Content";
                        resp.AddHeader("Content-Range", $"bytes {start}-{end}/{totalLength}");
                        resp.ContentLength64 = end - start + 1;
                    }
                    else
                    {
                        resp.StatusCode = 200;
                        resp.StatusDescription = "OK";
                        resp.ContentLength64 = totalLength;
                    }

                    // 设置下载时的文件名提示（attachment）
                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        resp.AddHeader("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                    }
                    catch { }

                    // 启动后台任务进行异步流式传输，避免阻塞请求处理线程
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await StreamFileToResponseAsync(context, filePath, start, end, isPartial, totalLength).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"后台流式传输文件时发生错误: {ex}");
                            try { context.Response.StatusCode = 500; context.Response.OutputStream.Close(); } catch { }
                        }
                    });

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"TryServeFileStream发生错误: {ex}");
                try { context.Response.StatusCode = 500; context.Response.OutputStream.Close(); } catch { }
                return true;
            }
        }

        /// <summary>
        /// 将指定文件的指定范围异步写入到响应输出流，完成后关闭输出流。
        /// </summary>
        private async Task StreamFileToResponseAsync(HttpListenerContext context, string filePath, long start, long end, bool isPartial, long totalLength)
        {
            const int BufferSize = 64 * 1024;
            var resp = context.Response;

            // 自动附加常用响应头（仅在调用方未设置时添加），以提升兼容性和客户端体验
            try
            {
                var fi = new FileInfo(filePath);
                // Content-Type：若未设置则根据扩展名推断
                if (string.IsNullOrEmpty(resp.ContentType))
                {
                    try { resp.ContentType = GetMimeType(Path.GetExtension(filePath)); } catch { }
                }

                // Accept-Ranges：建议设置为 bytes
                try { if (resp.Headers["Accept-Ranges"] == null) resp.AddHeader("Accept-Ranges", "bytes"); } catch { }

                // Content-Disposition：若未设置则设置为 attachment，包含兼容的 filename 和 filename*
                bool hasContentDisposition = false;
                try
                {
                    for (int i = 0; i < resp.Headers.Count; i++)
                    {
                        var k = resp.Headers.GetKey(i);
                        if (string.Equals(k, "Content-Disposition", StringComparison.OrdinalIgnoreCase)) { hasContentDisposition = true; break; }
                    }
                }
                catch { }

                if (!hasContentDisposition)
                {
                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        var safeName = SanitizeFileNameForHeader(fileName);
                        var disposition = $"attachment; filename=\"{safeName}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
                        resp.AddHeader("Content-Disposition", disposition);
                    }
                    catch { }
                }

                // Last-Modified：若未设置则使用文件最后写入时间（UTC，RFC1123）
                try { if (resp.Headers["Last-Modified"] == null) resp.AddHeader("Last-Modified", fi.LastWriteTimeUtc.ToString("R")); } catch { }

                // ETag：基于长度与最后写入时间生成简单 ETag
                try { if (resp.Headers["ETag"] == null) resp.AddHeader("ETag", $"\"{fi.Length}-{fi.LastWriteTimeUtc.Ticks}\""); } catch { }

                // Cache-Control：默认私有且不缓存（可被调用方覆盖）
                try { if (resp.Headers["Cache-Control"] == null) resp.AddHeader("Cache-Control", "private, no-cache"); } catch { }
            }
            catch (Exception ex)
            {
                // 如果头部设置失败不应阻止流式传输，记录并继续
                Logger.Warn($"自动附加响应头时发生错误: {ex.Message}");
            }

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous))
                {
                    fs.Seek(start, SeekOrigin.Begin);
                    var remaining = (isPartial ? (end - start + 1) : totalLength);
                    var buffer = new byte[BufferSize];
                    while (remaining > 0)
                    {
                        int toRead = (int)Math.Min(buffer.Length, remaining);
                        var read = await fs.ReadAsync(buffer.AsMemory(0, toRead)).ConfigureAwait(false);
                        if (read <= 0) break;
                        try
                        {
                            await resp.OutputStream.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            if (IsClientDisconnect(ex))
                            {
                                Logger.Warn($"客户端在流式传输期间断开连接: {ex.Message}");
                                break;
                            }
                            Logger.Warn($"写入响应输出流时发生错误（文件流）: {ex}");
                            break;
                        }
                        remaining -= read;
                    }
                }
            }
            catch (Exception ex)
            {
                if (IsClientDisconnect(ex))
                {
                    Logger.Warn($"流式传输文件时客户端断开: {ex.Message}");
                }
                else
                {
                    Logger.Error($"后台流式传输文件时发生错误: {ex}");
                }
            }

            try { resp.OutputStream.Close(); } catch { }
        }

        private static bool IsClientDisconnect(Exception ex)
        {
            if (ex == null) return false;
            try
            {
                if (ex is HttpListenerException hle)
                {
                    return hle.ErrorCode == 64 || hle.ErrorCode == 995 || hle.ErrorCode == 10054;
                }

                if (ex is IOException ioe && ioe.InnerException is System.Net.Sockets.SocketException se)
                {
                    return se.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset || se.SocketErrorCode == System.Net.Sockets.SocketError.Shutdown;
                }

                if (ex is System.Net.Sockets.SocketException socketEx)
                {
                    return socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset || socketEx.SocketErrorCode == System.Net.Sockets.SocketError.Shutdown;
                }
            }
            catch { }
            return false;
        }

        private static HttpMethod? ParseHttpMethod(string methodString)
        {
            return methodString.ToUpper() switch
            {
                "GET" => HttpMethod.Get,
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                "PATCH" => HttpMethod.Patch,
                "HEAD" => HttpMethod.Head,
                "OPTIONS" => HttpMethod.Options,
                _ => null
            };
        }

        private HttpRequest ParseRequest(HttpListenerRequest request)
        {
            try
            {
                byte[] bodyBytes = null;
                string body = "";
                if (request.HasEntityBody)
                {
                    using var memoryStream = new MemoryStream();
                    request.InputStream.CopyTo(memoryStream);
                    bodyBytes = memoryStream.ToArray();
                    body = Encoding.UTF8.GetString(bodyBytes);
                }

                return new HttpRequest
                {
                    Method = request.HttpMethod,
                    Path = request.Url!.AbsolutePath,
                    Query = request.QueryString,
                    Headers = request.Headers,
                    Body = body,
                    BodyBytes = bodyBytes,
                    Content = body,
                    RemoteEndPoint = request.RemoteEndPoint
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"解析请求时发生错误: {ex}");
                throw;
            }
        }

        private async Task<HttpRequest> ParseRequestAsync(HttpListenerRequest request)
        {
            try
            {
                byte[] bodyBytes = null;
                string body = "";
                var httpRequest = new HttpRequest
                {
                    Method = request.HttpMethod,
                    Path = request.Url!.AbsolutePath,
                    Query = request.QueryString,
                    Headers = request.Headers,
                    RemoteEndPoint = request.RemoteEndPoint
                };

                var contentType = request.Headers["Content-Type"] ?? request.Headers["content-type"];
                if (!string.IsNullOrEmpty(contentType) && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        var mediaType = MediaTypeHeaderValue.Parse(contentType);
                        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).ToString();
                        var reader = new MultipartReader(boundary, request.InputStream);

                        MultipartSection section;
                        while ((section = await reader.ReadNextSectionAsync().ConfigureAwait(false)) != null)
                        {
                            var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);
                            if (!hasContentDispositionHeader) continue;

                            if (contentDisposition != null && contentDisposition.IsFileDisposition())
                            {
                                var fileName = HeaderUtilities.RemoveQuotes(contentDisposition.FileName).ToString();
                                var name = HeaderUtilities.RemoveQuotes(contentDisposition.Name).ToString();

                                var ms = new MemoryStream();
                                await section.Body.CopyToAsync(ms).ConfigureAwait(false);
                                ms.Position = 0;

                                httpRequest.UploadFile = new HttpRequest.UploadFileDescriptor
                                {
                                    Stream = ms,
                                    FileName = string.IsNullOrEmpty(fileName) ? "file" : fileName,
                                    FieldName = string.IsNullOrEmpty(name) ? "file" : name,
                                    CancellationToken = CancellationToken.None,
                                    Progress = null
                                };
                            }
                            else if (contentDisposition != null && contentDisposition.IsFormDisposition())
                            {
                                var name = HeaderUtilities.RemoveQuotes(contentDisposition.Name).ToString();
                                using var sr = new StreamReader(section.Body, Encoding.UTF8);
                                var value = await sr.ReadToEndAsync().ConfigureAwait(false);
                                if (string.Equals(name, "metadata", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "body", StringComparison.OrdinalIgnoreCase))
                                {
                                    body = value;
                                    try
                                    {
                                        httpRequest.Headers.Add(name, value);
                                    }
                                    catch (Exception)
                                    {
                                        try
                                        {
                                            httpRequest.Form.Add(name, value);
                                        }
                                        catch
                                        {
                                            if (string.IsNullOrEmpty(httpRequest.Body)) httpRequest.Body = value; else httpRequest.Body += "\n" + value;
                                        }
                                    }
                                }
                                else
                                {
                                    try { httpRequest.Form.Add(name, value); } catch { }
                                    try
                                    {
                                        httpRequest.Headers.Add(name, value);
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(body))
                        {
                            httpRequest.Body = body;
                            httpRequest.BodyBytes = Encoding.UTF8.GetBytes(body);
                            httpRequest.Content = new System.Dynamic.ExpandoObject();
                            try { ((System.Collections.Generic.IDictionary<string, object>)httpRequest.Content)["Text"] = body; } catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Multipart解析失败: {ex}");
                        throw;
                    }

                    return httpRequest;
                }

                if (request.HasEntityBody)
                {
                    using var memoryStream = new MemoryStream();
                    request.InputStream.CopyTo(memoryStream);
                    bodyBytes = memoryStream.ToArray();
                    body = Encoding.UTF8.GetString(bodyBytes);
                }

                httpRequest.Body = body;
                httpRequest.BodyBytes = bodyBytes;
                httpRequest.Content = new System.Dynamic.ExpandoObject();
                try { ((System.Collections.Generic.IDictionary<string, object>)httpRequest.Content)["Text"] = body; } catch { }
                return httpRequest;
            }
            catch (Exception ex)
            {
                Logger.Error($"解析请求时发生错误: {ex}");
                throw;
            }
        }

        private void SendResponse(HttpListenerResponse response, HttpResponse httpResponse)
        {
            try
            {
                response.StatusCode = httpResponse.StatusCode;
                response.StatusDescription = httpResponse.StatusDescription ?? GetDefaultStatusDescription(httpResponse.StatusCode);

                for (int i = 0; i < httpResponse.Headers.Count; i++)
                {
                    var key = httpResponse.Headers.GetKey(i);
                    var val = httpResponse.Headers.Get(i);
                    if (string.IsNullOrEmpty(key) || val == null)
                        continue;

                    if (string.Equals(key, "Content-Length", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        response.AddHeader(key, val);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"跳过无法添加的响应头 {key}: {ex}");
                    }
                }

                if (httpResponse.FileStream != null)
                {
                    var fs = httpResponse.FileStream;
                    try
                    {
                        if (fs.CanSeek)
                        {
                            long remaining = fs.Length - fs.Position;
                            try { response.ContentLength64 = remaining; }
                            catch (InvalidOperationException ioe)
                            {
                                Logger.Warn($"无法设置 ContentLength64（响应头可能已发送）: {ioe.Message}");
                                try { response.SendChunked = true; } catch { }
                            }
                        }
                        else
                        {
                            try { response.SendChunked = true; } catch { }
                        }
                    }
                    catch { try { response.SendChunked = true; } catch { } }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            const int BufferSize = 64 * 1024;
                            var buffer = new byte[BufferSize];

                            long localRemaining = -1;
                            try { localRemaining = response.ContentLength64; } catch { localRemaining = -1; }

                            long bytesPerSecond = 0;
                            try { bytesPerSecond = (long)httpResponse.BandwidthLimitKb * 1024L; } catch { bytesPerSecond = 0; }

                            var sw = Stopwatch.StartNew();
                            long totalBytesSent = 0;

                            while (true)
                            {
                                int toRead = (int)Math.Min(buffer.Length, (localRemaining >= 0) ? Math.Min(localRemaining, buffer.Length) : buffer.Length);
                                if (toRead <= 0) break;

                                int read = 0;
                                try { read = await fs.ReadAsync(buffer.AsMemory(0, toRead)).ConfigureAwait(false); } catch (Exception ex) { Logger.Warn($"读取文件流时发生错误: {ex}"); break; }
                                if (read <= 0) break;

                                try
                                {
                                    int writeCount = read;
                                    if (localRemaining >= 0)
                                    {
                                        writeCount = (int)Math.Min(read, localRemaining);
                                    }

                                    await response.OutputStream.WriteAsync(buffer.AsMemory(0, writeCount)).ConfigureAwait(false);
                                    totalBytesSent += writeCount;
                                    if (localRemaining >= 0) localRemaining -= writeCount;

                                    if (bytesPerSecond > 0)
                                    {
                                        double expectedMs = (double)totalBytesSent * 1000.0 / bytesPerSecond;
                                        var actualMs = sw.Elapsed.TotalMilliseconds;
                                        if (expectedMs > actualMs)
                                        {
                                            var waitMs = (int)Math.Ceiling(expectedMs - actualMs);
                                            if (waitMs > 0) await Task.Delay(waitMs).ConfigureAwait(false);
                                        }
                                    }

                                    if (writeCount < read)
                                    {
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    if (IsClientDisconnect(ex))
                                    {
                                        Logger.Warn($"客户端已断开连接，停止写入响应: {ex.Message}");
                                    }
                                    else
                                    {
                                        Logger.Warn($"写入响应输出流时发生错误（文件流）: {ex}");
                                    }
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (IsClientDisconnect(ex))
                            {
                                Logger.Warn($"客户端在传输中断开连接: {ex.Message}");
                            }
                            else
                            {
                                Logger.Error($"在传输文件到响应时发生错误: {ex}");
                            }
                        }
                        finally
                        {
                            try { httpResponse.FileStream?.Dispose(); } catch { }
                            try { response.OutputStream.Close(); } catch { }
                        }
                    });

                    return;
                }

                byte[] responseBytes = null;
                if (httpResponse.BodyBytes != null)
                {
                    responseBytes = httpResponse.BodyBytes;
                }
                else if (httpResponse.BodyObject != null)
                {
                    // 假设 BodyObject 是可序列化的对象，序列化为 JSON
                    var json = System.Text.Json.JsonSerializer.Serialize(httpResponse.BodyObject);
                    responseBytes = Encoding.UTF8.GetBytes(json);
                    // 设置 Content-Type，仅当未由调用方指定时设置
                    if (string.IsNullOrEmpty(response.ContentType))
                        response.ContentType = "application/json";
                }
                else if (!string.IsNullOrEmpty(httpResponse.Body))
                {
                    responseBytes = Encoding.UTF8.GetBytes(httpResponse.Body);
                }

                if (responseBytes != null)
                {
                    try
                    {
                        response.ContentLength64 = responseBytes.Length;
                    }
                    catch (InvalidOperationException ioe)
                    {
                        // 如果响应头已发送，无法设置 ContentLength64，记录警告并继续写入（可能使用分块传输）
                        Logger.Warn($"无法设置 ContentLength64（响应头可能已发送）: {ioe.Message}");
                    }
                    try
                    {
                        response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    response.ContentLength64 = 0;
                }

                try
                {
                    response.OutputStream.Close();
                }
                catch (Exception ex)
                {
                    Logger.Warn($"关闭响应流时发生错误: {ex}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"发送响应时发生错误: {ex}");
            }
        }

        // 提供一个本地的默认状态描述，以防 HttpResponse 没有提供
        private string GetDefaultStatusDescription(int statusCode)
        {
            return statusCode switch
            {
                200 => "OK",
                201 => "Created",
                204 => "No Content",
                400 => "Bad Request",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Not Found",
                500 => "Internal Server Error",
                _ => ""
            };
        }

        private bool TryServeStaticFile(string path, out HttpResponse? response)
        {
            response = null;
            if (string.IsNullOrEmpty(_staticFileRoot) || !path.StartsWith("/static/"))
                return false;

            var filePath = Path.Combine(_staticFileRoot, path.Substring("/static/".Length));
            if (!File.Exists(filePath))
                return false;

            try
            {
                var content = File.ReadAllText(filePath);
                var mimeType = GetMimeType(Path.GetExtension(filePath));
                response = new HttpResponse(200, content);
                response.Headers.Add("Content-Type", mimeType);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"服务静态文件 {filePath} 时发生错误: {ex}");
                return false;
            }
        }

        private string GetMimeType(string extension)
        {
            return extension.ToLower() switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                _ => "text/plain"
            };
        }

        private bool MatchRoute(string path, string template, out Dictionary<string, string>? parameters)
        {
            parameters = null;
            var templateParts = template.Split('/');
            var pathParts = path.Split('/');

            if (templateParts.Length != pathParts.Length)
                return false;

            parameters = new Dictionary<string, string>();
            for (int i = 0; i < templateParts.Length; i++)
            {
                if (string.IsNullOrEmpty(templateParts[i]))
                    continue;

                if (templateParts[i].StartsWith("{") && templateParts[i].EndsWith("}"))
                {
                    // 路由参数
                    var paramName = templateParts[i][1..^1];
                    parameters[paramName] = pathParts[i];
                }
                else if (!string.Equals(templateParts[i], pathParts[i], StringComparison.OrdinalIgnoreCase))
                {
                    // 确定不匹配的部分
                    return false;
                }
            }

            return true;
        }

        private static Func<string, Dictionary<string, string>> CreateParameterExtractor(string template)
        {
            if (!template.Contains('{'))
            {
                return path => path == template ? new Dictionary<string, string>() : null;
            }

            // 提取参数名并构建正则模式，安全地转义模板中的文本部分
            var paramNames = new List<string>();
            var sb = new System.Text.StringBuilder();
            int lastIndex = 0;
            var matches = Regex.Matches(template, "\\{([^}]+)\\}");
            foreach (Match m in matches)
            {
                //追加参数前的文字（转义）
                if (m.Index > lastIndex)
                {
                    sb.Append(Regex.Escape(template.Substring(lastIndex, m.Index - lastIndex)));
                }

                // 用捕获组替换参数
                sb.Append("([^/]+)");
                paramNames.Add(m.Groups[1].Value);
                lastIndex = m.Index + m.Length;
            }

            //追加尾部文字
            if (lastIndex < template.Length)
            {
                sb.Append(Regex.Escape(template.Substring(lastIndex)));
            }

            var regexPattern = "^" + sb.ToString() + "$";
            var regex = new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            return path =>
            {
                var m = regex.Match(path);
                if (!m.Success) return null;
                var dict = new Dictionary<string, string>();
                for (int i = 0; i < paramNames.Count; i++)
                {
                    dict[paramNames[i]] = m.Groups[i + 1].Value;
                }
                return dict;
            };
        }

        /// <summary>
        /// 将请求中的上传流保存为文件。
        /// 若未提供 savePath，则使用默认目录 AppContext.BaseDirectory/uploads/mods。
        /// </summary>
        /// <param name="request">包含 UploadFile 流的请求对象</param>
        /// <param name="savePath">目标保存目录</param>
        /// <param name="fileName">保存的文件名。默认值 "upload_" 会追加时间戳以避免冲突</param>
        /// <returns>处理结果 HttpResponse</returns>
        public static HttpResponse SaveUploadFile(HttpRequest request, string savePath, string fileName = "upload_")
        {
            if (request?.UploadFile == null || request.UploadFile.Stream == null)
            {
                return new HttpResponse(400, "缺少上传的文件流");
            }

            try
            {
                var upload = request.UploadFile;

                // 如果未指定保存路径，使用应用程序目录下的默认上传目录
                var defaultUploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads");
                if (string.IsNullOrEmpty(savePath))
                {
                    savePath = defaultUploadsDir;
                }

                // 确保目录存在
                Directory.CreateDirectory(savePath);

                //生成文件名（若使用默认前缀则追加时间戳）
                if (fileName == "upload_")
                {
                    fileName += DateTime.UtcNow.Ticks;
                }

                var filePath = Path.Combine(savePath, fileName);

                // 将上传流保存到文件（同步写入以保持原实现行为）
                using (var outFs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    upload.Stream.CopyTo(outFs);
                }

                return new HttpResponse(200, "上传成功");
            }
            catch (Exception ex)
            {
                Logger.Error($"处理上传时发生错误: {ex}");
                return new HttpResponse(500, $"上传处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建一个用于流式下载的 HttpResponse（快捷方法）。
        /// 调用方只需传入本地文件路径，本方法打开文件流并设置常用响应头（Content-Type、Content-Disposition），
        /// 并对文件名做安全过滤以避免非法控制字符导致的头部添加失败。
        /// </summary>
        /// <param name="filePath">本地文件路径</param>
        /// <param name="fileName">用于提示的下载文件名（可选），为空时使用 filePath 的文件名</param>
        /// <param name="contentType">Content-Type，默认为 application/octet-stream</param>
        /// <param name="bandwidthLimitKb">可选的带宽限制（KB/s），0 表示不限制</param>
        /// <returns>HttpResponse，若文件不存在返回404 响应</returns>
        public static HttpResponse CreateFileResponse(string filePath, string? fileName = null, string contentType = "application/octet-stream", int bandwidthLimitKb = 0)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return new HttpResponse(404, "Not Found");
            }

            FileStream fs = null;
            try
            {
                fs = File.OpenRead(filePath);
                var resp = new HttpResponse(200) { FileStream = fs };
                // 保存带宽限制（KB/s），0 表示不限制
                resp.BandwidthLimitKb = bandwidthLimitKb;

                // 尝试设置 Content-Type（忽略可能的异常）
                try { resp.Headers.Add("Content-Type", contentType); } catch { }

                if (string.IsNullOrEmpty(fileName)) fileName = Path.GetFileName(filePath);
                var safeName = SanitizeFileNameForHeader(fileName);

                // 同时提供 filename 和 filename*（RFC5987）以兼容多客户端并支持非 ASCII 名称
                try
                {
                    var disposition = $"attachment; filename=\"{safeName}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
                    resp.Headers.Add("Content-Disposition", disposition);
                }
                catch
                {
                    // 如果添加失败则忽略（SendResponse 中也会安全处理）
                }

                return resp;
            }
            catch (Exception ex)
            {
                // 打开文件失败时确保释放流并返回500
                try { fs?.Dispose(); } catch { }
                Logger.Error($"创建文件响应时发生错误: {ex}");
                return new HttpResponse(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// 打开指定的文件用于读取，并返回一个用于访问其内容的流。
        /// </summary>
        /// <remarks>
        /// 如果文件不存在或打开文件时发生错误，该方法将返回 null，而不是抛出异常。
        /// 调用方需负责在不再需要时释放返回的流资源。
        /// </remarks>
        /// <param name="filePath">
        /// 要打开的文件的完整路径。不能为空或空字符串。该文件必须存在。
        /// </param>
        /// <returns>
        /// 如果文件存在且可以成功打开，则返回一个只读流；否则返回 null。
        /// </returns>
        public static Stream GetFileStream(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }
            try
            {
                return File.OpenRead(filePath);
            }
            catch (Exception ex)
            {
                Logger.Error($"获取文件流时发生错误: {ex}");
                return null;
            }
        }

        // 辅助：清理文件名以便安全放入 HTTP头（移除控制字符和双引号）
        private static string SanitizeFileNameForHeader(string name)
        {
            if (string.IsNullOrEmpty(name)) return "file";
            var sb = new System.Text.StringBuilder();
            foreach (var ch in name)
            {
                // 跳过控制字符（包括 \r \n \t 等）和引号、反斜杠
                if (char.IsControl(ch) || ch == '"' || ch == '\\') continue;
                sb.Append(ch);
            }
            var result = sb.ToString().Trim();
            if (string.IsNullOrEmpty(result)) result = "file";
            return result;
        }

        /// <summary>
        /// 检查指定IP是否超出速率限制。
        /// 如果未设置限制或未超出，返回false；否则返回true并记录请求。
        /// </summary>
        private bool IsRateLimitExceeded(string ip)
        {
            int maxRequests;
            TimeSpan window;
            lock (_rateLimitLock)
            {
                maxRequests = _rateLimitMaxRequests;
                window = _rateLimitWindow;
            }
            if (maxRequests <= 0 || window == TimeSpan.Zero) return false;

            var now = DateTime.UtcNow;
            var cutoff = now - window;

            var queue = _ipRequestHistory.GetOrAdd(ip, _ => new System.Collections.Generic.Queue<DateTime>());

            // 清理过期请求
            while (queue.Count > 0 && queue.Peek() < cutoff)
            {
                queue.Dequeue();
            }

            // 检查是否超出
            if (queue.Count >= maxRequests)
            {
                return true;
            }

            // 记录当前请求
            queue.Enqueue(now);
            return false;
        }

        /// <summary>
        /// 检查指定IP在指定路由（routeKey）上是否超出速率限制。
        /// routeKey 是唯一标识某路由的字符串（例如: "ROUTE:GET:/api/foo" 或 "RAW:/upload"）。
        /// 如果未设置限制返回 false；否则在检查并记录请求后返回是否超出。
        /// </summary>
        private bool IsRateLimitExceededForRoute(string ip, string routeKey, int maxRequests, int windowSeconds)
        {
            if (maxRequests <= 0 || windowSeconds <= 0) return false;

            var now = DateTime.UtcNow;
            var window = TimeSpan.FromSeconds(windowSeconds);
            var cutoff = now - window;

            var dictKey = ip + "#" + routeKey;
            var queue = _ipRouteRequestHistory.GetOrAdd(dictKey, _ => new System.Collections.Generic.Queue<DateTime>());

            // 清理过期请求
            while (queue.Count > 0 && queue.Peek() < cutoff)
            {
                queue.Dequeue();
            }

            if (queue.Count >= maxRequests)
            {
                return true;
            }

            queue.Enqueue(now);
            return false;
        }

        public ValueTask DisposeAsync()
        {
            Stop();
            return ValueTask.CompletedTask;
        }
    }

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
