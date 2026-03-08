using System;

namespace Drx.Sdk.Network.Http.Cache
{
    /// <summary>
    /// 缓存配置模块。
    /// 负责集中定义各缓存子系统的容量、过期时间与 Redis 可选配置。
    /// 供 DrxHttpServerOptions 与后续 DrxCacheProvider 使用。
    /// </summary>
    public sealed class DrxCacheOptions
    {
        /// <summary>
        /// 静态资源缓存最大条目数。
        /// 默认值：10000。
        /// </summary>
        public int StaticContentMaxEntries { get; set; } = 10_000;

        /// <summary>
        /// 小文件内容缓存阈值（字节）。
        /// 小于等于该值时可将文件内容直接缓存到内存。
        /// 默认值：512KB。
        /// </summary>
        public int SmallFileCacheThresholdBytes { get; set; } = 512 * 1024;

        /// <summary>
        /// 静态资源缓存默认 TTL。
        /// 默认值：1 小时。
        /// </summary>
        public TimeSpan StaticContentDefaultDuration { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// 路由匹配缓存最大条目数。
        /// 默认值：2048。
        /// </summary>
        public int RouteCacheMaxSize { get; set; } = 2048;

        /// <summary>
        /// 路由匹配缓存 TTL。
        /// 默认值：30 分钟。
        /// </summary>
        public TimeSpan RouteCacheDuration { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// 中间件路径缓存最大条目数。
        /// 默认值：2048。
        /// </summary>
        public int MiddlewareCacheMaxSize { get; set; } = 2048;

        /// <summary>
        /// 中间件路径缓存 TTL。
        /// 默认值：30 分钟。
        /// </summary>
        public TimeSpan MiddlewareCacheDuration { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// 令牌桶空闲过期时间。
        /// 默认值：5 分钟。
        /// </summary>
        public TimeSpan TokenBucketIdleExpiry { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Redis 连接字符串。
        /// 为空表示禁用 Redis 二级缓存。
        /// </summary>
        public string? RedisConnectionString { get; set; }

        /// <summary>
        /// 是否为静态资源缓存启用 Redis 二级缓存。
        /// 默认值：false。
        /// </summary>
        public bool EnableRedisForStaticContent { get; set; } = false;

        /// <summary>
        /// 是否为头像缓存启用 Redis 二级缓存。
        /// 默认值：false。
        /// </summary>
        public bool EnableRedisForAvatarCache { get; set; } = false;

        /// <summary>
        /// 校验并规范化缓存配置，避免非法值导致运行时异常。
        /// </summary>
        public void Validate()
        {
            if (StaticContentMaxEntries <= 0) StaticContentMaxEntries = 10_000;
            if (SmallFileCacheThresholdBytes < 0) SmallFileCacheThresholdBytes = 0;
            if (StaticContentDefaultDuration <= TimeSpan.Zero) StaticContentDefaultDuration = TimeSpan.FromHours(1);

            if (RouteCacheMaxSize <= 0) RouteCacheMaxSize = 2048;
            if (RouteCacheDuration <= TimeSpan.Zero) RouteCacheDuration = TimeSpan.FromMinutes(30);

            if (MiddlewareCacheMaxSize <= 0) MiddlewareCacheMaxSize = 2048;
            if (MiddlewareCacheDuration <= TimeSpan.Zero) MiddlewareCacheDuration = TimeSpan.FromMinutes(30);

            if (TokenBucketIdleExpiry <= TimeSpan.Zero) TokenBucketIdleExpiry = TimeSpan.FromMinutes(5);
        }
    }
}
