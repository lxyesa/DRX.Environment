using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.ResourceManagement;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpClient 资源下载部分：基于 ResourceDownloadContext 回调的流式文件下载
    /// 支持五阶段生命周期回调（BeforeDownload → Downloading → DownloadCompleted → BeforeSave → AfterSave）
    /// </summary>
    public partial class DrxHttpClient
    {
        /// <summary>
        /// 下载文件到服务器（文件路径版本）：支持完整生命周期回调
        /// </summary>
        /// <param name="url">下载源 URL</param>
        /// <param name="destPath">本地保存路径</param>
        /// <param name="callback">下载生命周期回调（可选），在各阶段触发</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task DownloadFileAsync(
            string url,
            string destPath,
            Func<ResourceDownloadContext, Task> callback,
            CancellationToken cancellationToken = default)
        {
            await DownloadFileInternalAsync(url, destPath, callback, null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 下载文件到服务器（文件路径版本）：支持自定义请求头和完整生命周期回调
        /// </summary>
        /// <param name="url">下载源 URL</param>
        /// <param name="destPath">本地保存路径</param>
        /// <param name="callback">下载生命周期回调（可选），在各阶段触发</param>
        /// <param name="headers">可选请求头集合</param>
        /// <param name="query">可选查询参数集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task DownloadFileAsync(
            string url,
            string destPath,
            Func<ResourceDownloadContext, Task> callback,
            NameValueCollection? headers = null,
            NameValueCollection? query = null,
            CancellationToken cancellationToken = default)
        {
            await DownloadFileInternalAsync(url, destPath, callback, headers, cancellationToken, query).ConfigureAwait(false);
        }

        /// <summary>
        /// 下载文件到流（Stream 版本）：支持完整生命周期回调（不触发 BeforeSave/AfterSave）
        /// </summary>
        /// <param name="url">下载源 URL</param>
        /// <param name="destination">目标写入流（调用方负责流的生命周期）</param>
        /// <param name="callback">下载生命周期回调（可选），在各阶段触发</param>
        /// <param name="headers">可选请求头集合</param>
        /// <param name="query">可选查询参数集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task DownloadToStreamAsync(
            string url,
            Stream destination,
            Func<ResourceDownloadContext, Task> callback,
            NameValueCollection? headers = null,
            NameValueCollection? query = null,
            CancellationToken cancellationToken = default)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            await DownloadToStreamInternalAsync(url, destination, callback, headers, query, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 内部文件下载实现：流式接收数据，触发各阶段回调，临时文件管理与原子替换
        /// </summary>
        private async Task DownloadFileInternalAsync(
            string url,
            string destPath,
            Func<ResourceDownloadContext, Task>? callback,
            NameValueCollection? headers,
            CancellationToken cancellationToken,
            NameValueCollection? query = null)
        {
            var context = new ResourceDownloadContext
            {
                SourceUrl = url,
                TargetDirectory = Path.GetDirectoryName(destPath) ?? string.Empty,
                SavedFilePath = destPath,
                DownloadStartTime = DateTime.UtcNow
            };

            try
            {
                var requestUrl = BuildUrl(url, query);
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                if (headers != null)
                {
                    foreach (string key in headers)
                        request.Headers.TryAddWithoutValidation(key, headers[key]);
                }

                ApplySessionToRequest(request);

                var response = await _httpClient.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                context.StatusCode = (int)response.StatusCode;
                context.TotalBytes = response.Content.Headers.ContentLength ?? -1L;
                context.ContentType = response.Content.Headers.ContentType?.ToString();

                if (response.Headers.ETag != null)
                    context.ETag = response.Headers.ETag.Tag;

                if (response.Content.Headers.LastModified.HasValue)
                    context.LastModified = response.Content.Headers.LastModified.Value;

                context.ServerMetadata = ParseServerMetadata(response);
                context.FileName = ParseFileNameFromResponse(response) ?? Path.GetFileName(destPath);

                response.EnsureSuccessStatusCode();

                #region BeforeDownload 阶段

                context.Status = DownloadStatus.BeforeDownload;
                if (callback != null)
                {
                    await callback(context).ConfigureAwait(false);
                    if (context.Cancel)
                    {
                        Logger.Info("ResourceDownload", $"下载被取消（BeforeDownload）: {context.CancelReason}");
                        return;
                    }
                }

                #endregion

                var destDir = Path.GetDirectoryName(context.SavedFilePath ?? destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                var tempFile = (context.SavedFilePath ?? destPath) + ".download" + Guid.NewGuid().ToString("N");
                context.TempFilePath = tempFile;

                #region Downloading 阶段

                using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                using (var sha256 = SHA256.Create())
                {
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        sha256.TransformBlock(buffer, 0, read, null, 0);
                        totalRead += read;

                        context.DownloadedBytes = totalRead;
                        context.Status = DownloadStatus.Downloading;

                        if (callback != null)
                        {
                            await callback(context).ConfigureAwait(false);
                            if (context.Cancel)
                            {
                                Logger.Info("ResourceDownload", $"下载中被取消: {context.CancelReason}");
                                CleanupTempFile(tempFile);
                                return;
                            }
                        }
                    }

                    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    context.FileHash = BitConverter.ToString(sha256.Hash!).Replace("-", "").ToLowerInvariant();
                }

                #endregion

                #region DownloadCompleted 阶段

                context.Status = DownloadStatus.DownloadCompleted;

                if (!string.IsNullOrEmpty(context.ExpectedHash) &&
                    !string.Equals(context.FileHash, context.ExpectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    context.HashVerified = false;
                }
                else if (!string.IsNullOrEmpty(context.ExpectedHash))
                {
                    context.HashVerified = true;
                }

                if (callback != null)
                {
                    await callback(context).ConfigureAwait(false);
                    if (context.Cancel)
                    {
                        Logger.Info("ResourceDownload", $"下载完成后被取消: {context.CancelReason}");
                        CleanupTempFile(tempFile);
                        return;
                    }
                }

                #endregion

                #region BeforeSave 阶段

                context.Status = DownloadStatus.BeforeSave;
                if (callback != null)
                {
                    await callback(context).ConfigureAwait(false);
                    if (context.Cancel)
                    {
                        Logger.Info("ResourceDownload", $"保存前被取消: {context.CancelReason}");
                        CleanupTempFile(tempFile);
                        return;
                    }
                }

                #endregion

                var finalPath = context.SavedFilePath ?? destPath;
                AtomicFileReplace(tempFile, finalPath);
                context.SavedFilePath = finalPath;

                #region AfterSave 阶段

                context.Status = DownloadStatus.AfterSave;
                if (callback != null)
                {
                    await callback(context).ConfigureAwait(false);
                }

                #endregion

                Logger.Info("ResourceDownload", $"下载完成: {context.FileName}, 大小: {context.DownloadedBytes} 字节, Hash: {context.FileHash}");
            }
            catch (OperationCanceledException)
            {
                context.Cancel = true;
                context.CancelReason = "操作已取消";
                if (callback != null)
                    await callback(context).ConfigureAwait(false);

                CleanupTempFile(context.TempFilePath);
                Logger.Warn($"[ResourceDownload] 下载已取消: {context.FileName}");
            }
            catch (Exception ex)
            {
                CleanupTempFile(context.TempFilePath);
                Logger.Error($"[ResourceDownload] 下载异常: {url}, 文件: {context.FileName}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 内部流下载实现：流式接收数据，触发各阶段回调（不包含 BeforeSave/AfterSave）
        /// </summary>
        private async Task DownloadToStreamInternalAsync(
            string url,
            Stream destination,
            Func<ResourceDownloadContext, Task>? callback,
            NameValueCollection? headers,
            NameValueCollection? query,
            CancellationToken cancellationToken)
        {
            var context = new ResourceDownloadContext
            {
                SourceUrl = url,
                DownloadStartTime = DateTime.UtcNow
            };

            try
            {
                var requestUrl = BuildUrl(url, query);
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                if (headers != null)
                {
                    foreach (string key in headers)
                        request.Headers.TryAddWithoutValidation(key, headers[key]);
                }

                ApplySessionToRequest(request);

                var response = await _httpClient.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                context.StatusCode = (int)response.StatusCode;
                context.TotalBytes = response.Content.Headers.ContentLength ?? -1L;
                context.ContentType = response.Content.Headers.ContentType?.ToString();

                if (response.Headers.ETag != null)
                    context.ETag = response.Headers.ETag.Tag;

                if (response.Content.Headers.LastModified.HasValue)
                    context.LastModified = response.Content.Headers.LastModified.Value;

                context.ServerMetadata = ParseServerMetadata(response);
                context.FileName = ParseFileNameFromResponse(response) ?? string.Empty;

                response.EnsureSuccessStatusCode();

                context.Status = DownloadStatus.BeforeDownload;
                if (callback != null)
                {
                    await callback(context).ConfigureAwait(false);
                    if (context.Cancel)
                    {
                        Logger.Info("ResourceDownload", $"流下载被取消（BeforeDownload）: {context.CancelReason}");
                        return;
                    }
                }

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var sha256 = SHA256.Create();

                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    sha256.TransformBlock(buffer, 0, read, null, 0);
                    totalRead += read;

                    context.DownloadedBytes = totalRead;
                    context.Status = DownloadStatus.Downloading;

                    if (callback != null)
                    {
                        await callback(context).ConfigureAwait(false);
                        if (context.Cancel)
                        {
                            Logger.Info("ResourceDownload", $"流下载中被取消: {context.CancelReason}");
                            return;
                        }
                    }
                }

                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                context.FileHash = BitConverter.ToString(sha256.Hash!).Replace("-", "").ToLowerInvariant();

                if (!string.IsNullOrEmpty(context.ExpectedHash) &&
                    !string.Equals(context.FileHash, context.ExpectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    context.HashVerified = false;
                }
                else if (!string.IsNullOrEmpty(context.ExpectedHash))
                {
                    context.HashVerified = true;
                }

                context.Status = DownloadStatus.DownloadCompleted;
                if (callback != null)
                {
                    await callback(context).ConfigureAwait(false);
                }

                Logger.Info("ResourceDownload", $"流下载完成: {context.FileName}, 大小: {context.DownloadedBytes} 字节, Hash: {context.FileHash}");
            }
            catch (OperationCanceledException)
            {
                context.Cancel = true;
                context.CancelReason = "操作已取消";
                if (callback != null)
                    await callback(context).ConfigureAwait(false);

                Logger.Warn($"[ResourceDownload] 流下载已取消: {context.FileName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[ResourceDownload] 流下载异常: {url}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 清理临时文件（静默处理异常）
        /// </summary>
        private void CleanupTempFile(string? tempFile)
        {
            try
            {
                if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch { }
        }
    }
}
