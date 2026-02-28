using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpClient 下载部分：文件下载、流式下载、哈希校验与元数据解析
    /// </summary>
    public partial class DrxHttpClient
    {
        #region 快捷下载方法

        /// <summary>
        /// 下载远程文件到指定本地路径，支持进度回调和取消操作，并在可能时进行原子替换目标文件。
        /// </summary>
        public async Task DownloadFileAsync(string url, string destPath, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            await DownloadFileAsync(url, destPath, headers: null, progress: progress, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 下载远程文件到指定本地路径，支持自定义请求头、查询参数、进度回调和取消操作。
        /// </summary>
        /// <param name="url">文件的远程 URL</param>
        /// <param name="destPath">本地目标路径，下载完成后会尝试原子替换该文件（若已存在）</param>
        /// <param name="headers">可选请求头集合</param>
        /// <param name="query">可选查询参数集合</param>
        /// <param name="progress">可选进度回调，报告已下载字节数</param>
        /// <param name="cancellationToken">可选取消令牌</param>
        public async Task DownloadFileAsync(
            string url,
            string destPath,
            NameValueCollection? headers = null,
            NameValueCollection? query = null,
            IProgress<long>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var requestUrl = BuildUrl(url, query);
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, requestUrl);

                if (headers != null)
                {
                    foreach (string key in headers)
                        request.Headers.TryAddWithoutValidation(key, headers[key]);
                }

                ApplySessionToRequest(request);

                var response = await _httpClient.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1L;
                var tempFile = destPath + ".download" + Guid.NewGuid().ToString("N");

                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int read;
                    while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        totalRead += read;
                        progress?.Report(totalRead);
                    }
                }

                AtomicFileReplace(tempFile, destPath);
                Logger.Info($"下载文件成功: {url} -> {destPath} (总字节: {total})");
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"下载被取消: {url}");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"下载文件失败: {url}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 下载远程文件到指定本地路径，完成后计算文件 SHA256 哈希并返回，支持与期望哈希对比校验。
        /// </summary>
        /// <param name="url">文件的远程 URL</param>
        /// <param name="destPath">本地目标路径</param>
        /// <param name="expectedHash">期望的文件哈希（可选），下载后自动比对，不匹配时抛出异常</param>
        /// <param name="headers">可选请求头集合</param>
        /// <param name="query">可选查询参数集合</param>
        /// <param name="progress">可选进度回调，报告已下载字节数</param>
        /// <param name="cancellationToken">可选取消令牌</param>
        /// <returns>下载文件的 SHA256 哈希字符串</returns>
        /// <exception cref="InvalidDataException">当 expectedHash 不为空且与实际哈希不匹配时抛出</exception>
        public async Task<string> DownloadFileWithHashAsync(
            string url,
            string destPath,
            string? expectedHash = null,
            NameValueCollection? headers = null,
            NameValueCollection? query = null,
            IProgress<long>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var requestUrl = BuildUrl(url, query);
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, requestUrl);

                if (headers != null)
                {
                    foreach (string key in headers)
                        request.Headers.TryAddWithoutValidation(key, headers[key]);
                }

                ApplySessionToRequest(request);

                var response = await _httpClient.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1L;
                var tempFile = destPath + ".download" + Guid.NewGuid().ToString("N");

                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                string fileHash;
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
                        progress?.Report(totalRead);
                    }
                    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    fileHash = BitConverter.ToString(sha256.Hash!).Replace("-", "").ToLowerInvariant();
                }

                if (!string.IsNullOrEmpty(expectedHash) &&
                    !string.Equals(fileHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                    throw new InvalidDataException($"文件哈希校验失败: 期望 {expectedHash}, 实际 {fileHash}");
                }

                AtomicFileReplace(tempFile, destPath);
                Logger.Info($"下载文件成功（含哈希校验）: {url} -> {destPath}, Hash: {fileHash}");
                return fileHash;
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"下载被取消: {url}");
                throw;
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"下载文件失败（哈希模式）: {url}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 下载远程文件到指定本地路径，自动解析服务器返回的元数据（X-MetaData 响应头）。
        /// </summary>
        /// <param name="url">文件的远程 URL</param>
        /// <param name="destPath">本地目标路径</param>
        /// <param name="headers">可选请求头集合</param>
        /// <param name="query">可选查询参数集合</param>
        /// <param name="progress">可选进度回调，报告已下载字节数</param>
        /// <param name="cancellationToken">可选取消令牌</param>
        /// <returns>下载结果，包含元数据和文件哈希</returns>
        public async Task<DownloadResult> DownloadFileWithMetadataAsync(
            string url,
            string destPath,
            NameValueCollection? headers = null,
            NameValueCollection? query = null,
            IProgress<long>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var requestUrl = BuildUrl(url, query);
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, requestUrl);

                if (headers != null)
                {
                    foreach (string key in headers)
                        request.Headers.TryAddWithoutValidation(key, headers[key]);
                }

                ApplySessionToRequest(request);

                var response = await _httpClient.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var result = new DownloadResult
                {
                    StatusCode = (int)response.StatusCode,
                    TotalBytes = response.Content.Headers.ContentLength ?? -1L,
                    ContentType = response.Content.Headers.ContentType?.ToString(),
                    ServerMetadata = ParseServerMetadata(response)
                };

                if (response.Headers.ETag != null)
                    result.ETag = response.Headers.ETag.Tag;

                if (response.Content.Headers.LastModified.HasValue)
                    result.LastModified = response.Content.Headers.LastModified.Value;

                var fileName = ParseFileNameFromResponse(response) ?? Path.GetFileName(destPath);
                result.FileName = fileName;

                var tempFile = destPath + ".download" + Guid.NewGuid().ToString("N");
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

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
                        progress?.Report(totalRead);
                    }
                    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    result.FileHash = BitConverter.ToString(sha256.Hash!).Replace("-", "").ToLowerInvariant();
                }

                result.DownloadedBytes = new FileInfo(tempFile).Length;
                AtomicFileReplace(tempFile, destPath);
                result.SavedFilePath = destPath;

                Logger.Info($"下载文件成功（含元数据）: {url} -> {destPath}, Hash: {result.FileHash}");
                return result;
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"下载被取消: {url}");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"下载文件失败（元数据模式）: {url}, 错误: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region 流式下载方法

        /// <summary>
        /// 将远程文件流写入到目标流，支持进度回调与取消操作。方法不会关闭传入的目标流，调用方负责流的生命周期。
        /// </summary>
        public async Task DownloadToStreamAsync(string url, Stream destination, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            await DownloadToStreamAsync(url, destination, headers: null, progress: progress, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 将远程文件流写入到目标流，支持自定义请求头、查询参数、进度回调与取消操作。
        /// </summary>
        /// <param name="url">文件的远程 URL</param>
        /// <param name="destination">目标写入流，不能为 null</param>
        /// <param name="headers">可选请求头集合</param>
        /// <param name="query">可选查询参数集合</param>
        /// <param name="progress">可选进度回调，报告已下载字节数</param>
        /// <param name="cancellationToken">可选取消令牌</param>
        public async Task DownloadToStreamAsync(
            string url,
            Stream destination,
            NameValueCollection? headers = null,
            NameValueCollection? query = null,
            IProgress<long>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            try
            {
                var requestUrl = BuildUrl(url, query);
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, requestUrl);

                if (headers != null)
                {
                    foreach (string key in headers)
                        request.Headers.TryAddWithoutValidation(key, headers[key]);
                }

                ApplySessionToRequest(request);

                var response = await _httpClient.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

#if DEBUG
                Logger.Debug($"响应头: {response.Headers}");
                Logger.Debug($"内容头: {response.Content.Headers}");
                Logger.Debug($"状态码: {response.StatusCode}");
#endif

                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1L;

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var buffer = new byte[81920];
                long totalRead = 0;
                int read;
                while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    totalRead += read;
                    progress?.Report(totalRead);
                }

                Logger.Info($"DownloadToStreamAsync 完成: {url} (总字节: {total})");
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"下载被取消: {url}");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"下载流式文件失败: {url}, 错误: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region 下载辅助方法

        /// <summary>
        /// 原子替换文件：优先使用 File.Replace，失败时回退到 Delete+Move
        /// </summary>
        private void AtomicFileReplace(string tempFile, string destPath)
        {
            try
            {
                if (File.Exists(destPath))
                {
                    try
                    {
                        File.Replace(tempFile, destPath, null);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Delete(destPath);
                        File.Move(tempFile, destPath);
                    }
                    catch (IOException)
                    {
                        try
                        {
                            File.Delete(destPath);
                            File.Move(tempFile, destPath);
                        }
                        catch (Exception inner)
                        {
                            Logger.Error($"替换文件失败（尝试 File.Delete+Move）: {inner.Message}");
                            throw;
                        }
                    }
                }
                else
                {
                    File.Move(tempFile, destPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"下载完成后替换目标文件时发生错误: {ex.Message}");
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                throw;
            }
        }

        /// <summary>
        /// 从响应头解析服务器元数据（X-MetaData 头，支持 URL 编码和 Base64 编码）
        /// </summary>
        private Dictionary<string, object>? ParseServerMetadata(System.Net.Http.HttpResponseMessage response)
        {
            try
            {
                if (response.Headers.TryGetValues("X-MetaData", out var values))
                {
                    var rawValue = string.Join("", values);
                    if (string.IsNullOrEmpty(rawValue)) return null;

                    string decoded;
                    try
                    {
                        decoded = Uri.UnescapeDataString(rawValue);
                    }
                    catch
                    {
                        try
                        {
                            decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(rawValue));
                        }
                        catch
                        {
                            decoded = rawValue;
                        }
                    }

                    return JsonSerializer.Deserialize<Dictionary<string, object>>(decoded);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"解析服务器元数据失败: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 从响应的 Content-Disposition 头解析文件名
        /// </summary>
        private string? ParseFileNameFromResponse(System.Net.Http.HttpResponseMessage response)
        {
            try
            {
                var contentDisposition = response.Content.Headers.ContentDisposition;
                if (contentDisposition != null)
                {
                    if (!string.IsNullOrEmpty(contentDisposition.FileNameStar))
                        return contentDisposition.FileNameStar;

                    if (!string.IsNullOrEmpty(contentDisposition.FileName))
                        return contentDisposition.FileName.Trim('"');
                }

                if (response.Headers.TryGetValues("X-File-Name", out var fileNameValues))
                {
                    var fileName = string.Join("", fileNameValues);
                    if (!string.IsNullOrEmpty(fileName))
                        return Uri.UnescapeDataString(fileName);
                }
            }
            catch { }
            return null;
        }

        #endregion
    }

    /// <summary>
    /// 下载结果：包含文件下载的完整信息（元数据、哈希、保存路径等）
    /// </summary>
    public class DownloadResult
    {
        /// <summary>
        /// 下载的文件名（从 Content-Disposition 或 URL 解析）
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// 文件最终保存的完整路径
        /// </summary>
        public string? SavedFilePath { get; set; }

        /// <summary>
        /// 已下载的字节数
        /// </summary>
        public long DownloadedBytes { get; set; }

        /// <summary>
        /// 文件总大小（来自 Content-Length）
        /// </summary>
        public long TotalBytes { get; set; } = -1;

        /// <summary>
        /// 文件内容 SHA256 哈希
        /// </summary>
        public string? FileHash { get; set; }

        /// <summary>
        /// 文件 MIME 类型
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// HTTP 响应状态码
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 服务器返回的 ETag
        /// </summary>
        public string? ETag { get; set; }

        /// <summary>
        /// 服务器文件最后修改时间
        /// </summary>
        public DateTimeOffset? LastModified { get; set; }

        /// <summary>
        /// 服务器元数据（来自 X-MetaData 响应头）
        /// </summary>
        public Dictionary<string, object>? ServerMetadata { get; set; }
    }
}
