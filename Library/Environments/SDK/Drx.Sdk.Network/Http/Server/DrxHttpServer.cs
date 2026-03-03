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
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Drx.Sdk.Network.Email;
using System.Collections.Concurrent;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Serialization;
using Drx.Sdk.Network.Http.Performance;
using Drx.Sdk.Network.Http.Auth;
using Drx.Sdk.Network.Http.Utilities;
using Drx.Sdk.Network.Http.Entry;
using Drx.Sdk.Network.Http.Session;
using Drx.Sdk.Network.Http.Authorization;
using Drx.Sdk.Network.Http.Commands;
using Drx.Sdk.Network.DataBase;
using Drx.Sdk.Network.Http.Models;

namespace Drx.Sdk.Network.Http
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
    /// DrxHttpServer 已拆分为多个 partial 文件以便于维护。
    /// 各部分功能如下：
    /// - DrxHttpServer.cs (base): 核心定义、字段、构造函数
    /// - DrxHttpServer.Routing.cs: 路由添加与匹配
    /// - DrxHttpServer.Middleware.cs: 中间件管理
    /// - DrxHttpServer.RequestProcessing.cs: 请求处理生命周期
    /// - DrxHttpServer.StaticContent.cs: 静态资源服务与基于Hash的缓存（HTML/CSS/JS/图片等）
    /// - DrxHttpServer.FileServing.cs: 二进制文件传输（上传/下载/Range断点续传）
    /// - DrxHttpServer.RateLimit.cs: 限流控制
    /// - DrxHttpServer.SerializationHelpers.cs: 序列化和辅助工具
    /// - DrxHttpServer.HandlerRegistration.cs: 处理程序反射注册
    /// - DrxHttpServer.SessionAuthCommands.cs: 会话/认证/命令管理
    /// - DrxHttpServer.Ticker.cs: 高性能定时器
    /// - DrxHttpServer.Email.cs: 邮件发送
    /// </summary>
    public partial class DrxHttpServer : IAsyncDisposable
    {
        /// <summary>
        /// 文件根目录路径，供结果类型（如 HtmlResultFromFile / FileResult）以相对路径定位文件。
        /// 如果为 null 或空，表示不启用基于根目录的相对路径解析，处理者应使用绝对路径或 AddFileRoute。
        /// 默认为 null。
        /// </summary>
        public string? FileRootPath { get; set; }

        /// <summary>
        /// 自定义 404 页面的文件路径（绝对路径）
        /// 当请求未匹配任何路由和静态文件时，返回此 HTML 页面作为 404 响应。
        /// 如果为 null 或文件不存在，则回退到默认纯文本 "Not Found"。
        /// </summary>
        public string? NotFoundPagePath { get; set; }

        /// <summary>
        /// 页面根目录（与 FileRootPath 分离）
        /// - 用于存放服务器端渲染或页面模板（如 .html/.htm/.css/.js 等）的目录
        /// - 在解析相对页面路径时优先从 ViewRoot 查找，找不到再回退到 FileRootPath
        /// </summary>
        public string? ViewRoot { get; set; }

        /// <summary>
        /// 配置全局 JSON 序列化策略
        /// 默认为链式回退模式（先反射，后安全模式）
        /// </summary>
        /// <param name="serializer">自定义序列化器，或 null 则恢复默认配置</param>
        public static void ConfigureJsonSerializer(IDrxJsonSerializer? serializer)
        {
            if (serializer == null)
            {
                DrxJsonSerializerManager.ConfigureChainedMode();
            }
            else
            {
                DrxJsonSerializerManager.ConfigureCustom(serializer);
            }
        }

        /// <summary>
        /// 配置 JSON 序列化为反射模式
        /// 适合开发环境和非裁剪部署（但启用 PublishTrimmed 时需要为相关类型添加 DynamicDependency）
        /// </summary>
        public static void ConfigureJsonSerializerReflectionMode()
        {
            DrxJsonSerializerManager.ConfigureReflectionMode();
        }

        /// <summary>
        /// 配置 JSON 序列化为安全模式
        /// 适合启用代码裁剪（PublishTrimmed/NativeAOT）的环境，包含自动回退机制
        /// </summary>
        public static void ConfigureJsonSerializerSafeMode()
        {
            DrxJsonSerializerManager.ConfigureSafeMode();
        }

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

                if (Path.IsPathRooted(pathOrIndicator))
                {
                    var abs = Path.GetFullPath(pathOrIndicator);
                    return File.Exists(abs) ? abs : null;
                }

                if (!string.IsNullOrEmpty(ViewRoot))
                {
                    var trimmedView = pathOrIndicator.TrimStart('/', '\\');
                    var candidateView = Path.Combine(ViewRoot, trimmedView);
                    var fullView = Path.GetFullPath(candidateView);
                    if (File.Exists(fullView)) return fullView;
                }

                if (!string.IsNullOrEmpty(FileRootPath))
                {
                    var trimmed = pathOrIndicator.TrimStart('/', '\\');
                    var candidate = Path.Combine(FileRootPath, trimmed);
                    var full = Path.GetFullPath(candidate);
                    if (File.Exists(full)) return full;
                }

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
        /// <summary>
        /// 路由表快照（不可变引用）。读取路径无锁——直接读 volatile 引用。
        /// 写入路径通过 _routesLock 保护，创建新 Dictionary 后原子替换引用。
        /// </summary>
        private volatile System.Collections.Generic.Dictionary<HttpMethod, List<RouteEntry>> _routesByMethod = new();
        private readonly object _routesLock = new();
        private readonly System.Collections.Generic.Dictionary<string, string> _rateLimitKeyCache = new();
        private readonly object _rateLimitKeyCacheLock = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RouteEntry?> _routeCache = new();
        private readonly object _routeCacheLock = new();
        private readonly System.Collections.Generic.List<(string Template, Func<HttpListenerContext, Task> Handler, int RateLimitMaxRequests, int RateLimitWindowSeconds, Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? RateLimitCallback)> _rawRoutes = new();
        private readonly string? _staticFileRoot;
        private readonly List<MiddlewareEntry> _middlewares = new();
        private int _middlewareCounter = 0;
        private List<MiddlewareEntry>? _cachedSortedMiddlewares = null;
        private readonly object _middlewareCacheLock = new();
        private readonly SessionManager _sessionManager;
        private readonly AuthorizationManager _authorizationManager;
        private readonly SqliteV2<AuthAppDataModel> _authAppDatabase;
        private readonly DataPersistentManager _dataPersistentManager;
        private readonly CommandManager _commandManager;
        private InteractiveCommandConsole? _interactiveConsole;
        private Task? _interactiveConsoleTask;

        private CancellationTokenSource _cts;
        private readonly Channel<HttpListenerContext> _requestChannel;
        private readonly ConcurrentDictionary<int, long> _requestEnqueueTimestamps = new();
        private readonly AdaptiveConcurrencyLimiter _concurrencyLimiter;
        private readonly ThreadPoolManager _threadPool;
        private volatile int _perMessageProcessingDelayMs = 0;

        /// <summary>
        /// 服务器配置选项（不可变，构造后只读）
        /// </summary>
        internal readonly DrxHttpServerOptions _options;

        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, TickerEntry> _tickers;
        private int _tickerIdCounter = 0;
        private Thread? _tickerThread;
        private AutoResetEvent? _tickerWake;
        private readonly object _tickerLock = new();

        private readonly Channel<CommandQueueEntry> _commandInputChannel = Channel.CreateUnbounded<CommandQueueEntry>();
        public Func<string, string, Task>? OnCommandCompleted { get; set; }

        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Generic.Queue<DateTime>> _ipRequestHistory = new();
        private int _rateLimitMaxRequests = 0;
        private TimeSpan _rateLimitWindow = TimeSpan.Zero;
        private readonly object _rateLimitLock = new object();

        public Func<int, HttpRequest, Task>? OnGlobalRateLimitExceeded { get; set; }

        public Func<int, HttpRequest, string, Task>? OnRouteRateLimitExceeded { get; set; }

        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Generic.Queue<DateTime>> _ipRouteRequestHistory = new();

        private TokenBucketManager? _tokenBucketManager;

        private RouteMatchCache? _routeMatchCache;


        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="prefixes">监听前缀，如 "http://localhost:8080/"</param>
        /// <param name="staticFileRoot">静态文件根目录（可为 null）</param>
        /// <param name="sessionTimeoutMinutes">会话超时时间（分钟），默认30分钟</param>
        public DrxHttpServer(IEnumerable<string> prefixes, string? staticFileRoot = null, int sessionTimeoutMinutes = 30)
            : this(prefixes, staticFileRoot, new DrxHttpServerOptions { SessionTimeoutMinutes = sessionTimeoutMinutes })
        {
        }

        /// <summary>
        /// 构造函数（带完整配置选项）
        /// </summary>
        /// <param name="prefixes">监听前缀，如 "http://localhost:8080/"</param>
        /// <param name="staticFileRoot">静态文件根目录（可为 null）</param>
        /// <param name="options">服务器配置选项</param>
        public DrxHttpServer(IEnumerable<string> prefixes, string? staticFileRoot, DrxHttpServerOptions options)
        {
            _options = options ?? DrxHttpServerOptions.Default;
            _options.Validate();

            _listener = new HttpListener();
            foreach (var prefix in prefixes)
            {
                _listener.Prefixes.Add(prefix);
            }
            _staticFileRoot = staticFileRoot;
            _fileRoutes = new List<(string Prefix, string RootDir)>();
            _rawRoutes = new System.Collections.Generic.List<(string Template, Func<HttpListenerContext, Task> Handler, int RateLimitMaxRequests, int RateLimitWindowSeconds, Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? RateLimitCallback)>();
            _requestChannel = Channel.CreateBounded<HttpListenerContext>(new BoundedChannelOptions(_options.ChannelCapacity)
            {
                // 使用 Wait 模式而非 DropOldest：DropOldest 会丢弃旧的 HttpListenerContext 而不关闭它们，
                // 导致客户端连接永久挂起（无响应），最终占满服务器连接数。
                // Wait 模式在 channel 满时会施加反压（backpressure），让 ListenAsync 暂停接收新连接。
                FullMode = BoundedChannelFullMode.Wait
            });

            // 自适应并发控制器：替代硬编码的 SemaphoreSlim(100)
            // 根据排队延迟动态调整并发上限，在 [MinConcurrency, MaxConcurrency] 范围内 AIMD 调节
            _concurrencyLimiter = new AdaptiveConcurrencyLimiter(
                initialLimit: _options.MaxConcurrentRequests,
                minConcurrency: _options.AdaptiveMinConcurrency,
                maxConcurrency: _options.AdaptiveMaxConcurrency,
                targetLatencyMs: _options.AdaptiveTargetQueueLatencyMs
            );

            // 核心亲和线程池：per-core Worker 绑定 CPU 核心，支持工作窃取
            _threadPool = new ThreadPoolManager(
                workerCount: _options.ResolvedWorkerCount,
                enableAffinity: _options.EnableCoreAffinity,
                perCoreCapacity: _options.PerCoreQueueCapacity,
                overflowCapacity: _options.OverflowQueueCapacity
            );

            _sessionManager = new SessionManager(_options.SessionTimeoutMinutes);
            _authorizationManager = new AuthorizationManager(5);
            _authAppDatabase = new SqliteV2<AuthAppDataModel>("drx_http_auth_apps.db", AppDomain.CurrentDomain.BaseDirectory);
            _commandManager = new CommandManager();

            _dataPersistentManager = new DataPersistentManager();

            _tokenBucketManager = new TokenBucketManager();

            _routeMatchCache = new RouteMatchCache(_options.RouteCacheMaxSize);

            DrxJsonSerializerManager.ConfigureChainedMode();

            _tickers = new System.Collections.Concurrent.ConcurrentDictionary<int, TickerEntry>();
            _tickerWake = new AutoResetEvent(false);

            Logger.Info($"DrxHttpServer 初始化完成: workers={_options.ResolvedWorkerCount}, affinity={_options.EnableCoreAffinity}, maxConcurrency={_options.MaxConcurrentRequests}");
        }

    }
}
