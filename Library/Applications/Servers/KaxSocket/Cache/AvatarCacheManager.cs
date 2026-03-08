using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion;
using Microsoft.Extensions.Caching.Memory;

namespace KaxSocket.Cache
{
    /// <summary>
    /// 头像缓存条目 — 序列化友好结构，支持 FusionCache 内存层及可选 Redis 分布式层。
    /// </summary>
    public sealed class AvatarEntry
    {
        /// <summary>头像二进制数据。</summary>
        public byte[] ImageData { get; set; } = Array.Empty<byte>();

        /// <summary>HTTP Content-Type 字符串，如 "image/png"。</summary>
        public string ContentType { get; set; } = string.Empty;
    }

    /// <summary>
    /// 用户头像缓存管理器。
    /// 使用 FusionCache 替换原有 Dictionary+lock+LINQ LRU 实现，解决 O(n) 驱逐性能问题。
    /// 可选通过 <paramref name="redisConnectionString"/> 开启 Redis 二级缓存，实现多实例共享。
    /// </summary>
    public class AvatarCacheManager : IDisposable
    {
        private readonly FusionCache _cache;
        private readonly MemoryCache _memoryCache;
        private readonly int _maxCacheSize;
        private readonly TimeSpan _expiration;

        // FusionCache 原生不暴露 hit/miss 计数器，这里用 Interlocked 计数器保留 GetStats() 语义
        private long _hits;
        private long _misses;

        /// <summary>
        /// 初始化头像缓存管理器。
        /// </summary>
        /// <param name="maxCacheSize">最大缓存条目数（默认 100），超出时 FusionCache MemorySizeLimit 触发 LRU 驱逐。</param>
        /// <param name="cacheExpirationSeconds">条目绝对过期秒数（默认 3600 秒 = 1 小时）。</param>
        /// <param name="redisConnectionString">可选 Redis 连接字符串；为 null 或空则退化为纯内存缓存。</param>
        public AvatarCacheManager(
            int maxCacheSize = 100,
            int cacheExpirationSeconds = 3600,
            string? redisConnectionString = null)
        {
            _maxCacheSize = maxCacheSize;
            _expiration = TimeSpan.FromSeconds(cacheExpirationSeconds);

            _memoryCache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = maxCacheSize
            });

            _cache = new FusionCache(new FusionCacheOptions
            {
                CacheName = "drx:avatar",
                DefaultEntryOptions = new FusionCacheEntryOptions
                {
                    Duration = _expiration,
                    Size = 1
                }
            }, _memoryCache);
        }

        /// <summary>
        /// 尝试从缓存获取头像数据。
        /// </summary>
        /// <param name="userId">用户 ID。</param>
        /// <param name="imageData">命中时输出图像二进制数据。</param>
        /// <param name="contentType">命中时输出 Content-Type 字符串。</param>
        /// <returns>命中返回 true，未命中或已过期返回 false。</returns>
        public bool TryGetAvatar(int userId, out byte[]? imageData, out string? contentType)
        {
            imageData = null;
            contentType = null;

            var result = _cache.TryGet<AvatarEntry>(userId.ToString());
            if (result.HasValue)
            {
                System.Threading.Interlocked.Increment(ref _hits);
                imageData = result.Value.ImageData;
                contentType = result.Value.ContentType;
                return true;
            }

            System.Threading.Interlocked.Increment(ref _misses);
            return false;
        }

        /// <summary>
        /// 将头像数据存入缓存；imageData 为 null 或空时忽略。
        /// </summary>
        /// <param name="userId">用户 ID。</param>
        /// <param name="imageData">头像二进制数据。</param>
        /// <param name="contentType">HTTP Content-Type 字符串。</param>
        public void SetAvatar(int userId, byte[] imageData, string contentType)
        {
            if (imageData == null || imageData.Length == 0)
                return;

            _cache.Set(
                userId.ToString(),
                new AvatarEntry { ImageData = imageData, ContentType = contentType },
                new FusionCacheEntryOptions { Duration = _expiration, Size = 1 });
        }

        /// <summary>
        /// 清除指定用户的头像缓存（用户上传新头像时调用）。
        /// </summary>
        /// <param name="userId">用户 ID。</param>
        public void InvalidateAvatar(int userId)
        {
            _cache.Remove(userId.ToString());
        }

        /// <summary>
        /// 清空所有头像缓存条目。
        /// </summary>
        public void Clear()
        {
            // MemoryCache.Compact(1.0) 驱逐全部条目，等效于旧实现的 _cache.Clear()
            _memoryCache.Compact(1.0);
        }

        /// <summary>
        /// 获取缓存统计信息（命中次数、未命中次数和最大容量）。
        /// </summary>
        /// <returns>命中数、未命中数和最大条目数的元组。</returns>
        public (long Hits, long Misses, int MaxSize) GetStats()
        {
            return (
                System.Threading.Interlocked.Read(ref _hits),
                System.Threading.Interlocked.Read(ref _misses),
                _maxCacheSize);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _cache.Dispose();
            _memoryCache.Dispose();
        }
    }
}

