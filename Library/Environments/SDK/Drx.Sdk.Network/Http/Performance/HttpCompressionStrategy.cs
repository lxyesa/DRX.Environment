using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// HTTP 响应压缩策略引擎（任务 3.1 / 3.2 / 3.3）。
    ///
    /// 功能：
    ///   1. 按内容类型（ContentType）+ 最小体积阈值决定是否压缩（3.1）
    ///   2. 支持 Gzip 和 Brotli；优先使用客户端 Accept-Encoding 声明的最优编码
    ///   3. 通过 CPU 守护线程采样 CPU 增幅，超阈值自动降级（3.2）
    ///   4. 独立开关（EnableCompression）支持灰度与回滚（3.3 / R7）
    ///
    /// 设计约束（对应 R2）：
    ///   - CPU 增幅守护：采样周期内 CPU % 增量 > CompressionCpuGuardPercent 时自动禁用压缩
    ///   - 不对已压缩的二进制资源（zip、png、mp4 等）二次压缩
    ///   - 线程安全：所有状态字段通过 Interlocked / volatile 保护
    /// </summary>
    public sealed class HttpCompressionStrategy : IDisposable
    {
        // ── 配置引用 ────────────────────────────────────────────────────────
        private readonly DrxHttpServerOptions _options;

        // ── 降级状态 ────────────────────────────────────────────────────────
        /// <summary>
        /// 压缩功能是否当前有效（受 CPU 守护自动降级影响）。
        /// 初始值由 options.EnableCompression 决定；CPU 守护可在运行时切换。
        /// </summary>
        private volatile bool _compressionActive;

        /// <summary>
        /// 上次 CPU 守护触发降级的时间戳（Stopwatch Ticks）。
        /// 用于实现冷却时间（CooldownSeconds）内不重复降级日志且到期后尝试恢复。
        /// </summary>
        private long _degradedAtTick;

        // ── CPU 采样 ────────────────────────────────────────────────────────
        private readonly Timer? _cpuGuardTimer;

        /// <summary>
        /// 上次采样时的进程 CPU 时间（用于计算增量 %）。
        /// </summary>
        private TimeSpan _lastCpuTime;
        private long _lastCpuWallTick;

        // ── 压缩指标（供 HttpMetrics 采集） ────────────────────────────────
        private long _compressedRequests;
        private long _skippedRequests;
        private long _degradedRequests;
        private long _savedBytes;

        /// <summary>
        /// 已压缩响应总数
        /// </summary>
        public long CompressedRequests => Interlocked.Read(ref _compressedRequests);

        /// <summary>
        /// 因阈值/类型跳过压缩的响应总数
        /// </summary>
        public long SkippedRequests => Interlocked.Read(ref _skippedRequests);

        /// <summary>
        /// 因 CPU 守护降级而跳过压缩的响应总数
        /// </summary>
        public long DegradedRequests => Interlocked.Read(ref _degradedRequests);

        /// <summary>
        /// 通过压缩节省的出站字节（原始大小 - 压缩后大小）
        /// </summary>
        public long SavedBytes => Interlocked.Read(ref _savedBytes);

        /// <summary>
        /// 当前压缩功能是否处于活跃状态
        /// </summary>
        public bool IsCompressionActive => _compressionActive;

        // ── 单例（可被 DrxHttpServer 持有） ────────────────────────────────
        public HttpCompressionStrategy(DrxHttpServerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _compressionActive = options.EnableCompression;

            if (options.EnableCompression && options.CompressionCpuSampleIntervalSeconds > 0)
            {
                // 初始化 CPU 基线
                _lastCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
                _lastCpuWallTick = Stopwatch.GetTimestamp();

                var intervalMs = options.CompressionCpuSampleIntervalSeconds * 1000;
                _cpuGuardTimer = new Timer(OnCpuGuardTick, null, intervalMs, intervalMs);
            }
        }

        // ── 公共主入口 ──────────────────────────────────────────────────────

        /// <summary>
        /// 尝试对响应体进行压缩，返回压缩结果。
        /// 若不满足压缩条件（已禁用/类型不可压缩/体积过小），返回原始字节（不修改）。
        /// </summary>
        /// <param name="responseBytes">原始响应字节</param>
        /// <param name="contentType">响应内容类型（ContentType）</param>
        /// <param name="acceptEncoding">客户端 Accept-Encoding 头值</param>
        /// <param name="appliedEncoding">实际应用的编码名称（"gzip" / "br" / null）</param>
        /// <returns>压缩后（或原始）字节</returns>
        public byte[] TryCompress(byte[] responseBytes, string? contentType, string? acceptEncoding, out string? appliedEncoding)
        {
            appliedEncoding = null;

            if (responseBytes == null || responseBytes.Length == 0)
                return responseBytes ?? Array.Empty<byte>();

            // 独立开关：全局禁用 or CPU 守护降级
            if (!_options.EnableCompression || !_compressionActive)
            {
                Interlocked.Increment(ref _degradedRequests);
                return responseBytes;
            }

            // 体积阈值：太小则跳过
            if (responseBytes.Length < _options.CompressionMinSizeBytes)
            {
                Interlocked.Increment(ref _skippedRequests);
                return responseBytes;
            }

            // 内容类型过滤：不可压缩类型直接跳过
            if (!IsCompressibleContentType(contentType))
            {
                Interlocked.Increment(ref _skippedRequests);
                return responseBytes;
            }

            // 选择编码：根据 Accept-Encoding 协商
            var encoding = NegotiateEncoding(acceptEncoding);
            if (encoding == null)
            {
                Interlocked.Increment(ref _skippedRequests);
                return responseBytes;
            }

            // 执行压缩
            try
            {
                var compressed = Compress(responseBytes, encoding);
                if (compressed != null && compressed.Length < responseBytes.Length)
                {
                    var saved = responseBytes.Length - compressed.Length;
                    Interlocked.Increment(ref _compressedRequests);
                    Interlocked.Add(ref _savedBytes, saved);
                    appliedEncoding = encoding;
                    return compressed;
                }
                // 压缩后反而更大（如很小的 JSON）→ 原样返回
                Interlocked.Increment(ref _skippedRequests);
                return responseBytes;
            }
            catch (Exception ex)
            {
                Logger.Warn($"[Compression] 压缩失败，回退到原始响应: {ex.Message}");
                Interlocked.Increment(ref _skippedRequests);
                return responseBytes;
            }
        }

        // ── 内部：内容类型判断 ────────────────────────────────────────────────

        private bool IsCompressibleContentType(string? contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return false;

            // 去掉 charset 等参数，只取类型主体
            var semi = contentType.IndexOf(';');
            var ct = semi >= 0 ? contentType.Substring(0, semi).Trim() : contentType.Trim();

            foreach (var prefix in _options.CompressibleContentTypes)
            {
                if (ct.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // ── 内部：编码协商 ────────────────────────────────────────────────────

        private static string? NegotiateEncoding(string? acceptEncoding)
        {
            if (string.IsNullOrEmpty(acceptEncoding))
                return null;

            // 优先 Brotli（更高压缩率），其次 Gzip（通用兼容）
            if (acceptEncoding.Contains("br", StringComparison.OrdinalIgnoreCase))
                return "br";

            if (acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
                return "gzip";

            if (acceptEncoding.Contains("deflate", StringComparison.OrdinalIgnoreCase))
                return "deflate";

            return null;
        }

        // ── 内部：压缩执行 ────────────────────────────────────────────────────

        private byte[] Compress(byte[] data, string encoding)
        {
            var level = ResolveCompressionLevel(_options.CompressionLevel);
            using var ms = new MemoryStream(data.Length / 2);

            switch (encoding.ToLowerInvariant())
            {
                case "br":
                    using (var brotli = new BrotliStream(ms, level, leaveOpen: true))
                    {
                        brotli.Write(data, 0, data.Length);
                    }
                    break;

                case "gzip":
                    using (var gzip = new GZipStream(ms, level, leaveOpen: true))
                    {
                        gzip.Write(data, 0, data.Length);
                    }
                    break;

                case "deflate":
                    using (var deflate = new DeflateStream(ms, level, leaveOpen: true))
                    {
                        deflate.Write(data, 0, data.Length);
                    }
                    break;

                default:
                    return data;
            }

            return ms.ToArray();
        }

        /// <summary>
        /// 将 0~9 范围的整数压缩级别映射到 <see cref="CompressionLevel"/> 枚举。
        /// </summary>
        private static CompressionLevel ResolveCompressionLevel(int level)
        {
            return level switch
            {
                0 => CompressionLevel.NoCompression,
                1 => CompressionLevel.Fastest,
                >= 9 => CompressionLevel.SmallestSize,
                _ => CompressionLevel.Optimal,
            };
        }

        // ── CPU 守护 ──────────────────────────────────────────────────────────

        private void OnCpuGuardTick(object? state)
        {
            try
            {
                var proc = Process.GetCurrentProcess();
                var nowCpuTime = proc.TotalProcessorTime;
                var nowWallTick = Stopwatch.GetTimestamp();

                var cpuDelta = (nowCpuTime - _lastCpuTime).TotalSeconds;
                var wallDelta = (nowWallTick - _lastCpuWallTick) / (double)Stopwatch.Frequency;

                _lastCpuTime = nowCpuTime;
                _lastCpuWallTick = nowWallTick;

                if (wallDelta <= 0) return;

                // CPU% = 进程 CPU 时间增量 / (墙钟时间增量 × CPU 核心数) × 100
                var cpuPercent = cpuDelta / (wallDelta * Environment.ProcessorCount) * 100.0;

                if (!_compressionActive)
                {
                    // 检查是否到达冷却期，尝试恢复压缩
                    var degradedAtTick = Interlocked.Read(ref _degradedAtTick);
                    if (degradedAtTick > 0)
                    {
                        var elapsedSinceDegrade = (nowWallTick - degradedAtTick) / (double)Stopwatch.Frequency;
                        if (elapsedSinceDegrade >= _options.CompressionCpuCooldownSeconds)
                        {
                            _compressionActive = true;
                            Interlocked.Exchange(ref _degradedAtTick, 0);
                            Logger.Info($"[Compression] CPU 已恢复正常（cpuPercent={cpuPercent:F1}%），压缩策略已重新启用。");
                        }
                    }
                    return;
                }

                // 超过守护阈值：自动降级
                if (cpuPercent > _options.CompressionCpuGuardPercent)
                {
                    _compressionActive = false;
                    Interlocked.Exchange(ref _degradedAtTick, nowWallTick);
                    Logger.Warn($"[Compression] CPU 增幅 {cpuPercent:F1}% 超过守护阈值 {_options.CompressionCpuGuardPercent}%，压缩策略已自动降级。冷却时间 {_options.CompressionCpuCooldownSeconds}s 后将尝试恢复。");

                    // 记录指标
                    HttpMetrics.Instance.RecordCompressionDegraded();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[Compression] CPU 守护采样失败: {ex.Message}");
            }
        }

        // ── 手动控制 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 手动强制启用压缩（覆盖 CPU 守护降级状态）。
        /// 仅在调试/紧急恢复时使用；正常情况下由 CPU 守护冷却后自动恢复。
        /// </summary>
        public void ForceEnable()
        {
            _compressionActive = true;
            Interlocked.Exchange(ref _degradedAtTick, 0);
            Logger.Info("[Compression] 压缩策略已手动强制启用。");
        }

        /// <summary>
        /// 手动强制禁用压缩（回滚场景）。
        /// </summary>
        public void ForceDisable()
        {
            _compressionActive = false;
            Interlocked.Exchange(ref _degradedAtTick, Stopwatch.GetTimestamp());
            Logger.Info("[Compression] 压缩策略已手动强制禁用。");
        }

        // ── 统计摘要 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 获取当前压缩统计摘要
        /// </summary>
        public CompressionStats GetStats()
        {
            var total = _compressedRequests + _skippedRequests + _degradedRequests;
            var compressionRatio = total > 0 && _compressedRequests > 0
                ? (double)_savedBytes / Math.Max(1, _savedBytes + _compressedRequests)
                : 0.0;

            return new CompressionStats
            {
                IsActive = _compressionActive,
                CompressedRequests = Interlocked.Read(ref _compressedRequests),
                SkippedRequests = Interlocked.Read(ref _skippedRequests),
                DegradedRequests = Interlocked.Read(ref _degradedRequests),
                SavedBytes = Interlocked.Read(ref _savedBytes),
                CompressionLevel = _options.CompressionLevel,
            };
        }

        public void Dispose()
        {
            _cpuGuardTimer?.Dispose();
        }
    }

    /// <summary>
    /// 压缩策略统计摘要（只读快照）
    /// </summary>
    public sealed class CompressionStats
    {
        /// <summary>当前压缩是否处于活跃状态（未被 CPU 守护降级）</summary>
        public bool IsActive { get; init; }

        /// <summary>已成功压缩的响应数</summary>
        public long CompressedRequests { get; init; }

        /// <summary>因阈值/类型等条件不满足而跳过的响应数</summary>
        public long SkippedRequests { get; init; }

        /// <summary>因 CPU 守护降级而跳过的响应数</summary>
        public long DegradedRequests { get; init; }

        /// <summary>通过压缩节省的出站字节</summary>
        public long SavedBytes { get; init; }

        /// <summary>配置的压缩级别（0~9）</summary>
        public int CompressionLevel { get; init; }

        public override string ToString() =>
            $"Compression[active={IsActive}, compressed={CompressedRequests}, skipped={SkippedRequests}, degraded={DegradedRequests}, saved={SavedBytes / 1024.0:F1}KB, level={CompressionLevel}]";
    }
}
