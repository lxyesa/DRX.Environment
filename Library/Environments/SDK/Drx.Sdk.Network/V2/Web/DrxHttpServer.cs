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
using System.Xml.Linq;
using System.Threading;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Drx.Sdk.Network.V2.Web
{
    /// <summary>
    /// 动作结果接口：允许处理器返回一个可被框架执行为 HttpResponse 的对象
    /// 实现类应负责把自身转换为 HttpResponse（异步或同步）。
    /// </summary>
    public interface IActionResult
    {
        /// <summary>
        /// 将当前 ActionResult 转换为 HttpResponse。
        /// </summary>
        /// <param name="request">当前请求信息（可能包含 ListenerContext）</param>
        /// <param name="server">服务器实例，供实现者访问辅助方法或配置</param>
        /// <returns>HttpResponse 对象</returns>
        Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server);
    }

    /// <summary>
    /// Cookie选项类
    /// </summary>
    public class CookieOptions
    {
        public bool HttpOnly { get; set; } = true;
        public bool Secure { get; set; } = false;
        public string? SameSite { get; set; } = "Lax";
        public string? Path { get; set; } = "/";
        public string? Domain { get; set; }
        public DateTime? Expires { get; set; }
        public TimeSpan? MaxAge { get; set; }
    }

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

    public class DrxHttpServer : IAsyncDisposable
    {
        /// <summary>
        /// 文件根目录路径，供结果类型（如 HtmlResultFromFile / FileResult）以相对路径定位文件。
        /// 如果为 null 或空，表示不启用基于根目录的相对路径解析，处理者应使用绝对路径或 AddFileRoute。
        /// 默认为 null。
        /// </summary>
        public string? FileRootPath { get; set; }

        /// <summary>
        /// 将用户传入的文件指示符解析为磁盘上的绝对路径：
        /// - 如果传入的是以 '/' 或 '\\' 开头的相对路径且 FileRootPath 已设置，则把它解释为相对于 FileRootPath 的路径；
        /// - 否则如果是相对路径（不以盘符开头），也尝试相对于 FileRootPath 解析；
        /// - 否则返回原始路径（可能为绝对路径）。
        /// 方法不会抛出异常；若无法解析或文件不存在返回 null。
        /// </summary>
        /// <param name="pathOrIndicator">相对或绝对路径指示（例如 "/index.html" 或 "C:\\wwwroot\\index.html"）</param>
        /// <returns>存在的绝对文件路径或 null</returns>
        public string? ResolveFilePath(string pathOrIndicator)
        {
            try
            {
                if (string.IsNullOrEmpty(pathOrIndicator)) return null;

                // 如果已经是绝对路径并存在，直接返回
                if (Path.IsPathRooted(pathOrIndicator))
                {
                    var abs = Path.GetFullPath(pathOrIndicator);
                    return File.Exists(abs) ? abs : null;
                }

                // 尝试使用 FileRootPath
                if (!string.IsNullOrEmpty(FileRootPath))
                {
                    // 去除前导 '/' 或 '\\'
                    var trimmed = pathOrIndicator.TrimStart('/', '\\');
                    var candidate = Path.Combine(FileRootPath, trimmed);
                    var full = Path.GetFullPath(candidate);
                    if (File.Exists(full)) return full;
                }

                // 最后尝试以当前工作目录解析
                var cwdCandidate = Path.GetFullPath(pathOrIndicator);
                if (File.Exists(cwdCandidate)) return cwdCandidate;

                return null;
            }
            catch
            {
                return null;
            }
        }
        private HttpListener _listener;
        private readonly List<(string Prefix, string RootDir)> _fileRoutes = new();
        private readonly List<RouteEntry> _routes = new();
        // raw route entries 包含可选的速率限制字段
        // 最后一项为可选的路由级速率触发回调（见 RouteEntry.RateLimitCallback 签名）
        private readonly System.Collections.Generic.List<(string Template, Func<HttpListenerContext, Task> Handler, int RateLimitMaxRequests, int RateLimitWindowSeconds, Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? RateLimitCallback)> _rawRoutes = new();
        private readonly string? _staticFileRoot;
        private readonly List<MiddlewareEntry> _middlewares = new();
        private int _middlewareCounter = 0;
        private readonly SessionManager _sessionManager;
        private readonly System.Threading.AsyncLocal<Session?> _currentSession = new();

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

        // 触发回调：当全局速率限制被触发时调用（参数：触发次数, HttpRequest）
        // 如果用户需要异步处理，设置为一个返回 Task 的委托；若为 null 则不调用
        public Func<int, HttpRequest, Task>? OnGlobalRateLimitExceeded { get; set; }

        // 路由级触发回调（参数：触发次数, HttpRequest, routeKey）
        public Func<int, HttpRequest, string, Task>? OnRouteRateLimitExceeded { get; set; }

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
            // 可选的路由级触发回调（若设置则在该路由触发限流时优先调用）
            // 签名: (int triggeredCount, HttpRequest req, OverrideContext ctx) -> Task<HttpResponse?>
            public Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? RateLimitCallback { get; set; }
        }

        private class MiddlewareEntry
        {
            public Func<HttpListenerContext, Task> Handler { get; set; }
            public string? Path { get; set; } // null for global
            public int Priority { get; set; }
            public bool OverrideGlobal { get; set; }
            public int AddOrder { get; set; }
            // 可选：基于 HttpRequest 的中间件实现，签名为 (HttpRequest, Func<HttpRequest, Task<HttpResponse?>>) -> Task<HttpResponse?>
            public Func<HttpRequest, Func<HttpRequest, Task<HttpResponse?>>, Task<HttpResponse?>>? RequestMiddleware { get; set; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="prefixes">监听前缀，如 "http://localhost:8080/"</param>
        /// <param name="staticFileRoot">静态文件根目录（可为 null）</param>
        /// <param name="sessionTimeoutMinutes">会话超时时间（分钟），默认30分钟</param>
        public DrxHttpServer(IEnumerable<string> prefixes, string? staticFileRoot = null, int sessionTimeoutMinutes = 30)
        {
            _listener = new HttpListener();
            foreach (var prefix in prefixes)
            {
                _listener.Prefixes.Add(prefix);
            }
            _staticFileRoot = staticFileRoot;
            _fileRoutes = new List<(string Prefix, string RootDir)>();
            _rawRoutes = new System.Collections.Generic.List<(string Template, Func<HttpListenerContext, Task> Handler, int RateLimitMaxRequests, int RateLimitWindowSeconds, Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? RateLimitCallback)>();
            _requestChannel = Channel.CreateBounded<HttpListenerContext>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });
            _semaphore = new SemaphoreSlim(MaxConcurrentRequests);
            _messageQueue = new MessageQueue<HttpListenerContext>(1000);
            _threadPool = new ThreadPoolManager(Environment.ProcessorCount);
            _sessionManager = new SessionManager(sessionTimeoutMinutes);
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

        /// <summary>
        /// 添加中间件
        /// </summary>
        /// <param name="middleware">中间件处理委托</param>
        /// <param name="path">路径前缀，为 null 表示全局中间件</param>
        /// <param name="priority">优先级，-1 表示使用默认</param>
        /// <param name="overrideGlobal">是否覆盖全局优先级</param>
        public void AddMiddleware(Func<HttpListenerContext, Task> middleware, string? path = null, int priority = -1, bool overrideGlobal = false)
        {
            if (middleware == null) return;
            if (path != null && !path.StartsWith("/")) path = "/" + path;

            if (priority == -1)
            {
                priority = (path == null) ? 0 : 100; // 默认全局 0，路由 100
            }

            if (overrideGlobal)
            {
                priority = -1; // 覆盖时设为最高优先级
            }

            var entry = new MiddlewareEntry
            {
                Handler = middleware,
                Path = path,
                Priority = priority,
                OverrideGlobal = overrideGlobal,
                AddOrder = _middlewareCounter++
            };

            _middlewares.Add(entry);
            Logger.Info($"添加中间件: {path ?? "全局"} (优先级: {priority})");
        }

        private void _raw_routes_add(string path, Func<HttpListenerContext, Task> handler, int rateLimitMaxRequests, int rateLimitWindowSeconds)
        {
            _raw_routes_internal_add(path, handler, rateLimitMaxRequests, rateLimitWindowSeconds);
        }

        private void _raw_routes_internal_add(string path, Func<HttpListenerContext, Task> handler, int rateLimitMaxRequests, int rateLimitWindowSeconds)
        {
            // 新增的第五项为路由级速率触发回调，默认 null（稍后在注册时可通过属性绑定）
            _rawRoutes.Add((path, handler, rateLimitMaxRequests, rateLimitWindowSeconds, null));
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
                        ClientAddress = HttpRequest.Address.FromEndPoint(listenerReq.RemoteEndPoint, listenerReq.Headers),
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

        // 异步路由的可选速率限制重载（带可选回调）
        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, Task<HttpResponse>> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0, Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? rateLimitCallback = null)
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
                    RateLimitWindowSeconds = rateLimitWindowSeconds,
                    RateLimitCallback = rateLimitCallback
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
        /// 添加返回 IActionResult 的同步路由（框架会执行 IActionResult 并把结果转换为 HttpResponse）
        /// </summary>
        /// <param name="method">HTTP 方法</param>
        /// <param name="path">路径</param>
        /// <param name="handler">处理委托，返回 IActionResult</param>
        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, IActionResult> handler)
        {
            if (handler == null) return;
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = async (request) =>
                    {
                        var action = handler(request);
                        if (action == null)
                            return await Task.FromResult(new HttpResponse(204, string.Empty));
                        return await action.ExecuteAsync(request, this).ConfigureAwait(false);
                    },
                    ExtractParameters = CreateParameterExtractor(path)
                };
                _routes.Add(route);
                Logger.Info($"添加 IActionResult 同步路由: {method} {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加 IActionResult 同步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        /// <summary>
        /// 添加返回 IActionResult 的异步路由（框架会执行 IActionResult 并把结果转换为 HttpResponse）
        /// </summary>
        /// <param name="method">HTTP 方法</param>
        /// <param name="path">路径</param>
        /// <param name="handler">异步处理委托，返回 Task&lt;IActionResult&gt;</param>
        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, Task<IActionResult>> handler)
        {
            if (handler == null) return;
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = async (request) =>
                    {
                        var action = await handler(request).ConfigureAwait(false);
                        if (action == null)
                            return new HttpResponse(204, string.Empty);
                        return await action.ExecuteAsync(request, this).ConfigureAwait(false);
                    },
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = 0,
                    RateLimitWindowSeconds = 0
                };
                _routes.Add(route);
                Logger.Info($"添加 IActionResult 异步路由: {method} {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加 IActionResult 异步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        /// <summary>
        /// 添加返回 Task&lt;IActionResult&gt; 的异步路由（带速率限制重载）
        /// </summary>
        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, Task<IActionResult>> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0, Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? rateLimitCallback = null)
        {
            if (handler == null) return;
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = async (request) =>
                    {
                        var action = await handler(request).ConfigureAwait(false);
                        if (action == null)
                            return new HttpResponse(204, string.Empty);
                        return await action.ExecuteAsync(request, this).ConfigureAwait(false);
                    },
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = rateLimitMaxRequests,
                    RateLimitWindowSeconds = rateLimitWindowSeconds,
                    RateLimitCallback = rateLimitCallback
                };
                _routes.Add(route);
                Logger.Info($"添加 IActionResult 异步路由: {method} {path} (rate={rateLimitMaxRequests}/{rateLimitWindowSeconds}s)");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加 IActionResult 异步路由 {method} {path} 时发生错误: {ex}");
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
        /// <summary>
        /// 从程序集中注册带有 HttpHandle 特性的方法
        /// 可选：将扫描到的 handler 类型写入 linker 描述文件（用于在启用 PublishTrimmed 时保留反射目标）。
        /// </summary>
        /// <param name="assembly">要扫描的程序集</param>
        /// <param name="server">HttpServer 实例</param>
        /// <param name="emitLinkerDescriptor">是否生成 linker 描述文件（linker.xml），以便在裁剪时保留被反射访问的类型</param>
        /// <param name="descriptorPath">可选的输出路径；若为空则写到应用目录下的 linker.{assemblyName}.xml</param>
        public static void RegisterHandlersFromAssembly(Assembly assembly, DrxHttpServer server, bool emitLinkerDescriptor = false, string? descriptorPath = null)
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
                            var handler = CreateHandlerDelegate(method, server);
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
                                // 如果方法声明需要 HttpListenerContext 或 返回 void/Task，则将其作为 raw 路由注册，
                                // 以便方法可以直接通过 server.Response(...) 写入响应并控制流式场景。
                                var parameters = method.GetParameters();
                                var returnType = method.ReturnType;
                                var shouldRegisterAsRaw = parameters.Any(p => p.ParameterType == typeof(HttpListenerContext))
                                                       || returnType == typeof(void)
                                                       || returnType == typeof(Task);

                                if (shouldRegisterAsRaw)
                                {
                                    Func<HttpListenerContext, Task> rawHandler = async ctx =>
                                    {
                                        try
                                        {
                                            // 构建参数列表：支持 HttpRequest、DrxHttpServer、HttpListenerContext
                                            var req = await server.ParseRequestAsync(ctx.Request).ConfigureAwait(false);
                                            var args = new List<object?>();
                                            foreach (var p in parameters)
                                            {
                                                if (p.ParameterType == typeof(HttpRequest)) args.Add(req);
                                                else if (p.ParameterType == typeof(DrxHttpServer)) args.Add(server);
                                                else if (p.ParameterType == typeof(HttpListenerContext)) args.Add(ctx);
                                                else args.Add(null);
                                            }

                                            var result = method.Invoke(null, args.ToArray());
                                            if (result is Task t)
                                            {
                                                await t.ConfigureAwait(false);
                                            }
                                            // 方法被视为自行处理响应（通过 server.Response 等）
                                        }
                                        catch (TargetInvocationException tie)
                                        {
                                            Logger.Error($"注册的原始方法 {method.Name} 调用失败: {tie.InnerException?.Message ?? tie.Message}\n{tie.InnerException?.StackTrace ?? tie.StackTrace}");
                                            try { ctx.Response.StatusCode = 500; using var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8, leaveOpen: false); sw.Write("Internal Server Error"); } catch { }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Error($"注册的原始方法 {method.Name} 执行时发生错误: {ex}");
                                            try { ctx.Response.StatusCode = 500; using var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8, leaveOpen: false); sw.Write("Internal Server Error"); } catch { }
                                        }
                                    };

                                    server.AddRawRoute(attr.Path, rawHandler, attr.RateLimitMaxRequests, attr.RateLimitWindowSeconds);
                                }
                                else
                                {
                                    var handler = CreateHandlerDelegate(method, server);
                                    if (handler != null)
                                    {
                                        // 绑定路由级速率限制回调（如果属性中指定了）
                                        var rateLimitCallback = BindRateLimitCallback(method.DeclaringType!, attr);
                                        server.AddRoute(httpMethod, attr.Path, handler, attr.RateLimitMaxRequests, attr.RateLimitWindowSeconds, rateLimitCallback);
                                    }
                                }
                            }
                            else
                            {
                                Logger.Warn($"无效的 HTTP 方法: {attr.Method}");
                            }
                        }
                    }
                }

                // 注册中间件
                var middlewareMethods = assembly.GetTypes()
                    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    .Where(m => m.GetCustomAttributes(typeof(HttpMiddlewareAttribute), false).Length > 0)
                    .ToList();

                foreach (var method in middlewareMethods)
                {
                    var attributes = method.GetCustomAttributes<HttpMiddlewareAttribute>();
                    foreach (var attr in attributes)
                    {
                        var parameters = method.GetParameters();
                        // 支持两种中间件签名：老的 (HttpListenerContext) 或新的 (HttpRequest, Func<HttpRequest, HttpResponse>) / (HttpRequest, Func<HttpRequest, Task<HttpResponse>>)
                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(HttpListenerContext))
                        {
                            var returnType = method.ReturnType;
                            Func<HttpListenerContext, Task> middlewareHandler;
                            if (returnType == typeof(void))
                            {
                                middlewareHandler = ctx => { method.Invoke(null, new object[] { ctx }); return Task.CompletedTask; };
                            }
                            else if (returnType == typeof(Task))
                            {
                                middlewareHandler = ctx => (Task)method.Invoke(null, new object[] { ctx });
                            }
                            else
                            {
                                Logger.Warn($"不能注册中间件: 方法 {method.Name} 返回类型不受支持: {returnType}");
                                continue;
                            }

                            server.AddMiddleware(middlewareHandler, attr.Path, attr.Priority, attr.OverrideGlobal);
                        }
                        else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(HttpRequest))
                        {
                            // 支持第二个参数为同步或异步的 next 委托
                            var secondParam = parameters[1].ParameterType;
                            var returnType = method.ReturnType;

                            // 创建一个通用的 RequestMiddleware 包装器
                            Func<HttpRequest, Func<HttpRequest, Task<HttpResponse?>>, Task<HttpResponse?>> requestMiddleware = null;

                            if (secondParam == typeof(Func<HttpRequest, HttpResponse>))
                            {
                                if (returnType == typeof(HttpResponse))
                                {
                                    requestMiddleware = async (req, next) =>
                                    {
                                        try
                                        {
                                            // 将异步 next 包装为同步调用（阻塞），以适配方法期望的签名
                                            Func<HttpRequest, HttpResponse> nextSync = r => next(r).GetAwaiter().GetResult();
                                            var resp = (HttpResponse)method.Invoke(null, new object[] { req, nextSync })!;
                                            return resp ?? new HttpResponse(500, "Internal Server Error");
                                        }
                                        catch (TargetInvocationException tie)
                                        {
                                            Logger.Error($"执行中间件方法 {method.Name} 时发生错误: {tie.InnerException?.Message ?? tie.Message}");
                                            return new HttpResponse(500, $"Internal Server Error: {tie.InnerException?.Message ?? tie.Message}");
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Error($"执行中间件方法 {method.Name} 时发生错误: {ex}");
                                            return new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                                        }
                                    };
                                }
                                else if (returnType == typeof(Task<HttpResponse>))
                                {
                                    requestMiddleware = async (req, next) =>
                                    {
                                        try
                                        {
                                            Func<HttpRequest, HttpResponse> nextSync = r => next(r).GetAwaiter().GetResult();
                                            var task = (Task<HttpResponse>)method.Invoke(null, new object[] { req, nextSync })!;
                                            var resp = await task.ConfigureAwait(false);
                                            return resp ?? new HttpResponse(500, "Internal Server Error");
                                        }
                                        catch (TargetInvocationException tie)
                                        {
                                            Logger.Error($"执行中间件方法 {method.Name} 时发生错误: {tie.InnerException?.Message ?? tie.Message}");
                                            return new HttpResponse(500, $"Internal Server Error: {tie.InnerException?.Message ?? tie.Message}");
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Error($"执行中间件方法 {method.Name} 时发生错误: {ex}");
                                            return new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                                        }
                                    };
                                }
                                else
                                {
                                    Logger.Warn($"不能注册中间件: 方法 {method.Name} 返回类型不受支持: {returnType}");
                                    continue;
                                }
                            }
                            else if (secondParam == typeof(Func<HttpRequest, Task<HttpResponse>>))
                            {
                                if (returnType == typeof(HttpResponse))
                                {
                                    requestMiddleware = async (req, next) =>
                                    {
                                        try
                                        {
                                            Func<HttpRequest, Task<HttpResponse?>> nextAsync = r => next(r);
                                            var resp = (HttpResponse)method.Invoke(null, new object[] { req, nextAsync })!;
                                            return resp ?? new HttpResponse(500, "Internal Server Error");
                                        }
                                        catch (TargetInvocationException tie)
                                        {
                                            Logger.Error($"执行中间件方法 {method.Name} 时发生错误: {tie.InnerException?.Message ?? tie.Message}");
                                            return new HttpResponse(500, $"Internal Server Error: {tie.InnerException?.Message ?? tie.Message}");
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Error($"执行中间件方法 {method.Name} 时发生错误: {ex}");
                                            return new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                                        }
                                    };
                                }
                                else if (returnType == typeof(Task<HttpResponse>))
                                {
                                    requestMiddleware = async (req, next) =>
                                    {
                                        try
                                        {
                                            Func<HttpRequest, Task<HttpResponse?>> nextAsync = r => next(r);
                                            var task = (Task<HttpResponse>)method.Invoke(null, new object[] { req, nextAsync })!;
                                            var resp = await task.ConfigureAwait(false);
                                            return resp ?? new HttpResponse(500, "Internal Server Error");
                                        }
                                        catch (TargetInvocationException tie)
                                        {
                                            Logger.Error($"执行中间件方法 {method.Name} 时发生错误: {tie.InnerException?.Message ?? tie.Message}");
                                            return new HttpResponse(500, $"Internal Server Error: {tie.InnerException?.Message ?? tie.Message}");
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Error($"执行中间件方法 {method.Name} 时发生错误: {ex}");
                                            return new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                                        }
                                    };
                                }
                                else
                                {
                                    Logger.Warn($"不能注册中间件: 方法 {method.Name} 返回类型不受支持: {returnType}");
                                    continue;
                                }
                            }
                            else
                            {
                                Logger.Warn($"标注为中间件的方法 {method.Name} 的第二个参数类型不支持: {secondParam}");
                                continue;
                            }

                            var priority = attr.Priority;
                            if (priority == -1)
                            {
                                priority = (attr.Path == null) ? 0 : 100;
                            }
                            if (attr.OverrideGlobal)
                            {
                                priority = -1;
                            }

                            var entry = new MiddlewareEntry
                            {
                                Path = attr.Path,
                                Priority = priority,
                                OverrideGlobal = attr.OverrideGlobal,
                                AddOrder = server._middlewareCounter++,
                                Handler = ctx => Task.CompletedTask,
                                RequestMiddleware = requestMiddleware
                            };

                            // 使用内部添加以保持排序逻辑
                            server._middlewares.Add(entry);
                        }
                        else
                        {
                            Logger.Warn($"标注为中间件的方法 {method.Name} 必须接受 HttpListenerContext 或 (HttpRequest, next) 签名");
                        }
                    }
                }

                Logger.Info($"从程序集 {assembly.FullName} 注册了 {methods.Count} 个 HTTP处理方法和 {middlewareMethods.Count} 个中间件");

                // 可选：生成 linker 描述文件，供 ILLink/Trim 使用以保留反射访问的类型和成员
                if (emitLinkerDescriptor)
                {
                    try
                    {
                        GenerateLinkerDescriptorForAssembly(assembly, descriptorPath);
                        Logger.Info($"已生成 linker 描述文件，用于保留 {assembly.GetName().Name} 中的反射目标");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"生成 linker 描述文件失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"注册 HTTP处理方法时发生错误: {ex}");
            }
        }

        /// <summary>
        /// 基于程序集内带有 HttpHandleAttribute 或 HttpMiddlewareAttribute 的类型，生成一个 linker 描述文件（linker.xml），
        /// 用于在发布时告知裁剪器保留这些类型的所有成员（preserve="all"）。
        /// 注意：此文件需在发布/裁剪之前被 MSBuild 项目包含（通过 &lt;TrimmerRootDescriptor Include="..." /&gt;），
        /// 或者你可以将它手动放到项目并在 csproj 中引用。
        /// </summary>
        private static void GenerateLinkerDescriptorForAssembly(Assembly assembly, string? descriptorPath)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            // 收集包含 HttpHandleAttribute 或 HttpMiddlewareAttribute 的声明类型
            var types = assembly.GetTypes()
                .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Any(m => m.GetCustomAttributes(typeof(HttpHandleAttribute), false).Length > 0 || m.GetCustomAttributes(typeof(HttpMiddlewareAttribute), false).Length > 0))
                .Select(t => t.FullName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToList();

            if (types.Count == 0)
            {
                // 仍然生成一个最小文件以避免构建错误（可选）
            }

            var assemblyName = assembly.GetName().Name ?? "UnknownAssembly";

            var root = new XElement("linker");
            var asmElem = new XElement("assembly", new XAttribute("fullname", assemblyName));

            foreach (var t in types)
            {
                asmElem.Add(new XElement("type", new XAttribute("fullname", t!), new XAttribute("preserve", "all")));
            }

            // 如果没有类型也把 assembly 元素写出（可按需保留整个程序集）
            root.Add(asmElem);

            var doc = new XDocument(new XComment(" Auto-generated by DrxHttpServer.RegisterHandlersFromAssembly - contains types to preserve for ILLink trimming "), root);

            string outPath;
            if (!string.IsNullOrEmpty(descriptorPath))
            {
                outPath = descriptorPath!;
            }
            else
            {
                var fileName = $"linker.{assemblyName}.xml";
                outPath = Path.Combine(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(), fileName);
            }

            // 确保目录存在
            var dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var settings = new System.Xml.XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 };
                using (var xw = System.Xml.XmlWriter.Create(fs, settings))
                {
                    doc.WriteTo(xw);
                }
            }
        }

        private static Func<HttpRequest, Task<HttpResponse>> CreateHandlerDelegate(MethodInfo method, DrxHttpServer server)
        {
            try
            {
                var parameters = method.GetParameters();

                // 支持方法参数包含 HttpRequest、DrxHttpServer（注入 server）和可选的 HttpListenerContext（不推荐用于此路径）
                foreach (var p in parameters)
                {
                    if (p.ParameterType != typeof(HttpRequest) && p.ParameterType != typeof(DrxHttpServer) && p.ParameterType != typeof(HttpListenerContext))
                    {
                        Logger.Warn($"方法 {method.Name} 的参数类型不受支持: {p.ParameterType}");
                        return null;
                    }
                }

                var returnType = method.ReturnType;
                var returnsHttpResponse = returnType == typeof(HttpResponse);
                var returnsTaskHttpResponse = returnType == typeof(Task<HttpResponse>) || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>) && returnType.GetGenericArguments()[0] == typeof(HttpResponse));
                var returnsActionResult = typeof(IActionResult).IsAssignableFrom(returnType);
                var returnsTaskActionResult = returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>) && typeof(IActionResult).IsAssignableFrom(returnType.GetGenericArguments()[0]);

                // 允许 HttpResponse / Task<HttpResponse> / IActionResult / Task<IActionResult>
                if (!returnsHttpResponse && !returnsTaskHttpResponse && !returnsActionResult && !returnsTaskActionResult)
                {
                    Logger.Warn($"方法 {method.Name} 的返回类型不受支持，应为 HttpResponse/Task<HttpResponse>/IActionResult/Task<IActionResult}}");
                    return null;
                }

                return async (HttpRequest request) =>
                {
                    try
                    {
                        // 构建参数列表
                        var args = new List<object?>();
                        foreach (var p in parameters)
                        {
                            if (p.ParameterType == typeof(HttpRequest)) args.Add(request);
                            else if (p.ParameterType == typeof(DrxHttpServer)) args.Add(server);
                            else if (p.ParameterType == typeof(HttpListenerContext)) args.Add(request.ListenerContext);
                            else args.Add(null);
                        }

                        var result = method.Invoke(null, args.ToArray());

                        // 异步 Task<T> 返回
                        if (result is Task task)
                        {
                            await task.ConfigureAwait(false);

                            if (returnsTaskHttpResponse)
                            {
                                var prop = task.GetType().GetProperty("Result");
                                var resp = (HttpResponse)prop!.GetValue(task)!;
                                return resp ?? new HttpResponse(500, "Internal Server Error");
                            }

                            if (returnsTaskActionResult)
                            {
                                var prop = task.GetType().GetProperty("Result");
                                var action = (IActionResult)prop!.GetValue(task)!;
                                if (action == null) return new HttpResponse(500, "Internal Server Error");
                                return await action.ExecuteAsync(request, server).ConfigureAwait(false);
                            }

                            // 如果是 Task 且不含返回值（应已被视为 raw），则返回 204
                            return new HttpResponse(204, "");
                        }

                        // 同步返回
                        if (returnsHttpResponse)
                        {
                            return (HttpResponse)result!;
                        }

                        if (returnsActionResult)
                        {
                            var action = (IActionResult)result!;
                            return await action.ExecuteAsync(request, server).ConfigureAwait(false);
                        }

                        // 不应到达此处，但为了安全返回 500
                        return new HttpResponse(500, "Internal Server Error");
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

        /// <summary>
        /// 绑定路由级速率限制回调（从属性中指定的方法名解析为编译委托，以优化性能）
        /// </summary>
        /// <param name="declaringType">声明路由方法的类型（默认在此类型中查找回调方法）</param>
        /// <param name="attr">HttpHandle 属性</param>
        /// <returns>编译后的回调委托，签名为 (int, HttpRequest, OverrideContext) -> Task<HttpResponse?>；若解析失败则返回 null</returns>
        private static Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? BindRateLimitCallback(Type declaringType, HttpHandleAttribute attr)
        {
            try
            {
                var callbackMethodName = attr.RateLimitCallbackMethodName;
                if (string.IsNullOrEmpty(callbackMethodName))
                    return null;

                var targetType = attr.RateLimitCallbackType ?? declaringType;
                var method = targetType.GetMethod(callbackMethodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                if (method == null)
                {
                    Logger.Warn($"未找到速率限制回调方法: {targetType.FullName}.{callbackMethodName}");
                    return null;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != 3
                    || parameters[0].ParameterType != typeof(int)
                    || parameters[1].ParameterType != typeof(HttpRequest)
                    || parameters[2].ParameterType != typeof(OverrideContext))
                {
                    Logger.Warn($"速率限制回调方法 {targetType.FullName}.{callbackMethodName} 的签名不匹配，应为 (int, HttpRequest, OverrideContext)");
                    return null;
                }

                var returnType = method.ReturnType;
                var returnsTask = typeof(Task).IsAssignableFrom(returnType);
                var returnsHttpResponse = returnType == typeof(HttpResponse);
                var returnsTaskHttpResponse = returnType == typeof(Task<HttpResponse>) || returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>) && Nullable.GetUnderlyingType(returnType.GetGenericArguments()[0]) == typeof(HttpResponse);

                if (!returnsHttpResponse && !returnsTaskHttpResponse)
                {
                    Logger.Warn($"速率限制回调方法 {targetType.FullName}.{callbackMethodName} 的返回类型不受支持，应为 HttpResponse、HttpResponse?、Task<HttpResponse> 或 Task<HttpResponse?>");
                    return null;
                }

                // 使用编译委托（compiled delegate）避免反射调用开销
                if (returnsTaskHttpResponse)
                {
                    // 异步方法，直接创建委托
                    var compiledDelegate = (Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>)Delegate.CreateDelegate(
                        typeof(Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>), method, throwOnBindFailure: false);

                    if (compiledDelegate != null)
                    {
                        return async (count, req, ctx) =>
                        {
                            try
                            {
                                return await compiledDelegate(count, req, ctx).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"执行速率限制回调 {targetType.FullName}.{callbackMethodName} 时发生错误: {ex.Message}");
                                return null; // 异常时返回 null，使用默认行为
                            }
                        };
                    }
                }
                else if (returnsHttpResponse)
                {
                    // 同步方法，包装为 Task
                    var compiledDelegate = (Func<int, HttpRequest, OverrideContext, HttpResponse?>)Delegate.CreateDelegate(
                        typeof(Func<int, HttpRequest, OverrideContext, HttpResponse?>), method, throwOnBindFailure: false);

                    if (compiledDelegate != null)
                    {
                        return (count, req, ctx) =>
                        {
                            try
                            {
                                var result = compiledDelegate(count, req, ctx);
                                return Task.FromResult(result);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"执行速率限制回调 {targetType.FullName}.{callbackMethodName} 时发生错误: {ex.Message}");
                                return Task.FromResult<HttpResponse?>(null);
                            }
                        };
                    }
                }

                // 如果编译委托失败，回退到反射调用（兼容性保障）
                Logger.Warn($"无法为 {targetType.FullName}.{callbackMethodName} 创建编译委托，使用反射调用");
                return async (count, req, ctx) =>
                {
                    try
                    {
                        var result = method.Invoke(null, new object[] { count, req, ctx });
                        if (result is Task<HttpResponse?> taskResp)
                            return await taskResp.ConfigureAwait(false);
                        else if (result is Task<HttpResponse> taskResp2)
                            return await taskResp2.ConfigureAwait(false);
                        else
                            return (HttpResponse?)result;
                    }
                    catch (TargetInvocationException tie)
                    {
                        Logger.Error($"执行速率限制回调 {targetType.FullName}.{callbackMethodName} 时发生错误: {tie.InnerException?.Message ?? tie.Message}");
                        return null;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"执行速率限制回调 {targetType.FullName}.{callbackMethodName} 时发生错误: {ex.Message}");
                        return null;
                    }
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"绑定速率限制回调时发生错误: {ex}");
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

        private async Task<HttpResponse?> ExecuteMiddlewarePipelineAsync(HttpListenerContext context, HttpRequest request, Func<HttpRequest, Task<HttpResponse?>> finalHandler)
        {
            var rawPath = context.Request.Url?.AbsolutePath ?? "/";

            // 收集适用的中间件
            var applicableMiddlewares = new List<MiddlewareEntry>();

            // 添加全局中间件
            applicableMiddlewares.AddRange(_middlewares.Where(m => m.Path == null));

            // 添加路由特定中间件
            var routeMiddlewares = _middlewares.Where(m => m.Path != null && rawPath.StartsWith(m.Path, StringComparison.OrdinalIgnoreCase));
            applicableMiddlewares.AddRange(routeMiddlewares);

            // 排序：优先级升序（低优先级先执行），然后按添加顺序
            applicableMiddlewares.Sort((a, b) =>
            {
                int aPriority = a.OverrideGlobal ? -1 : a.Priority;
                int bPriority = b.OverrideGlobal ? -1 : b.Priority;
                int priorityCompare = aPriority.CompareTo(bPriority);
                if (priorityCompare != 0) return priorityCompare;
                return a.AddOrder.CompareTo(b.AddOrder);
            });

            // 从最后一个中间件向前组合管道
            Func<HttpRequest, Task<HttpResponse?>> pipeline = finalHandler;
            for (int i = applicableMiddlewares.Count - 1; i >= 0; i--)
            {
                var mw = applicableMiddlewares[i];
                var next = pipeline;
                pipeline = async (req) =>
                {
                    if (mw.RequestMiddleware != null)
                    {
                        try
                        {
                            return await mw.RequestMiddleware(req, next).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"执行基于请求的中间件时发生错误: {ex}");
                            return new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                        }
                    }
                    else
                    {
                        try
                        {
                            await mw.Handler(context).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"执行基于上下文的中间件时发生错误: {ex}");
                        }
                        return await next(req).ConfigureAwait(false);
                    }
                };
            }

            return await pipeline(request).ConfigureAwait(false);
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                // 先解析请求，以便基于 HttpRequest 的中间件能访问
                var request = await ParseRequestAsync(context.Request);
                request.ListenerContext = context;

                // 最终处理器：包含文件流、原始路由、常规模式路由与静态文件回退的逻辑
                Func<HttpRequest, Task<HttpResponse?>> finalHandler = async (req) =>
                {
                    // 优先尝试以流方式服务文件下载（支持大文件与 Range）
                    if (TryServeFileStream(context))
                    {
                        return null; // 已直接响应
                    }

                    var clientIP = req.ClientAddress.Ip ?? context.Request.RemoteEndPoint?.Address.ToString();

                    // 尝试原始路由（raw handlers）
                    var rawPath = context.Request.Url?.AbsolutePath ?? "/";
                    foreach (var (Template, Handler, RateLimitMaxRequests, RateLimitWindowSeconds, RateLimitCallback) in _raw_routes_reader())
                    {
                        if (rawPath.StartsWith(Template, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                // 路由级速率限制优先（支持自定义回调）
                                if (!string.IsNullOrEmpty(clientIP) && RateLimitMaxRequests > 0 && RateLimitWindowSeconds > 0)
                                {
                                    var routeKey = $"RAW:{Template}";
                                    var (isExceeded, customResponse) = await CheckRateLimitForRouteAsync(clientIP, routeKey, RateLimitMaxRequests, RateLimitWindowSeconds, req, RateLimitCallback).ConfigureAwait(false);

                                    if (isExceeded)
                                    {
                                        // 如果有自定义响应，使用它；否则返回默认 429
                                        return customResponse ?? new HttpResponse(429, "Too Many Requests");
                                    }
                                }

                                // 若没有路由级超限，则检查全局限流（如果设置）
                                if (!string.IsNullOrEmpty(clientIP) && RateLimitMaxRequests <= 0 && IsRateLimitExceeded(clientIP, req))
                                {
                                    return new HttpResponse(429, "Too Many Requests");
                                }

                                await Handler(context).ConfigureAwait(false);
                                return null; // raw handler 已直接写入响应
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Raw handler 错误: {ex}");
                                return new HttpResponse(500, "Internal Server Error");
                            }
                        }
                    }                    // 常规模式路由匹配
                    var method = ParseHttpMethod(req.Method);
                    if (method != null)
                    {
                        foreach (var route in _routes)
                        {
                            if (route.Method == method)
                            {
                                var parameters = route.ExtractParameters(req.Path);
                                if (parameters != null)
                                {
                                    req.PathParameters = parameters;

                                    // 路由级速率限制优先（支持自定义回调）
                                    if (!string.IsNullOrEmpty(clientIP) && route.RateLimitMaxRequests > 0 && route.RateLimitWindowSeconds > 0)
                                    {
                                        var routeKey = $"ROUTE:{route.Method}:{route.Template}";
                                        var (isExceeded, customResponse) = await CheckRateLimitForRouteAsync(clientIP, routeKey, route.RateLimitMaxRequests, route.RateLimitWindowSeconds, req, route.RateLimitCallback).ConfigureAwait(false);

                                        if (isExceeded)
                                        {
                                            // 如果有自定义响应，使用它；否则返回默认 429
                                            return customResponse ?? new HttpResponse(429, "Too Many Requests");
                                        }
                                    }

                                    // 若未命中路由级限制，则检查全局限流
                                    if (!string.IsNullOrEmpty(clientIP) && (route.RateLimitMaxRequests == 0) && IsRateLimitExceeded(clientIP, req))
                                    {
                                        return new HttpResponse(429, "Too Many Requests");
                                    }

                                    try
                                    {
                                        var resp = await route.Handler(req).ConfigureAwait(false);
                                        return resp;
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error($"执行路由处理器时发生错误: {ex}");
                                        return new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }

                    // 如果没有匹配的路由，尝试静态文件
                    if (_staticFileRoot != null && TryServeStaticFile(req.Path, out var fileResponse))
                    {
                        return fileResponse ?? new HttpResponse(500, "Internal Server Error");
                    }

                    return new HttpResponse(404, "Not Found");
                };

                // 组合并执行中间件管道
                var response = await ExecuteMiddlewarePipelineAsync(context, request, finalHandler).ConfigureAwait(false);

                if (response == null)
                {
                    // 中间件或 raw/file 已经直接响应
                    return;
                }

                SendResponse(context.Response, response);
            }
            catch (Exception ex)
            {
                Logger.Error($"处理请求时发生错误: {ex}");
                var errorResponse = new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                SendResponse(context.Response, errorResponse);
            }
        }

        private IEnumerable<(string Template, Func<HttpListenerContext, Task> Handler, int RateLimitMaxRequests, int RateLimitWindowSeconds, Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? RateLimitCallback)> _raw_routes_reader() => _rawRoutes;

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
                    RemoteEndPoint = request.RemoteEndPoint,
                    ClientAddress = HttpRequest.Address.FromEndPoint(request.RemoteEndPoint, request.Headers)
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
                    RemoteEndPoint = request.RemoteEndPoint,
                    ClientAddress = HttpRequest.Address.FromEndPoint(request.RemoteEndPoint, request.Headers)
                };

                var contentType = request.Headers["Content-Type"] ?? request.Headers["content-type"];
                // 将表单解析逻辑委托到 HttpRequest.ParseFormAsync，以便集中处理表单解析（包括 multipart 与 urlencoded）
                if (!string.IsNullOrEmpty(contentType) && (contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0 || contentType.IndexOf("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    try
                    {
                        if (request.HasEntityBody && request.InputStream != null)
                        {
                            await httpRequest.ParseFormAsync(contentType, request.InputStream, Encoding.UTF8).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"解析请求表单时发生错误: {ex}");
                        throw;
                    }

                    httpRequest.Session = _currentSession.Value;
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
                httpRequest.Session = _currentSession.Value;
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

        /// <summary>
        /// 允许处理器直接通过 server.Response(ctx, resp) 发送响应（同步 HttpResponse）
        /// </summary>
        /// <param name="ctx">当前 HttpListenerContext</param>
        /// <param name="resp">要发送的 HttpResponse</param>
        public void Response(HttpListenerContext ctx, HttpResponse resp)
        {
            try
            {
                SendResponse(ctx.Response, resp ?? new HttpResponse(204, ""));
            }
            catch (Exception ex)
            {
                Logger.Error($"通过 server.Response 发送响应时发生错误: {ex}");
            }
        }

        /// <summary>
        /// 允许处理器直接通过 server.Response(ctx, actionResult) 异步发送响应
        /// </summary>
        /// <param name="ctx">当前 HttpListenerContext</param>
        /// <param name="action">实现 IActionResult 的结果对象</param>
        public async Task ResponseAsync(HttpListenerContext ctx, IActionResult action)
        {
            try
            {
                var req = await ParseRequestAsync(ctx.Request).ConfigureAwait(false);
                var resp = await action.ExecuteAsync(req, this).ConfigureAwait(false);
                SendResponse(ctx.Response, resp ?? new HttpResponse(500, "Internal Server Error"));
            }
            catch (Exception ex)
            {
                Logger.Error($"通过 server.ResponseAsync 发送响应时发生错误: {ex}");
                try { ctx.Response.StatusCode = 500; using var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8, leaveOpen: false); sw.Write("Internal Server Error"); } catch { }
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
        private bool IsRateLimitExceeded(string ip, HttpRequest? request = null)
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
                // 触发次数 = 当前队列中的请求数 + 本次尝试
                int triggeredCount = queue.Count + 1;
                if (request != null && OnGlobalRateLimitExceeded != null)
                {
                    var cb = OnGlobalRateLimitExceeded;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await cb(triggeredCount, request).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"执行全局速率限制回调时发生错误: {ex.Message}");
                        }
                    });
                }

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
        private bool IsRateLimitExceededForRoute(string ip, string routeKey, int maxRequests, int windowSeconds, HttpRequest? request = null)
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
                int triggeredCount = queue.Count + 1;
                // 记录此次超限请求，使得 count 能够继续增长
                queue.Enqueue(now);

                if (request != null && OnRouteRateLimitExceeded != null)
                {
                    var cb = OnRouteRateLimitExceeded;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await cb(triggeredCount, request, routeKey).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"执行路由速率限制回调时发生错误: {ex.Message}");
                        }
                    });
                }

                return true;
            }

            queue.Enqueue(now);
            return false;
        }

        /// <summary>
        /// 检查路由级速率限制，如果超限则优先调用路由级回调并返回其响应（若有）；否则返回特定响应表示处理结果。
        /// 带回调的版本：支持路由自定义超限响应。
        /// </summary>
        /// <param name="ip">客户端 IP</param>
        /// <param name="routeKey">路由标识符</param>
        /// <param name="maxRequests">最大请求数</param>
        /// <param name="windowSeconds">时间窗口（秒）</param>
        /// <param name="request">当前请求</param>
        /// <param name="rateLimitCallback">路由级回调（可选），如果设置且触发限流，将调用此回调获取自定义响应</param>
        /// <returns>返回元组 (isExceeded, customResponse)：isExceeded 表示是否超限，customResponse 为自定义响应（若回调设置且返回非 null）</returns>
        private async Task<(bool isExceeded, HttpResponse? customResponse)> CheckRateLimitForRouteAsync(string ip, string routeKey, int maxRequests, int windowSeconds, HttpRequest request, Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? rateLimitCallback)
        {
            if (maxRequests <= 0 || windowSeconds <= 0) return (false, null);

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
                int triggeredCount = queue.Count + 1;
                // 记录此次超限请求，使得 count 能够继续增长
                queue.Enqueue(now);

                // 优先调用路由级回调（如果设置）
                if (rateLimitCallback != null)
                {
                    try
                    {
                        var ctx = new OverrideContext(routeKey, maxRequests, windowSeconds);
                        var customResponse = await rateLimitCallback(triggeredCount, request, ctx).ConfigureAwait(false);

                        // 触发全局通知回调（如果设置）
                        if (OnRouteRateLimitExceeded != null)
                        {
                            var cb = OnRouteRateLimitExceeded;
                            _ = Task.Run(async () =>
                            {
                                try { await cb(triggeredCount, request, routeKey).ConfigureAwait(false); }
                                catch (Exception ex) { Logger.Warn($"执行路由速率限制通知回调时发生错误: {ex.Message}"); }
                            });
                        }

                        // 如果回调返回 null（或调用了 context.Default()），表示使用默认行为
                        return (true, customResponse); // 返回超限 + 自定义响应（可能为 null）
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"执行路由级速率限制回调时发生错误: {ex.Message}，回退到默认行为");
                    }
                }

                // 如果没有回调或回调抛异常，触发全局通知
                if (OnRouteRateLimitExceeded != null)
                {
                    var cb = OnRouteRateLimitExceeded;
                    _ = Task.Run(async () =>
                    {
                        try { await cb(triggeredCount, request, routeKey).ConfigureAwait(false); }
                        catch (Exception ex) { Logger.Warn($"执行路由速率限制通知回调时发生错误: {ex.Message}"); }
                    });
                }

                return (true, null); // 超限且无自定义响应，使用默认 429
            }

            queue.Enqueue(now);
            return (false, null); // 未超限
        }

        /// <summary>
        /// 添加会话中间件（便捷方法）
        /// 该中间件会自动管理会话Cookie和会话数据
        /// </summary>
        /// <param name="cookieName">会话Cookie名称，默认"session_id"</param>
        /// <param name="cookieOptions">Cookie选项</param>
        public void AddSessionMiddleware(string cookieName = "session_id", CookieOptions? cookieOptions = null)
        {
            cookieOptions ??= new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // 本地开发设为false，生产环境应为true
                Path = "/",
                MaxAge = TimeSpan.FromMinutes(30)
            };

            AddMiddleware(async ctx =>
            {
                // 从请求Cookie获取会话ID
                var sessionId = ctx.Request.Cookies[cookieName]?.Value;

                // 获取或创建会话
                var session = _sessionManager.GetOrCreateSession(sessionId);

                // 将会话存储到AsyncLocal
                _currentSession.Value = session;

                // 如果是新会话或ID不同，设置Cookie
                if (session.IsNew || sessionId != session.Id)
                {
                    var cookie = new Cookie(cookieName, session.Id)
                    {
                        HttpOnly = cookieOptions.HttpOnly,
                        Secure = cookieOptions.Secure,
                        Path = cookieOptions.Path ?? "/",
                        Domain = cookieOptions.Domain,
                        Expires = cookieOptions.Expires ?? DateTime.UtcNow.Add(cookieOptions.MaxAge ?? TimeSpan.FromMinutes(30))
                    };
                    ctx.Response.Cookies.Add(cookie);
                }
            });
        }

        /// <summary>
        /// 获取会话管理器
        /// </summary>
        public SessionManager SessionManager => _sessionManager;

        public ValueTask DisposeAsync()
        {
            Stop();
            _sessionManager?.Dispose();
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

    /// <summary>
    /// 会话数据类
    /// </summary>
    public class Session
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 会话数据存储
        /// </summary>
        public System.Collections.Concurrent.ConcurrentDictionary<string, object> Data { get; } = new();

        /// <summary>
        /// 最后访问时间
        /// </summary>
        public DateTime LastAccess { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// 是否为新会话
        /// </summary>
        public bool IsNew { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="id">会话ID</param>
        public Session(string id)
        {
            Id = id;
            Created = DateTime.UtcNow;
            LastAccess = Created;
            IsNew = true;
        }

        /// <summary>
        /// 更新最后访问时间
        /// </summary>
        public void UpdateAccess()
        {
            LastAccess = DateTime.UtcNow;
            IsNew = false;
        }
    }

    /// <summary>
    /// 会话管理器
    /// </summary>
    public class SessionManager
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Session> _sessions = new();
        private readonly TimeSpan _timeout;
        private readonly Timer _cleanupTimer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="timeoutMinutes">会话超时时间（分钟），默认30分钟</param>
        public SessionManager(int timeoutMinutes = 30)
        {
            _timeout = TimeSpan.FromMinutes(timeoutMinutes);
            // 每5分钟清理一次过期会话
            _cleanupTimer = new Timer(CleanupExpiredSessions, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// 创建新会话
        /// </summary>
        /// <returns>新会话</returns>
        public Session CreateSession()
        {
            var id = GenerateSessionId();
            var session = new Session(id);
            _sessions[id] = session;
            return session;
        }

        /// <summary>
        /// 获取会话，如果不存在则返回null
        /// </summary>
        /// <param name="id">会话ID</param>
        /// <returns>会话对象或null</returns>
        public Session? GetSession(string id)
        {
            if (_sessions.TryGetValue(id, out var session))
            {
                session.UpdateAccess();
                return session;
            }
            return null;
        }

        /// <summary>
        /// 获取或创建会话
        /// </summary>
        /// <param name="id">会话ID，如果为null或空则创建新会话</param>
        /// <returns>会话对象</returns>
        public Session GetOrCreateSession(string? id)
        {
            if (!string.IsNullOrEmpty(id) && _sessions.TryGetValue(id, out var existing))
            {
                existing.UpdateAccess();
                return existing;
            }
            return CreateSession();
        }

        /// <summary>
        /// 移除会话
        /// </summary>
        /// <param name="id">会话ID</param>
        public void RemoveSession(string id)
        {
            _sessions.TryRemove(id, out _);
        }

        /// <summary>
        /// 清理过期会话
        /// </summary>
        private void CleanupExpiredSessions(object? state)
        {
            var cutoff = DateTime.UtcNow - _timeout;
            var expiredKeys = _sessions.Where(kvp => kvp.Value.LastAccess < cutoff)
                                      .Select(kvp => kvp.Key)
                                      .ToList();

            foreach (var key in expiredKeys)
            {
                _sessions.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                Logger.Info($"清理了 {expiredKeys.Count} 个过期会话");
            }
        }

        /// <summary>
        /// 生成唯一会话ID
        /// </summary>
        /// <returns>会话ID</returns>
        private string GenerateSessionId()
        {
            return Guid.NewGuid().ToString("N") + DateTime.UtcNow.Ticks.ToString();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }
}
