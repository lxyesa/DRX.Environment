using System;
using System.Net;
using System.Net.Http;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// DrxHttpClient 连接池与并发配置选项。
    /// 集中管理客户端的连接复用、并发控制、队列容量等关键参数，
    /// 与 DrxHttpServerOptions 联动，形成端到端的策略化控制。
    /// 
    /// 默认值与历史硬编码行为完全兼容，调用方无需修改任何代码即可平滑接入。
    /// </summary>
    public sealed class DrxHttpClientOptions
    {
        // ── 连接池参数 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 每个目标服务器的最大并发连接数。
        /// 控制 SocketsHttpHandler.MaxConnectionsPerServer，限制底层 TCP 连接复用池规模。
        /// 默认值：int.MaxValue（无上限，与 .NET 默认一致，保持历史行为兼容）。
        /// 推荐高并发场景：设为 CPU 核心数 × 8，避免 FIN_WAIT 积累。
        /// </summary>
        public int MaxConnectionsPerServer { get; set; } = int.MaxValue;

        /// <summary>
        /// 连接池连接空闲超时（秒）。
        /// 空闲连接超过此时间将被关闭并从池中移除，防止持有过期 TCP 连接。
        /// 默认值：90 秒（与 .NET SocketsHttpHandler 默认 PooledConnectionIdleTimeout 保持一致）。
        /// </summary>
        public int PooledConnectionIdleTimeoutSeconds { get; set; } = 90;

        /// <summary>
        /// 连接存活最长时间（秒）。
        /// 连接复用时间超过此值后将在请求完成时被关闭，防止 DNS 变更和负载均衡漂移。
        /// 默认值：600 秒（10 分钟），0 表示无限制（与老行为兼容）。
        /// </summary>
        public int PooledConnectionLifetimeSeconds { get; set; } = 600;

        /// <summary>
        /// 是否启用连接保活（Keep-Alive）。
        /// 启用后 TCP 层面定期发送 keepalive 探针，尽早发现死连接。
        /// 默认值：true。
        /// </summary>
        public bool EnableConnectionKeepAlive { get; set; } = true;

        // ── 并发控制参数 ───────────────────────────────────────────────────────

        /// <summary>
        /// 客户端最大并发请求数（信号量控制）。
        /// 控制同时向服务端发出的 in-flight 请求数，超出时请求在内部队列中排队。
        /// 默认值：10（与历史 MaxConcurrentRequests 常量保持兼容）。
        /// 推荐高并发批量场景：设为 MaxConnectionsPerServer × 2。
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = 10;

        /// <summary>
        /// 内部请求队列容量（BoundedChannel 容量）。
        /// 超出时 WriteAsync 阻塞（施加背压），防止内存无限增长。
        /// 默认值：100（与历史 Channel.CreateBounded(100) 保持兼容）。
        /// </summary>
        public int RequestQueueCapacity { get; set; } = 100;

        // ── 超时参数 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 请求超时（秒）。
        /// 单次请求从发出到收到完整响应的最长等待时间。
        /// 默认值：30 秒。
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 30;

        // ── 并发自适应参数 ─────────────────────────────────────────────────────

        /// <summary>
        /// 是否启用客户端自适应并发控制。
        /// 启用后根据队列等待时间动态调整 MaxConcurrentRequests，
        /// 兼顾吞吐与尾延迟（AIMD 算法，与服务端 AdaptiveConcurrencyLimiter 思路一致）。
        /// 默认值：false（保持历史行为，需显式开启）。
        /// </summary>
        public bool EnableAdaptiveConcurrency { get; set; } = false;

        /// <summary>
        /// 自适应并发的目标队列等待延迟阈值（毫秒）。
        /// 队列延迟低于此值时增加并发，高于此值 × 2 时减少并发。
        /// 默认值：20ms。仅当 EnableAdaptiveConcurrency = true 时生效。
        /// </summary>
        public double AdaptiveTargetQueueLatencyMs { get; set; } = 20.0;

        /// <summary>
        /// 自适应并发的最小并发数下界。
        /// 默认值：2。
        /// </summary>
        public int AdaptiveMinConcurrency { get; set; } = 2;

        /// <summary>
        /// 自适应并发的最大并发数上界。
        /// 默认值：MaxConcurrentRequests × 4，或至少 64。
        /// </summary>
        public int AdaptiveMaxConcurrency { get; set; } = 0; // 0 = 延迟计算

        // ── 重试策略 ────────────────────────────────────────────────────────────

        /// <summary>
        /// HTTP 请求自动重试策略。
        /// null 表示使用默认策略（GET/HEAD，最多 3 次，指数退避 + full jitter）。
        /// 设置为 <see cref="HttpRetryPolicy.None"/> 可完全禁用重试，与历史无重试行为兼容。
        /// 
        /// 注意：重试预算与 <see cref="RequestTimeoutSeconds"/> 联动——
        ///   若 TimeoutBudgetMs = 0，则实际超时由 HttpClient.Timeout（RequestTimeoutSeconds）控制；
        ///   若需精确控制重试期间的总时间，请在策略中显式设置 TimeoutBudgetMs。
        /// </summary>
        public HttpRetryPolicy? RetryPolicy { get; set; } = HttpRetryPolicy.Default;

        // ── Feature Flags ──────────────────────────────────────────────────────

        /// <summary>
        /// 是否启用此选项对象（总开关）。
        /// false 时所有参数退化为最简默认值，完全不改变原始行为。
        /// 默认值：true。
        /// </summary>
        public bool Enabled { get; set; } = true;

        // ── 大文件传输 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 大文件传输阈值（字节）。
        /// 当单次上传/下载的数据量超过此阈值时，自动切换到更大的分片缓冲区（256KB），
        /// 减少 IO 系统调用次数，提升吞吐。
        /// 默认值：4MB（与 HttpObjectPool.LargeFileThresholdBytes 保持一致）。
        /// 设为 0 表示始终使用大缓冲区；设为 long.MaxValue 表示始终使用标准缓冲区。
        /// </summary>
        public long LargeFileThresholdBytes { get; set; } = HttpObjectPool.LargeFileThresholdBytes;

        /// <summary>
        /// 是否对大文件传输启用指标采集。
        /// 启用后，>= LargeFileThresholdBytes 的传输会记录到 HttpMetrics 的大文件传输统计中，
        /// 可用于量化优化效果（上报次数、吞吐字节、缓冲区租借次数）。
        /// 默认值：true。
        /// </summary>
        public bool EnableLargeFileMetrics { get; set; } = true;

        // ── 工厂方法 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 创建默认配置（与历史硬编码行为完全兼容）。
        /// </summary>
        public static DrxHttpClientOptions Default => new();

        /// <summary>
        /// 创建高并发优化预设（适合批量/高 QPS 场景）。
        /// MaxConnectionsPerServer 设为 CPU 核心数 × 8，
        /// MaxConcurrentRequests 设为 CPU 核心数 × 4，
        /// 队列容量 1000，启用自适应并发。
        /// </summary>
        public static DrxHttpClientOptions HighConcurrency()
        {
            var cores = Environment.ProcessorCount;
            return new DrxHttpClientOptions
            {
                MaxConnectionsPerServer = Math.Max(32, cores * 8),
                MaxConcurrentRequests = Math.Max(16, cores * 4),
                RequestQueueCapacity = 1000,
                EnableAdaptiveConcurrency = true,
                AdaptiveMinConcurrency = Math.Max(4, cores),
                AdaptiveMaxConcurrency = Math.Max(128, cores * 16),
                PooledConnectionIdleTimeoutSeconds = 60,
                PooledConnectionLifetimeSeconds = 300,
            };
        }

        /// <summary>
        /// 创建低延迟优化预设（适合小包高并发场景）。
        /// 更小的连接池防止拥塞放大，保守并发上限保护尾延迟。
        /// </summary>
        public static DrxHttpClientOptions LowLatency()
        {
            var cores = Environment.ProcessorCount;
            return new DrxHttpClientOptions
            {
                MaxConnectionsPerServer = Math.Max(16, cores * 4),
                MaxConcurrentRequests = Math.Max(8, cores * 2),
                RequestQueueCapacity = 500,
                EnableAdaptiveConcurrency = true,
                AdaptiveTargetQueueLatencyMs = 10.0,
                AdaptiveMinConcurrency = Math.Max(2, cores / 2),
                AdaptiveMaxConcurrency = Math.Max(64, cores * 8),
                PooledConnectionIdleTimeoutSeconds = 45,
                PooledConnectionLifetimeSeconds = 180,
            };
        }

        /// <summary>
        /// 创建大文件传输优化预设（适合大文件上传/下载场景）。
        /// 连接数适度减少避免并发放大内存压力，缓冲区选用最大挡位，
        /// 并发上限较低（单个大文件即占满带宽）。
        /// </summary>
        public static DrxHttpClientOptions LargeFileTransfer()
        {
            var cores = Environment.ProcessorCount;
            return new DrxHttpClientOptions
            {
                MaxConnectionsPerServer = Math.Max(8, cores * 2),
                MaxConcurrentRequests = Math.Max(4, cores),
                RequestQueueCapacity = 50,
                EnableAdaptiveConcurrency = false,
                PooledConnectionIdleTimeoutSeconds = 120,
                PooledConnectionLifetimeSeconds = 600,
                RequestTimeoutSeconds = 300,
                // 大文件场景：降低阈值使更多传输进入大缓冲区路径
                LargeFileThresholdBytes = 1 * 1024 * 1024, // 1MB
                EnableLargeFileMetrics = true,
            };
        }

        // ── 验证 ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 验证并修正参数，保证运行时不出现非法值。
        /// </summary>
        internal void Validate()
        {
            if (MaxConnectionsPerServer <= 0) MaxConnectionsPerServer = int.MaxValue;
            if (MaxConcurrentRequests <= 0) MaxConcurrentRequests = 10;
            if (RequestQueueCapacity <= 0) RequestQueueCapacity = 100;
            if (RequestTimeoutSeconds <= 0) RequestTimeoutSeconds = 30;
            if (PooledConnectionIdleTimeoutSeconds <= 0) PooledConnectionIdleTimeoutSeconds = 90;
            if (PooledConnectionLifetimeSeconds < 0) PooledConnectionLifetimeSeconds = 0;
            if (AdaptiveTargetQueueLatencyMs <= 0) AdaptiveTargetQueueLatencyMs = 20.0;
            if (AdaptiveMinConcurrency <= 0) AdaptiveMinConcurrency = 2;
            if (AdaptiveMaxConcurrency <= 0)
                AdaptiveMaxConcurrency = Math.Max(64, MaxConcurrentRequests * 4);
            if (AdaptiveMaxConcurrency < AdaptiveMinConcurrency)
                AdaptiveMaxConcurrency = AdaptiveMinConcurrency * 4;
        }

        /// <summary>
        /// 根据本配置构建 SocketsHttpHandler，应用连接池参数。
        /// 返回已配置好的 handler，供调用方创建 HttpClient 时使用。
        /// </summary>
        internal SocketsHttpHandler BuildSocketsHttpHandler(System.Net.CookieContainer? cookieContainer = null)
        {
            var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = MaxConnectionsPerServer == int.MaxValue
                    ? int.MaxValue
                    : MaxConnectionsPerServer,
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(PooledConnectionIdleTimeoutSeconds),
                KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
                ConnectTimeout = TimeSpan.FromSeconds(Math.Min(RequestTimeoutSeconds, 30)),
            };

            // PooledConnectionLifetime: 0 表示无限制
            if (PooledConnectionLifetimeSeconds > 0)
                handler.PooledConnectionLifetime = TimeSpan.FromSeconds(PooledConnectionLifetimeSeconds);

            // 启用 TCP Keep-Alive（在支持该属性的平台上）
            if (EnableConnectionKeepAlive)
            {
                try
                {
                    handler.KeepAlivePingDelay = TimeSpan.FromSeconds(30);
                    handler.KeepAlivePingTimeout = TimeSpan.FromSeconds(15);
                }
                catch
                {
                    // 旧版 .NET 可能不支持，静默跳过
                }
            }

            if (cookieContainer != null)
            {
                handler.CookieContainer = cookieContainer;
                handler.UseCookies = true;
            }

            return handler;
        }
    }
}
