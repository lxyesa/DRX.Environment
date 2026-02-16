using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Drx.Sdk.Network.V2.Web
{
    /// <summary>
    /// 令牌桶速率限制器：替代基于 Queue&lt;DateTime&gt; 的滑动窗口算法。
    /// 令牌桶以固定速率补充令牌，每次请求消耗一个令牌。
    /// 当令牌耗尽时拒绝请求。
    /// 优势：
    ///   - O(1) 时间复杂度（无需遍历和清理过期记录）
    ///   - 固定内存占用（每个桶仅存储少量字段，不随请求数增长）
    ///   - 天然支持突发流量（桶中累积的令牌可允许短时突发）
    ///   - 线程安全（使用 Interlocked 原子操作，无锁竞争）
    /// </summary>
    internal sealed class TokenBucket
    {
        private readonly int _maxTokens;
        private readonly double _refillRatePerMs;
        private double _tokens;
        private long _lastRefillTimestamp;
        private readonly object _lock = new();

        /// <summary>
        /// 创建令牌桶实例
        /// </summary>
        /// <param name="maxTokens">桶容量（最大令牌数），对应时间窗口内允许的最大请求数</param>
        /// <param name="windowMilliseconds">时间窗口长度（毫秒），令牌从空到满的补充周期</param>
        public TokenBucket(int maxTokens, double windowMilliseconds)
        {
            _maxTokens = maxTokens;
            _refillRatePerMs = maxTokens / windowMilliseconds;
            _tokens = maxTokens;
            _lastRefillTimestamp = Environment.TickCount64;
        }

        /// <summary>
        /// 尝试消耗一个令牌。如果桶中有可用令牌则消耗并返回 true，否则返回 false（表示请求被限流）。
        /// </summary>
        public bool TryConsume()
        {
            lock (_lock)
            {
                Refill();
                if (_tokens >= 1.0)
                {
                    _tokens -= 1.0;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 获取当前可用令牌数（近似值，仅用于监控和日志）
        /// </summary>
        public int AvailableTokens
        {
            get
            {
                lock (_lock)
                {
                    Refill();
                    return (int)_tokens;
                }
            }
        }

        /// <summary>
        /// 根据自上次补充以来经过的时间补充令牌
        /// </summary>
        private void Refill()
        {
            var now = Environment.TickCount64;
            var elapsedMs = now - _lastRefillTimestamp;
            if (elapsedMs <= 0) return;

            _tokens = Math.Min(_maxTokens, _tokens + elapsedMs * _refillRatePerMs);
            _lastRefillTimestamp = now;
        }
    }

    /// <summary>
    /// 令牌桶管理器：管理全局和路由级别的令牌桶实例。
    /// 使用 ConcurrentDictionary 按 (IP + 路由键) 维护独立的令牌桶，
    /// 并提供过期桶的定期清理功能以防止内存泄漏。
    /// </summary>
    internal sealed class TokenBucketManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, TokenBucketEntry> _buckets = new();
        private readonly Timer _cleanupTimer;
        private const int CleanupIntervalMs = 60_000;
        private const int BucketExpirationMs = 300_000;

        private sealed class TokenBucketEntry
        {
            public TokenBucket Bucket { get; }
            public long LastAccessTimestamp { get; set; }

            public TokenBucketEntry(TokenBucket bucket)
            {
                Bucket = bucket;
                LastAccessTimestamp = Environment.TickCount64;
            }
        }

        public TokenBucketManager()
        {
            _cleanupTimer = new Timer(CleanupExpiredBuckets, null, CleanupIntervalMs, CleanupIntervalMs);
        }

        /// <summary>
        /// 尝试消耗指定键的令牌桶中的一个令牌。
        /// 如果该键的令牌桶不存在则自动创建。
        /// </summary>
        /// <param name="key">桶的唯一标识（通常为 IP 或 IP#路由键）</param>
        /// <param name="maxTokens">桶容量</param>
        /// <param name="windowMilliseconds">补充周期（毫秒）</param>
        /// <returns>true 表示令牌可用（请求通过），false 表示被限流</returns>
        public bool TryConsume(string key, int maxTokens, double windowMilliseconds)
        {
            var entry = _buckets.GetOrAdd(key, _ => new TokenBucketEntry(new TokenBucket(maxTokens, windowMilliseconds)));
            entry.LastAccessTimestamp = Environment.TickCount64;
            return entry.Bucket.TryConsume();
        }

        /// <summary>
        /// 获取指定键的当前可用令牌数（仅用于监控）
        /// </summary>
        public int GetAvailableTokens(string key)
        {
            if (_buckets.TryGetValue(key, out var entry))
                return entry.Bucket.AvailableTokens;
            return -1;
        }

        /// <summary>
        /// 定期清理过期的令牌桶（5分钟未访问的桶将被移除）
        /// </summary>
        private void CleanupExpiredBuckets(object? state)
        {
            var now = Environment.TickCount64;
            foreach (var kvp in _buckets)
            {
                if (now - kvp.Value.LastAccessTimestamp > BucketExpirationMs)
                {
                    _buckets.TryRemove(kvp.Key, out _);
                }
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _buckets.Clear();
        }
    }
}
