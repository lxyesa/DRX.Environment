using Drx.Sdk.Shared;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Drx.Sdk.Network.V2.Web
{
    /// <summary>
    /// HTTP 客户端，用于发送各类 HTTP 请求并支持流式上传/下载。
    ///说明：上传功能已与 `SendAsync(HttpRequest)` 合并，调用方可通过 `HttpRequest.UploadFile` 提供流式上传信息。
    /// 为兼容性仍保留部分便捷方法（如 UploadFileAsync / UploadFileWithMetadataAsync）。
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
        /// 默认构造函数，使用内部 HttpClient 并启动请求处理通道。
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
        /// 指定基础地址的构造函数。
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

        // 将 header 值保证为 ASCII，不可 ASCII 的字节会以百分号转义形式编码。
        private static string EnsureAsciiHeaderValue(string? value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? "";
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
            var bytes = Encoding.UTF8.GetBytes(value);
            var sb = new StringBuilder();
            foreach (var b in bytes)
            {
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
        ///发送 HTTP 请求（字符串 body）。
        /// </summary>
        public async Task<HttpResponse> SendAsync(System.Net.Http.HttpMethod method, string url, string? body = null, NameValueCollection? headers = null, NameValueCollection? query = null)
        {
            return await SendAsyncInternal(method, url, body, null, null, headers, query, null);
        }

        /// <summary>
        ///发送 HTTP 请求（字节数组 body）。
        /// </summary>
        public async Task<HttpResponse> SendAsync(System.Net.Http.HttpMethod method, string url, byte[]? bodyBytes, NameValueCollection? headers = null, NameValueCollection? query = null)
        {
            return await SendAsyncInternal(method, url, null, bodyBytes, null, headers, query, null);
        }

        /// <summary>
        ///发送 HTTP 请求（对象 body，会被序列化为 JSON）。
        /// </summary>
        public async Task<HttpResponse> SendAsync(System.Net.Http.HttpMethod method, string url, object? bodyObject, NameValueCollection? headers = null, NameValueCollection? query = null)
        {
            return await SendAsyncInternal(method, url, null, null, bodyObject, headers, query, null);
        }

        /// <summary>
        ///便捷的文件上传（保持兼容，内部最终调用 SendAsync(HttpRequest)）。
        /// </summary>
        public async Task<HttpResponse> UploadFileAsync(string url, string filePath, string fieldName = "file", NameValueCollection? headers = null, NameValueCollection? query = null, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) throw new FileNotFoundException("上传文件不存在", filePath);

            var fileInfo = new FileInfo(filePath);
            using var fileStream = File.OpenRead(filePath);
            return await UploadFileAsync(url, fileStream, Path.GetFileName(filePath), fieldName, headers, query, progress, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 上传流并支持进度/取消。
        /// </summary>
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

        //进度流包装：用于在流读取时上报进度（不会在 Dispose 时关闭底层流）。
        private class ProgressableStreamContent : Stream
        {
            private readonly Stream _inner;
            private readonly int _bufferSize;
            private readonly IProgress<long>? _progress;
            private readonly CancellationToken _cancellation;
            private long _totalRead;

            public ProgressableStreamContent(Stream inner, int bufferSize, IProgress<long>? progress, CancellationToken cancellation)
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
            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => _inner.BeginRead(buffer, offset, count, callback, state);
            public override int EndRead(IAsyncResult asyncResult) => _inner.EndRead(asyncResult);
            protected override void Dispose(bool disposing)
            {
                // 不在此处关闭底层流，调用方负责管理流的生命周期
                base.Dispose(disposing);
            }
        }

        private async Task<HttpResponse> SendAsyncInternal(System.Net.Http.HttpMethod method, string url, string? body, byte[]? bodyBytes, object? bodyObject, NameValueCollection? headers, NameValueCollection? query, HttpRequest.UploadFileDescriptor? uploadFile)
        {
            try
            {
                // 如果包含文件上传描述，则构造 multipart/form-data 并上传
                if (uploadFile != null && uploadFile.Stream != null)
                {
                    var requestUrl = BuildUrl(url, query);
                    using var content = new MultipartFormDataContent();

                    var progressContent = new ProgressableStreamContent(uploadFile.Stream, 81920, uploadFile.Progress, uploadFile.CancellationToken);
                    var streamContent = new StreamContent(progressContent, 81920);
                    streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = $"\"{uploadFile.FieldName}\"", FileNameStar = uploadFile.FileName ?? "file" };
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                    content.Add(streamContent, uploadFile.FieldName, uploadFile.FileName ?? "file");

                    // 附加 metadata（body/bodyObject/bodyBytes）到 multipart 的 metadata 部分
                    try
                    {
                        if (bodyBytes != null && bodyBytes.Length > 0)
                        {
                            var metaBytesContent = new ByteArrayContent(bodyBytes);
                            metaBytesContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "metadata_bytes" };
                            content.Add(metaBytesContent, "metadata_bytes");
                        }
                        else if (bodyObject != null)
                        {
                            var json = System.Text.Json.JsonSerializer.Serialize(bodyObject);
                            var metaString = new StringContent(json, Encoding.UTF8, "application/json");
                            metaString.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "metadata" };
                            content.Add(metaString, "metadata");
                        }
                        else if (!string.IsNullOrEmpty(body))
                        {
                            var metaString = new StringContent(body, Encoding.UTF8, "application/json");
                            metaString.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "metadata" };
                            content.Add(metaString, "metadata");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"附加 metadata 到 multipart 时发生错误: {ex}");
                    }

                    var uploadRequestMessage = new HttpRequestMessage(method, requestUrl)
                    {
                        Content = content
                    };

                    if (headers != null)
                    {
                        foreach (string key in headers)
                        {
                            if (headers[key] != null)
                                uploadRequestMessage.Headers.Add(key, EnsureAsciiHeaderValue(headers[key]));
                        }
                    }

                    var uploadServerResponse = await _httpClient.SendAsync(uploadRequestMessage, HttpCompletionOption.ResponseContentRead, uploadFile.CancellationToken).ConfigureAwait(false);
                    var uploadServerResponseBody = await uploadServerResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var uploadServerResponseBytes = await uploadServerResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    var uploadHttpResponse = new HttpResponse((int)uploadServerResponse.StatusCode, uploadServerResponseBody, uploadServerResponse.ReasonPhrase ?? "");
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

                var httpResponse = new HttpResponse((int)response.StatusCode, responseBody, response.ReasonPhrase ?? "");
                httpResponse.BodyBytes = responseBytes;

                // 尝试将响应反序列化为对象（假设 JSON）
                try
                {
                    httpResponse.BodyObject = System.Text.Json.JsonSerializer.Deserialize<object>(responseBody);
                }
                catch
                {
                    //解析失败则保持为 null
                }

                //复制响应头
                foreach (var header in response.Headers)
                {
                    httpResponse.Headers.Add(header.Key, string.Join(",", header.Value));
                }

                foreach (var header in response.Content.Headers)
                {
                    httpResponse.Headers.Add(header.Key, string.Join(",", header.Value));
                }

                return httpResponse;
            }
            catch (HttpRequestException ex)
            {
                Logger.Error($"发送 HTTP 请求时发生网络错误: {method}, 错误: {ex.Message}");
                throw;
            }
            catch (TaskCanceledException ex)
            {
                Logger.Error($"发送 HTTP 请求超时: {method}, 错误: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"发送 HTTP 请求时发生未知错误: {method}, 错误: {ex.Message}");
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
                // 支持队列化的上传请求
                if (requestTask.UploadFile != null && requestTask.UploadFile.Stream != null)
                {
                    var requestUrl = BuildUrl(requestTask.Url, requestTask.Query);
                    using var content = new MultipartFormDataContent();

                    var progressContent = new ProgressableStreamContent(requestTask.UploadFile.Stream, 81920, requestTask.UploadFile.Progress, requestTask.UploadFile.CancellationToken);
                    var streamContent = new StreamContent(progressContent, 81920);
                    streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = $"\"{requestTask.UploadFile.FieldName}\"", FileNameStar = requestTask.UploadFile.FileName ?? "file" };
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                    content.Add(streamContent, requestTask.UploadFile.FieldName, requestTask.UploadFile.FileName ?? "file");

                    try
                    {
                        if (requestTask.BodyBytes != null && requestTask.BodyBytes.Length > 0)
                        {
                            var metaBytesContent = new ByteArrayContent(requestTask.BodyBytes);
                            metaBytesContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "metadata_bytes" };
                            content.Add(metaBytesContent, "metadata_bytes");
                        }
                        else if (requestTask.BodyObject != null)
                        {
                            var json = System.Text.Json.JsonSerializer.Serialize(requestTask.BodyObject);
                            var metaString = new StringContent(json, Encoding.UTF8, "application/json");
                            metaString.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "metadata" };
                            content.Add(metaString, "metadata");
                        }
                        else if (!string.IsNullOrEmpty(requestTask.Body))
                        {
                            var metaString = new StringContent(requestTask.Body, Encoding.UTF8, "application/json");
                            metaString.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "metadata" };
                            content.Add(metaString, "metadata");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"附加 metadata 到 multipart 时发生错误: {ex}");
                    }

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

                    var uploadResult = new HttpResponse((int)serverResponse.StatusCode, serverResponseBody, serverResponse.ReasonPhrase ?? "");
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

                var httpResponseNormal = new HttpResponse((int)responseNormal.StatusCode, responseBodyNormal, responseNormal.ReasonPhrase ?? "");
                httpResponseNormal.BodyBytes = responseBytesNormal;

                try
                {
                    httpResponseNormal.BodyObject = System.Text.Json.JsonSerializer.Deserialize<object>(responseBodyNormal);
                }
                catch
                {
                }

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
        ///发送 GET 请求
        /// </summary>
        public Task<HttpResponse> GetAsync(string url, NameValueCollection? headers = null, NameValueCollection? query = null)
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
        ///发送 POST 请求（支持 string/byte[]/object）
        /// </summary>
        public Task<HttpResponse> PostAsync(string url, object? body = null, NameValueCollection? headers = null, NameValueCollection? query = null)
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
        ///发送 PUT 请求
        /// </summary>
        public Task<HttpResponse> PutAsync(string url, object? body = null, NameValueCollection? headers = null, NameValueCollection? query = null)
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
        ///发送 DELETE 请求
        /// </summary>
        public Task<HttpResponse> DeleteAsync(string url, NameValueCollection? headers = null, NameValueCollection? query = null)
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
        /// 下载文件到指定路径，支持进度与取消。完成后原子替换目标文件。
        /// </summary>
        public async Task DownloadFileAsync(string url, string destPath, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
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

                try
                {
                    if (File.Exists(destPath))
                    {
                        try
                        {
                            // Prefer atomic replace when possible
                            File.Replace(tempFile, destPath, null);
                        }
                        catch (PlatformNotSupportedException) // unlikely on Windows, but be defensive
                        {
                            // fallback to delete and move
                            File.Delete(destPath);
                            File.Move(tempFile, destPath);
                        }
                        catch (IOException)
                        {
                            // Could be file locked or race; try best-effort delete+move
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
        /// 将远程文件流写入到目标流，支持进度与取消（不会关闭目标流）。
        /// </summary>
        public async Task DownloadToStreamAsync(string url, Stream destination, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            try
            {
                using var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
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

        /// <summary>
        /// 设置默认请求头
        /// </summary>
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
                _requestChannel.Writer.TryComplete();
                _cts.Cancel();

                if (_processingTask != null)
                {
                    try
                    {
                        await _processingTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
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

        private string BuildUrl(string url, NameValueCollection? query)
        {
            try
            {
                if (query == null || query.Count == 0)
                    return url;

                var queryString = string.Join("&", query.AllKeys.Where(k => k != null && query[k] != null).Select(key => $"{Uri.EscapeDataString(key!)}={Uri.EscapeDataString(query[key!]!)}"));
                return url.Contains("?") ? $"{url}&{queryString}" : $"{url}?{queryString}";
            }
            catch (Exception ex)
            {
                Logger.Error($"构建 URL 时发生错误: {ex.Message}");
                return url;
            }
        }

        /// <summary>
        ///统一的 SendAsync 接口，接受自定义 HttpRequest 对象。
        /// 如果需要上传文件，请设置 `HttpRequest.UploadFile`。
        /// </summary>
        public async Task<HttpResponse> SendAsync(HttpRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            try
            {
                if (request.UploadFile == null)
                {
                    if (request.BodyObject is Stream bodyStream)
                    {
                        var fileName = request.Headers?[HttpHeaders.X_FILE_NAME];
                        if (string.IsNullOrEmpty(fileName) && request.Headers != null)
                        {
                            if (!string.IsNullOrEmpty(request.Headers[HttpHeaders.X_FILE_NAME_BASE64]))
                            {
                                try { fileName = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers[HttpHeaders.X_FILE_NAME_BASE64]!)); } catch { fileName = request.Headers[HttpHeaders.X_FILE_NAME_BASE64]; }
                            }
                            else if (!string.IsNullOrEmpty(request.Headers[HttpHeaders.X_FILE_NAME_ENCODED]))
                            {
                                fileName = Uri.UnescapeDataString(request.Headers[HttpHeaders.X_FILE_NAME_ENCODED]!);
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
                        request.BodyObject = null;
                    }
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

        /// <summary>
        ///便捷方法：上传本地文件并带可选 metadata（对象将被序列化为 JSON）。
        /// </summary>
        public async Task<HttpResponse> UploadFileWithMetadataAsync(string url, string filePath, object? metadata = null, NameValueCollection? headers = null, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) throw new FileNotFoundException("上传文件不存在", filePath);

            using var fs = File.OpenRead(filePath);
            return await UploadFileWithMetadataAsync(url, fs, Path.GetFileName(filePath), metadata, headers, progress, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///便捷方法：上传流并带可选 metadata（对象将被序列化为 JSON）。
        /// 注意：不会在方法结束时关闭传入的流。
        /// </summary>
        public async Task<HttpResponse> UploadFileWithMetadataAsync(string url, Stream fileStream, string fileName, object? metadata = null, NameValueCollection? headers = null, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
            if (string.IsNullOrEmpty(fileName)) fileName = "file";

            string body = null;
            if (metadata != null)
            {
                try
                {
                    if (metadata is JToken jToken)
                    {
                        body = jToken.ToString();
                    }
                    else
                    {
                        body = JsonSerializer.Serialize(metadata);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"序列化 metadata 时发生错误: {ex.Message}");
                    body = metadata.ToString();
                }
            }


            var req = new HttpRequest
            {
                Url = url,
                Method = "POST",
                Body = body,
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

            try
            {
                if (string.IsNullOrEmpty(req.Headers[HttpHeaders.X_FILE_NAME]))
                {
                    req.Headers.Add(HttpHeaders.X_FILE_NAME, fileName);
                }
            }
            catch { }

            return await SendAsync(req).ConfigureAwait(false);
        }

    }
}
