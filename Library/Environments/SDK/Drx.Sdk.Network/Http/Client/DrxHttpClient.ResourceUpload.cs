using System;
using System.Collections.Specialized;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Performance;
using Drx.Sdk.Network.Http.ResourceManagement;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpClient 资源上传部分：基于 ResourceUploadContext 回调的流式文件上传
    /// 支持自动从文件路径/FileInfo/Stream 隐式转换为流式上传
    /// </summary>
    public partial class DrxHttpClient
    {
        /// <summary>
        /// 上传文件到服务器（文件路径版本）：自动将文件路径转换为流
        /// </summary>
        /// <param name="url">上传目标 URL</param>
        /// <param name="filePath">本地文件路径</param>
        /// <param name="callback">上传生命周期回调（可选），在各阶段触发</param>
        /// <param name="metadata">用户自定义元数据（可选，将序列化为 JSON 放入请求头）</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task UploadFileAsync(
            string url,
            string filePath,
            Func<ResourceUploadContext, Task>? callback = null,
            object? metadata = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("上传文件不存在", filePath);

            var fileInfo = new FileInfo(filePath);
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);

            await UploadFileInternalAsync(url, fileStream, fileInfo.Name, fileInfo.Length,
                callback, metadata, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 上传文件到服务器（FileInfo 版本）：自动将 FileInfo 转换为流
        /// </summary>
        /// <param name="url">上传目标 URL</param>
        /// <param name="file">文件信息对象</param>
        /// <param name="callback">上传生命周期回调（可选）</param>
        /// <param name="metadata">用户自定义元数据（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task UploadFileAsync(
            string url,
            FileInfo file,
            Func<ResourceUploadContext, Task>? callback = null,
            object? metadata = null,
            CancellationToken cancellationToken = default)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            if (!file.Exists)
                throw new FileNotFoundException("上传文件不存在", file.FullName);

            using var fileStream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

            await UploadFileInternalAsync(url, fileStream, file.Name, file.Length,
                callback, metadata, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 上传文件到服务器（Stream 版本）：直接使用给定的流
        /// </summary>
        /// <param name="url">上传目标 URL</param>
        /// <param name="stream">数据流（调用方负责流的生命周期）</param>
        /// <param name="fileName">文件名（用于服务端识别）</param>
        /// <param name="callback">上传生命周期回调（可选）</param>
        /// <param name="metadata">用户自定义元数据（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task UploadFileAsync(
            string url,
            Stream stream,
            string fileName,
            Func<ResourceUploadContext, Task>? callback = null,
            object? metadata = null,
            CancellationToken cancellationToken = default)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (string.IsNullOrEmpty(fileName))
                fileName = $"upload_{DateTime.UtcNow:yyyyMMddHHmmss}";

            long totalBytes = -1;
            try
            {
                if (stream.CanSeek) totalBytes = stream.Length;
            }
            catch { }

            await UploadFileInternalAsync(url, stream, fileName, totalBytes,
                callback, metadata, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 内部上传实现：流式发送数据，触发各阶段回调
        /// </summary>
        private async Task UploadFileInternalAsync(
            string url,
            Stream dataStream,
            string fileName,
            long totalBytes,
            Func<ResourceUploadContext, Task>? callback,
            object? metadata,
            CancellationToken cancellationToken)
        {
            var context = new ResourceUploadContext
            {
                FileName = fileName,
                TotalBytes = totalBytes,
                UploadStartTime = DateTime.UtcNow
            };

            if (metadata != null)
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(
                        JsonSerializer.Serialize(metadata));
                    context.UserMetadata = dict;
                }
                catch { }
            }

            context.Status = UploadStatus.BeforeUpload;
            if (callback != null)
            {
                await callback(context).ConfigureAwait(false);
                if (context.Cancel)
                {
                    Logger.Info("ResourceUpload", $"上传被取消: {context.CancelReason}");
                    return;
                }
            }

            var requestUrl = BuildUrl(url, null);
            using var content = new MultipartFormDataContent();

            var callbackProgress = callback != null
                ? new Progress<long>(uploaded =>
                {
                    context.UploadedBytes = uploaded;
                    context.Status = UploadStatus.Uploading;

                    if (callback != null)
                    {
                        callback(context).ConfigureAwait(false).GetAwaiter().GetResult();
                        if (context.Cancel)
                        {
                            Logger.Info("ResourceUpload", $"上传中被取消: {context.CancelReason}");
                        }
                    }
                })
                : null;

            var progressContent = new ProgressableStreamContent(dataStream, 81920, callbackProgress, cancellationToken);
            var streamContent = new StreamContent(progressContent, 81920);
            streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "\"file\"",
                FileName = $"\"{fileName}\""
            };
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            content.Add(streamContent, "file", fileName);

            var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, requestUrl)
            {
                Content = content
            };

            request.Headers.Add("X-File-Name", Uri.EscapeDataString(fileName));

            if (metadata != null)
            {
                try
                {
                    var metaJson = JsonSerializer.Serialize(metadata);
                    request.Headers.Add("X-MetaData", Uri.EscapeDataString(metaJson));
                }
                catch { }
            }

            ApplySessionToRequest(request);

            try
            {
                var response = await _httpClient.SendAsync(request,
                    HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                context.Status = UploadStatus.UploadCompleted;
                context.UploadedBytes = totalBytes > 0 ? totalBytes : context.UploadedBytes;

                try
                {
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    if (responseObj.TryGetProperty("hash", out var hashProp))
                        context.FileHash = hashProp.GetString();
                    if (responseObj.TryGetProperty("resourceId", out var idProp))
                        context.ResourceId = idProp.GetString();
                }
                catch { }

                if (callback != null)
                {
                    await callback(context).ConfigureAwait(false);
                }

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"[ResourceUpload] 上传失败: {url}, 状态码: {response.StatusCode}, 响应: {responseBody}");
                }
                else
                {
                    Logger.Info("ResourceUpload", $"上传完成: {fileName}, 大小: {context.UploadedBytes} 字节");
                }
            }
            catch (OperationCanceledException)
            {
                context.Cancel = true;
                context.CancelReason = "操作已取消";
                if (callback != null)
                    await callback(context).ConfigureAwait(false);

                Logger.Warn($"[ResourceUpload] 上传已取消: {fileName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[ResourceUpload] 上传异常: {url}, 文件: {fileName}, 错误: {ex.Message}");
                throw;
            }
        }
    }
}
