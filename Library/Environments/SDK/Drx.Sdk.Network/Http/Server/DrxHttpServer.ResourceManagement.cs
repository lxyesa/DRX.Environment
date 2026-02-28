using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.ResourceManagement;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpServer 资源管理部分：资源索引初始化、自动化文件上传路由、资源查询
    /// 提供一站式的文件上传/下载自动化：
    ///   - FileUploadRouter() 自动注册上传路由，无需手写
    ///   - InitializeResourceIndexAsync() 初始化资源索引系统
    ///   - RefreshResourceIndexAsync() 手动刷新索引
    /// </summary>
    public partial class DrxHttpServer
    {
        private ResourceIndexManager? _resourceIndexManager;
        private string? _resourceRootPath;

        /// <summary>
        /// 资源索引管理器实例（初始化后可用）
        /// </summary>
        public ResourceIndexManager? ResourceIndex => _resourceIndexManager;

        #region 资源索引初始化

        /// <summary>
        /// 初始化资源索引系统
        /// </summary>
        /// <param name="resourcePath">资源根目录路径（相对或绝对）</param>
        /// <param name="excludePatterns">要排除的目录/文件模式（如 ".temp", "node_modules"）</param>
        /// <param name="version">索引版本号</param>
        /// <param name="enableFileWatcher">是否启用 FileSystemWatcher 实时监控</param>
        /// <param name="periodicRefreshSeconds">后台周期刷新间隔（秒），0 为不启用</param>
        public async Task InitializeResourceIndexAsync(
            string resourcePath = "resources",
            IEnumerable<string>? excludePatterns = null,
            string version = "1.0",
            bool enableFileWatcher = true,
            int periodicRefreshSeconds = 0)
        {
            _resourceRootPath = Path.IsPathRooted(resourcePath)
                ? resourcePath
                : Path.Combine(AppContext.BaseDirectory, resourcePath);

            _resourceRootPath = Path.GetFullPath(_resourceRootPath);
            Directory.CreateDirectory(_resourceRootPath);

            _resourceIndexManager = new ResourceIndexManager(_resourceRootPath, excludePatterns, version);

            await _resourceIndexManager.InitializeAsync(enableFileWatcher, periodicRefreshSeconds).ConfigureAwait(false);

            Logger.Info("DrxHttpServer", $"资源索引系统已初始化: {_resourceRootPath} ({_resourceIndexManager.EntryCount} 个文件)");
        }

        /// <summary>
        /// 手动刷新资源索引
        /// </summary>
        public async Task RefreshResourceIndexAsync()
        {
            if (_resourceIndexManager == null)
            {
                Logger.Warn("[DrxHttpServer] 资源索引尚未初始化，请先调用 InitializeResourceIndexAsync()");
                return;
            }
            await _resourceIndexManager.RefreshAsync().ConfigureAwait(false);
        }

        #endregion

        #region 自动化文件上传路由

        /// <summary>
        /// 注册自动化文件上传路由：自动处理文件接收、保存和索引追加
        /// </summary>
        /// <param name="url">上传 URL 路径（如 "/api/upload"）</param>
        /// <param name="targetDirectory">目标存储子目录（可选，相对于 resources 根目录且自动创建于其下）</param>
        /// <param name="callback">上传生命周期回调（可选），在各阶段触发以允许拦截</param>
        /// <param name="subIndexName">子索引名称（可选，为该目录创建独立子索引）</param>
        /// <param name="rateLimitMaxRequests">限流：最大请求数（0=不限流）</param>
        /// <param name="rateLimitWindowSeconds">限流：时间窗口（秒）</param>
        public void FileUploadRouter(
            string url,
            string? targetDirectory = null,
            Func<ResourceUploadContext, Task>? callback = null,
            string? subIndexName = null,
            int rateLimitMaxRequests = 0,
            int rateLimitWindowSeconds = 0)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (!url.StartsWith("/")) url = "/" + url;

            var resourceRoot = _resourceRootPath ?? Path.Combine(AppContext.BaseDirectory, "resources");

            var saveDir = string.IsNullOrEmpty(targetDirectory)
                ? resourceRoot
                : Path.Combine(resourceRoot, targetDirectory);

            Directory.CreateDirectory(saveDir);

            Func<HttpListenerContext, Task> rawHandler = async ctx =>
            {
                await HandleAutoUploadAsync(ctx, saveDir, targetDirectory ?? "", callback, subIndexName)
                    .ConfigureAwait(false);
            };

            _raw_routes_add(url, rawHandler, rateLimitMaxRequests, rateLimitWindowSeconds);
            Logger.Info("DrxHttpServer", $"注册自动上传路由: {url} -> {saveDir}{(subIndexName != null ? $" [子索引: {subIndexName}]" : "")}");
        }

        /// <summary>
        /// 注册自动化文件上传路由（Raw 版本）：提供完整的 HttpListenerContext 控制
        /// </summary>
        /// <param name="url">上传 URL 路径</param>
        /// <param name="rawHandler">原始处理委托</param>
        /// <param name="rateLimitMaxRequests">限流最大请求数</param>
        /// <param name="rateLimitWindowSeconds">限流时间窗口</param>
        public void FileUploadRouterRaw(
            string url,
            Func<HttpListenerContext, Task> rawHandler,
            int rateLimitMaxRequests = 0,
            int rateLimitWindowSeconds = 0)
        {
            if (string.IsNullOrEmpty(url) || rawHandler == null) return;
            if (!url.StartsWith("/")) url = "/" + url;

            _raw_routes_add(url, rawHandler, rateLimitMaxRequests, rateLimitWindowSeconds);
            Logger.Info("DrxHttpServer", $"注册原始上传路由: {url}");
        }

        #endregion

        #region 自动上传处理核心

        /// <summary>
        /// 自动上传处理核心逻辑：接收流式数据 → 回调通知 → 保存文件 → 追加索引
        /// </summary>
        private async Task HandleAutoUploadAsync(
            HttpListenerContext ctx,
            string saveDir,
            string targetDirectory,
            Func<ResourceUploadContext, Task>? callback,
            string? subIndexName)
        {
            var listenerReq = ctx.Request;
            var listenerResp = ctx.Response;
            string? tempFilePath = null;

            try
            {
                var fileName = ExtractUploadFileName(listenerReq);
                var contentLength = listenerReq.ContentLength64;
                var contentType = listenerReq.ContentType;
                var clientIp = listenerReq.RemoteEndPoint?.Address?.ToString();

                var context = new ResourceUploadContext
                {
                    FileName = fileName,
                    TargetDirectory = targetDirectory,
                    TotalBytes = contentLength,
                    ContentType = contentType,
                    ClientIp = clientIp,
                    UploadStartTime = DateTime.UtcNow
                };

                context.Status = UploadStatus.BeforeUpload;
                if (callback != null)
                {
                    await callback(context).ConfigureAwait(false);
                    if (context.Cancel)
                    {
                        SendUploadResponse(listenerResp, 403, context.CancelReason ?? "上传被取消");
                        return;
                    }
                }

                Directory.CreateDirectory(saveDir);
                tempFilePath = Path.Combine(saveDir, $".upload_{Guid.NewGuid():N}.tmp");

                long uploadedBytes = 0;
                var hashAlgorithm = SHA256.Create();
                var buffer = ArrayPool<byte>.Shared.Rent(256 * 1024);

                try
                {
                    var inputStream = listenerReq.InputStream;

                    var isMultipart = contentType != null &&
                        contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;

                    Stream dataStream;

                    if (isMultipart)
                    {
                        var parsedResult = await ExtractMultipartStreamAsync(listenerReq).ConfigureAwait(false);
                        dataStream = parsedResult.Stream;
                        if (!string.IsNullOrEmpty(parsedResult.FileName))
                            context.FileName = parsedResult.FileName;
                        fileName = context.FileName;
                    }
                    else
                    {
                        dataStream = inputStream;
                    }

                    using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write,
                               FileShare.None, 256 * 1024, FileOptions.Asynchronous))
                    {
                        int bytesRead;
                        while ((bytesRead = await dataStream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false)) > 0)
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
                            hashAlgorithm.TransformBlock(buffer, 0, bytesRead, null, 0);

                            uploadedBytes += bytesRead;
                            context.UploadedBytes = uploadedBytes;
                            context.Status = UploadStatus.Uploading;

                            if (callback != null)
                            {
                                await callback(context).ConfigureAwait(false);
                                if (context.Cancel)
                                {
                                    await fileStream.DisposeAsync().ConfigureAwait(false);
                                    CleanupTempFile(tempFilePath);
                                    SendUploadResponse(listenerResp, 403, context.CancelReason ?? "上传被取消");
                                    return;
                                }
                            }
                        }
                    }

                    hashAlgorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    var fileHash = Convert.ToHexString(hashAlgorithm.Hash!, 0, 16).ToLowerInvariant();
                    context.FileHash = fileHash;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    hashAlgorithm.Dispose();
                }

                context.Status = UploadStatus.UploadCompleted;
                if (callback != null)
                {
                    await callback(context).ConfigureAwait(false);
                    if (context.Cancel)
                    {
                        CleanupTempFile(tempFilePath);
                        SendUploadResponse(listenerResp, 403, context.CancelReason ?? "上传被取消");
                        return;
                    }
                }

                var finalFilePath = Path.Combine(saveDir, fileName);
                context.SavedFilePath = finalFilePath;

                context.Status = UploadStatus.BeforeSave;
                if (callback != null)
                {
                    await callback(context).ConfigureAwait(false);
                    if (context.Cancel)
                    {
                        CleanupTempFile(tempFilePath);
                        SendUploadResponse(listenerResp, 403, context.CancelReason ?? "保存被取消");
                        return;
                    }
                    finalFilePath = context.SavedFilePath ?? finalFilePath;
                }

                if (File.Exists(finalFilePath))
                    File.Replace(tempFilePath, finalFilePath, null);
                else
                    File.Move(tempFilePath, finalFilePath);

                tempFilePath = null;

                context.Status = UploadStatus.AfterSave;
                context.SavedFilePath = finalFilePath;

                if (context.ShouldAddToIndex && _resourceIndexManager != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            ResourceIndexEntry? entry;
                            if (!string.IsNullOrEmpty(subIndexName))
                                entry = await _resourceIndexManager.AddFileToSubIndexAsync(finalFilePath, subIndexName).ConfigureAwait(false);
                            else
                                entry = await _resourceIndexManager.AddFileAsync(finalFilePath).ConfigureAwait(false);

                            if (entry != null)
                                context.ResourceId = entry.Id;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"[DrxHttpServer] 追加索引失败: {ex.Message}");
                        }
                    });
                }

                if (callback != null)
                {
                    await callback(context).ConfigureAwait(false);
                }

                var responseBody = JsonSerializer.Serialize(new
                {
                    success = true,
                    fileName = context.FileName,
                    size = context.UploadedBytes,
                    hash = context.FileHash,
                    resourceId = context.ResourceId
                });

                SendUploadResponse(listenerResp, 200, responseBody, "application/json");
            }
            catch (Exception ex)
            {
                Logger.Error($"[DrxHttpServer] 自动上传处理失败: {ex.Message}\n{ex.StackTrace}");
                CleanupTempFile(tempFilePath);
                SendUploadResponse(listenerResp, 500, $"上传处理失败: {ex.Message}");
            }
        }

        #endregion

        #region 上传辅助方法

        /// <summary>
        /// 从请求头中提取上传文件名
        /// </summary>
        private static string ExtractUploadFileName(HttpListenerRequest request)
        {
            var fileName = request.Headers["X-File-Name"];
            if (!string.IsNullOrEmpty(fileName))
            {
                try { fileName = Uri.UnescapeDataString(fileName); } catch { }
                return SanitizeFileName(fileName);
            }

            var disposition = request.Headers["Content-Disposition"];
            if (!string.IsNullOrEmpty(disposition))
            {
                var fileNameStart = disposition.IndexOf("filename=\"", StringComparison.OrdinalIgnoreCase);
                if (fileNameStart >= 0)
                {
                    fileNameStart += "filename=\"".Length;
                    var fileNameEnd = disposition.IndexOf('"', fileNameStart);
                    if (fileNameEnd > fileNameStart)
                    {
                        fileName = disposition.Substring(fileNameStart, fileNameEnd - fileNameStart);
                        return SanitizeFileName(fileName);
                    }
                }
            }

            var pathFileName = Path.GetFileName(request.Url?.AbsolutePath ?? "");
            if (!string.IsNullOrEmpty(pathFileName) && pathFileName.Contains('.'))
                return SanitizeFileName(pathFileName);

            return $"upload_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
        }

        /// <summary>
        /// 清理文件名中的非法字符
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');
            return fileName;
        }

        /// <summary>
        /// 从 multipart 请求中提取文件流和文件名
        /// </summary>
        private static async Task<(Stream Stream, string FileName)> ExtractMultipartStreamAsync(HttpListenerRequest request)
        {
            var contentType = request.ContentType ?? "";
            var boundaryStart = contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
            if (boundaryStart < 0)
                return (request.InputStream, string.Empty);

            var boundary = contentType.Substring(boundaryStart + "boundary=".Length).Trim('"', ' ');

            var reader = new Microsoft.AspNetCore.WebUtilities.MultipartReader(boundary, request.InputStream);
            var section = await reader.ReadNextSectionAsync().ConfigureAwait(false);

            while (section != null)
            {
                if (Microsoft.Net.Http.Headers.ContentDispositionHeaderValue.TryParse(
                    section.ContentDisposition, out var cd))
                {
                    var fileNameSegment = cd.FileName.HasValue ? cd.FileName : cd.FileNameStar;
                    if (!Microsoft.Extensions.Primitives.StringSegment.IsNullOrEmpty(fileNameSegment))
                    {
                        var fn = Microsoft.Net.Http.Headers.HeaderUtilities.RemoveQuotes(fileNameSegment).ToString();
                        return (section.Body, fn);
                    }
                }
                section = await reader.ReadNextSectionAsync().ConfigureAwait(false);
            }

            return (request.InputStream, string.Empty);
        }

        /// <summary>
        /// 发送上传响应
        /// </summary>
        private static void SendUploadResponse(HttpListenerResponse response, int statusCode, string body, string contentType = "text/plain")
        {
            try
            {
                response.StatusCode = statusCode;
                response.ContentType = contentType + "; charset=utf-8";

                var bodyBytes = Encoding.UTF8.GetBytes(body);
                response.ContentLength64 = bodyBytes.Length;
                response.OutputStream.Write(bodyBytes, 0, bodyBytes.Length);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Logger.Warn($"[DrxHttpServer] 发送上传响应时出错: {ex.Message}");
                try { response.OutputStream.Close(); } catch { }
            }
        }

        /// <summary>
        /// 清理临时文件
        /// </summary>
        private static void CleanupTempFile(string? tempFilePath)
        {
            if (string.IsNullOrEmpty(tempFilePath)) return;
            try
            {
                if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
            }
            catch { }
        }

        #endregion
    }
}
