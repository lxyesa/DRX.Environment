using System;
using System.Collections.Generic;
using System.Threading;
using ZiggyCreatures.Caching.Fusion;

// 文件职责：路由匹配结果缓存外壳，内部由 IFusionCache 提供内存 LRU 存储；对外暴露与旧版完全兼容的 TryGet/Set/Invalidate/Clear API。
// 依赖：DrxCacheProvider.RouteMatch（drx:route 实例）；不启用 Redis 分布式层。
namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// 路由匹配结果缓存：对热点路径的路由匹配结果进行缓存，避免每次请求都遍历所有路由并执行正则匹配。
    /// 设计要点：
    ///   - 使用 (HttpMethod, Path) 作为缓存键
    ///   - 缓存命中时直接返回匹配的路由索引和提取的路径参数（O(1) 查询）
    ///   - FusionCache SizeLimit 提供 LRU 驱逐，替代原有自定义 PriorityQueue 堆排序
    ///   - 缓存失效：路由注册变更时递增版本号 + ExpireAsync 全量失效
    ///   - 线程安全：FusionCache 内置并发安全
    /// </summary>
    internal sealed class RouteMatchCache
    {
        private readonly IFusionCache _cache;
        private long _version = 0;
        private long _hits = 0;
        private long _misses = 0;
        private int _currentCount = 0;

        /// <summary>
        /// 缓存命中次数（用于性能监控）
        /// </summary>
        public long Hits => Interlocked.Read(ref _hits);

        /// <summary>
        /// 缓存未命中次数（用于性能监控）
        /// </summary>
        public long Misses => Interlocked.Read(ref _misses);

        /// <summary>
        /// 已记录的缓存写入次数（近似值，FusionCache 不暴露精确条目数）
        /// </summary>
        public int Count => Interlocked.CompareExchange(ref _currentCount, 0, 0);

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
            /// 缓存创建时的路由版本号，用于检测路由注册变更后的条目失效
            /// </summary>
            public long Version { get; set; }

            /// <summary>
            /// 是否为"未匹配"结果的缓存（避免反复查找不存在的路由）
            /// </summary>
            public bool IsNotFound { get; set; }
        }

        /// <summary>
        /// 创建路由匹配缓存，由调用方传入 FusionCache 实例（来自 DrxCacheProvider.RouteMatch）。
        /// </summary>
        /// <param name="cache">FusionCache 实例，已配置 SizeLimit 和默认 TTL。</param>
        public RouteMatchCache(IFusionCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
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
        /// <returns>true 表示缓存命中且结果有效（版本一致）</returns>
        public bool TryGet(string method, string path, out RouteMatchResult? result)
        {
            var key = BuildCacheKey(method, path);
            var maybeValue = _cache.TryGet<RouteMatchResult>(key);
            if (maybeValue.HasValue)
            {
                var cached = maybeValue.Value;
                var currentVersion = Interlocked.Read(ref _version);
                if (cached.Version == currentVersion)
                {
                    result = cached;
                    Interlocked.Increment(ref _hits);
                    return true;
                }
                // 版本不匹配，条目已因路由变更失效——异步移除，不阻塞请求路径
                _cache.Remove(key);
            }
            result = null;
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
                Version = Interlocked.Read(ref _version),
                IsNotFound = routeIndex < 0
            };
            _cache.Set(key, result);
            Interlocked.Increment(ref _currentCount);
        }

        /// <summary>
        /// 使缓存失效（当路由注册表发生变化时调用）。
        /// 递增版本号——后续所有 TryGet 对旧版本条目均视为 miss，条目在 TTL 到期后自然淘汰。
        /// </summary>
        public void Invalidate()
        {
            Interlocked.Increment(ref _version);
            Interlocked.Exchange(ref _currentCount, 0);
        }

        /// <summary>
        /// 完全清空缓存（递增版本号，旧条目在 TTL 内自然淘汰）
        /// </summary>
        public void Clear()
        {
            Interlocked.Increment(ref _version);
            Interlocked.Exchange(ref _currentCount, 0);
        }
    }
}

