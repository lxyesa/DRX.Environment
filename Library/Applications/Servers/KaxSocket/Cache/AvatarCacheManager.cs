using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KaxSocket.Cache
{
    /// <summary>
    /// 用户头像本地缓存管理器
    /// 使用 LRU 缓存策略，避免频繁的磁盘 I/O 操作
    /// </summary>
    public class AvatarCacheManager
    {
        private class CacheEntry
        {
            public int UserId { get; set; }
            public byte[] ImageData { get; set; }
            public string ContentType { get; set; }
            public long CreatedAt { get; set; }
            public long LastAccessedAt { get; set; }
        }

        private readonly Dictionary<int, CacheEntry> _cache = new();
        private readonly int _maxCacheSize;
        private readonly long _cacheExpirationMs;
        private readonly object _lockObj = new();

        /// <summary>
        /// 初始化缓存管理器
        /// </summary>
        /// <param name="maxCacheSize">最大缓存条目数（默认 100）</param>
        /// <param name="cacheExpirationSeconds">缓存过期时间（秒，默认 3600 秒 = 1 小时）</param>
        public AvatarCacheManager(int maxCacheSize = 100, int cacheExpirationSeconds = 3600)
        {
            _maxCacheSize = maxCacheSize;
            _cacheExpirationMs = (long)cacheExpirationSeconds * 1000;
        }

        /// <summary>
        /// 尝试从缓存获取头像数据
        /// </summary>
        public bool TryGetAvatar(int userId, out byte[]? imageData, out string? contentType)
        {
            imageData = null;
            contentType = null;

            lock (_lockObj)
            {
                if (!_cache.TryGetValue(userId, out var entry))
                    return false;

                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (now - entry.CreatedAt > _cacheExpirationMs)
                {
                    _cache.Remove(userId);
                    return false;
                }

                entry.LastAccessedAt = now;
                imageData = entry.ImageData;
                contentType = entry.ContentType;
                return true;
            }
        }

        /// <summary>
        /// 将头像数据存入缓存
        /// </summary>
        public void SetAvatar(int userId, byte[] imageData, string contentType)
        {
            if (imageData == null || imageData.Length == 0)
                return;

            lock (_lockObj)
            {
                if (_cache.Count >= _maxCacheSize)
                {
                    EvictLRU();
                }

                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _cache[userId] = new CacheEntry
                {
                    UserId = userId,
                    ImageData = imageData,
                    ContentType = contentType,
                    CreatedAt = now,
                    LastAccessedAt = now
                };
            }
        }

        /// <summary>
        /// 清除指定用户的缓存（用户上传新头像时调用）
        /// </summary>
        public void InvalidateAvatar(int userId)
        {
            lock (_lockObj)
            {
                _cache.Remove(userId);
            }
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void Clear()
        {
            lock (_lockObj)
            {
                _cache.Clear();
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public (int CacheCount, int MaxSize) GetStats()
        {
            lock (_lockObj)
            {
                return (_cache.Count, _maxCacheSize);
            }
        }

        /// <summary>
        /// 驱逐最久未使用的缓存条目
        /// </summary>
        private void EvictLRU()
        {
            if (_cache.Count == 0)
                return;

            var lruEntry = _cache.Values.OrderBy(e => e.LastAccessedAt).First();
            _cache.Remove(lruEntry.UserId);
        }
    }
}
