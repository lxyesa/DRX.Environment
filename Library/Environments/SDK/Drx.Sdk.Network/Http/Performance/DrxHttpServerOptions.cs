using System;
using System.Collections.Generic;
using System.Threading;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// DrxHttpServer 配置选项。
    /// 集中管理服务器的并发、线程池、核心亲和、缓存等关键参数。
    /// 可在构造 DrxHttpServer 时传入，替代之前的硬编码常量。
    /// </summary>
    public sealed class DrxHttpServerOptions
    {
        /// <summary>
        /// 最大并发请求数。
        /// 控制同时处理的请求上限（通过 per-core Worker 的总并发度实现）。
        /// 默认值：Environment.ProcessorCount × 16。
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = Math.Max(64, Environment.ProcessorCount * 16);

        /// <summary>
        /// 请求 Channel 容量。
        /// ListenAsync 接受的连接排队容量，超出时施加背压。
        /// 默认值：1000。
        /// </summary>
        public int ChannelCapacity { get; set; } = 1000;

        /// <summary>
        /// 是否启用 CPU 核心亲和性绑定。
        /// 启用后每个 Worker 线程绑定到独立 CPU 核心，减少上下文切换和缓存失效。
        /// 默认值：true。
        /// </summary>
        public bool EnableCoreAffinity { get; set; } = true;

        /// <summary>
        /// Worker 线程数量。
        /// 0 表示自动（等于逻辑处理器数量）。
        /// 默认值：0（自动）。
        /// </summary>
        public int WorkerCount { get; set; } = 0;

        /// <summary>
        /// 每核心任务队列容量。
        /// 每个 Worker 核心的本地队列大小，队列满时自动溢出到全局队列。
        /// 默认值：256。
        /// </summary>
        public int PerCoreQueueCapacity { get; set; } = 256;

        /// <summary>
        /// 溢出队列容量。
        /// 当所有 per-core 队列满时使用的全局回退队列容量。
        /// 默认值：1024。
        /// </summary>
        public int OverflowQueueCapacity { get; set; } = 1024;

        /// <summary>
        /// 路由匹配缓存最大条目数。
        /// 默认值：2048。
        /// </summary>
        public int RouteCacheMaxSize { get; set; } = 2048;

        /// <summary>
        /// 是否启用自适应并发控制。
        /// 启用后根据排队延迟和 CPU 利用率动态调整并发上限。
        /// 默认值：true。
        /// </summary>
        public bool EnableAdaptiveConcurrency { get; set; } = true;

        /// <summary>
        /// 自适应并发控制的目标排队延迟阈值（毫秒）。
        /// 排队延迟低于此值时增加并发，高于此值时减少并发。
        /// 默认值：50ms。
        /// </summary>
        public double AdaptiveTargetQueueLatencyMs { get; set; } = 50.0;

        /// <summary>
        /// 自适应并发控制的最小并发数。
        /// 默认值：Environment.ProcessorCount。
        /// </summary>
        public int AdaptiveMinConcurrency { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// 自适应并发控制的最大并发数。
        /// 默认值：Environment.ProcessorCount × 32。
        /// </summary>
        public int AdaptiveMaxConcurrency { get; set; } = Math.Max(128, Environment.ProcessorCount * 32);

        /// <summary>
        /// 慢请求告警阈值（毫秒）。
        /// 处理时间超过此值的请求会被记录为慢请求。
        /// 默认值：1000ms。
        /// </summary>
        public int SlowRequestWarnThresholdMs { get; set; } = 1000;

        /// <summary>
        /// 会话超时时间（分钟）。
        /// 默认值：30。
        /// </summary>
        public int SessionTimeoutMinutes { get; set; } = 30;

        // ── 新增：并发队列联动参数 ─────────────────────────────────────────────

        /// <summary>
        /// 队列满时的背压警告水位线（百分比，0~100）。
        /// 当排队中的请求数超过 ChannelCapacity × (WatermarkPercent/100) 时，
        /// 自适应控制器将提前触发减速（MD 步长加倍），阻止拥塞持续放大。
        /// 默认值：80（即队列 80% 满时触发）。
        /// </summary>
        public int QueueWatermarkPercent { get; set; } = 80;

        /// <summary>
        /// 是否在队列深度超过水位线时记录告警日志。
        /// 默认值：true。
        /// </summary>
        public bool LogQueueWatermarkWarnings { get; set; } = true;

        /// <summary>
        /// 并发窗口自适应调整的采样窗口大小（请求数）。
        /// 每处理此数量的请求后重新评估并发阈值。
        /// 默认值：50（与 AdaptiveConcurrencyLimiter 内部 SampleWindowSize 联动）。
        /// 0 表示使用内部默认。
        /// </summary>
        public int AdaptiveSampleWindowSize { get; set; } = 0;

        // ── 压缩策略 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 是否启用响应压缩（Gzip / Brotli 自动协商）。
        /// 启用后按内容类型与体积阈值决定是否压缩，降低出站流量。
        /// 默认值：true。
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// 触发压缩的最小响应体积（字节）。
        /// 小于此阈值的响应不压缩（压缩收益低且 CPU 开销相对较大）。
        /// 默认值：1024（1 KB）。
        /// </summary>
        public int CompressionMinSizeBytes { get; set; } = 1024;

        /// <summary>
        /// 压缩级别（0=最快/最低压缩；9=最慢/最高压缩；推荐 1~2 控制 CPU 增幅）。
        /// 默认值：1（最快档，CPU 增幅最小）。
        /// </summary>
        public int CompressionLevel { get; set; } = 1;

        /// <summary>
        /// 允许压缩的内容类型列表（ContentType 前缀匹配）。
        /// 已压缩格式（image/png, video/*, application/zip 等）不在此列表中。
        /// 修改此列表可精细控制哪些类型参与压缩。
        /// </summary>
        public HashSet<string> CompressibleContentTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            "text/",
            "application/json",
            "application/javascript",
            "application/xml",
            "application/xhtml",
            "application/atom",
            "application/rss",
            "application/ld+json",
            "application/manifest",
            "image/svg+xml",
        };

        /// <summary>
        /// CPU 占用率守护阈值（百分比，0~100）。
        /// 采样 CPU % 增幅超过此值时自动将 EnableCompression 暂时置为 false（降级）。
        /// 默认值：3（对应 R2 的 CPU ≤ 3% 增幅约束）。
        /// </summary>
        public double CompressionCpuGuardPercent { get; set; } = 3.0;

        /// <summary>
        /// CPU 守护采样间隔（秒）。
        /// 默认值：10。
        /// </summary>
        public int CompressionCpuSampleIntervalSeconds { get; set; } = 10;

        /// <summary>
        /// CPU 守护冷却时间（秒）。
        /// 触发降级后至少等待此时长再尝试恢复压缩。
        /// 默认值：60。
        /// </summary>
        public int CompressionCpuCooldownSeconds { get; set; } = 60;

        // ── 开发态运行时 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 开发态前端运行时配置。
        /// 默认关闭，避免生产环境暴露调试能力。
        /// </summary>
        public DevRuntimeOptions DevRuntime { get; set; } = DevRuntimeOptions.CreateDefault();

        /// <summary>
        /// 获取实际 Worker 数量（解析 0 为自动值）
        /// </summary>
        internal int ResolvedWorkerCount => WorkerCount > 0 ? WorkerCount : Environment.ProcessorCount;

        /// <summary>
        /// 验证配置参数的合法性
        /// </summary>
        internal void Validate()
        {
            if (MaxConcurrentRequests <= 0) MaxConcurrentRequests = 64;
            if (ChannelCapacity <= 0) ChannelCapacity = 1000;
            if (PerCoreQueueCapacity <= 0) PerCoreQueueCapacity = 256;
            if (OverflowQueueCapacity <= 0) OverflowQueueCapacity = 1024;
            if (RouteCacheMaxSize <= 0) RouteCacheMaxSize = 2048;
            if (SlowRequestWarnThresholdMs <= 0) SlowRequestWarnThresholdMs = 1000;
            if (SessionTimeoutMinutes <= 0) SessionTimeoutMinutes = 30;
            if (AdaptiveMinConcurrency <= 0) AdaptiveMinConcurrency = Environment.ProcessorCount;
            if (AdaptiveMaxConcurrency < AdaptiveMinConcurrency) AdaptiveMaxConcurrency = AdaptiveMinConcurrency * 4;
            if (AdaptiveTargetQueueLatencyMs <= 0) AdaptiveTargetQueueLatencyMs = 50.0;
            if (QueueWatermarkPercent < 0 || QueueWatermarkPercent > 100) QueueWatermarkPercent = 80;

            // 压缩参数校验
            if (CompressionMinSizeBytes < 0) CompressionMinSizeBytes = 0;
            if (CompressionLevel < 0) CompressionLevel = 0;
            if (CompressionLevel > 9) CompressionLevel = 9;
            if (CompressionCpuGuardPercent <= 0 || CompressionCpuGuardPercent > 100) CompressionCpuGuardPercent = 3.0;
            if (CompressionCpuSampleIntervalSeconds <= 0) CompressionCpuSampleIntervalSeconds = 10;
            if (CompressionCpuCooldownSeconds <= 0) CompressionCpuCooldownSeconds = 60;
            CompressibleContentTypes ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            DevRuntime ??= DevRuntimeOptions.CreateDefault();
            DevRuntime.Validate();
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static DrxHttpServerOptions Default => new();

        /// <summary>
        /// 创建高吞吐优化预设（适合高并发请求处理）。
        /// 扩大 ChannelCapacity 与并发范围，降低自适应目标延迟阈值以更敏感地响应负载。
        /// </summary>
        public static DrxHttpServerOptions HighThroughput()
        {
            var cores = Environment.ProcessorCount;
            return new DrxHttpServerOptions
            {
                MaxConcurrentRequests = Math.Max(128, cores * 32),
                ChannelCapacity = 4000,
                PerCoreQueueCapacity = 512,
                OverflowQueueCapacity = 4096,
                EnableAdaptiveConcurrency = true,
                AdaptiveTargetQueueLatencyMs = 30.0,
                AdaptiveMinConcurrency = Math.Max(cores, 16),
                AdaptiveMaxConcurrency = Math.Max(512, cores * 64),
                QueueWatermarkPercent = 75,
            };
        }

        /// <summary>
        /// 创建低延迟优化预设（适合小包高并发，保护尾延迟）。
        /// 更小的并发上限与更严格的水位线，防止队列积压放大 P99。
        /// </summary>
        public static DrxHttpServerOptions LowLatency()
        {
            var cores = Environment.ProcessorCount;
            return new DrxHttpServerOptions
            {
                MaxConcurrentRequests = Math.Max(64, cores * 8),
                ChannelCapacity = 2000,
                PerCoreQueueCapacity = 256,
                OverflowQueueCapacity = 2048,
                EnableAdaptiveConcurrency = true,
                AdaptiveTargetQueueLatencyMs = 20.0,
                AdaptiveMinConcurrency = Math.Max(cores / 2, 4),
                AdaptiveMaxConcurrency = Math.Max(128, cores * 16),
                QueueWatermarkPercent = 60,
            };
        }

        /// <summary>
        /// 根据客户端配置推导出联动的服务端配置建议，
        /// 确保客户端并发上限与服务端队列容量匹配，避免"连接池饱和 + 队列堆积"双重放大。
        /// </summary>
        /// <param name="clientOptions">客户端连接池配置</param>
        /// <returns>调整后的服务端选项（对当前实例的副本）</returns>
        public DrxHttpServerOptions WithClientOptionsAligned(DrxHttpClientOptions clientOptions)
        {
            if (clientOptions == null || !clientOptions.Enabled) return this;

            // 服务端 ChannelCapacity 应不低于客户端 MaxConcurrentRequests × 安全系数 (3)
            var minChannelCapacity = clientOptions.MaxConcurrentRequests * 3;
            if (ChannelCapacity < minChannelCapacity)
            {
                ChannelCapacity = minChannelCapacity;
                Logger.Info($"[ConnectionStrategy] 服务端 ChannelCapacity 联动调整为 {ChannelCapacity}（客户端 MaxConcurrentRequests={clientOptions.MaxConcurrentRequests}）");
            }

            // 服务端并发上限应 ≥ 客户端最大并发数
            if (MaxConcurrentRequests < clientOptions.MaxConcurrentRequests)
            {
                MaxConcurrentRequests = clientOptions.MaxConcurrentRequests * 2;
                Logger.Info($"[ConnectionStrategy] 服务端 MaxConcurrentRequests 联动调整为 {MaxConcurrentRequests}");
            }

            return this;
        }
    }

    /// <summary>
    /// 自适应并发控制器：根据运行时指标动态调整并发上限。
    /// 
    /// 算法：AIMD（Additive Increase, Multiplicative Decrease）
    ///   - 当平均排队延迟 < 目标阈值：线性增加并发上限（+1）
    ///   - 当平均排队延迟 > 目标阈值 × 2：乘法减少并发上限（×0.75）
    ///   - 有上下界限制，避免过度振荡
    /// 
    /// 采样窗口：每 N 个请求重新评估一次（避免频繁调整）
    /// </summary>
    internal sealed class AdaptiveConcurrencyLimiter
    {
        private readonly int _minConcurrency;
        private readonly int _maxConcurrency;
        private readonly double _targetLatencyMs;
        private int _currentLimit;
        private readonly SemaphoreSlim _semaphore;

        // 滑动窗口采样
        private long _sampleCount;
        private double _sampleLatencySum;
        private readonly object _sampleLock = new();
        private const int SampleWindowSize = 50; // 每50个请求评估一次

        /// <summary>
        /// 当前并发上限
        /// </summary>
        public int CurrentLimit => Volatile.Read(ref _currentLimit);

        /// <summary>
        /// 当前可用的并发槽位数
        /// </summary>
        public int AvailableCount => _semaphore.CurrentCount;

        public AdaptiveConcurrencyLimiter(int initialLimit, int minConcurrency, int maxConcurrency, double targetLatencyMs)
        {
            _minConcurrency = Math.Max(1, minConcurrency);
            _maxConcurrency = Math.Max(_minConcurrency, maxConcurrency);
            _targetLatencyMs = Math.Max(1.0, targetLatencyMs);
            _currentLimit = Math.Clamp(initialLimit, _minConcurrency, _maxConcurrency);
            _semaphore = new SemaphoreSlim(_currentLimit, _maxConcurrency);
        }

        /// <summary>
        /// 等待获取一个并发槽位
        /// </summary>
        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            return _semaphore.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// 释放并发槽位，并报告本次请求的排队延迟用于自适应调整。
        /// </summary>
        /// <param name="queueLatencyMs">本次请求在队列中等待的时间（毫秒）</param>
        public void Release(double queueLatencyMs)
        {
            RecordSample(queueLatencyMs);

            try
            {
                _semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // 并发限制被降低后可能出现多余的 Release
            }
        }

        private void RecordSample(double latencyMs)
        {
            bool shouldAdjust = false;
            double avgLatency = 0;

            lock (_sampleLock)
            {
                _sampleCount++;
                _sampleLatencySum += latencyMs;

                if (_sampleCount >= SampleWindowSize)
                {
                    avgLatency = _sampleLatencySum / _sampleCount;
                    _sampleCount = 0;
                    _sampleLatencySum = 0;
                    shouldAdjust = true;
                }
            }

            if (shouldAdjust)
            {
                AdjustLimit(avgLatency);
            }
        }

        private void AdjustLimit(double avgLatencyMs)
        {
            int current = Volatile.Read(ref _currentLimit);
            int newLimit;

            if (avgLatencyMs < _targetLatencyMs)
            {
                // 延迟低于目标：缓慢增加（Additive Increase）
                newLimit = Math.Min(current + 1, _maxConcurrency);
            }
            else if (avgLatencyMs > _targetLatencyMs * 2)
            {
                // 延迟远高于目标：快速减少（Multiplicative Decrease）
                newLimit = Math.Max((int)(current * 0.75), _minConcurrency);
            }
            else
            {
                // 延迟在目标范围内：保持不变
                return;
            }

            if (newLimit == current) return;

            var oldLimit = Interlocked.Exchange(ref _currentLimit, newLimit);

            // 调整 semaphore 容量
            if (newLimit > oldLimit)
            {
                // 增加可用槽位
                int delta = newLimit - oldLimit;
                for (int i = 0; i < delta; i++)
                {
                    try { _semaphore.Release(); }
                    catch (SemaphoreFullException) { break; }
                }
            }
            // 减少时不主动收回，让自然消耗降低并发度

            Logger.Info($"自适应并发调整: {oldLimit} → {newLimit} (avgLatency={avgLatencyMs:F1}ms, target={_targetLatencyMs:F1}ms)");
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }
}
