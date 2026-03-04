using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// HTTP 性能指标采集中心。
    /// 负责统一收集、聚合和输出 HTTP 传输链路的关键性能与流量指标。
    /// 
    /// 采集指标包括：
    ///   - 延迟百分位：P50/P95/P99
    ///   - 吞吐量：QPS、请求/秒
    ///   - 流量指标：出站字节、入站字节、压缩比
    ///   - 缓存指标：304命中率
    ///   - 重试指标：重试次数、重试成功率
    ///   - 队列指标：队列深度、排队延迟
    ///   - 限流指标：限流命中次数
    /// 
    /// 设计原则：
    ///   - 低开销：使用 Interlocked 和 lock-free 结构
    ///   - 滑动窗口：基于时间窗口的统计，避免无限增长
    ///   - 线程安全：支持多线程并发采集
    ///   - 可观测性：支持导出为结构化报告
    /// </summary>
    public sealed class HttpMetrics : IDisposable
    {
        #region 配置常量

        /// <summary>
        /// 滑动窗口大小（秒）
        /// </summary>
        public const int SlidingWindowSeconds = 60;

        /// <summary>
        /// 延迟采样保留的最大样本数
        /// </summary>
        public const int MaxLatencySamples = 10000;

        #endregion

        #region 单例

        private static HttpMetrics? _instance;
        private static readonly object _instanceLock = new();

        /// <summary>
        /// 获取全局单例实例
        /// </summary>
        public static HttpMetrics Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        _instance ??= new HttpMetrics();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region 原子计数器

        // 请求相关
        private long _totalRequests;
        private long _successfulRequests;
        private long _failedRequests;
        private long _timedOutRequests;

        // 流量相关（字节）
        private long _totalBytesReceived;
        private long _totalBytesSent;
        private long _totalCompressedBytesSent;
        private long _totalUncompressedBytesSent;

        // 缓存相关
        private long _conditionalRequests;    // 带 If-None-Match/If-Modified-Since 的请求
        private long _notModifiedResponses;   // 304 响应数
        private long _cacheHits;
        private long _cacheMisses;

        // 重试相关
        private long _totalRetries;
        private long _successfulRetries;
        private long _failedRetries;

        // 限流相关
        private long _rateLimitHits;

        // 队列相关
        private long _totalQueued;
        private long _queueOverflows;

        // 压缩相关（任务 3.2）
        private long _compressionDegradedCount;   // CPU 守护触发降级次数
        private long _compressionAppliedCount;    // 成功压缩次数
        private long _compressionSavedBytes;      // 节省字节数

        // 大文件传输相关（任务 6.2）
        private long _largeFileUploadCount;       // 大文件上传次数
        private long _largeFileDownloadCount;     // 大文件下载次数
        private long _largeFileTotalUploadBytes;  // 大文件累计上传字节
        private long _largeFileTotalDownloadBytes;// 大文件累计下载字节
        private long _largeFilePooledBufferRentCount; // 池化缓冲区租借次数（验证复用效果）

        #endregion

        #region 延迟采样

        private readonly ConcurrentQueue<LatencySample> _latencySamples = new();
        private int _latencySampleCount;

        /// <summary>
        /// 延迟样本
        /// </summary>
        private readonly struct LatencySample
        {
            public readonly double LatencyMs;
            public readonly long TimestampTicks;

            public LatencySample(double latencyMs)
            {
                LatencyMs = latencyMs;
                TimestampTicks = Stopwatch.GetTimestamp();
            }

            public double AgeSeconds => (Stopwatch.GetTimestamp() - TimestampTicks) / (double)Stopwatch.Frequency;
        }

        #endregion

        #region 队列延迟采样

        private readonly ConcurrentQueue<LatencySample> _queueLatencySamples = new();
        private int _queueLatencySampleCount;

        #endregion

        #region 时间窗口计数器（QPS 计算）

        private readonly ConcurrentQueue<long> _requestTimestamps = new();
        private int _windowRequestCount;

        #endregion

        #region 当前队列深度（由外部设置）

        private long _currentQueueDepth;

        #endregion

        private readonly Stopwatch _startTime;
        private bool _disposed;

        private HttpMetrics()
        {
            _startTime = Stopwatch.StartNew();
        }

        #region 采集方法

        /// <summary>
        /// 记录一次请求完成
        /// </summary>
        /// <param name="success">是否成功</param>
        /// <param name="latencyMs">请求延迟（毫秒）</param>
        /// <param name="timedOut">是否超时</param>
        public void RecordRequest(bool success, double latencyMs, bool timedOut = false)
        {
            Interlocked.Increment(ref _totalRequests);

            if (success)
                Interlocked.Increment(ref _successfulRequests);
            else
                Interlocked.Increment(ref _failedRequests);

            if (timedOut)
                Interlocked.Increment(ref _timedOutRequests);

            // 记录延迟样本
            RecordLatencySample(latencyMs);

            // 记录 QPS 时间戳
            RecordRequestTimestamp();
        }

        /// <summary>
        /// 记录流量
        /// </summary>
        /// <param name="bytesReceived">接收字节数</param>
        /// <param name="bytesSent">发送字节数</param>
        /// <param name="uncompressedSize">压缩前大小（用于计算压缩比）</param>
        public void RecordTraffic(long bytesReceived, long bytesSent, long uncompressedSize = 0)
        {
            if (bytesReceived > 0)
                Interlocked.Add(ref _totalBytesReceived, bytesReceived);

            if (bytesSent > 0)
            {
                Interlocked.Add(ref _totalBytesSent, bytesSent);
                Interlocked.Add(ref _totalCompressedBytesSent, bytesSent);
            }

            if (uncompressedSize > 0)
                Interlocked.Add(ref _totalUncompressedBytesSent, uncompressedSize);
            else if (bytesSent > 0)
                Interlocked.Add(ref _totalUncompressedBytesSent, bytesSent);
        }

        /// <summary>
        /// 记录条件请求与 304 命中
        /// </summary>
        /// <param name="isConditional">是否为条件请求</param>
        /// <param name="is304">是否返回 304</param>
        public void RecordConditionalRequest(bool isConditional, bool is304)
        {
            if (isConditional)
            {
                Interlocked.Increment(ref _conditionalRequests);
                if (is304)
                    Interlocked.Increment(ref _notModifiedResponses);
            }
        }

        /// <summary>
        /// 记录条件请求结果（任务 4.2 简化入口）。
        /// 每次带条件头的下载请求均调用此方法：
        ///   hit=true  → 服务端返回 304，带宽节省；
        ///   hit=false → 服务端返回 200，需要重新传输。
        /// </summary>
        /// <param name="hit">是否 304 命中</param>
        public void RecordConditionalRequest(bool hit)
        {
            Interlocked.Increment(ref _conditionalRequests);
            if (hit)
                Interlocked.Increment(ref _notModifiedResponses);
        }

        /// <summary>
        /// 记录缓存命中
        /// </summary>
        public void RecordCacheHit()
        {
            Interlocked.Increment(ref _cacheHits);
        }

        /// <summary>
        /// 记录缓存未命中
        /// </summary>
        public void RecordCacheMiss()
        {
            Interlocked.Increment(ref _cacheMisses);
        }

        /// <summary>
        /// 记录重试
        /// </summary>
        /// <param name="success">重试是否成功</param>
        public void RecordRetry(bool success)
        {
            Interlocked.Increment(ref _totalRetries);
            if (success)
                Interlocked.Increment(ref _successfulRetries);
            else
                Interlocked.Increment(ref _failedRetries);
        }

        /// <summary>
        /// 记录通用重试事件（含方法、路径、重试次数和失败原因）。
        /// 由 SendAsyncInternal 重试循环调用，用于追踪每次重试的详细信息。
        /// </summary>
        /// <param name="method">HTTP 方法字符串（如 "GET"）。</param>
        /// <param name="url">请求 URL。</param>
        /// <param name="attemptNumber">当前重试序号（1 = 第一次重试）。</param>
        /// <param name="failureReason">失败原因描述，null 表示基于状态码触发的重试。</param>
        public void RecordRetry(string method, string url, int attemptNumber, string? failureReason)
        {
            Interlocked.Increment(ref _totalRetries);
            // 失败原因为 null 表示是状态码触发的重试（中间状态），不计入最终失败；
            // 有失败原因则视为异常重试，计入 failedRetries 指标。
            if (failureReason != null)
                Interlocked.Increment(ref _failedRetries);
        }

        /// <summary>
        /// 记录限流命中
        /// </summary>
        public void RecordRateLimitHit()
        {
            Interlocked.Increment(ref _rateLimitHits);
        }

        /// <summary>
        /// 记录队列操作
        /// </summary>
        /// <param name="queueLatencyMs">排队延迟（毫秒）</param>
        /// <param name="isOverflow">是否溢出到回退队列</param>
        public void RecordQueueOperation(double queueLatencyMs, bool isOverflow = false)
        {
            Interlocked.Increment(ref _totalQueued);

            if (isOverflow)
                Interlocked.Increment(ref _queueOverflows);

            // 记录队列延迟样本
            RecordQueueLatencySample(queueLatencyMs);
        }

        /// <summary>
        /// 设置当前队列深度
        /// </summary>
        public void SetQueueDepth(long depth)
        {
            Interlocked.Exchange(ref _currentQueueDepth, depth);
        }

        // ── 压缩指标（任务 3.2）─────────────────────────────────────────────

        /// <summary>
        /// 记录一次 CPU 守护降级事件（压缩被自动禁用）。
        /// </summary>
        public void RecordCompressionDegraded()
        {
            Interlocked.Increment(ref _compressionDegradedCount);
        }

        /// <summary>
        /// 记录一次成功压缩操作。
        /// </summary>
        /// <param name="savedBytes">节省的字节数（原始大小 - 压缩后大小）</param>
        public void RecordCompressionApplied(long savedBytes)
        {
            Interlocked.Increment(ref _compressionAppliedCount);
            if (savedBytes > 0)
                Interlocked.Add(ref _compressionSavedBytes, savedBytes);
        }

        /// <summary>
        /// 获取压缩降级次数（CPU 守护触发）
        /// </summary>
        public long GetCompressionDegradedCount() => Interlocked.Read(ref _compressionDegradedCount);

        /// <summary>
        /// 获取已压缩响应数
        /// </summary>
        public long GetCompressionAppliedCount() => Interlocked.Read(ref _compressionAppliedCount);

        /// <summary>
        /// 获取通过压缩节省的总字节数
        /// </summary>
        public long GetCompressionSavedBytes() => Interlocked.Read(ref _compressionSavedBytes);

        // ── 大文件传输指标（任务 6.2）─────────────────────────────────────────

        /// <summary>
        /// 记录一次大文件上传完成（>= LargeFileThresholdBytes）。
        /// </summary>
        /// <param name="totalBytes">实际上传字节数。</param>
        public void RecordLargeFileUpload(long totalBytes)
        {
            Interlocked.Increment(ref _largeFileUploadCount);
            if (totalBytes > 0)
                Interlocked.Add(ref _largeFileTotalUploadBytes, totalBytes);
        }

        /// <summary>
        /// 记录一次大文件下载完成（>= LargeFileThresholdBytes）。
        /// </summary>
        /// <param name="totalBytes">实际下载字节数。</param>
        public void RecordLargeFileDownload(long totalBytes)
        {
            Interlocked.Increment(ref _largeFileDownloadCount);
            if (totalBytes > 0)
                Interlocked.Add(ref _largeFileTotalDownloadBytes, totalBytes);
        }

        /// <summary>
        /// 记录一次池化缓冲区租借（大文件传输）。
        /// 用于量化 ArrayPool 复用效果，理想情况下尽量接近传输总次数。
        /// </summary>
        public void RecordPooledBufferRent()
        {
            Interlocked.Increment(ref _largeFilePooledBufferRentCount);
        }

        /// <summary>
        /// 获取大文件上传次数
        /// </summary>
        public long GetLargeFileUploadCount() => Interlocked.Read(ref _largeFileUploadCount);

        /// <summary>
        /// 获取大文件下载次数
        /// </summary>
        public long GetLargeFileDownloadCount() => Interlocked.Read(ref _largeFileDownloadCount);

        /// <summary>
        /// 获取大文件累计上传字节数
        /// </summary>
        public long GetLargeFileTotalUploadBytes() => Interlocked.Read(ref _largeFileTotalUploadBytes);

        /// <summary>
        /// 获取大文件累计下载字节数
        /// </summary>
        public long GetLargeFileTotalDownloadBytes() => Interlocked.Read(ref _largeFileTotalDownloadBytes);

        /// <summary>
        /// 获取池化缓冲区租借总次数
        /// </summary>
        public long GetPooledBufferRentCount() => Interlocked.Read(ref _largeFilePooledBufferRentCount);

        #endregion

        #region 私有采样方法

        private void RecordLatencySample(double latencyMs)
        {
            // 移除过期样本
            PruneOldSamples(_latencySamples, ref _latencySampleCount);

            // 添加新样本
            if (Interlocked.Increment(ref _latencySampleCount) <= MaxLatencySamples)
            {
                _latencySamples.Enqueue(new LatencySample(latencyMs));
            }
            else
            {
                Interlocked.Decrement(ref _latencySampleCount);
            }
        }

        private void RecordQueueLatencySample(double latencyMs)
        {
            // 移除过期样本
            PruneOldSamples(_queueLatencySamples, ref _queueLatencySampleCount);

            // 添加新样本
            if (Interlocked.Increment(ref _queueLatencySampleCount) <= MaxLatencySamples)
            {
                _queueLatencySamples.Enqueue(new LatencySample(latencyMs));
            }
            else
            {
                Interlocked.Decrement(ref _queueLatencySampleCount);
            }
        }

        private void RecordRequestTimestamp()
        {
            var now = Stopwatch.GetTimestamp();

            // 移除窗口外的时间戳
            var windowStart = now - (SlidingWindowSeconds * Stopwatch.Frequency);
            while (_requestTimestamps.TryPeek(out var oldTs) && oldTs < windowStart)
            {
                if (_requestTimestamps.TryDequeue(out _))
                    Interlocked.Decrement(ref _windowRequestCount);
            }

            // 添加新时间戳
            _requestTimestamps.Enqueue(now);
            Interlocked.Increment(ref _windowRequestCount);
        }

        private static void PruneOldSamples(ConcurrentQueue<LatencySample> queue, ref int count)
        {
            while (queue.TryPeek(out var sample) && sample.AgeSeconds > SlidingWindowSeconds)
            {
                if (queue.TryDequeue(out _))
                    Interlocked.Decrement(ref count);
            }
        }

        #endregion

        #region 查询方法

        /// <summary>
        /// 获取当前 QPS（每秒请求数）
        /// </summary>
        public double GetCurrentQps()
        {
            var count = Volatile.Read(ref _windowRequestCount);
            return count / (double)SlidingWindowSeconds;
        }

        /// <summary>
        /// 获取延迟百分位
        /// </summary>
        public LatencyPercentiles GetLatencyPercentiles()
        {
            var samples = _latencySamples
                .Where(s => s.AgeSeconds <= SlidingWindowSeconds)
                .Select(s => s.LatencyMs)
                .ToList();

            return PercentileCalculator.Calculate(samples);
        }

        /// <summary>
        /// 获取队列延迟百分位
        /// </summary>
        public LatencyPercentiles GetQueueLatencyPercentiles()
        {
            var samples = _queueLatencySamples
                .Where(s => s.AgeSeconds <= SlidingWindowSeconds)
                .Select(s => s.LatencyMs)
                .ToList();

            return PercentileCalculator.Calculate(samples);
        }

        /// <summary>
        /// 获取压缩比（压缩后/压缩前）
        /// </summary>
        public double GetCompressionRatio()
        {
            var uncompressed = Interlocked.Read(ref _totalUncompressedBytesSent);
            var compressed = Interlocked.Read(ref _totalCompressedBytesSent);

            if (uncompressed <= 0) return 1.0;
            return (double)compressed / uncompressed;
        }

        /// <summary>
        /// 获取 304 命中率
        /// </summary>
        public double Get304HitRate()
        {
            var conditional = Interlocked.Read(ref _conditionalRequests);
            var notModified = Interlocked.Read(ref _notModifiedResponses);

            if (conditional <= 0) return 0;
            return (double)notModified / conditional;
        }

        /// <summary>
        /// 获取缓存命中率
        /// </summary>
        public double GetCacheHitRate()
        {
            var hits = Interlocked.Read(ref _cacheHits);
            var misses = Interlocked.Read(ref _cacheMisses);
            var total = hits + misses;

            if (total <= 0) return 0;
            return (double)hits / total;
        }

        /// <summary>
        /// 获取重试率
        /// </summary>
        public double GetRetryRate()
        {
            var total = Interlocked.Read(ref _totalRequests);
            var retries = Interlocked.Read(ref _totalRetries);

            if (total <= 0) return 0;
            return (double)retries / total;
        }

        /// <summary>
        /// 获取限流命中率
        /// </summary>
        public double GetRateLimitRate()
        {
            var total = Interlocked.Read(ref _totalRequests);
            var hits = Interlocked.Read(ref _rateLimitHits);

            if (total <= 0) return 0;
            return (double)hits / total;
        }

        /// <summary>
        /// 获取当前队列深度
        /// </summary>
        public long GetQueueDepth()
        {
            return Interlocked.Read(ref _currentQueueDepth);
        }

        #endregion

        #region 快照与报告

        /// <summary>
        /// 获取当前指标快照
        /// </summary>
        public HttpMetricsSnapshot GetSnapshot()
        {
            var latencyPercentiles = GetLatencyPercentiles();
            var queueLatencyPercentiles = GetQueueLatencyPercentiles();

            return new HttpMetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                UptimeSeconds = _startTime.Elapsed.TotalSeconds,

                // 请求计数
                TotalRequests = Interlocked.Read(ref _totalRequests),
                SuccessfulRequests = Interlocked.Read(ref _successfulRequests),
                FailedRequests = Interlocked.Read(ref _failedRequests),
                TimedOutRequests = Interlocked.Read(ref _timedOutRequests),

                // QPS
                CurrentQps = GetCurrentQps(),

                // 延迟百分位
                P50LatencyMs = latencyPercentiles.P50,
                P95LatencyMs = latencyPercentiles.P95,
                P99LatencyMs = latencyPercentiles.P99,
                AvgLatencyMs = latencyPercentiles.Average,
                MinLatencyMs = latencyPercentiles.Min,
                MaxLatencyMs = latencyPercentiles.Max,

                // 流量
                TotalBytesReceived = Interlocked.Read(ref _totalBytesReceived),
                TotalBytesSent = Interlocked.Read(ref _totalBytesSent),
                CompressionRatio = GetCompressionRatio(),

                // 缓存
                ConditionalRequests = Interlocked.Read(ref _conditionalRequests),
                NotModifiedResponses = Interlocked.Read(ref _notModifiedResponses),
                CacheHitRate = GetCacheHitRate(),
                Cache304HitRate = Get304HitRate(),

                // 重试
                TotalRetries = Interlocked.Read(ref _totalRetries),
                SuccessfulRetries = Interlocked.Read(ref _successfulRetries),
                FailedRetries = Interlocked.Read(ref _failedRetries),
                RetryRate = GetRetryRate(),

                // 限流
                RateLimitHits = Interlocked.Read(ref _rateLimitHits),
                RateLimitRate = GetRateLimitRate(),

                // 队列
                TotalQueued = Interlocked.Read(ref _totalQueued),
                QueueOverflows = Interlocked.Read(ref _queueOverflows),
                CurrentQueueDepth = GetQueueDepth(),
                QueueP50LatencyMs = queueLatencyPercentiles.P50,
                QueueP95LatencyMs = queueLatencyPercentiles.P95,
                QueueP99LatencyMs = queueLatencyPercentiles.P99,

                // 大文件传输（任务 6.2）
                LargeFileUploadCount = Interlocked.Read(ref _largeFileUploadCount),
                LargeFileDownloadCount = Interlocked.Read(ref _largeFileDownloadCount),
                LargeFileTotalUploadBytes = Interlocked.Read(ref _largeFileTotalUploadBytes),
                LargeFileTotalDownloadBytes = Interlocked.Read(ref _largeFileTotalDownloadBytes),
                PooledBufferRentCount = Interlocked.Read(ref _largeFilePooledBufferRentCount)
            };
        }

        /// <summary>
        /// 重置所有计数器（用于基线测试）
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _totalRequests, 0);
            Interlocked.Exchange(ref _successfulRequests, 0);
            Interlocked.Exchange(ref _failedRequests, 0);
            Interlocked.Exchange(ref _timedOutRequests, 0);
            Interlocked.Exchange(ref _totalBytesReceived, 0);
            Interlocked.Exchange(ref _totalBytesSent, 0);
            Interlocked.Exchange(ref _totalCompressedBytesSent, 0);
            Interlocked.Exchange(ref _totalUncompressedBytesSent, 0);
            Interlocked.Exchange(ref _conditionalRequests, 0);
            Interlocked.Exchange(ref _notModifiedResponses, 0);
            Interlocked.Exchange(ref _cacheHits, 0);
            Interlocked.Exchange(ref _cacheMisses, 0);
            Interlocked.Exchange(ref _totalRetries, 0);
            Interlocked.Exchange(ref _successfulRetries, 0);
            Interlocked.Exchange(ref _failedRetries, 0);
            Interlocked.Exchange(ref _rateLimitHits, 0);
            Interlocked.Exchange(ref _totalQueued, 0);
            Interlocked.Exchange(ref _queueOverflows, 0);
            Interlocked.Exchange(ref _currentQueueDepth, 0);
            // 大文件传输（任务 6.2）
            Interlocked.Exchange(ref _largeFileUploadCount, 0);
            Interlocked.Exchange(ref _largeFileDownloadCount, 0);
            Interlocked.Exchange(ref _largeFileTotalUploadBytes, 0);
            Interlocked.Exchange(ref _largeFileTotalDownloadBytes, 0);
            Interlocked.Exchange(ref _largeFilePooledBufferRentCount, 0);

            // 清空采样队列
            while (_latencySamples.TryDequeue(out _)) { }
            while (_queueLatencySamples.TryDequeue(out _)) { }
            while (_requestTimestamps.TryDequeue(out _)) { }

            Interlocked.Exchange(ref _latencySampleCount, 0);
            Interlocked.Exchange(ref _queueLatencySampleCount, 0);
            Interlocked.Exchange(ref _windowRequestCount, 0);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _startTime.Stop();
        }

        #endregion
    }

    /// <summary>
    /// HTTP 指标快照，包含某一时刻的完整指标数据
    /// </summary>
    public sealed class HttpMetricsSnapshot
    {
        public DateTime Timestamp { get; init; }
        public double UptimeSeconds { get; init; }

        // 请求计数
        public long TotalRequests { get; init; }
        public long SuccessfulRequests { get; init; }
        public long FailedRequests { get; init; }
        public long TimedOutRequests { get; init; }

        // QPS
        public double CurrentQps { get; init; }

        // 延迟（毫秒）
        public double P50LatencyMs { get; init; }
        public double P95LatencyMs { get; init; }
        public double P99LatencyMs { get; init; }
        public double AvgLatencyMs { get; init; }
        public double MinLatencyMs { get; init; }
        public double MaxLatencyMs { get; init; }

        // 流量（字节）
        public long TotalBytesReceived { get; init; }
        public long TotalBytesSent { get; init; }
        public double CompressionRatio { get; init; }

        // 缓存
        public long ConditionalRequests { get; init; }
        public long NotModifiedResponses { get; init; }
        public double CacheHitRate { get; init; }
        public double Cache304HitRate { get; init; }

        // 重试
        public long TotalRetries { get; init; }
        public long SuccessfulRetries { get; init; }
        public long FailedRetries { get; init; }
        public double RetryRate { get; init; }

        // 限流
        public long RateLimitHits { get; init; }
        public double RateLimitRate { get; init; }

        // 队列
        public long TotalQueued { get; init; }
        public long QueueOverflows { get; init; }
        public long CurrentQueueDepth { get; init; }
        public double QueueP50LatencyMs { get; init; }
        public double QueueP95LatencyMs { get; init; }
        public double QueueP99LatencyMs { get; init; }

        // 大文件传输（任务 6.2）
        public long LargeFileUploadCount { get; init; }
        public long LargeFileDownloadCount { get; init; }
        public long LargeFileTotalUploadBytes { get; init; }
        public long LargeFileTotalDownloadBytes { get; init; }
        public long PooledBufferRentCount { get; init; }

        /// <summary>
        /// 成功率（0-1）
        /// </summary>
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0;

        /// <summary>
        /// 流量节省比（通过压缩节省的比例）
        /// </summary>
        public double BandwidthSavings => CompressionRatio < 1 ? 1 - CompressionRatio : 0;

        /// <summary>
        /// 输出为格式化字符串
        /// </summary>
        public override string ToString()
        {
            return $@"=== HTTP Metrics Snapshot ===
Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss} UTC
Uptime: {UptimeSeconds:F1}s

-- Request Stats --
Total: {TotalRequests}, Success: {SuccessfulRequests}, Failed: {FailedRequests}, Timeout: {TimedOutRequests}
Success Rate: {SuccessRate:P2}
QPS: {CurrentQps:F2}

-- Latency (ms) --
P50: {P50LatencyMs:F2}, P95: {P95LatencyMs:F2}, P99: {P99LatencyMs:F2}
Avg: {AvgLatencyMs:F2}, Min: {MinLatencyMs:F2}, Max: {MaxLatencyMs:F2}

-- Traffic --
Received: {FormatBytes(TotalBytesReceived)}, Sent: {FormatBytes(TotalBytesSent)}
Compression Ratio: {CompressionRatio:P1}, Bandwidth Savings: {BandwidthSavings:P1}

-- Cache --
Conditional Requests: {ConditionalRequests}, 304 Responses: {NotModifiedResponses}
304 Hit Rate: {Cache304HitRate:P2}, Cache Hit Rate: {CacheHitRate:P2}

-- Retry --
Total Retries: {TotalRetries}, Success: {SuccessfulRetries}, Failed: {FailedRetries}
Retry Rate: {RetryRate:P2}

-- Rate Limit --
Hits: {RateLimitHits}, Rate: {RateLimitRate:P2}

-- Queue --
Total Queued: {TotalQueued}, Overflows: {QueueOverflows}, Current Depth: {CurrentQueueDepth}
Queue Latency - P50: {QueueP50LatencyMs:F2}ms, P95: {QueueP95LatencyMs:F2}ms, P99: {QueueP99LatencyMs:F2}ms

-- Large File Transfer --
Uploads: {LargeFileUploadCount} ({FormatBytes(LargeFileTotalUploadBytes)} total)
Downloads: {LargeFileDownloadCount} ({FormatBytes(LargeFileTotalDownloadBytes)} total)
Pooled Buffer Rents: {PooledBufferRentCount}
";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F2} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
