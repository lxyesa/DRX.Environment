using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// 路由匹配结果缓存：对热点路径的路由匹配结果进行缓存，避免每次请求都遍历所有路由并执行正则匹配。
    /// 设计要点：
    ///   - 使用 (HttpMethod, Path) 作为缓存键
    ///   - 缓存命中时直接返回匹配的路由索引和提取的路径参数（O(1) 查询）
    ///   - LRU 淘汰策略：当缓存条目超过上限时，清除最久未访问的条目
    ///   - 缓存失效：当路由注册表发生变化时，自动清除整个缓存
    ///   - 线程安全：使用 ConcurrentDictionary 保证并发安全
    /// </summary>
    internal sealed class RouteMatchCache
    {
        private readonly ConcurrentDictionary<string, RouteMatchResult> _cache = new();
        private int _currentCount = 0;
        private readonly int _maxSize;
        private long _version = 0;
        private long _hits = 0;
        private long _misses = 0;

        /// <summary>
        /// 缓存命中次数（用于性能监控）
        /// </summary>
        public long Hits => Interlocked.Read(ref _hits);

        /// <summary>
        /// 缓存未命中次数（用于性能监控）
        /// </summary>
        public long Misses => Interlocked.Read(ref _misses);

        /// <summary>
        /// 当前缓存条目数
        /// </summary>
        public int Count => _currentCount;

        /// <summary>
        /// 路由匹配结果
        /// </summary>
        internal sealed class RouteMatchResult
        {
            /// <summary>
            /// 匹配到的路由在列表中的索引（-1 表示无匹配）
            /// </summary>
            public int RouteIndex { get; set; } = -1;

            /// <summary>
            /// 从路径中提取的命名参数（可能为 null 表示精确路径无参数）
            /// </summary>
            public Dictionary<string, string>? Parameters { get; set; }

            /// <summary>
            /// 上次访问时间戳（用于 LRU 淘汰）
            /// </summary>
            public long LastAccessTimestamp { get; set; }

            /// <summary>
            /// 缓存创建时的路由版本号
            /// </summary>
            public long Version { get; set; }

            /// <summary>
            /// 是否为"未匹配"结果的缓存（避免反复查找不存在的路由）
            /// </summary>
            public bool IsNotFound { get; set; }
        }

        /// <summary>
        /// 创建路由匹配缓存
        /// </summary>
        /// <param name="maxSize">缓存最大条目数，超出时触发淘汰。默认 2048，覆盖大多数 API 服务器的路由规模。</param>
        public RouteMatchCache(int maxSize = 2048)
        {
            _maxSize = maxSize;
        }

        /// <summary>
        /// 构建缓存键（复合 HttpMethod + Path）
        /// </summary>
        private static string BuildCacheKey(string method, string path)
        {
            return string.Concat(method, ":", path);
        }

        /// <summary>
        /// 尝试从缓存中获取路由匹配结果
        /// </summary>
        /// <param name="method">HTTP 方法</param>
        /// <param name="path">请求路径</param>
        /// <param name="result">缓存的匹配结果</param>
        /// <returns>true 表示缓存命中且结果有效</returns>
        public bool TryGet(string method, string path, out RouteMatchResult? result)
        {
            var key = BuildCacheKey(method, path);
            if (_cache.TryGetValue(key, out result))
            {
                var currentVersion = Interlocked.Read(ref _version);
                if (result.Version == currentVersion)
                {
                    result.LastAccessTimestamp = Environment.TickCount64;
                    Interlocked.Increment(ref _hits);
                    return true;
                }
                _cache.TryRemove(key, out _);
                Interlocked.Decrement(ref _currentCount);
                result = null;
            }
            Interlocked.Increment(ref _misses);
            return false;
        }

        /// <summary>
        /// 将路由匹配结果存入缓存
        /// </summary>
        /// <param name="method">HTTP 方法</param>
        /// <param name="path">请求路径</param>
        /// <param name="routeIndex">匹配到的路由索引（-1 表示未匹配）</param>
        /// <param name="parameters">路径参数</param>
        public void Set(string method, string path, int routeIndex, Dictionary<string, string>? parameters)
        {
            var key = BuildCacheKey(method, path);
            var result = new RouteMatchResult
            {
                RouteIndex = routeIndex,
                Parameters = parameters != null ? new Dictionary<string, string>(parameters) : null,
                LastAccessTimestamp = Environment.TickCount64,
                Version = Interlocked.Read(ref _version),
                IsNotFound = routeIndex < 0
            };

            if (_cache.TryAdd(key, result))
            {
                var count = Interlocked.Increment(ref _currentCount);
                if (count > _maxSize)
                {
                    EvictOldEntries();
                }
            }
            else
            {
                _cache[key] = result;
            }
        }

        /// <summary>
        /// 使缓存失效（当路由注册表发生变化时调用）。
        /// 通过递增版本号实现延迟失效，避免清空操作的锁竞争。
        /// </summary>
        public void Invalidate()
        {
            Interlocked.Increment(ref _version);
        }

        /// <summary>
        /// 完全清空缓存
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            Interlocked.Exchange(ref _currentCount, 0);
            Interlocked.Increment(ref _version);
        }

        /// <summary>
        /// 淘汰最久未访问的条目（保留最近 75% 的条目）
        /// 优化：使用堆排序而非完整排序，减少内存分配和 CPU 开销
        /// </summary>
        private void EvictOldEntries()
        {
            var targetCount = _maxSize * 3 / 4;
            var toRemoveCount = _cache.Count - targetCount;
            
            if (toRemoveCount <= 0) return;

            // 使用优先队列（最小堆）找出最久未访问的 toRemoveCount 个条目
            var minHeap = new PriorityQueue<(string Key, long Timestamp), long>();
            
            foreach (var kvp in _cache)
            {
                minHeap.Enqueue((kvp.Key, kvp.Value.LastAccessTimestamp), kvp.Value.LastAccessTimestamp);
                
                // 保持堆大小为 toRemoveCount，只保留最小的元素
                if (minHeap.Count > toRemoveCount)
                {
                    minHeap.Dequeue();
                }
            }

            // 移除堆中的所有条目（这些是最久未访问的）
            while (minHeap.Count > 0)
            {
                var (key, _) = minHeap.Dequeue();
                if (_cache.TryRemove(key, out _))
                {
                    Interlocked.Decrement(ref _currentCount);
                }
            }
        }
    }
}
