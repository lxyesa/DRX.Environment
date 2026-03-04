using System;
using System.Collections.Concurrent;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// 客户端条件请求元数据缓存（任务 4.1）。
    /// 
    /// 轻量、线程安全的本地缓存，存储已下载资源的 ETag 和 Last-Modified。
    /// 供后续请求自动注入 If-None-Match / If-Modified-Since 条件请求头，
    /// 命中时服务端返回 304（无响应体），大幅减少重复流量。
    /// 
    /// 设计原则：
    ///   - URL 规范化（小写、去尾斜杠）作为缓存 key，避免重复缓存同一资源。
    ///   - 条目数量限制 + 随机驱逐，防止无限增长。
    ///   - 不持久化、不依赖外部存储，进程级生命周期。
    ///   - 启用开关（Enabled = false 时完全透明，不影响现有行为）。
    /// </summary>
    public sealed class HttpConditionalRequestCache
    {
        #region 单例

        private static HttpConditionalRequestCache? _instance;
        private static readonly object _instanceLock = new();

        /// <summary>
        /// 全局单例实例。与 HttpMetrics 类似采用懒加载单例。
        /// </summary>
        public static HttpConditionalRequestCache Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        _instance ??= new HttpConditionalRequestCache();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region 元数据条目

        /// <summary>
        /// 资源条件请求元数据：ETag 与 Last-Modified
        /// </summary>
        public sealed class CacheEntry
        {
            /// <summary>
            /// 资源 ETag（含引号，如 "\"abc123\""）；null 表示服务端未返回 ETag。
            /// </summary>
            public string? ETag { get; set; }

            /// <summary>
            /// 资源 Last-Modified；null 表示服务端未返回该头。
            /// </summary>
            public DateTimeOffset? LastModified { get; set; }

            /// <summary>
            /// 条目最后更新时间（UTC）
            /// </summary>
            public DateTime UpdatedAtUtc { get; set; }
        }

        #endregion

        #region 存储与配置

        /// <summary>
        /// 内部缓存字典：规范化 URL → 元数据条目
        /// </summary>
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

        /// <summary>
        /// 缓存最大条目数；超出时随机驱逐，防止内存溢出。
        /// </summary>
        public int MaxEntries { get; set; } = 4096;

        /// <summary>
        /// 全局启用开关。False 时所有操作均为空操作，完全不影响现有行为。
        /// </summary>
        public bool Enabled { get; set; } = true;

        private static readonly Random _random = new();

        #endregion

        private HttpConditionalRequestCache() { }

        #region 公共 API

        /// <summary>
        /// 从响应中提取并存储条件请求元数据（ETag / Last-Modified）。
        /// 应在每次成功下载（200）后调用，为下一次条件请求做准备。
        /// </summary>
        /// <param name="url">请求的原始 URL</param>
        /// <param name="etag">响应 ETag 头（可为 null）</param>
        /// <param name="lastModified">响应 Last-Modified 头（可为 null）</param>
        public void Store(string url, string? etag, DateTimeOffset? lastModified)
        {
            if (!Enabled) return;
            if (string.IsNullOrEmpty(url)) return;
            if (string.IsNullOrEmpty(etag) && lastModified == null) return;

            var key = NormalizeUrl(url);
            var entry = new CacheEntry
            {
                ETag = etag,
                LastModified = lastModified,
                UpdatedAtUtc = DateTime.UtcNow
            };

            // 超出容量时随机驱逐一个条目
            if (_cache.Count >= MaxEntries)
                EvictRandom();

            _cache[key] = entry;
        }

        /// <summary>
        /// 尝试获取指定 URL 的条件请求元数据。
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="entry">找到时返回缓存条目；否则为 null</param>
        /// <returns>true 表示找到缓存；false 表示未缓存</returns>
        public bool TryGet(string url, out CacheEntry? entry)
        {
            if (!Enabled || string.IsNullOrEmpty(url))
            {
                entry = null;
                return false;
            }

            var key = NormalizeUrl(url);
            return _cache.TryGetValue(key, out entry);
        }

        /// <summary>
        /// 使指定 URL 的缓存失效（资源已知发生变更时调用）。
        /// </summary>
        public void Invalidate(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            _cache.TryRemove(NormalizeUrl(url), out _);
        }

        /// <summary>
        /// 清空所有缓存条目。
        /// </summary>
        public void Clear() => _cache.Clear();

        /// <summary>
        /// 当前缓存条目数。
        /// </summary>
        public int Count => _cache.Count;

        #endregion

        #region 内部辅助

        /// <summary>
        /// URL 规范化：统一小写、移除查询参数中的随机成分（仅保留路径+固定参数）。
        /// 简单版：直接小写 + 去尾斜杠，适合大多数静态资源场景。
        /// </summary>
        private static string NormalizeUrl(string url)
        {
            var lower = url.Trim().ToLowerInvariant();
            // 去掉尾部斜杠（路径层面）
            if (lower.Length > 1 && lower.EndsWith('/'))
                lower = lower.TrimEnd('/');
            return lower;
        }

        /// <summary>
        /// 随机驱逐一个缓存条目，用于容量超限的简单淘汰策略。
        /// </summary>
        private void EvictRandom()
        {
            try
            {
                // 获取所有 key 快照，随机取一个删除
                var keys = _cache.Keys;
                int total = _cache.Count;
                if (total == 0) return;

                int skip = _random.Next(total);
                int idx = 0;
                foreach (var k in keys)
                {
                    if (idx++ >= skip)
                    {
                        _cache.TryRemove(k, out _);
                        return;
                    }
                }
            }
            catch
            {
                // 驱逐失败静默处理
            }
        }

        #endregion
    }
}
