using System;
using System.Net.Http;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// HTTP 请求通用重试策略。
    /// 
    /// 设计要点：
    ///   - 指数退避 + full jitter，避免惊群与幂次放大。
    ///   - 默认仅对幂等方法（GET/HEAD）开启；非幂等方法（POST/PUT/DELETE/PATCH）默认不重试。
    ///   - 重试次数与每次延迟均计入超时预算，防止尾延迟放大。
    ///   - 独立开关（Enabled），可通过配置快速回滚到无重试行为。
    /// 
    /// 使用方式：
    ///   在 <see cref="DrxHttpClientOptions"/> 中设置 <c>RetryPolicy</c> 属性即可生效，
    ///   不需要调用方修改业务代码。
    /// </summary>
    public sealed class HttpRetryPolicy
    {
        // ── 幂等方法白名单 ──────────────────────────────────────────────────────

        /// <summary>
        /// 默认允许自动重试的 HTTP 方法（幂等方法）。
        /// RFC 7231：GET、HEAD、OPTIONS、TRACE 为安全/幂等方法；PUT、DELETE 幂等但非安全。
        /// 本策略保守实现：默认仅对 GET/HEAD 自动重试，其余方法须显式启用。
        /// </summary>
        private static readonly System.Net.Http.HttpMethod[] DefaultIdempotentMethods =
        {
            System.Net.Http.HttpMethod.Get,
            System.Net.Http.HttpMethod.Head,
        };

        // ── 核心参数 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 是否启用自动重试（总开关）。
        /// false 时完全不重试，与未启用策略行为一致，可一键回退。
        /// 默认值：true。
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 最大重试次数（不含首次请求）。
        /// 0 表示不重试；-1 表示无限重试（不推荐，应配合超时预算使用）。
        /// 默认值：3。
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// 初始退避基准延迟（毫秒）。
        /// 首次重试前等待时间的随机范围上界为 InitialDelayMs。
        /// 默认值：200ms。
        /// </summary>
        public int InitialDelayMs { get; set; } = 200;

        /// <summary>
        /// 退避延迟上限（毫秒）。
        /// 无论指数增长到多大，单次等待不超过此值。
        /// 默认值：10000ms（10 秒）。
        /// </summary>
        public int MaxDelayMs { get; set; } = 10000;

        /// <summary>
        /// 指数退避倍率。
        /// 第 n 次重试的最大延迟 = InitialDelayMs × (Multiplier ^ n)，再 clamp 到 MaxDelayMs。
        /// 默认值：2.0（标准二进制指数退避）。
        /// </summary>
        public double Multiplier { get; set; } = 2.0;

        /// <summary>
        /// 是否使用 full jitter（均匀随机 [0, 计算延迟]）。
        /// true：full jitter，分散重试峰值，降低服务端压力；
        /// false：deterministic exponential（固定值，调试方便但不推荐生产）。
        /// 默认值：true。
        /// </summary>
        public bool UseJitter { get; set; } = true;

        /// <summary>
        /// 允许自动重试的 HTTP 方法列表。
        /// null 表示使用内置默认白名单（GET/HEAD）。
        /// 若需对所有方法重试，传入 null 并设置 AllowNonIdempotentRetry = true。
        /// </summary>
        public System.Net.Http.HttpMethod[]? AllowedMethods { get; set; } = null;

        /// <summary>
        /// 是否允许对非幂等方法（POST/PUT/DELETE/PATCH 等）进行重试。
        /// 危险选项：仅在确认接口幂等性且业务侧显式要求时开启。
        /// 默认值：false（R4 约束：POST 默认不重试）。
        /// </summary>
        public bool AllowNonIdempotentRetry { get; set; } = false;

        /// <summary>
        /// 超时预算（毫秒）。
        /// 所有重试（含等待时间）的总时间预算。超出预算后放弃重试并抛出原始异常。
        /// 0 或负数表示不设预算限制（依赖 HttpClient.Timeout）。
        /// 默认值：0（依赖 DrxHttpClientOptions.RequestTimeoutSeconds）。
        /// </summary>
        public int TimeoutBudgetMs { get; set; } = 0;

        // ── 随机数源（线程安全） ──────────────────────────────────────────────

        private static readonly Random _sharedRandom = new();
        private static readonly object _randomLock = new();

        // ── 工厂方法 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 默认策略：GET/HEAD，最多 3 次，200ms 起步，最大 10s，full jitter，无超时预算额外限制。
        /// </summary>
        public static HttpRetryPolicy Default => new();

        /// <summary>
        /// 不重试策略（等同于 Enabled = false）。
        /// </summary>
        public static HttpRetryPolicy None => new() { Enabled = false, MaxRetries = 0 };

        /// <summary>
        /// 激进重试策略：最多 5 次，100ms 起步，5s 上限，full jitter。
        /// 适合短暂抖动频繁的内网环境，仅对 GET/HEAD 生效。
        /// </summary>
        public static HttpRetryPolicy Aggressive => new()
        {
            MaxRetries = 5,
            InitialDelayMs = 100,
            MaxDelayMs = 5000,
            Multiplier = 2.0,
            UseJitter = true,
        };

        /// <summary>
        /// 保守重试策略：最多 2 次，500ms 起步，15s 上限，full jitter。
        /// 适合出口流量成本敏感的外网调用。
        /// </summary>
        public static HttpRetryPolicy Conservative => new()
        {
            MaxRetries = 2,
            InitialDelayMs = 500,
            MaxDelayMs = 15000,
            Multiplier = 3.0,
            UseJitter = true,
        };

        // ── 核心方法 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 判断指定请求方法是否在本策略的重试白名单内。
        /// </summary>
        /// <param name="method">HTTP 请求方法。</param>
        /// <returns>true 表示允许重试；false 表示不允许。</returns>
        public bool IsMethodRetryable(System.Net.Http.HttpMethod method)
        {
            if (!Enabled) return false;
            if (AllowNonIdempotentRetry) return true;

            var allowed = AllowedMethods ?? DefaultIdempotentMethods;
            foreach (var m in allowed)
            {
                if (m == method) return true;
            }
            return false;
        }

        /// <summary>
        /// 判断给定的异常是否属于可重试的瞬时故障。
        /// 
        /// 可重试：网络 I/O 异常（HttpRequestException）、超时（TaskCanceledException）。
        /// 不可重试：业务逻辑异常、参数异常等。
        /// </summary>
        /// <param name="ex">捕获到的异常。</param>
        /// <returns>true 表示可重试；false 表示不可重试。</returns>
        public bool IsTransientException(Exception ex)
        {
            return ex is HttpRequestException
                || ex is TaskCanceledException
                || ex is System.IO.IOException
                || (ex.InnerException != null && IsTransientException(ex.InnerException));
        }

        /// <summary>
        /// 判断给定的 HTTP 状态码是否应触发重试。
        /// 可重试状态：429（限流）、502/503/504（上游不可用）、408（请求超时）。
        /// </summary>
        /// <param name="statusCode">HTTP 响应状态码。</param>
        /// <returns>true 表示应重试；false 表示不应重试。</returns>
        public bool IsRetryableStatusCode(int statusCode)
        {
            return statusCode is 408 or 429 or 502 or 503 or 504;
        }

        /// <summary>
        /// 计算第 <paramref name="attempt"/> 次重试（0-based）的等待时间（毫秒）。
        /// 使用 full jitter：delay = random(0, min(InitialDelayMs × Multiplier^attempt, MaxDelayMs))。
        /// </summary>
        /// <param name="attempt">当前为第几次重试（0 表示第一次重试）。</param>
        /// <returns>等待毫秒数（≥ 0）。</returns>
        public int CalculateDelay(int attempt)
        {
            // 计算指数退避上限
            double rawDelay = InitialDelayMs * Math.Pow(Multiplier, attempt);
            int cappedDelay = (int)Math.Min(rawDelay, MaxDelayMs);
            cappedDelay = Math.Max(cappedDelay, 0);

            if (!UseJitter || cappedDelay == 0)
                return cappedDelay;

            // Full jitter：均匀随机 [0, cappedDelay]
            lock (_randomLock)
            {
                return _sharedRandom.Next(0, cappedDelay + 1);
            }
        }

        /// <summary>
        /// 计算所有重试（不含首次请求）的最坏情况总等待时间（毫秒）。
        /// 用于预估超时预算消耗，实际因 jitter 通常低于此值。
        /// </summary>
        public int EstimateWorstCaseTotalDelayMs()
        {
            if (!Enabled || MaxRetries <= 0) return 0;

            int total = 0;
            int count = MaxRetries < 0 ? 10 : MaxRetries; // 无限重试取 10 次估算
            for (int i = 0; i < count; i++)
            {
                double rawDelay = InitialDelayMs * Math.Pow(Multiplier, i);
                total += (int)Math.Min(rawDelay, MaxDelayMs);
            }
            return total;
        }
    }
}
