using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Drx.Sdk.Shared;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

// 文件职责：集中创建与管理 Drx HTTP 服务器使用的 5 个 FusionCache 实例，并统一处理可选 Redis 二级缓存挂载与诊断日志。
// 结构关系：本模块被 DrxHttpServer 生命周期持有；依赖 DrxCacheOptions 提供容量/TTL/Redis 参数；对外暴露各缓存实例供静态资源、路由、中间件与限流子系统复用。
namespace Drx.Sdk.Network.Http.Cache
{
    /// <summary>
    /// DRX 缓存提供器：创建并托管命名 FusionCache 实例，统一内存容量、默认 TTL 与可选 Redis 二级缓存配置。
    /// </summary>
    public sealed class DrxCacheProvider : IDisposable, IAsyncDisposable
    {
        private readonly List<object> _ownedResources = new();
        private bool _disposed;

        /// <summary>
        /// 静态资源缓存实例（drx:static）。
        /// </summary>
        public IFusionCache StaticContent { get; }

        /// <summary>
        /// 路由匹配缓存实例（drx:route）。
        /// </summary>
        public IFusionCache RouteMatch { get; }

        /// <summary>
        /// 中间件路径缓存实例（drx:middleware）。
        /// </summary>
        public IFusionCache MiddlewarePath { get; }

        /// <summary>
        /// 令牌桶生命周期缓存实例（drx:ratelimit）。
        /// </summary>
        public IFusionCache TokenBucket { get; }

        /// <summary>
        /// 限流键字符串缓存实例（drx:ratelimit-key）。
        /// </summary>
        public IFusionCache RateLimitKey { get; }

        /// <summary>
        /// 创建缓存提供器并初始化全部缓存实例。
        /// </summary>
        /// <param name="options">缓存容量、TTL 与 Redis 开关配置。</param>
        public DrxCacheProvider(DrxCacheOptions options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            options.Validate();

            StaticContent = CreateCache(
                cacheName: "drx:static",
                sizeLimit: options.StaticContentMaxEntries,
                duration: options.StaticContentDefaultDuration,
                enableRedisDistributedLayer: options.EnableRedisForStaticContent,
                redisConnectionString: options.RedisConnectionString
            );

            RouteMatch = CreateCache(
                cacheName: "drx:route",
                sizeLimit: options.RouteCacheMaxSize,
                duration: options.RouteCacheDuration,
                enableRedisDistributedLayer: false,
                redisConnectionString: null
            );

            MiddlewarePath = CreateCache(
                cacheName: "drx:middleware",
                sizeLimit: options.MiddlewareCacheMaxSize,
                duration: options.MiddlewareCacheDuration,
                enableRedisDistributedLayer: false,
                redisConnectionString: null
            );

            TokenBucket = CreateCache(
                cacheName: "drx:ratelimit",
                sizeLimit: Math.Max(options.RouteCacheMaxSize, 2048),
                duration: options.TokenBucketIdleExpiry,
                enableRedisDistributedLayer: false,
                redisConnectionString: null
            );

            RateLimitKey = CreateCache(
                cacheName: "drx:ratelimit-key",
                sizeLimit: Math.Max(options.RouteCacheMaxSize, 4096),
                duration: TimeSpan.FromMinutes(30),
                enableRedisDistributedLayer: false,
                redisConnectionString: null
            );
        }

        /// <summary>
        /// 释放所有缓存及其底层资源。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            DisposeOwnedResources(syncOnly: true).GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 异步释放所有缓存及其底层资源。
        /// </summary>
        /// <returns>表示异步释放流程的任务。</returns>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            await DisposeOwnedResources(syncOnly: false);
            GC.SuppressFinalize(this);
        }

        private IFusionCache CreateCache(
            string cacheName,
            long sizeLimit,
            TimeSpan duration,
            bool enableRedisDistributedLayer,
            string? redisConnectionString)
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = sizeLimit,
            });

            var fusionOptions = new FusionCacheOptions
            {
                CacheName = cacheName,
                DefaultEntryOptions = new FusionCacheEntryOptions(duration).SetSize(1),
                EnableAutoRecovery = true,
            };

            var cache = new FusionCache(
                optionsAccessor: Options.Create(fusionOptions),
                memoryCache: memoryCache,
                logger: null,
                memoryLocker: null
            );

            RegisterDiagnostics(cacheName, cache);
            TryAttachRedisDistributedLayer(cacheName, cache, enableRedisDistributedLayer, redisConnectionString);

            _ownedResources.Add(cache);
            _ownedResources.Add(memoryCache);

            return cache;
        }

        private static void RegisterDiagnostics(string cacheName, IFusionCache cache)
        {
            cache.Events.Hit += (_, evt) => Logger.Debug($"[DrxCacheProvider:{cacheName}] hit key={evt.Key}");
            cache.Events.Miss += (_, evt) => Logger.Debug($"[DrxCacheProvider:{cacheName}] miss key={evt.Key}");
            cache.Events.Set += (_, evt) => Logger.Debug($"[DrxCacheProvider:{cacheName}] set key={evt.Key}");
        }

        private void TryAttachRedisDistributedLayer(
            string cacheName,
            IFusionCache cache,
            bool enabled,
            string? redisConnectionString)
        {
            if (enabled is false || string.IsNullOrWhiteSpace(redisConnectionString))
                return;

            try
            {
                var redisBackplane = new RedisBackplane(Options.Create(new RedisBackplaneOptions
                {
                    Configuration = redisConnectionString,
                }), logger: null);

                cache.SetupBackplane(redisBackplane);

                _ownedResources.Add(redisBackplane);
                Logger.Info($"[DrxCacheProvider:{cacheName}] Redis backplane enabled");
            }
            catch (Exception ex)
            {
                Logger.Warn($"[DrxCacheProvider:{cacheName}] Redis attach failed, fallback to memory-only: {ex.Message}");
            }
        }

        private async Task DisposeOwnedResources(bool syncOnly)
        {
            for (var i = _ownedResources.Count - 1; i >= 0; i--)
            {
                var resource = _ownedResources[i];

                try
                {
                    if (!syncOnly && resource is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                        continue;
                    }

                    if (resource is IDisposable disposable)
                    {
                        disposable.Dispose();
                        continue;
                    }

                    if (syncOnly && resource is IAsyncDisposable syncAsyncDisposable)
                    {
                        syncAsyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[DrxCacheProvider] Dispose resource failed: {ex.Message}");
                }
            }

            _ownedResources.Clear();
        }
    }
}
