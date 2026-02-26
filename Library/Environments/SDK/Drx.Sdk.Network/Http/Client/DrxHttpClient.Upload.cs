using System;
using System.Collections.Specialized;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Performance;
using Newtonsoft.Json.Linq;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpClient 上传部分：文件上传及带元数据的上传
    /// </summary>
    public partial class DrxHttpClient
    {
        /// <summary>
        /// 便捷的本地文件上传方法（兼容性封装，最终内部调用 SendAsync(HttpRequest)）。
        /// </summary>
        /// <param name="url">上传目标 URL。</param>
        /// <param name="filePath">本地文件路径，文件必须存在。</param>
        /// <param name="fieldName">表单字段名，默认 file。</param>
        /// <param name="headers">可选请求头集合。</param>
        /// <param name="query">可选查询参数集合。</param>
        /// <param name="progress">可选进度回调，报告已上传字节数。</param>
        /// <param name="cancellationToken">可选取消令牌。</param>
        /// <exception cref="System.IO.FileNotFoundException">当指定的本地文件不存在时抛出。</exception>
        /// <returns>返回服务器响应的 <see cref="HttpResponse"/>。</returns>
        public async Task<HttpResponse> UploadFileAsync(string url, string filePath, string fieldName = "file", NameValueCollection? headers = null, NameValueCollection? query = null, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) throw new FileNotFoundException("上传文件不存在", filePath);

            var fileInfo = new FileInfo(filePath);
            using var fileStream = File.OpenRead(filePath);
            return await UploadFileAsync(url, fileStream, Path.GetFileName(filePath), fieldName, headers, query, progress, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 将给定的流作为文件上传到指定 URL，支持上传进度和取消操作。
        /// </summary>
        /// <param name="url">上传目标 URL。</param>
        /// <param name="fileStream">要上传的源流，不能为 null。</param>
        /// <param name="fileName">上传时使用的文件名；若为空则使用默认 file。</param>
        /// <param name="fieldName">表单字段名，默认 file。</param>
        /// <param name="headers">可选请求头集合。</param>
        /// <param name="query">可选查询参数集合。</param>
        /// <param name="progress">可选进度回调，报告已上传字节数。</param>
        /// <param name="cancellationToken">可选取消令牌。</param>
        /// <returns>返回服务器响应的 <see cref="HttpResponse"/>。</returns>
        /// <exception cref="System.ArgumentNullException">当 fileStream 为 null 时抛出。</exception>
        public async Task<HttpResponse> UploadFileAsync(string url, Stream fileStream, string fileName, string fieldName = "file", NameValueCollection? headers = null, NameValueCollection? query = null, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
            if (string.IsNullOrEmpty(fileName)) fileName = "file";

            var requestUrl = BuildUrl(url, query);
            using var content = new MultipartFormDataContent();

            var progressContent = new ProgressableStreamContent(fileStream, 81920, progress, cancellationToken);
            var streamContent = new StreamContent(progressContent, 81920);
            streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = $"\"{fieldName}\"", FileName = $"\"{fileName}\"" };
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            content.Add(streamContent, fieldName, fileName);

            var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, requestUrl)
            {
                Content = content
            };

            if (headers != null)
            {
                foreach (string key in headers)
                {
                    request.Headers.Add(key, EnsureAsciiHeaderValue(headers[key]));
                }
            }

            ApplySessionToRequest(request);

            try
            {
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var responseBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                var httpResponse = new HttpResponse((int)response.StatusCode, responseBody, response.ReasonPhrase);
                httpResponse.BodyBytes = responseBytes;
                try
                {
                    httpResponse.BodyObject = System.Text.Json.JsonSerializer.Deserialize<object>(responseBody);
                }
                catch { }

                foreach (var header in response.Headers)
                {
                    httpResponse.Headers.Add(header.Key, string.Join(",", header.Value));
                }
                foreach (var header in response.Content.Headers)
                {
                    httpResponse.Headers.Add(header.Key, string.Join(",", header.Value));
                }

                Logger.Info($"上传文件完成: {url}, 文件: {fileName}, 状态码: {response.StatusCode}");
                return httpResponse;
            }
            catch (Exception ex)
            {
                Logger.Error($"上传文件失败: {url}, 文件: {fileName}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 便捷方法：上传本地文件并附带可选 metadata（metadata 对象将被序列化为 JSON）。
        /// </summary>
        public async Task<HttpResponse> UploadFileWithMetadataAsync(string url, string filePath, object? metadata = null, NameValueCollection? headers = null, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) throw new FileNotFoundException("上传文件不存在", filePath);

            using var fs = File.OpenRead(filePath);
            return await UploadFileWithMetadataAsync(url, fs, Path.GetFileName(filePath), metadata, headers, progress, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 便捷方法：上传流并附带可选 metadata（metadata 对象将被序列化为 JSON）。
        /// </summary>
        public async Task<HttpResponse> UploadFileWithMetadataAsync(string url, Stream fileStream, string fileName, object? metadata = null, NameValueCollection? headers = null, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
            if (string.IsNullOrEmpty(fileName)) fileName = "file";

            string mDataJson = null;
            if (metadata != null)
            {
                try
                {
                    if (metadata is JToken jToken)
                    {
                        mDataJson = jToken.ToString();
                    }
                    else
                    {
                        mDataJson = JsonSerializer.Serialize(metadata);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"序列化 metadata 时发生错误: {ex.Message}");
                    mDataJson = metadata.ToString();
                }
            }

            Logger.Debug($"Uploading file with metadata to {url}, filename: {fileName}, metadata: {mDataJson}");

            var req = new HttpRequest
            {
                Url = url,
                Method = "POST",
                Headers = headers ?? new NameValueCollection(),
                UploadFile = new HttpRequest.UploadFileDescriptor
                {
                    Stream = fileStream,
                    FileName = fileName,
                    FieldName = "file",
                    Progress = progress,
                    CancellationToken = cancellationToken
                }
            };

            req.AddMetaData(mDataJson ?? @"{""metadata"":{}}");

            try
            {
                if (string.IsNullOrEmpty(req.Headers[Protocol.HttpHeaders.X_FILE_NAME]))
                {
                    req.Headers.Add(Protocol.HttpHeaders.X_FILE_NAME, fileName);
                }
            }
            catch { }

            return await SendAsync(req).ConfigureAwait(false);
        }
    }
}
