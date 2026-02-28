using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpServer 静态资源服务部分：HTML/CSS/JS/图片等静态内容的提供与基于内容Hash的缓存
    /// 职责边界：仅负责"页面资源"类静态文件的解析与响应，不涉及二进制文件上传/下载传输。
    /// </summary>
    public partial class DrxHttpServer
    {
        #region 静态文件缓存条目

        /// <summary>
        /// 静态文件缓存条目：存储文件的 ETag、最后修改时间、大小以及可选的内容缓存
        /// </summary>
        private sealed class StaticFileCacheEntry
        {
            public string ETag { get; set; } = "";
            public DateTime LastModifiedUtc { get; set; }
            public long FileSize { get; set; }
            public byte[]? CachedContent { get; set; }
            public string ContentType { get; set; } = "application/octet-stream";
        }

        /// <summary>
        /// 小文件内容缓存阈值（小于此值的文件将缓存到内存中）
        /// </summary>
        private const int SmallFileCacheThreshold = 512 * 1024;

        /// <summary>
        /// 静态资源缓存字典：文件绝对路径 → 缓存条目
        /// </summary>
        private readonly ConcurrentDictionary<string, StaticFileCacheEntry> _staticContentCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 静态资源浏览器缓存时长（秒），默认 0（每次请求都走 ETag 验证）。
        /// 设为 0 时使用 no-cache 策略：浏览器仍会缓存文件，但每次请求都会向服务器发送
        /// ETag/If-None-Match 验证，未修改时返回 304（无传输开销），文件变更后立即生效。
        /// 设为正数 N 时使用 max-age=N：浏览器在 N 秒内完全不向服务器发请求，有可能导致
        /// 文件更新后浏览器仍使用旧缓存。仅适用于内容极少变更的生产环境。
        /// </summary>
        public int StaticContentMaxAgeSec { get; set; } = 0;

        #endregion

        #region 静态资源服务入口

        /// <summary>
        /// 尝试为请求提供静态资源（HTML/CSS/JS/图片等），并支持 ETag 缓存验证（304 Not Modified）。
        /// 此方法统一处理来自 ViewRoot、FileRootPath 和旧版 _staticFileRoot 的静态资源查找。
        /// 返回非 null 的 HttpResponse 表示已匹配到静态资源（可能是 200 或 304）；返回 null 表示未匹配。
        /// </summary>
        private HttpResponse? TryServeStaticContent(HttpRequest request)
        {
            try
            {
                var relPath = request.Path ?? "/";
                if (relPath.StartsWith("/")) relPath = relPath.Substring(1);

                if (string.IsNullOrEmpty(relPath) || !relPath.Contains('.'))
                    return null;

                var safeRel = relPath.Replace('/', Path.DirectorySeparatorChar);
                if (safeRel.Contains(".."))
                    return new HttpResponse(403, "Forbidden");

                string? filePath = ResolveStaticFilePath(safeRel);
                if (filePath == null)
                {
                    if (_staticFileRoot != null && (request.Path ?? "/").StartsWith("/static/"))
                    {
                        var legacyPath = Path.Combine(_staticFileRoot, (request.Path ?? "/").Substring("/static/".Length));
                        if (File.Exists(legacyPath))
                            filePath = legacyPath;
                    }
                }

                if (filePath == null)
                    return null;

                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                var mime = GetContentMimeType(ext);

                var cacheEntry = GetOrUpdateCacheEntry(filePath, mime);
                if (cacheEntry == null)
                    return new HttpResponse(500, "Internal Server Error");

                var ifNoneMatch = request.Headers?["If-None-Match"];
                if (!string.IsNullOrEmpty(ifNoneMatch))
                {
                    var clientETag = ifNoneMatch.Trim().Trim('"');
                    var serverETag = cacheEntry.ETag.Trim('"');
                    if (string.Equals(clientETag, serverETag, StringComparison.Ordinal))
                    {
                        Logger.Info($"[静态资源缓存命中] 路径: {request.Path} | ETag: {serverETag} | 大小: {cacheEntry.FileSize} 字节");
                        return Build304Response(cacheEntry);
                    }
                }

                var ifModifiedSince = request.Headers?["If-Modified-Since"];
                if (!string.IsNullOrEmpty(ifModifiedSince))
                {
                    if (DateTime.TryParse(ifModifiedSince, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AdjustToUniversal, out var clientDate))
                    {
                        var serverDate = new DateTime(
                            cacheEntry.LastModifiedUtc.Year, cacheEntry.LastModifiedUtc.Month, cacheEntry.LastModifiedUtc.Day,
                            cacheEntry.LastModifiedUtc.Hour, cacheEntry.LastModifiedUtc.Minute, cacheEntry.LastModifiedUtc.Second,
                            DateTimeKind.Utc);
                        if (clientDate >= serverDate)
                        {
                            Logger.Info($"[静态资源缓存命中] 路径: {request.Path} | 修改时间: {serverDate:O} | 大小: {cacheEntry.FileSize} 字节");
                            return Build304Response(cacheEntry);
                        }
                    }
                }

                Logger.Info($"[静态资源缓存未命中] 路径: {request.Path} | ETag: {cacheEntry.ETag} | 大小: {cacheEntry.FileSize} 字节");
                return BuildStaticFileResponse(filePath, cacheEntry);
            }
            catch (Exception ex)
            {
                Logger.Error($"提供静态资源时发生错误: {ex}");
                return new HttpResponse(500, "Internal Server Error");
            }
        }

        #endregion

        #region 静态文件路径解析

        /// <summary>
        /// 根据请求相对路径从 ViewRoot 和 FileRootPath 中解析文件的绝对路径。
        /// 文本类资源优先从 ViewRoot 查找，二进制资源优先从 FileRootPath 查找。
        /// </summary>
        private string? ResolveStaticFilePath(string safeRelPath)
        {
            var ext = Path.GetExtension(safeRelPath).ToLowerInvariant();
            bool isTextLike = IsTextLikeExtension(ext);

            if (isTextLike)
            {
                if (!string.IsNullOrEmpty(ViewRoot))
                {
                    var candidateView = Path.Combine(ViewRoot, safeRelPath);
                    if (File.Exists(candidateView)) return candidateView;
                }
                if (!string.IsNullOrEmpty(FileRootPath))
                {
                    var candidateFile = Path.Combine(FileRootPath, safeRelPath);
                    if (File.Exists(candidateFile)) return candidateFile;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(FileRootPath))
                {
                    var candidateFile = Path.Combine(FileRootPath, safeRelPath);
                    if (File.Exists(candidateFile)) return candidateFile;
                }
                if (!string.IsNullOrEmpty(ViewRoot))
                {
                    var candidateView = Path.Combine(ViewRoot, safeRelPath);
                    if (File.Exists(candidateView)) return candidateView;
                }
            }

            return null;
        }

        private static bool IsTextLikeExtension(string ext)
        {
            return ext switch
            {
                ".html" or ".htm" or ".css" or ".js" or ".json" or ".txt" or ".xml" or ".svg" or ".map" => true,
                _ => false
            };
        }

        #endregion

        #region 缓存管理

        /// <summary>
        /// 获取或更新指定文件的缓存条目。
        /// 若缓存不存在或文件已被修改（修改时间/大小变化），则重新计算 SHA256 ETag 并更新缓存。
        /// </summary>
        private StaticFileCacheEntry? GetOrUpdateCacheEntry(string filePath, string contentType)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists) return null;

                var lastModified = fileInfo.LastWriteTimeUtc;
                var fileSize = fileInfo.Length;

                if (_staticContentCache.TryGetValue(filePath, out var existing))
                {
                    if (existing.LastModifiedUtc == lastModified && existing.FileSize == fileSize)
                    {
                        return existing;
                    }
                }

                var entry = ComputeCacheEntry(filePath, fileInfo, contentType);
                _staticContentCache[filePath] = entry;
                return entry;
            }
            catch (Exception ex)
            {
                Logger.Error($"更新静态file缓存时发生错误: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 为指定文件计算完整的缓存条目（包含 SHA256 ETag 和可选内容缓存）
        /// 对大文件使用流式 SHA256 计算，避免将整个文件读入内存
        /// </summary>
        private StaticFileCacheEntry ComputeCacheEntry(string filePath, FileInfo fileInfo, string contentType)
        {
            var entry = new StaticFileCacheEntry
            {
                LastModifiedUtc = fileInfo.LastWriteTimeUtc,
                FileSize = fileInfo.Length,
                ContentType = contentType
            };

            if (fileInfo.Length <= SmallFileCacheThreshold)
            {
                var fileBytes = File.ReadAllBytes(filePath);
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(fileBytes);
                entry.ETag = $"\"{Convert.ToHexString(hashBytes)}\"";
                entry.CachedContent = fileBytes;
            }
            else
            {
                using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920);
                var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
                try
                {
                    int bytesRead;
                    while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        sha256.AppendData(buffer, 0, bytesRead);
                    }
                    var hashBytes = sha256.GetHashAndReset();
                    entry.ETag = $"\"{Convert.ToHexString(hashBytes)}\"";
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            return entry;
        }

        /// <summary>
        /// 清除静态资源缓存（当文件可能被外部修改时可手动调用）
        /// </summary>
        public void ClearStaticContentCache()
        {
            _staticContentCache.Clear();
            Logger.Info("静态资源缓存已清除");
        }

        /// <summary>
        /// 使指定文件的缓存失效
        /// </summary>
        public void InvalidateStaticContentCache(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                _staticContentCache.TryRemove(filePath, out _);
            }
        }

        #endregion

        #region 响应构建

        /// <summary>
        /// 构建 304 Not Modified 响应（无响应体，仅包含缓存相关头）
        /// </summary>
        private HttpResponse Build304Response(StaticFileCacheEntry cacheEntry)
        {
            var resp = new HttpResponse(304, "");
            resp.Headers["ETag"] = cacheEntry.ETag;
            resp.Headers["Last-Modified"] = cacheEntry.LastModifiedUtc.ToString("R");
            resp.Headers["Cache-Control"] = StaticContentMaxAgeSec > 0
                ? $"public, max-age={StaticContentMaxAgeSec}"
                : "no-cache";
            return resp;
        }

        /// <summary>
        /// 构建携带完整内容的静态文件 200 响应
        /// </summary>
        private HttpResponse BuildStaticFileResponse(string filePath, StaticFileCacheEntry cacheEntry)
        {
            HttpResponse resp;

            if (cacheEntry.CachedContent != null)
            {
                resp = new HttpResponse(200) { BodyBytes = cacheEntry.CachedContent };
            }
            else
            {
                try
                {
                    var content = File.ReadAllBytes(filePath);
                    resp = new HttpResponse(200) { BodyBytes = content };
                }
                catch (Exception ex)
                {
                    Logger.Error($"读取静态文件时发生错误: {ex}");
                    return new HttpResponse(500, "Internal Server Error");
                }
            }

            resp.Headers["Content-Type"] = cacheEntry.ContentType;
            resp.Headers["ETag"] = cacheEntry.ETag;
            resp.Headers["Last-Modified"] = cacheEntry.LastModifiedUtc.ToString("R");
            resp.Headers["Cache-Control"] = StaticContentMaxAgeSec > 0
                ? $"public, max-age={StaticContentMaxAgeSec}"
                : "no-cache";
            resp.Headers["Content-Length"] = (resp.BodyBytes?.Length ?? 0).ToString();

            return resp;
        }

        #endregion

        #region MIME 类型映射

        /// <summary>
        /// 根据文件扩展名获取完整的 Content-Type（含 charset）
        /// </summary>
        private static string GetContentMimeType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".html" or ".htm" => "text/html; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".js" or ".mjs" => "application/javascript; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".xml" => "application/xml; charset=utf-8",
                ".txt" => "text/plain; charset=utf-8",
                ".csv" => "text/csv; charset=utf-8",
                ".svg" => "image/svg+xml",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".ico" => "image/x-icon",
                ".bmp" => "image/bmp",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                ".ttf" => "font/ttf",
                ".otf" => "font/otf",
                ".eot" => "application/vnd.ms-fontobject",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mp3" => "audio/mpeg",
                ".ogg" => "audio/ogg",
                ".wav" => "audio/wav",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                ".gz" or ".gzip" => "application/gzip",
                ".map" => "application/json",
                ".wasm" => "application/wasm",
                _ => "application/octet-stream"
            };
        }

        #endregion
    }
}
