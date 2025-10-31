using Drx.Sdk.Shared;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.V2.Web
{
    /// <summary>
    /// HTTP 客户端类，用于发送 HTTP 请求
    ///说明: 上传功能已合并到统一的 `SendAsync(HttpRequest)` 接口中，调用方可通过 `HttpRequest.UploadFile` 提供流式上传信息。
    /// 为兼容性仍保留原有的 `UploadFileAsync` 方法。
    /// </summary>
    public class HttpClient : IAsyncDisposable
    {
        private readonly System.Net.Http.HttpClient _httpClient;
        private readonly Channel<HttpRequestTask> _requestChannel;
        private readonly SemaphoreSlim _semaphore;
        private readonly CancellationTokenSource _cts;
        private const int MaxConcurrentRequests = 10; // 最大并发请求数
        private Task _processingTask;

        private class HttpRequestTask
        {
            public System.Net.Http.HttpMethod Method { get; set; }
            public string Url { get; set; }
            public string Body { get; set; }
            public byte[] BodyBytes { get; set; }
            public object BodyObject { get; set; }
            public NameValueCollection Headers { get; set; }
            public NameValueCollection Query { get; set; }
            public HttpRequest.UploadFileDescriptor UploadFile { get; set; }
            public TaskCompletionSource<HttpResponse> Tcs { get; set; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public HttpClient()
        {
            _httpClient = new System.Net.Http.HttpClient();
            _requestChannel = Channel.CreateBounded<HttpRequestTask>(new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
            _semaphore = new SemaphoreSlim(MaxConcurrentRequests);
            _cts = new CancellationTokenSource();
            _processingTask = Task.Run(() => ProcessRequestsAsync(_cts.Token));
        }

        /// <summary>
        /// 构造函数，指定基础地址
        /// </summary>
        /// <param name="baseAddress">基础地址</param>
        public HttpClient(string baseAddress)
        {
            try
            {
                _httpClient = new System.Net.Http.HttpClient
                {
                    BaseAddress = new Uri(baseAddress)
                };
                _requestChannel = Channel.CreateBounded<HttpRequestTask>(new BoundedChannelOptions(100)
                {
                    FullMode = BoundedChannelFullMode.Wait
                });
                _semaphore = new SemaphoreSlim(MaxConcurrentRequests);
                _cts = new CancellationTokenSource();
                _processingTask = Task.Run(() => ProcessRequestsAsync(_cts.Token));
                Logger.Info($"HttpClient 初始化，基础地址: {baseAddress}");
            }
            catch (Exception ex)
            {
                Logger.Error($"初始化 HttpClient 时发生错误: {ex.Message}");
                throw;
            }
        }

        // Helper to ensure header values are ASCII-only. Non-ASCII characters will be percent-encoded.
        private static string EnsureAsciiHeaderValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            bool allAscii = true;
            foreach (var ch in value)
            {
                if (ch > 127)
                {
                    allAscii = false;
                    break;
                }
            }
            if (allAscii) return value;
            // Percent-encode using UTF8 similar to Uri.EscapeDataString but operate on raw bytes
            var bytes = Encoding.UTF8.GetBytes(value);
            var sb = new StringBuilder();
            foreach (var b in bytes)
            {
                // safe ASCII characters which are commonly allowed in headers: alnum and few symbols
                if ((b >= 0x30 && b <= 0x39) || (b >= 0x41 && b <= 0x5A) || (b >= 0x61 && b <= 0x7A) || b == 0x2D || b == 0x5F || b == 0x2E || b == 0x7E)
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append('%');
                    sb.Append(b.ToString("X2"));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 发送 HTTP 请求
        /// </summary>
        /// <param name="method">HTTP 方法</param>
        /// <param name="url">请求 URL</param>
        /// <param name="body">请求体 (字符串)</param>
        /// <param name="headers">请求头</param>
        /// <param name="query">查询参数</param>
        /// <returns>HTTP 响应</returns>
        public async Task<HttpResponse> SendAsync(System.Net.Http.HttpMethod method, string url, string body = null, NameValueCollection headers = null, NameValueCollection query = null)
        {
            return await SendAsyncInternal(method, url, body, null, null, headers, query, null);
        }

        /// <summary>
        /// 发送 HTTP 请求 (字节数组)
        /// </summary>
        /// <param name="method">HTTP 方法</param>
        /// <param name="url">请求 URL</param>
        /// <param name="bodyBytes">请求体 (字节数组)</param>
        /// <param name="headers">请求头</param>
        /// <param name="query">查询参数</param>
        /// <returns>HTTP 响应</returns>
        public async Task<HttpResponse> SendAsync(System.Net.Http.HttpMethod method, string url, byte[] bodyBytes, NameValueCollection headers = null, NameValueCollection query = null)
        {
            return await SendAsyncInternal(method, url, null, bodyBytes, null, headers, query, null);
        }

        /// <summary>
        /// 发送 HTTP 请求 (对象)
        /// </summary>
        /// <param name="method">HTTP 方法</param>
        /// <param name="url">请求 URL</param>
        /// <param name="bodyObject">请求体 (对象)</param>
        /// <param name="headers">请求头</param>
        /// <param name="query">查询参数</param>
        /// <returns>HTTP 响应</returns>
        public async Task<HttpResponse> SendAsync(System.Net.Http.HttpMethod method, string url, object bodyObject, NameValueCollection headers = null, NameValueCollection query = null)
        {
            return await SendAsyncInternal(method, url, null, null, bodyObject, headers, query, null);
        }

        /// <summary>
        /// 上传文件到服务器（multipart/form-data），支持进度和取消。
        /// 注意: 文件上传功能已与 `SendAsync(HttpRequest)` 合并。
        /// 推荐使用：构造 `HttpRequest` 并设置 `UploadFile` 字段，然后调用 `SendAsync(request)`。
        ///例如：
        /// <code>
        /// var req = new HttpRequest { Method = "POST", Path = "/api/upload" };
        /// req.UploadFile = new HttpRequest.UploadFileDescriptor { Stream = fs, FileName = "a.bin" };
        /// var resp = await client.SendAsync(req);
        /// </code>
        /// 本方法仍然保留以兼容旧代码。
        /// </summary>
        /// <param name="url">目标 URL</param>
        /// <param name="filePath">本地文件路径</param>
        /// <param name="fieldName">表单字段名，默认 "file"</param>
        /// <param name="headers">额外请求头</param>
        /// <param name="query">查询参数</param>
        /// <param name="progress">进度（已上传字节数）</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<HttpResponse> UploadFileAsync(string url, string filePath, string fieldName = "file", NameValueCollection headers = null, NameValueCollection query = null, IProgress<long> progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) throw new FileNotFoundException("上传文件不存在", filePath);

            var fileInfo = new FileInfo(filePath);
            using var fileStream = File.OpenRead(filePath);
            return await UploadFileAsync(url, fileStream, Path.GetFileName(filePath), fieldName, headers, query, progress, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 上传文件到服务器（流），支持进度和取消。
        /// </summary>
        public async Task<HttpResponse> UploadFileAsync(string url, Stream fileStream, string fileName, string fieldName = "file", NameValueCollection headers = null, NameValueCollection query = null, IProgress<long> progress = null, CancellationToken cancellationToken = default)
        {
            if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
            if (string.IsNullOrEmpty(fileName)) fileName = "file";

            var requestUrl = BuildUrl(url, query);
            using var content = new MultipartFormDataContent();

            // 包裹流以报告进度
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

        //进度流包装器
        private class ProgressableStreamContent : Stream
        {
            private readonly Stream _inner;
            private readonly int _bufferSize;
            private readonly IProgress<long> _progress;
            private readonly CancellationToken _cancellation;
            private long _totalRead;

            public ProgressableStreamContent(Stream inner, int bufferSize, IProgress<long> progress, CancellationToken cancellation)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _bufferSize = bufferSize;
                _progress = progress;
                _cancellation = cancellation;
                _totalRead = 0;
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => false;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }

            public override void Flush() => _inner.Flush();
            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => _inner.SetLength(value);
            public override int Read(byte[] buffer, int offset, int count)
            {
                _cancellation.ThrowIfCancellationRequested();
                var read = _inner.Read(buffer, offset, Math.Min(count, _bufferSize));
                if (read > 0)
                {
                    _totalRead += read;
                    _progress?.Report(_totalRead);
                }
                return read;
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _cancellation.ThrowIfCancellationRequested();
                var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read > 0)
                {
                    _totalRead += read;
                    _progress?.Report(_totalRead);
                }
                return read;
            }

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();
            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) => _inner.BeginRead(buffer, offset, count, callback, state);
            public override int EndRead(IAsyncResult asyncResult) => _inner.EndRead(asyncResult);
            protected override void Dispose(bool disposing)
            {
                // 不要在这里关闭底层流，调用方负责
                base.Dispose(disposing);
            }
        }

        private async Task<HttpResponse> SendAsyncInternal(System.Net.Http.HttpMethod method, string url, string body, byte[] bodyBytes, object bodyObject, NameValueCollection headers, NameValueCollection query, HttpRequest.UploadFileDescriptor uploadFile)
        {
            try
            {
                // 如果提供了上传描述并且流不为空，则构造 multipart/form-data 请求
                if (uploadFile != null && uploadFile.Stream != null)
                {
                    var requestUrl = BuildUrl(url, query);
                    using var content = new MultipartFormDataContent();

                    var progressContent = new ProgressableStreamContent(uploadFile.Stream, 81920, uploadFile.Progress, uploadFile.CancellationToken);
                    var streamContent = new StreamContent(progressContent, 81920);
                    streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = $"\"{uploadFile.FieldName}\"", FileNameStar = uploadFile.FileName ?? "file" };
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                    content.Add(streamContent, uploadFile.FieldName, uploadFile.FileName ?? "file");

                    var uploadRequestMessage = new HttpRequestMessage(method, requestUrl)
                    {
                        Content = content
                    };

                    if (headers != null)
                    {
                        foreach (string key in headers)
                        {
                            uploadRequestMessage.Headers.Add(key, EnsureAsciiHeaderValue(headers[key]));
                        }
                    }

                    var uploadServerResponse = await _httpClient.SendAsync(uploadRequestMessage, HttpCompletionOption.ResponseContentRead, uploadFile.CancellationToken).ConfigureAwait(false);
                    var uploadServerResponseBody = await uploadServerResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var uploadServerResponseBytes = await uploadServerResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    var uploadHttpResponse = new HttpResponse((int)uploadServerResponse.StatusCode, uploadServerResponseBody, uploadServerResponse.ReasonPhrase);
                    uploadHttpResponse.BodyBytes = uploadServerResponseBytes;
                    try
                    {
                        uploadHttpResponse.BodyObject = System.Text.Json.JsonSerializer.Deserialize<object>(uploadServerResponseBody);
                    }
                    catch { }

                    foreach (var header in uploadServerResponse.Headers)
                    {
                        uploadHttpResponse.Headers.Add(header.Key, string.Join(",", header.Value));
                    }
                    foreach (var header in uploadServerResponse.Content.Headers)
                    {
                        uploadHttpResponse.Headers.Add(header.Key, string.Join(",", header.Value));
                    }

                    Logger.Info($"上传文件完成: {url}, 文件: {uploadFile.FileName}, 状态码: {uploadServerResponse.StatusCode}");
                    return uploadHttpResponse;
                }

                var requestMessage = new HttpRequestMessage(method, BuildUrl(url, query));

                // 添加请求头
                if (headers != null)
                {
                    foreach (string key in headers)
                    {
                        requestMessage.Headers.Add(key, EnsureAsciiHeaderValue(headers[key]));
                    }
                }

                // 添加请求体
                if (bodyBytes != null)
                {
                    requestMessage.Content = new ByteArrayContent(bodyBytes);
                }
                else if (bodyObject != null)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(bodyObject);
                    requestMessage.Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
                }
                else if (!string.IsNullOrEmpty(body))
                {
                    requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                var response = await _httpClient.SendAsync(requestMessage);
                var responseBody = await response.Content.ReadAsStringAsync();
                var responseBytes = await response.Content.ReadAsByteArrayAsync();

                var httpResponse = new HttpResponse((int)response.StatusCode, responseBody, response.ReasonPhrase);
                httpResponse.BodyBytes = responseBytes;

                // 尝试反序列化为对象 (假设是 JSON)
                try
                {
                    httpResponse.BodyObject = System.Text.Json.JsonSerializer.Deserialize<object>(responseBody);
                }
                catch
                {
                    // 如果反序列化失败，保持为 null
                }

                // 添加响应头
                foreach (var header in response.Headers)
                {
                    httpResponse.Headers.Add(header.Key, string.Join(",", header.Value));
                }

                foreach (var header in response.Content.Headers)
                {
                    httpResponse.Headers.Add(header.Key, string.Join(",", header.Value));
                }

                Logger.Info($"发送请求成功: {method} {url}, 状态码: {response.StatusCode}");
                return httpResponse;
            }
            catch (HttpRequestException ex)
            {
                Logger.Error($"发送 HTTP 请求时发生网络错误: {method} {url}, 错误: {ex.Message}");
                throw;
            }
            catch (TaskCanceledException ex)
            {
                Logger.Error($"发送 HTTP 请求超时: {method} {url}, 错误: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"发送 HTTP 请求时发生未知错误: {method} {url}, 错误: {ex.Message}");
                throw;
            }
        }

        private async Task ProcessRequestsAsync(CancellationToken token)
        {
            await foreach (var requestTask in _requestChannel.Reader.ReadAllAsync(token))
            {
                await _semaphore.WaitAsync(token);
                _ = Task.Run(() => ExecuteRequestAsync(requestTask), token).ContinueWith(t => _semaphore.Release());
            }
        }

        private async Task ExecuteRequestAsync(HttpRequestTask requestTask)
        {
            try
            {
                // 如果有上传描述，执行 multipart 上传
                if (requestTask.UploadFile != null && requestTask.UploadFile.Stream != null)
                {
                    var requestUrl = BuildUrl(requestTask.Url, requestTask.Query);
                    using var content = new MultipartFormDataContent();

                    var progressContent = new ProgressableStreamContent(requestTask.UploadFile.Stream, 81920, requestTask.UploadFile.Progress, requestTask.UploadFile.CancellationToken);
                    var streamContent = new StreamContent(progressContent, 81920);
                    streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = $"\"{requestTask.UploadFile.FieldName}\"", FileNameStar = requestTask.UploadFile.FileName ?? "file" };
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                    content.Add(streamContent, requestTask.UploadFile.FieldName, requestTask.UploadFile.FileName ?? "file");

                    var uploadRequestMessage = new HttpRequestMessage(requestTask.Method, requestUrl)
                    {
                        Content = content
                    };

                    if (requestTask.Headers != null)
                    {
                        foreach (string key in requestTask.Headers)
                        {
                            uploadRequestMessage.Headers.Add(key, EnsureAsciiHeaderValue(requestTask.Headers[key]));
                        }
                    }

                    var serverResponse = await _httpClient.SendAsync(uploadRequestMessage, HttpCompletionOption.ResponseContentRead, requestTask.UploadFile.CancellationToken);
                    var serverResponseBody = await serverResponse.Content.ReadAsStringAsync();
                    var serverResponseBytes = await serverResponse.Content.ReadAsByteArrayAsync();

                    var uploadResult = new HttpResponse((int)serverResponse.StatusCode, serverResponseBody, serverResponse.ReasonPhrase);
                    uploadResult.BodyBytes = serverResponseBytes;

                    try
                    {
                        uploadResult.BodyObject = System.Text.Json.JsonSerializer.Deserialize<object>(serverResponseBody);
                    }
                    catch { }

                    foreach (var header in serverResponse.Headers)
                    {
                        uploadResult.Headers.Add(header.Key, string.Join(",", header.Value));
                    }
                    foreach (var header in serverResponse.Content.Headers)
                    {
                        uploadResult.Headers.Add(header.Key, string.Join(",", header.Value));
                    }

                    Logger.Info($"上传队列请求完成: {requestTask.Url}, 文件: {requestTask.UploadFile.FileName}, 状态码: {serverResponse.StatusCode}");
                    requestTask.Tcs.SetResult(uploadResult);
                    return;
                }

                var requestMessage = new HttpRequestMessage(requestTask.Method, BuildUrl(requestTask.Url, requestTask.Query));

                // 添加请求头
                if (requestTask.Headers != null)
                {
                    foreach (string key in requestTask.Headers)
                    {
                        requestMessage.Headers.Add(key, EnsureAsciiHeaderValue(requestTask.Headers[key]));
                    }
                }

                // 添加请求体
                if (requestTask.BodyBytes != null)
                {
                    requestMessage.Content = new ByteArrayContent(requestTask.BodyBytes);
                }
                else if (requestTask.BodyObject != null)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(requestTask.BodyObject);
                    requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                else if (!string.IsNullOrEmpty(requestTask.Body))
                {
                    requestMessage.Content = new StringContent(requestTask.Body, Encoding.UTF8, "application/json");
                }

                var responseNormal = await _httpClient.SendAsync(requestMessage);
                var responseBodyNormal = await responseNormal.Content.ReadAsStringAsync();
                var responseBytesNormal = await responseNormal.Content.ReadAsByteArrayAsync();

                var httpResponseNormal = new HttpResponse((int)responseNormal.StatusCode, responseBodyNormal, responseNormal.ReasonPhrase);
                httpResponseNormal.BodyBytes = responseBytesNormal;

                // 尝试反序列化为对象 (假设是 JSON)
                try
                {
                    httpResponseNormal.BodyObject = System.Text.Json.JsonSerializer.Deserialize<object>(responseBodyNormal);
                }
                catch
                {
                    // 如果反序列化失败，保持为 null
                }

                // 添加响应头
                foreach (var header in responseNormal.Headers)
                {
                    httpResponseNormal.Headers.Add(header.Key, string.Join(",", header.Value));
                }

                foreach (var header in responseNormal.Content.Headers)
                {
                    httpResponseNormal.Headers.Add(header.Key, string.Join(",", header.Value));
                }

                Logger.Info($"发送请求成功: {requestTask.Method} {requestTask.Url}, 状态码: {responseNormal.StatusCode}");
                requestTask.Tcs.SetResult(httpResponseNormal);
            }
            catch (HttpRequestException ex)
            {
                Logger.Error($"发送 HTTP 请求时发生网络错误: {requestTask.Method} {requestTask.Url}, 错误: {ex.Message}");
                requestTask.Tcs.SetException(ex);
            }
            catch (TaskCanceledException ex)
            {
                Logger.Error($"发送 HTTP 请求超时: {requestTask.Method} {requestTask.Url}, 错误: {ex.Message}");
                requestTask.Tcs.SetException(ex);
            }
            catch (Exception ex)
            {
                Logger.Error($"发送 HTTP 请求时发生未知错误: {requestTask.Method} {requestTask.Url}, 错误: {ex.Message}");
                requestTask.Tcs.SetException(ex);
            }
        }

        /// <summary>
        /// 发送 GET 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="headers">请求头</param>
        /// <param name="query">查询参数</param>
        /// <returns>HTTP 响应</returns>
        public Task<HttpResponse> GetAsync(string url, NameValueCollection headers = null, NameValueCollection query = null)
        {
            try
            {
                return SendAsync(System.Net.Http.HttpMethod.Get, url, (string)null, headers, query);
            }
            catch (Exception ex)
            {
                Logger.Error($"发送 GET 请求时发生错误: {url}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 发送 POST 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="body">请求体 (字符串、字节数组或对象)</param>
        /// <param name="headers">请求头</param>
        /// <param name="query">查询参数</param>
        /// <returns>HTTP 响应</returns>
        public Task<HttpResponse> PostAsync(string url, object body = null, NameValueCollection headers = null, NameValueCollection query = null)
        {
            try
            {
                if (body is string str)
                    return SendAsync(System.Net.Http.HttpMethod.Post, url, str, headers, query);
                else if (body is byte[] bytes)
                    return SendAsync(System.Net.Http.HttpMethod.Post, url, bytes, headers, query);
                else if (body != null)
                    return SendAsync(System.Net.Http.HttpMethod.Post, url, body, headers, query);
                else
                    return SendAsync(System.Net.Http.HttpMethod.Post, url, (string)null, headers, query);
            }
            catch (Exception ex)
            {
                Logger.Error($"发送 POST 请求时发生错误: {url}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 发送 PUT 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="body">请求体 (字符串、字节数组或对象)</param>
        /// <param name="headers">请求头</param>
        /// <param name="query">查询参数</param>
        /// <returns>HTTP 响应</returns>
        public Task<HttpResponse> PutAsync(string url, object body = null, NameValueCollection headers = null, NameValueCollection query = null)
        {
            try
            {
                if (body is string str)
                    return SendAsync(System.Net.Http.HttpMethod.Put, url, str, headers, query);
                else if (body is byte[] bytes)
                    return SendAsync(System.Net.Http.HttpMethod.Put, url, bytes, headers, query);
                else if (body != null)
                    return SendAsync(System.Net.Http.HttpMethod.Put, url, body, headers, query);
                else
                    return SendAsync(System.Net.Http.HttpMethod.Put, url, (string)null, headers, query);
            }
            catch (Exception ex)
            {
                Logger.Error($"发送 PUT 请求时发生错误: {url}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 发送 DELETE 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="headers">请求头</param>
        /// <param name="query">查询参数</param>
        /// <returns>HTTP 响应</returns>
        public Task<HttpResponse> DeleteAsync(string url, NameValueCollection headers = null, NameValueCollection query = null)
        {
            try
            {
                return SendAsync(System.Net.Http.HttpMethod.Delete, url, (string)null, headers, query);
            }
            catch (Exception ex)
            {
                Logger.Error($"发送 DELETE 请求时发生错误: {url}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 下载文件到指定路径，支持进度报告、取消，并在完成后原子替换目标文件。
        /// </summary>
        /// <param name="url">文件 URL</param>
        /// <param name="destPath">目标路径（最终文件）</param>
        /// <param name="progress">可选的进度（已接收字节数）</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task DownloadFileAsync(string url, string destPath, IProgress<long> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1L;
                var tempFile = destPath + ".download" + Guid.NewGuid().ToString("N");

                // Ensure destination directory exists
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

                // 对目标文件执行原子替换
                try
                {
                    if (File.Exists(destPath))
                    {
                        // 使用覆盖复制再删除临时文件，这在大多数环境下能替换目标
                        File.Copy(tempFile, destPath, true);
                        File.Delete(tempFile);
                    }
                    else
                    {
                        File.Move(tempFile, destPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"下载完成后替换目标文件时发生错误: {ex.Message}");
                    // 尝试清理临时文件
                    try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                    throw;
                }

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
        /// 设置默认请求头
        /// </summary>
        /// <param name="name">头名称</param>
        /// <param name="value">头值</param>
        public void SetDefaultHeader(string name, string value)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Add(name, EnsureAsciiHeaderValue(value));
                Logger.Info($"设置默认请求头: {name} = {value}");
            }
            catch (Exception ex)
            {
                Logger.Error($"设置默认请求头时发生错误: {name}, 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置超时时间
        /// </summary>
        /// <param name="timeout">超时时间</param>
        public void SetTimeout(TimeSpan timeout)
        {
            try
            {
                _httpClient.Timeout = timeout;
                Logger.Info($"设置超时时间: {timeout.TotalSeconds} 秒");
            }
            catch (Exception ex)
            {
                Logger.Error($"设置超时时间时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步释放资源
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            try
            {
                //先停止接收新请求，再取消正在进行的循环
                _requestChannel.Writer.TryComplete();
                _cts.Cancel();

                // 等待后台处理任务结束，捕获取消异常
                if (_processingTask != null)
                {
                    try
                    {
                        await _processingTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // 忽略取消导致的异常
                    }
                }
            }
            finally
            {
                _httpClient?.Dispose();
                _semaphore?.Dispose();
                _cts?.Dispose();
            }
        }

        private string BuildUrl(string url, NameValueCollection query)
        {
            try
            {
                if (query == null || query.Count == 0)
                    return url;

                var queryString = string.Join("&", query.AllKeys.Select(key => $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(query[key])}"));
                return url.Contains("?") ? $"{url}&{queryString}" : $"{url}?{queryString}";
            }
            catch (Exception ex)
            {
                Logger.Error($"构建 URL 时发生错误: {ex.Message}");
                return url;
            }
        }

        /// <summary>
        ///发送 HTTP 请求（统一请求对象版本，接受库中的 `HttpRequest` 类型）
        ///
        ///说明: 如果需要上传文件，请在 `HttpRequest.UploadFile` 中设置上传信息（`Stream`、`FileName`、`FieldName` 等），
        ///本方法会自动以 multipart/form-data 流式上传文件并保持进度/取消支持。
        ///</summary>
        /// <param name="request">请求对象，支持包含常见属性: Url, Method (HttpMethod 或 string), Body (string), BodyBytes (byte[]), BodyObject (object), BodyJson (string), ExtraDataPack (byte[]), Headers (NameValueCollection), Query (NameValueCollection), UploadFile (UploadFileDescriptor)</param>
        /// <returns>HTTP 响应</returns>
        public async Task<HttpResponse> SendAsync(HttpRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // 如果调用方未显式提供 UploadFile，则尝试隐式创建
            try
            {
                if (request.UploadFile == null)
                {
                    // 优先从 BodyObject 中获取 Stream
                    if (request.BodyObject is Stream bodyStream)
                    {
                        var fileName = request.Headers?[HttpHeaders.X_FILE_NAME];
                        // 支持 Base64/Encoded头
                        if (string.IsNullOrEmpty(fileName) && request.Headers != null)
                        {
                            if (!string.IsNullOrEmpty(request.Headers[HttpHeaders.X_FILE_NAME_BASE64]))
                            {
                                try { fileName = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers[HttpHeaders.X_FILE_NAME_BASE64])); } catch { fileName = request.Headers[HttpHeaders.X_FILE_NAME_BASE64]; }
                            }
                            else if (!string.IsNullOrEmpty(request.Headers[HttpHeaders.X_FILE_NAME_ENCODED]))
                            {
                                fileName = Uri.UnescapeDataString(request.Headers[HttpHeaders.X_FILE_NAME_ENCODED]);
                            }
                        }

                        request.UploadFile = new HttpRequest.UploadFileDescriptor
                        {
                            Stream = bodyStream,
                            FileName = string.IsNullOrEmpty(fileName) ? "file" : fileName,
                            FieldName = "file",
                            Progress = request.UploadFile?.Progress ?? null,
                            CancellationToken = request.UploadFile?.CancellationToken ?? CancellationToken.None
                        };
                        // 清理原始载荷，避免重复使用
                        request.BodyObject = null;
                    }
                    // 如果没有 Stream，但有 BodyBytes，则用 MemoryStream 包装
                    else if (request.BodyBytes != null && request.BodyBytes.Length > 0)
                    {
                        var ms = new MemoryStream(request.BodyBytes, writable: false);
                        var fileName = request.Headers?[HttpHeaders.X_FILE_NAME] ?? "file";
                        request.UploadFile = new HttpRequest.UploadFileDescriptor
                        {
                            Stream = ms,
                            FileName = fileName,
                            FieldName = "file",
                            CancellationToken = CancellationToken.None
                        };
                        request.BodyBytes = null;
                    }
                    // 如果 Body 字符串是一个存在的本地文件路径，则打开文件流
                    else if (!string.IsNullOrEmpty(request.Body) && System.IO.File.Exists(request.Body))
                    {
                        var fs = System.IO.File.OpenRead(request.Body);
                        var fileName = request.Headers?[HttpHeaders.X_FILE_NAME] ?? System.IO.Path.GetFileName(request.Body);
                        request.UploadFile = new HttpRequest.UploadFileDescriptor
                        {
                            Stream = fs,
                            FileName = fileName,
                            FieldName = "file",
                            CancellationToken = CancellationToken.None
                        };
                        request.Body = null;
                    }
                }
            }
            catch (Exception ex)
            {
                // 不阻塞主流程，仅记录错误
                try { Logger.Error($"构建隐式 UploadFile 时发生错误: {ex.Message}"); } catch { }
            }

            System.Net.Http.HttpMethod method;
            try
            {
                method = ParseMethod(request.Method);
            }
            catch
            {
                method = System.Net.Http.HttpMethod.Get;
            }

            return await SendAsyncInternal(method, request.Path ?? request.Url ?? "", request.Body, request.BodyBytes, request.BodyObject, request.Headers, request.Query, request.UploadFile);
        }

        private static System.Net.Http.HttpMethod ParseMethod(string method)
        {
            if (string.IsNullOrEmpty(method)) return System.Net.Http.HttpMethod.Get;
            return method.ToUpper() switch
            {
                "GET" => System.Net.Http.HttpMethod.Get,
                "POST" => System.Net.Http.HttpMethod.Post,
                "PUT" => System.Net.Http.HttpMethod.Put,
                "DELETE" => System.Net.Http.HttpMethod.Delete,
                "PATCH" => System.Net.Http.HttpMethod.Patch,
                _ => new System.Net.Http.HttpMethod(method)
            };
        }
    }
}
