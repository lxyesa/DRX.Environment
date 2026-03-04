using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// HTTP 基线压测运行器。
    /// 支持多种核心场景的自动化压测，输出可对比的基线数据。
    /// 
    /// 核心场景：
    ///   1. 小包高并发：高 QPS 低延迟场景
    ///   2. 重复资源请求：缓存命中与条件请求场景
    ///   3. 大文件传输：带宽与吞吐场景
    /// </summary>
    public sealed class HttpBenchmarkRunner : IDisposable
    {
        private readonly HttpClient _client;
        private readonly HttpMetrics _metrics;
        private readonly List<BenchmarkScenarioResult> _results = new();
        private bool _disposed;

        /// <summary>
        /// 基准服务器地址
        /// </summary>
        public string BaseUrl { get; set; } = "http://localhost:8462";

        /// <summary>
        /// 预热请求数
        /// </summary>
        public int WarmupRequests { get; set; } = 100;

        /// <summary>
        /// 是否输出详细日志
        /// </summary>
        public bool Verbose { get; set; } = false;

        public HttpBenchmarkRunner(HttpClient? client = null, HttpMetrics? metrics = null)
        {
            _client = client ?? new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _metrics = metrics ?? HttpMetrics.Instance;
        }

        #region 主入口

        /// <summary>
        /// 运行完整基线测试套件
        /// </summary>
        public async Task<BenchmarkReport> RunFullBenchmarkAsync(CancellationToken cancellationToken = default)
        {
            _results.Clear();
            _metrics.Reset();

            var report = new BenchmarkReport
            {
                StartTime = DateTime.UtcNow,
                BaseUrl = BaseUrl,
                Environment = GetEnvironmentInfo()
            };

            try
            {
                // 预热
                await WarmupAsync(cancellationToken);

                // 场景 1: 小包高并发
                report.SmallPacketHighConcurrency = await RunSmallPacketHighConcurrencyAsync(cancellationToken: cancellationToken);
                _results.Add(report.SmallPacketHighConcurrency);

                // 场景 2: 重复资源请求
                report.RepeatedResourceRequests = await RunRepeatedResourceRequestsAsync(cancellationToken: cancellationToken);
                _results.Add(report.RepeatedResourceRequests);

                // 场景 3: 大文件传输
                report.LargeFileTransfer = await RunLargeFileTransferAsync(cancellationToken: cancellationToken);
                _results.Add(report.LargeFileTransfer);

                // 场景 4: 连接池策略对比（任务 2.3）
                report.ConnectionPoolComparison = await RunConnectionPoolComparisonAsync(cancellationToken: cancellationToken);

                // 汇总
                report.EndTime = DateTime.UtcNow;
                report.TotalDurationMs = (report.EndTime - report.StartTime).TotalMilliseconds;
                report.OverallMetrics = _metrics.GetSnapshot();
            }
            catch (Exception ex)
            {
                report.Error = ex.Message;
                Logger.Error($"基线测试失败: {ex}");
            }

            return report;
        }

        #endregion

        #region 预热

        private async Task WarmupAsync(CancellationToken cancellationToken)
        {
            if (Verbose) Logger.Info($"开始预热 ({WarmupRequests} 请求)...");

            for (int i = 0; i < WarmupRequests && !cancellationToken.IsCancellationRequested; i++)
            {
                try
                {
                    await _client.GetAsync(BaseUrl, cancellationToken);
                }
                catch
                {
                    // 忽略预热错误
                }
            }

            _metrics.Reset(); // 预热后重置指标
            if (Verbose) Logger.Info("预热完成");
        }

        #endregion

        #region 场景 1: 小包高并发

        /// <summary>
        /// 小包高并发测试：模拟大量小请求的高 QPS 场景
        /// </summary>
        public async Task<BenchmarkScenarioResult> RunSmallPacketHighConcurrencyAsync(
            int totalRequests = 10000,
            int concurrency = 100,
            CancellationToken cancellationToken = default)
        {
            var result = new BenchmarkScenarioResult
            {
                ScenarioName = "SmallPacketHighConcurrency",
                Description = "小包高并发: 大量小请求的高 QPS 场景",
                TotalRequests = totalRequests,
                Concurrency = concurrency,
                StartTime = DateTime.UtcNow
            };

            if (Verbose) Logger.Info($"开始场景: {result.Description}");

            var sw = Stopwatch.StartNew();
            var latencies = new List<double>();
            var latencyLock = new object();
            int successCount = 0;
            int failCount = 0;
            long bytesReceived = 0;

            using var semaphore = new SemaphoreSlim(concurrency);
            var tasks = new List<Task>();

            for (int i = 0; i < totalRequests && !cancellationToken.IsCancellationRequested; i++)
            {
                await semaphore.WaitAsync(cancellationToken);

                tasks.Add(Task.Run(async () =>
                {
                    var requestSw = Stopwatch.StartNew();
                    try
                    {
                        var response = await _client.GetAsync(BaseUrl, cancellationToken);
                        requestSw.Stop();

                        var latency = requestSw.Elapsed.TotalMilliseconds;
                        lock (latencyLock) { latencies.Add(latency); }

                        if (response.IsSuccessStatusCode)
                        {
                            Interlocked.Increment(ref successCount);
                            var content = await response.Content.ReadAsByteArrayAsync();
                            Interlocked.Add(ref bytesReceived, content.Length);
                            _metrics.RecordRequest(true, latency);
                            _metrics.RecordTraffic(content.Length, 0);
                        }
                        else
                        {
                            Interlocked.Increment(ref failCount);
                            _metrics.RecordRequest(false, latency);
                        }
                    }
                    catch (Exception)
                    {
                        requestSw.Stop();
                        Interlocked.Increment(ref failCount);
                        _metrics.RecordRequest(false, requestSw.Elapsed.TotalMilliseconds);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            // 计算结果
            result.EndTime = DateTime.UtcNow;
            result.DurationMs = sw.Elapsed.TotalMilliseconds;
            result.SuccessfulRequests = successCount;
            result.FailedRequests = failCount;
            result.BytesReceived = bytesReceived;
            result.Qps = successCount * 1000.0 / result.DurationMs;
            result.Latencies = PercentileCalculator.Calculate(latencies);

            if (Verbose) Logger.Info($"场景完成: QPS={result.Qps:F2}, P99={result.Latencies.P99:F2}ms");

            return result;
        }

        #endregion

        #region 场景 2: 重复资源请求

        /// <summary>
        /// 重复资源请求测试：测试条件请求和 304 命中
        /// </summary>
        public async Task<BenchmarkScenarioResult> RunRepeatedResourceRequestsAsync(
            int totalRequests = 1000,
            int concurrency = 20,
            string[]? resourcePaths = null,
            CancellationToken cancellationToken = default)
        {
            var result = new BenchmarkScenarioResult
            {
                ScenarioName = "RepeatedResourceRequests",
                Description = "重复资源请求: 测试缓存和条件请求场景",
                TotalRequests = totalRequests,
                Concurrency = concurrency,
                StartTime = DateTime.UtcNow
            };

            // 模拟常见的重复资源路径
            resourcePaths ??= new[]
            {
                "/",
                "/api/status",
                "/static/style.css",
                "/static/script.js",
                "/favicon.ico"
            };

            if (Verbose) Logger.Info($"开始场景: {result.Description}");

            var sw = Stopwatch.StartNew();
            var latencies = new List<double>();
            var latencyLock = new object();
            int successCount = 0;
            int failCount = 0;
            int notModifiedCount = 0;
            long bytesReceived = 0;
            var etagCache = new Dictionary<string, string>();

            using var semaphore = new SemaphoreSlim(concurrency);
            var tasks = new List<Task>();
            var random = new Random();

            for (int i = 0; i < totalRequests && !cancellationToken.IsCancellationRequested; i++)
            {
                await semaphore.WaitAsync(cancellationToken);

                var path = resourcePaths[i % resourcePaths.Length];
                var iteration = i;

                tasks.Add(Task.Run(async () =>
                {
                    var requestSw = Stopwatch.StartNew();
                    try
                    {
                        var url = BaseUrl.TrimEnd('/') + path;
                        using var request = new HttpRequestMessage(HttpMethod.Get, url);

                        // 第二次及以后的请求尝试使用条件头
                        string? etag = null;
                        lock (etagCache)
                        {
                            etagCache.TryGetValue(path, out etag);
                        }

                        bool isConditional = !string.IsNullOrEmpty(etag);
                        if (isConditional)
                        {
                            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
                        }

                        var response = await _client.SendAsync(request, cancellationToken);
                        requestSw.Stop();

                        var latency = requestSw.Elapsed.TotalMilliseconds;
                        lock (latencyLock) { latencies.Add(latency); }

                        var is304 = response.StatusCode == System.Net.HttpStatusCode.NotModified;
                        _metrics.RecordConditionalRequest(isConditional, is304);

                        if (is304)
                        {
                            Interlocked.Increment(ref notModifiedCount);
                            Interlocked.Increment(ref successCount);
                            _metrics.RecordRequest(true, latency);
                        }
                        else if (response.IsSuccessStatusCode)
                        {
                            Interlocked.Increment(ref successCount);
                            var content = await response.Content.ReadAsByteArrayAsync();
                            Interlocked.Add(ref bytesReceived, content.Length);

                            // 缓存 ETag
                            if (response.Headers.ETag != null)
                            {
                                lock (etagCache)
                                {
                                    etagCache[path] = response.Headers.ETag.Tag;
                                }
                            }

                            _metrics.RecordRequest(true, latency);
                            _metrics.RecordTraffic(content.Length, 0);
                        }
                        else
                        {
                            Interlocked.Increment(ref failCount);
                            _metrics.RecordRequest(false, latency);
                        }
                    }
                    catch (Exception)
                    {
                        requestSw.Stop();
                        Interlocked.Increment(ref failCount);
                        _metrics.RecordRequest(false, requestSw.Elapsed.TotalMilliseconds);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            // 计算结果
            result.EndTime = DateTime.UtcNow;
            result.DurationMs = sw.Elapsed.TotalMilliseconds;
            result.SuccessfulRequests = successCount;
            result.FailedRequests = failCount;
            result.BytesReceived = bytesReceived;
            result.Qps = successCount * 1000.0 / result.DurationMs;
            result.Latencies = PercentileCalculator.Calculate(latencies);
            result.AdditionalMetrics["NotModifiedResponses"] = notModifiedCount;
            result.AdditionalMetrics["304HitRate"] = totalRequests > 0 ? (double)notModifiedCount / totalRequests : 0;

            if (Verbose) Logger.Info($"场景完成: 304命中={notModifiedCount}, 命中率={result.AdditionalMetrics["304HitRate"]:P2}");

            return result;
        }

        #endregion

        #region 场景 3: 大文件传输

        /// <summary>
        /// 大文件传输测试：测试吞吐和带宽利用
        /// </summary>
        public async Task<BenchmarkScenarioResult> RunLargeFileTransferAsync(
            int totalTransfers = 20,
            int concurrency = 4,
            long fileSizeBytes = 10 * 1024 * 1024, // 10MB
            CancellationToken cancellationToken = default)
        {
            var result = new BenchmarkScenarioResult
            {
                ScenarioName = "LargeFileTransfer",
                Description = $"大文件传输: {FormatBytes(fileSizeBytes)} 文件吞吐测试",
                TotalRequests = totalTransfers,
                Concurrency = concurrency,
                StartTime = DateTime.UtcNow
            };

            if (Verbose) Logger.Info($"开始场景: {result.Description}");

            var sw = Stopwatch.StartNew();
            var latencies = new List<double>();
            var latencyLock = new object();
            int successCount = 0;
            int failCount = 0;
            long bytesReceived = 0;

            using var semaphore = new SemaphoreSlim(concurrency);
            var tasks = new List<Task>();

            // 假设服务端有 /download/test 端点返回指定大小的数据
            // 如果没有，可以请求根路径并记录实际传输量
            var downloadUrl = $"{BaseUrl.TrimEnd('/')}/download/test?size={fileSizeBytes}";

            for (int i = 0; i < totalTransfers && !cancellationToken.IsCancellationRequested; i++)
            {
                await semaphore.WaitAsync(cancellationToken);

                tasks.Add(Task.Run(async () =>
                {
                    var requestSw = Stopwatch.StartNew();
                    try
                    {
                        // 流式读取以避免内存问题
                        using var response = await _client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            using var stream = await response.Content.ReadAsStreamAsync();
                            // [\u4efb\u52a1 6.1/6.3] \u4f7f\u7528 ArrayPool \u6301\u7528\u7f13\u51b2\u533a\uff0c\u9a8c\u8bc1 GC \u538b\u529b\u964d\u4f4e\u6548\u679c
                            var dlBuf = HttpObjectPool.RentTransferBuffer(fileSizeBytes);
                            _metrics.RecordPooledBufferRent();
                            try
                            {
                                long totalRead = 0;
                                int bytesRead;

                                while ((bytesRead = await stream.ReadAsync(dlBuf.AsMemory(0, dlBuf.Length), cancellationToken)) > 0)
                                {
                                    totalRead += bytesRead;
                                }

                                requestSw.Stop();
                                var latency = requestSw.Elapsed.TotalMilliseconds;

                                lock (latencyLock) { latencies.Add(latency); }
                                Interlocked.Increment(ref successCount);
                                Interlocked.Add(ref bytesReceived, totalRead);

                                _metrics.RecordRequest(true, latency);
                                _metrics.RecordTraffic(totalRead, 0);
                                if (totalRead >= HttpObjectPool.LargeFileThresholdBytes)
                                    _metrics.RecordLargeFileDownload(totalRead);
                            }
                            finally
                            {
                                HttpObjectPool.ReturnTransferBuffer(dlBuf);
                            }
                        }
                        else
                        {
                            requestSw.Stop();
                            Interlocked.Increment(ref failCount);
                            _metrics.RecordRequest(false, requestSw.Elapsed.TotalMilliseconds);
                        }
                    }
                    catch (Exception)
                    {
                        requestSw.Stop();
                        Interlocked.Increment(ref failCount);
                        _metrics.RecordRequest(false, requestSw.Elapsed.TotalMilliseconds);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            // 计算结果
            result.EndTime = DateTime.UtcNow;
            result.DurationMs = sw.Elapsed.TotalMilliseconds;
            result.SuccessfulRequests = successCount;
            result.FailedRequests = failCount;
            result.BytesReceived = bytesReceived;
            result.Qps = successCount * 1000.0 / result.DurationMs;
            result.Latencies = PercentileCalculator.Calculate(latencies);

            // 计算吞吐量 (MB/s)
            var throughputMBps = (bytesReceived / (1024.0 * 1024.0)) / (result.DurationMs / 1000.0);
            result.AdditionalMetrics["ThroughputMBps"] = throughputMBps;
            result.AdditionalMetrics["TotalTransferredMB"] = bytesReceived / (1024.0 * 1024.0);

            // [任务 6.3] 采集 GC 内存指标，验证 ArrayPool 复用对堆内存峰值的影响
            result.AdditionalMetrics["GcTotalMemoryMB"] = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            result.AdditionalMetrics["PooledBufferRentCount"] = (double)_metrics.GetPooledBufferRentCount();
            result.AdditionalMetrics["LargeFileDownloadCount"] = (double)_metrics.GetLargeFileDownloadCount();

            if (Verbose) Logger.Info($"场景完成: 吞吐={throughputMBps:F2} MB/s, 总传输={result.AdditionalMetrics["TotalTransferredMB"]:F2} MB, GC堆={result.AdditionalMetrics["GcTotalMemoryMB"]:F2} MB");

            return result;
        }

        #endregion

        #region 场景 4: 连接池策略对比（尾延迟与吞吐）

        /// <summary>
        /// 连接池策略对比测试：对比默认连接池与优化连接池的 P95/P99 和吞吐差异。
        /// 用于验证任务 2 的连接池参数化改进效果。
        /// 
        /// 方法：
        ///  - 基线组：使用默认 HttpClient（MaxConnectionsPerServer=无限, Concurrent=10）
        ///  - 优化组：使用 HighConcurrency 预设（MaxConnectionsPerServer 受控, Concurrent 增加, 自适应开启）
        ///  两组在相同 URL、相同并发度下运行，输出 P95/P99 与吞吐对比。
        /// </summary>
        public async Task<ConnectionPoolComparisonResult> RunConnectionPoolComparisonAsync(
            int totalRequests = 5000,
            int concurrency = 50,
            CancellationToken cancellationToken = default)
        {
            var result = new ConnectionPoolComparisonResult
            {
                StartTime = DateTime.UtcNow,
                TotalRequests = totalRequests,
                Concurrency = concurrency,
            };

            if (Verbose) Logger.Info($"[连接池策略对比] 开始测试 (totalRequests={totalRequests}, concurrency={concurrency})");

            // ── 基线组：默认参数（与历史行为一致） ───────────────────────────────
            result.Baseline = await RunConnectionPoolVariantAsync(
                label: "Baseline(default)",
                clientFactory: () => new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
                totalRequests: totalRequests,
                concurrency: concurrency,
                cancellationToken: cancellationToken);

            // ── 优化组：高并发预设 + 自适应并发 ──────────────────────────────────
            var optimizedOptions = DrxHttpClientOptions.HighConcurrency();
            result.Optimized = await RunConnectionPoolVariantAsync(
                label: "Optimized(HighConcurrency)",
                clientFactory: () =>
                {
                    optimizedOptions.Validate();
                    var handler = optimizedOptions.BuildSocketsHttpHandler();
                    return new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromSeconds(optimizedOptions.RequestTimeoutSeconds)
                    };
                },
                totalRequests: totalRequests,
                concurrency: concurrency,
                cancellationToken: cancellationToken);

            result.EndTime = DateTime.UtcNow;

            // ── 输出对比摘要 ───────────────────────────────────────────────────
            var baseP95 = result.Baseline.Latencies.P95;
            var optP95 = result.Optimized.Latencies.P95;
            var baseP99 = result.Baseline.Latencies.P99;
            var optP99 = result.Optimized.Latencies.P99;
            var baseQps = result.Baseline.Qps;
            var optQps = result.Optimized.Qps;

            result.P95ImprovementPercent = baseP95 > 0 ? (baseP95 - optP95) / baseP95 * 100.0 : 0;
            result.P99ImprovementPercent = baseP99 > 0 ? (baseP99 - optP99) / baseP99 * 100.0 : 0;
            result.ThroughputImprovementPercent = baseQps > 0 ? (optQps - baseQps) / baseQps * 100.0 : 0;

            Logger.Info($"[连接池策略对比] 结果:\n" +
                        $"  P95: {baseP95:F1}ms → {optP95:F1}ms ({result.P95ImprovementPercent:+F1;-F1}%)\n" +
                        $"  P99: {baseP99:F1}ms → {optP99:F1}ms ({result.P99ImprovementPercent:+F1;-F1}%)\n" +
                        $"  QPS: {baseQps:F1} → {optQps:F1} ({result.ThroughputImprovementPercent:+F1;-F1}%)");

            return result;
        }

        /// <summary>
        /// 内部辅助：使用指定 HttpClient 工厂运行一个策略变体的基准测试。
        /// </summary>
        private async Task<BenchmarkScenarioResult> RunConnectionPoolVariantAsync(
            string label,
            Func<HttpClient> clientFactory,
            int totalRequests,
            int concurrency,
            CancellationToken cancellationToken)
        {
            var result = new BenchmarkScenarioResult
            {
                ScenarioName = label,
                Description = $"连接池策略: {label}",
                TotalRequests = totalRequests,
                Concurrency = concurrency,
                StartTime = DateTime.UtcNow
            };

            if (Verbose) Logger.Info($"  运行变体: {label}");

            using var variantClient = clientFactory();
            var sw = Stopwatch.StartNew();
            var latencies = new System.Collections.Concurrent.ConcurrentBag<double>();
            int successCount = 0;
            int failCount = 0;
            long bytesReceived = 0;

            // 预热（小批次）
            for (int w = 0; w < Math.Min(20, totalRequests / 10); w++)
            {
                try { await variantClient.GetAsync(BaseUrl, cancellationToken); } catch { }
            }

            sw.Restart();
            using var semaphore = new SemaphoreSlim(concurrency);
            var tasks = new List<Task>();

            for (int i = 0; i < totalRequests && !cancellationToken.IsCancellationRequested; i++)
            {
                await semaphore.WaitAsync(cancellationToken);

                tasks.Add(Task.Run(async () =>
                {
                    var reqSw = Stopwatch.StartNew();
                    try
                    {
                        var response = await variantClient.GetAsync(BaseUrl, cancellationToken);
                        reqSw.Stop();

                        latencies.Add(reqSw.Elapsed.TotalMilliseconds);

                        if (response.IsSuccessStatusCode)
                        {
                            Interlocked.Increment(ref successCount);
                            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                            Interlocked.Add(ref bytesReceived, bytes.Length);
                        }
                        else
                        {
                            Interlocked.Increment(ref failCount);
                        }
                    }
                    catch
                    {
                        reqSw.Stop();
                        Interlocked.Increment(ref failCount);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            result.EndTime = DateTime.UtcNow;
            result.DurationMs = sw.Elapsed.TotalMilliseconds;
            result.SuccessfulRequests = successCount;
            result.FailedRequests = failCount;
            result.BytesReceived = bytesReceived;
            result.Qps = successCount * 1000.0 / result.DurationMs;
            result.Latencies = PercentileCalculator.Calculate(latencies.ToList());

            return result;
        }

        #endregion

        #region 辅助方法

        private static EnvironmentInfo GetEnvironmentInfo()
        {
            return new EnvironmentInfo
            {
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                OsVersion = Environment.OSVersion.ToString(),
                RuntimeVersion = Environment.Version.ToString(),
                Is64Bit = Environment.Is64BitProcess,
                WorkingSetMB = Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0)
            };
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F2} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _client?.Dispose();
        }

        #endregion
    }

    #region 数据模型

    /// <summary>
    /// 基线测试报告
    /// </summary>
    public sealed class BenchmarkReport
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double TotalDurationMs { get; set; }
        public string BaseUrl { get; set; } = string.Empty;
        public string? Error { get; set; }

        public EnvironmentInfo Environment { get; set; } = new();

        // 各场景结果
        public BenchmarkScenarioResult? SmallPacketHighConcurrency { get; set; }
        public BenchmarkScenarioResult? RepeatedResourceRequests { get; set; }
        public BenchmarkScenarioResult? LargeFileTransfer { get; set; }

        /// <summary>连接池策略对比结果（任务 2.3）</summary>
        public ConnectionPoolComparisonResult? ConnectionPoolComparison { get; set; }

        // 汇总指标
        public HttpMetricsSnapshot? OverallMetrics { get; set; }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("                    HTTP 基线性能测试报告                        ");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"测试时间: {StartTime:yyyy-MM-dd HH:mm:ss} - {EndTime:HH:mm:ss} UTC");
            sb.AppendLine($"总耗时: {TotalDurationMs:F0}ms");
            sb.AppendLine($"目标地址: {BaseUrl}");
            sb.AppendLine();
            sb.AppendLine("--- 环境信息 ---");
            sb.AppendLine(Environment.ToString());
            sb.AppendLine();

            if (!string.IsNullOrEmpty(Error))
            {
                sb.AppendLine($"*** 错误: {Error} ***");
                return sb.ToString();
            }

            if (SmallPacketHighConcurrency != null)
            {
                sb.AppendLine("--- 场景1: 小包高并发 ---");
                sb.AppendLine(SmallPacketHighConcurrency.ToString());
            }

            if (RepeatedResourceRequests != null)
            {
                sb.AppendLine("--- 场景2: 重复资源请求 ---");
                sb.AppendLine(RepeatedResourceRequests.ToString());
            }

            if (LargeFileTransfer != null)
            {
                sb.AppendLine("--- 场景3: 大文件传输 ---");
                sb.AppendLine(LargeFileTransfer.ToString());
            }

            if (ConnectionPoolComparison != null)
            {
                sb.AppendLine("--- 场景4: 连接池策略对比 ---");
                sb.AppendLine(ConnectionPoolComparison.ToString());
            }

            if (OverallMetrics != null)
            {
                sb.AppendLine("--- 汇总指标 ---");
                sb.AppendLine(OverallMetrics.ToString());
            }

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 单个场景的测试结果
    /// </summary>
    public sealed class BenchmarkScenarioResult
    {
        public string ScenarioName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double DurationMs { get; set; }

        public int TotalRequests { get; set; }
        public int Concurrency { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public long BytesReceived { get; set; }
        public double Qps { get; set; }

        public LatencyPercentiles Latencies { get; set; }
        public Dictionary<string, double> AdditionalMetrics { get; set; } = new();

        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0;

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{Description}");
            sb.AppendLine($"  请求: {TotalRequests} 总计, {SuccessfulRequests} 成功, {FailedRequests} 失败");
            sb.AppendLine($"  成功率: {SuccessRate:P2}");
            sb.AppendLine($"  QPS: {Qps:F2}");
            sb.AppendLine($"  延迟: {Latencies}");
            sb.AppendLine($"  数据量: {FormatBytes(BytesReceived)}");

            foreach (var metric in AdditionalMetrics)
            {
                if (metric.Value is >= 0 and <= 1 && metric.Key.Contains("Rate"))
                    sb.AppendLine($"  {metric.Key}: {metric.Value:P2}");
                else
                    sb.AppendLine($"  {metric.Key}: {metric.Value:F2}");
            }

            return sb.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F2} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }

    /// <summary>
    /// 环境信息
    /// </summary>
    public sealed class EnvironmentInfo
    {
        public string MachineName { get; set; } = string.Empty;
        public int ProcessorCount { get; set; }
        public string OsVersion { get; set; } = string.Empty;
        public string RuntimeVersion { get; set; } = string.Empty;
        public bool Is64Bit { get; set; }
        public double WorkingSetMB { get; set; }

        public override string ToString()
        {
            return $"Machine: {MachineName}, CPU: {ProcessorCount} cores, OS: {OsVersion}, .NET: {RuntimeVersion}, {(Is64Bit ? "64-bit" : "32-bit")}, Memory: {WorkingSetMB:F1} MB";
        }
    }

    /// <summary>
    /// 连接池策略对比测试结果（任务 2.3 输出）。
    /// 记录基线与优化方案的 P95/P99 与吞吐指标，量化改进效果。
    /// </summary>
    public sealed class ConnectionPoolComparisonResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public int TotalRequests { get; set; }
        public int Concurrency { get; set; }

        /// <summary>基线组（默认参数）结果</summary>
        public BenchmarkScenarioResult Baseline { get; set; } = new();

        /// <summary>优化组（HighConcurrency 预设）结果</summary>
        public BenchmarkScenarioResult Optimized { get; set; } = new();

        /// <summary>P95 延迟改进百分比（正值为改进，负值为退化）</summary>
        public double P95ImprovementPercent { get; set; }

        /// <summary>P99 延迟改进百分比（正值为改进，负值为退化）</summary>
        public double P99ImprovementPercent { get; set; }

        /// <summary>吞吐（QPS）改进百分比（正值为改进）</summary>
        public double ThroughputImprovementPercent { get; set; }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("              连接池策略对比测试报告（任务 2.3）                  ");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"并发: {Concurrency}, 总请求: {TotalRequests}");
            sb.AppendLine();
            sb.AppendLine("--- 基线（默认参数） ---");
            sb.AppendLine($"  QPS={Baseline.Qps:F1}  P95={Baseline.Latencies.P95:F1}ms  P99={Baseline.Latencies.P99:F1}ms  成功率={Baseline.SuccessRate:P1}");
            sb.AppendLine();
            sb.AppendLine("--- 优化（HighConcurrency 预设） ---");
            sb.AppendLine($"  QPS={Optimized.Qps:F1}  P95={Optimized.Latencies.P95:F1}ms  P99={Optimized.Latencies.P99:F1}ms  成功率={Optimized.SuccessRate:P1}");
            sb.AppendLine();
            sb.AppendLine("--- 改进量 ---");
            sb.AppendLine($"  P95: {P95ImprovementPercent:+F1;-F1}%  P99: {P99ImprovementPercent:+F1;-F1}%  QPS: {ThroughputImprovementPercent:+F1;-F1}%");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            return sb.ToString();
        }
    }

    #endregion
}
