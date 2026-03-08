using System;
using System.Threading;
using ZiggyCreatures.Caching.Fusion;

namespace Drx.Sdk.Network.Http.Auth
{
    /// <summary>
    /// 无锁令牌桶速率限制器：使用 CAS (Compare-And-Swap) 替代 lock，实现零锁竞争。
    /// 令牌桶以固定速率补充令牌，每次请求消耗一个令牌。
    /// 当令牌耗尽时拒绝请求。
    /// 优势：
    ///   - O(1) 时间复杂度（无需遍历和清理过期记录）
    ///   - 固定内存占用（每个桶仅存储少量字段，不随请求数增长）
    ///   - 天然支持突发流量（桶中累积的令牌可允许短时突发）
    ///   - 线程安全（使用 Interlocked CAS 原子操作，无锁竞争）
    ///   - 高并发同 IP 场景下无热点 lock（相比旧实现显著降低争用）
    /// </summary>
    internal sealed class TokenBucket
    {
        private readonly int _maxTokens;
        private readonly double _refillRatePerMs;

        // 使用 long 编码 tokens（乘以 1000 保留精度）避免 double 的 CAS 兼容性问题
        // _state 高 32 位 = lastRefillTimestamp（相对于 _baseTimestamp 的偏移量，ms）
        // _state 低 32 位 = tokens × 1000（毫令牌）
        private long _state;
        private readonly long _baseTimestamp;

        private const int MilliTokenScale = 1000;

        /// <summary>
        /// 创建令牌桶实例
        /// </summary>
        /// <param name="maxTokens">桶容量（最大令牌数），对应时间窗口内允许的最大请求数</param>
        /// <param name="windowMilliseconds">时间窗口长度（毫秒），令牌从空到满的补充周期</param>
        public TokenBucket(int maxTokens, double windowMilliseconds)
        {
            _maxTokens = maxTokens;
            _refillRatePerMs = maxTokens / windowMilliseconds;
            _baseTimestamp = Environment.TickCount64;

            // 初始状态：满令牌，时间偏移为 0
            _state = PackState(0, maxTokens * MilliTokenScale);
        }

        /// <summary>
        /// 尝试消耗一个令牌。如果桶中有可用令牌则消耗并返回 true，否则返回 false（表示请求被限流）。
        /// 使用 CAS 自旋实现无锁并发安全。
        /// </summary>
        public bool TryConsume()
        {
            var now = Environment.TickCount64;
            var relativeNow = (int)(now - _baseTimestamp);

            // CAS 自旋：读取当前状态 → 计算新状态 → 尝试原子替换
            while (true)
            {
                var currentState = Interlocked.Read(ref _state);
                UnpackState(currentState, out var lastTimeDelta, out var milliTokens);

                // 补充令牌
                var elapsedMs = relativeNow - lastTimeDelta;
                if (elapsedMs > 0)
                {
                    var refill = (int)(elapsedMs * _refillRatePerMs * MilliTokenScale);
                    milliTokens = Math.Min(_maxTokens * MilliTokenScale, milliTokens + refill);
                    lastTimeDelta = relativeNow;
                }

                // 检查是否有足够令牌
                if (milliTokens < MilliTokenScale)
                {
                    // 令牌不足，仍然更新时间戳（写入补充后的状态）
                    var noConsumeState = PackState(lastTimeDelta, milliTokens);
                    Interlocked.CompareExchange(ref _state, noConsumeState, currentState);
                    return false;
                }

                // 消耗一个令牌
                var newMilliTokens = milliTokens - MilliTokenScale;
                var newState = PackState(lastTimeDelta, newMilliTokens);

                if (Interlocked.CompareExchange(ref _state, newState, currentState) == currentState)
                {
                    return true;
                }
                // CAS 失败，其他线程修改了状态，重试
            }
        }

        /// <summary>
        /// 获取当前可用令牌数（近似值，仅用于监控和日志）
        /// </summary>
        public int AvailableTokens
        {
            get
            {
                var now = Environment.TickCount64;
                var relativeNow = (int)(now - _baseTimestamp);
                var currentState = Interlocked.Read(ref _state);
                UnpackState(currentState, out var lastTimeDelta, out var milliTokens);

                var elapsedMs = relativeNow - lastTimeDelta;
                if (elapsedMs > 0)
                {
                    var refill = (int)(elapsedMs * _refillRatePerMs * MilliTokenScale);
                    milliTokens = Math.Min(_maxTokens * MilliTokenScale, milliTokens + refill);
                }

                return milliTokens / MilliTokenScale;
            }
        }

        private static long PackState(int timeDelta, int milliTokens)
        {
            return ((long)(uint)timeDelta << 32) | (uint)milliTokens;
        }

        private static void UnpackState(long state, out int timeDelta, out int milliTokens)
        {
            timeDelta = (int)(uint)(state >> 32);
            milliTokens = (int)(uint)(state & 0xFFFFFFFFL);
        }
    }

    /// <summary>
    /// 令牌桶管理器：管理全局和路由级别的令牌桶实例。
    /// 使用 FusionCache 按 (IP + 路由键) 维护独立的令牌桶生命周期，
    /// SlidingExpiration 自动替代手动定时清理，FusionCache SizeLimit 提供内存驱逐。
    /// </summary>
    internal sealed class TokenBucketManager : IDisposable
    {
        private readonly IFusionCache _cache;
        private readonly TimeSpan _idleExpiry;

        /// <summary>
        /// 创建令牌桶管理器。
        /// </summary>
        /// <param name="cache">FusionCache 实例，负责令牌桶的生命周期与驱逐（由 DrxCacheProvider 提供）。</param>
        /// <param name="idleExpiry">令牌桶空闲超时时间。超过此时间未访问的桶将被 FusionCache SlidingExpiration 自动移除。</param>
        public TokenBucketManager(IFusionCache cache, TimeSpan idleExpiry)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _idleExpiry = idleExpiry;
        }

        /// <summary>
        /// 尝试消耗指定键的令牌桶中的一个令牌。
        /// 如果该键的令牌桶不存在则自动创建；每次访问后 SlidingExpiration 自动续期。
        /// </summary>
        /// <param name="key">桶的唯一标识（通常为 IP 或 IP#路由键）</param>
        /// <param name="maxTokens">桶容量</param>
        /// <param name="windowMilliseconds">补充周期（毫秒）</param>
        /// <returns>true 表示令牌可用（请求通过），false 表示被限流</returns>
        public bool TryConsume(string key, int maxTokens, double windowMilliseconds)
        {
            var entryOptions = new FusionCacheEntryOptions
            {
                Duration = _idleExpiry,
                IsFailSafeEnabled = false,
                Size = 1
            };
            var bucket = _cache.GetOrSet<TokenBucket>(
                key,
                _ => new TokenBucket(maxTokens, windowMilliseconds),
                entryOptions
            );
            return bucket!.TryConsume();
        }

        /// <summary>
        /// 获取指定键的当前可用令牌数（仅用于监控）。
        /// 返回 -1 表示该键对应的令牌桶不存在（已过期或从未创建）。
        /// </summary>
        public int GetAvailableTokens(string key)
        {
            var result = _cache.TryGet<TokenBucket>(key);
            return result.HasValue ? result.Value.AvailableTokens : -1;
        }

        /// <summary>
        /// 释放管理器。令牌桶 FusionCache 实例由 DrxCacheProvider 统一管理，此处无需 Dispose。
        /// </summary>
        public void Dispose()
        {
            // _cache 由 DrxCacheProvider 统一 Dispose，此处不重复释放。
        }
    }
}
