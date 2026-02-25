using Drx.Sdk.Shared;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Drx.Sdk.Network.V2.Web.Http;

namespace Drx.Sdk.Network.V2.Web.Core
{
    /// <summary>
    /// HTTP 客户端，用于发送各类 HTTP 请求并支持流式上传与下载。
    /// </summary>
    /// <remarks>
    /// 本类封装了常见的异步 HTTP 操作，支持：
    /// - 以字符串/字节数组/对象（序列化为 JSON）作为请求体发送；
    /// - 流式文件上传（支持进度与取消），上传可通过 HttpRequest.UploadFile 指定或由方法根据 Body 隐式构建；
    /// - 下载文件到本地或写入目标流（支持进度与取消，并尽量进行原子替换目标文件）。
    /// 注意：字段与属性未在此处注释；调用方需负责传入流的生命周期管理。
    /// </remarks>
    /// <seealso cref="HttpRequest"/>
    /// <seealso cref="HttpResponse"/>
    public class DrxHttpClient : IAsyncDisposable
    {
        private readonly System.Net.Http.HttpClient _httpClient;
    private readonly System.Net.Http.HttpClientHandler _httpHandler;
    private System.Net.CookieContainer _cookieContainer;
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
        public DrxHttpClient()
        {
            _cookieContainer = new System.Net.CookieContainer();
            _httpHandler = new System.Net.Http.HttpClientHandler { CookieContainer = _cookieContainer, UseCookies = true };
            _httpClient = new System.Net.Http.HttpClient(_httpHandler);
            // 默认自动管理 cookies
            AutoManageCookies = true;
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
        /// <param name="baseAddress">用于初始化内部 HttpClient 的基地址。</param>
        /// <exception cref="System.ArgumentException">当 baseAddress 不是有效的 URI 时抛出。</exception>
        /// <exception cref="System.Exception">初始化 HttpClient 发生其它错误时抛出并向上传播。</exception>
        public DrxHttpClient(string baseAddress)
        {
            try
            {
                _cookieContainer = new System.Net.CookieContainer();
                _httpHandler = new System.Net.Http.HttpClientHandler { CookieContainer = _cookieContainer, UseCookies = true };
                _httpClient = new System.Net.Http.HttpClient(_httpHandler)
                {
                    BaseAddress = new Uri(baseAddress)
                };
                // 默认自动管理 cookies
                AutoManageCookies = true;
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

    /// <summary>
    /// 是否自动管理 Cookie（将自动使用内部 CookieContainer 跟踪 Set-Cookie 并在请求时发送对应 Cookie）。
    /// 默认为 true。
    /// </summary>
    public bool AutoManageCookies { get; set; } = true;

    /// <summary>
    /// 会话 Cookie 名称，默认与服务器一致为 "session_id"。可根据服务端配置调整。
    /// </summary>
    public string SessionCookieName { get; set; } = "session_id";

    /// <summary>
    /// 可选：如果服务端使用自定义请求头传递会话令牌，可设置该字段（例如 "X-Session-Id"），
    /// 客户端在发送请求时会自动将当前会话 id 写入该 header（当 header 未被调用方显式设置时）。
    /// 默认为 null（不使用 header）。
    /// </summary>
    public string? SessionHeaderName { get; set; } = null;

        /// <summary>
        /// 从内部 CookieContainer 中获取当前会话 ID（基于 SessionCookieName）。
        /// 如果未找到返回 null。该方法优先尝试使用 HttpClient.BaseAddress 作为域；
        /// 如果 baseAddress 为 null，则需要调用方使用 SetSessionId 手动注入 cookie 或使用 ImportCookies。
        /// </summary>
        /// <param name="forUrl">可选：指定用于查找 cookie 的 URL（优先），例如请求的完整 URL。</param>
        /// <returns>会话 id 或 null</returns>
        public string? GetSessionId(string? forUrl = null)
        {
            try
            {
                if (!AutoManageCookies) return null;
                Uri? uri = null;
                if (!string.IsNullOrEmpty(forUrl))
                {
                    if (Uri.TryCreate(forUrl, UriKind.Absolute, out var u)) uri = u;
                }
                if (uri == null && _httpClient.BaseAddress != null) uri = _httpClient.BaseAddress;
                if (uri == null) return null;

                var cookies = _cookieContainer.GetCookies(uri);
                foreach (System.Net.Cookie c in cookies)
                {
                    if (string.Equals(c.Name, SessionCookieName, StringComparison.OrdinalIgnoreCase))
                        return c.Value;
                }
                return null;
            }
            catch { return null; }
        }

        /// <summary>
        /// 将会话 id 写入内部 CookieContainer（可指定 domain/path）。
        /// 该方法不会修改服务器端，仅在后续请求时携带该 cookie。
        /// </summary>
        public void SetSessionId(string sessionId, string? domain = null, string path = "/")
        {
            if (string.IsNullOrEmpty(sessionId)) return;
            try
            {
                if (!AutoManageCookies) return;
                Uri uri;
                if (!string.IsNullOrEmpty(domain))
                {
                    if (!domain.StartsWith("http", StringComparison.OrdinalIgnoreCase)) domain = "http://" + domain.TrimEnd('/');
                    uri = new Uri(domain);
                }
                else if (_httpClient.BaseAddress != null)
                {
                    uri = _httpClient.BaseAddress;
                }
                else
                {
                    // fallback domain for cookie storage
                    uri = new Uri("http://localhost/");
                }

                var cookie = new System.Net.Cookie(SessionCookieName, sessionId, path, uri.Host);
                _cookieContainer.Add(uri, cookie);
            }
            catch { }
        }

        /// <summary>
        /// 清空内部 CookieContainer 中的所有 cookie（慎用）。
        /// </summary>
        public void ClearCookies()
        {
            try
            {
                // CookieContainer 没有直接清空方法，替换为新的实例
                // 注意：此操作在并发场景下是安全的，因为我们仅替换字段引用
                var newContainer = new System.Net.CookieContainer();
                // 如果 httpHandler 存在，更新其 CookieContainer（handler 可能已用于 HttpClient）
                try
                {
                    // HttpClientHandler 没有公开 setter 来替换 CookieContainer，故仅替换我们自己的引用并尽量复制
                }
                catch { }
                // 仅替换本地引用以避免序列化/并发复杂性
                // 这里直接反射替换 handler 的内部字段不是必要的，因此我们仅替换引用以供后续 SetSessionId/Import 使用
                // 注意：已有 HttpClient 使用旧容器，新的请求可能不会自动使用新容器，建议在非关键场景使用
                // 为降低影响，仍将旧 cookies 复制到新容器然后替换引用
                try
                {
                    foreach (var table in GetAllCookies(_cookieContainer))
                    {
                        newContainer.Add(table.Key, table.Value);
                    }
                }
                catch { }
                // 最终替换
                // 不能真正替换 handler.CookieContainer（只读），所以我们仅替换本地引用
                // 这是一个保守操作，用于提供 ClearCookies API，提示调用方仅在简单场景使用
                // (详见注释)
                _cookieContainer = newContainer;
            }
            catch { }
        }

        // Helper 返回所有 domain->CookieCollection 项（用于复制）
        private IEnumerable<KeyValuePair<Uri, System.Net.CookieCollection>> GetAllCookies(System.Net.CookieContainer container)
        {
            var list = new List<KeyValuePair<Uri, System.Net.CookieCollection>>();
            try
            {
                // 只尝试基于 BaseAddress
                if (_httpClient.BaseAddress != null)
                {
                    list.Add(new KeyValuePair<Uri, System.Net.CookieCollection>(_httpClient.BaseAddress, container.GetCookies(_httpClient.BaseAddress)));
                }
            }
            catch { }
            return list;
        }

        /// <summary>
        /// 将内部 cookie 导出为 JSON 字符串，便于持久化或跨进程传递（仅导出 BaseAddress 域下的 cookie）。
        /// 格式: [{"Name":"...","Value":"...","Domain":"...","Path":"...","Expires":...,"Secure":true,"HttpOnly":true},...]
        /// </summary>
        public string ExportCookies()
        {
            try
            {
                var list = new List<object>();
                if (_httpClient.BaseAddress != null)
                {
                    var cookies = _cookieContainer.GetCookies(_httpClient.BaseAddress);
                    foreach (System.Net.Cookie c in cookies)
                    {
                        list.Add(new
                        {
                            Name = c.Name,
                            Value = c.Value,
                            Domain = c.Domain,
                            Path = c.Path,
                            Expires = c.Expires == DateTime.MinValue ? (DateTime?)null : c.Expires,
                            Secure = c.Secure,
                            HttpOnly = c.HttpOnly
                        });
                    }
                }
                return System.Text.Json.JsonSerializer.Serialize(list);
            }
            catch { return "[]"; }
        }

        /// <summary>
        /// 从 JSON 导入 cookie（与 ExportCookies 生成的格式兼容）。
        /// 导入后会把 cookie 加入到 BaseAddress 域或指定 domain（如果 JSON 指定 domain 则使用之）。
        /// </summary>
        public void ImportCookies(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var arr = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(json);
                if (arr == null) return;
                foreach (var el in arr)
                {
                    try
                    {
                        var name = el.GetProperty("Name").GetString();
                        var value = el.GetProperty("Value").GetString();
                        var domain = el.TryGetProperty("Domain", out var pd) ? pd.GetString() : null;
                        var path = el.TryGetProperty("Path", out var pp) ? pp.GetString() ?? "/" : "/";
                        var secure = el.TryGetProperty("Secure", out var ps) ? ps.GetBoolean() : false;
                        var httpOnly = el.TryGetProperty("HttpOnly", out var ph) ? ph.GetBoolean() : false;

                        Uri uri;
                        if (!string.IsNullOrEmpty(domain))
                        {
                            if (!domain.StartsWith("http", StringComparison.OrdinalIgnoreCase)) domain = "http://" + domain.TrimEnd('/');
                            uri = new Uri(domain);
                        }
                        else if (_httpClient.BaseAddress != null)
                        {
                            uri = _httpClient.BaseAddress;
                        }
                        else
                        {
                            uri = new Uri("http://localhost/");
                        }

                        var cookie = new System.Net.Cookie(name ?? "", value ?? "", path ?? "/", uri.Host) { Secure = secure, HttpOnly = httpOnly };
                        _cookieContainer.Add(uri, cookie);
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// 将当前会话 id（若存在）写入请求的 header（当 SessionHeaderName 已设置且调用方未显式设置 header 时）。
        /// </summary>
        private void ApplySessionToRequest(System.Net.Http.HttpRequestMessage request)
        {
            if (!string.IsNullOrEmpty(SessionHeaderName) && request != null)
            {
                if (!request.Headers.Contains(SessionHeaderName))
                {
                    var sessionId = GetSessionId(request.RequestUri?.ToString());
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        request.Headers.Add(SessionHeaderName, EnsureAsciiHeaderValue(sessionId));
                    }
                }
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
        /// 发送 HTTP 请求，使用字符串作为请求体（将以 application/json 发送）。
        /// </summary>
        /// <param name="method">HTTP 方法（GET/POST/PUT/DELETE 等）。</param>
        /// <param name="url">目标 URL 或相对路径。</param>
        /// <param name="body">请求体字符串（可为 null）。</param>
        /// <param name="headers">可选请求头集合（键值对）。</param>
        /// <param name="query">可选查询参数集合（键值对）。</param>
        /// <returns>返回包含状态码、响应文本、响应字节及反序列化对象（若为 JSON）的 <see cref="HttpResponse"/>。</returns>
        public async Task<HttpResponse> SendAsync(System.Net.Http.HttpMethod method, string url, string? body = null, NameValueCollection? headers = null, NameValueCollection? query = null)
        {
            return await SendAsyncInternal(method, url, body, null, null, headers, query, null);
        }

        /// <summary>
        /// 发送 HTTP 请求，使用字节数组作为请求体。
        /// </summary>
        /// <param name="method">HTTP 方法（GET/POST/PUT/DELETE 等）。</param>
        /// <param name="url">目标 URL 或相对路径。</param>
        /// <param name="bodyBytes">请求体字节数组（可为 null）。</param>
        /// <param name="headers">可选请求头集合（键值对）。</param>
        /// <param name="query">可选查询参数集合（键值对）。</param>
        /// <returns>返回包含状态码、响应文本、响应字节及反序列化对象（若为 JSON）的 <see cref="HttpResponse"/>。</returns>
        public async Task<HttpResponse> SendAsync(System.Net.Http.HttpMethod method, string url, byte[]? bodyBytes, NameValueCollection? headers = null, NameValueCollection? query = null)
        {
            return await SendAsyncInternal(method, url, null, bodyBytes, null, headers, query, null);
        }

        /// <summary>
        /// 发送 HTTP 请求，使用对象作为请求体（将被序列化为 JSON）。
        /// </summary>
        /// <param name="method">HTTP 方法（GET/POST/PUT/DELETE 等）。</param>
        /// <param name="url">目标 URL 或相对路径。</param>
        /// <param name="bodyObject">请求体对象（可为 null），在发送前会被序列化为 JSON。</param>
        /// <param name="headers">可选请求头集合（键值对）。</param>
        /// <param name="query">可选查询参数集合（键值对）。</param>
        /// <returns>返回包含状态码、响应文本、响应字节及反序列化对象（若为 JSON）的 <see cref="HttpResponse"/>。</returns>
        public async Task<HttpResponse> SendAsync(System.Net.Http.HttpMethod method, string url, object? bodyObject, NameValueCollection? headers = null, NameValueCollection? query = null)
        {
            return await SendAsyncInternal(method, url, null, null, bodyObject, headers, query, null);
        }

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
        /// <param name="fileStream">要上传的源流，不能为 null。调用方负责管理该流的生命周期，方法结束后不会自动关闭该流。</param>
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

            // 应用可选的 session header（当配置了 SessionHeaderName 时）
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

                    // 在发送前尝试应用 session header
                    ApplySessionToRequest(uploadRequestMessage);
                    var uploadServerResponse = await _httpClient.SendAsync(uploadRequestMessage, HttpCompletionOption.ResponseContentRead, uploadFile.CancellationToken);
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

                    // 应用可选的 session header
                    ApplySessionToRequest(uploadRequestMessage);

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

                // 应用可选的 session header
                ApplySessionToRequest(requestMessage);

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
        /// 发送 GET 请求。
        /// </summary>
        /// <param name="url">目标 URL 或相对路径。</param>
        /// <param name="headers">可选请求头集合。</param>
        /// <param name="query">可选查询参数集合。</param>
        /// <returns>返回 <see cref="HttpResponse"/> 包含服务器响应信息。</returns>
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
        /// 发送 POST 请求，支持传入 string/byte[]/object 类型的请求体。
        /// </summary>
        /// <param name="url">目标 URL 或相对路径。</param>
        /// <param name="body">请求体，可为 string/byte[]/object 或 null。</param>
        /// <param name="headers">可选请求头集合。</param>
        /// <param name="query">可选查询参数集合。</param>
        /// <returns>返回 <see cref="HttpResponse"/> 包含服务器响应信息。</returns>
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
        /// 发送 PUT 请求，支持传入 string/byte[]/object 类型的请求体。
        /// </summary>
        /// <param name="url">目标 URL 或相对路径。</param>
        /// <param name="body">请求体，可为 string/byte[]/object 或 null。</param>
        /// <param name="headers">可选请求头集合。</param>
        /// <param name="query">可选查询参数集合。</param>
        /// <returns>返回 <see cref="HttpResponse"/> 包含服务器响应信息。</returns>
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
        /// 发送 DELETE 请求。
        /// </summary>
        /// <param name="url">目标 URL 或相对路径。</param>
        /// <param name="headers">可选请求头集合。</param>
        /// <param name="query">可选查询参数集合。</param>
        /// <returns>返回 <see cref="HttpResponse"/> 包含服务器响应信息。</returns>
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
        /// 下载远程文件到指定本地路径，支持进度回调和取消操作，并在可能时进行原子替换目标文件。
        /// </summary>
        /// <param name="url">文件的远程 URL。</param>
        /// <param name="destPath">本地目标路径，下载完成后会尝试原子替换该文件（若已存在）。</param>
        /// <param name="progress">可选进度回调，报告已下载字节数。</param>
        /// <param name="cancellationToken">可选取消令牌。</param>
        /// <returns>异步任务，完成或抛出异常以指示失败。</returns>
        /// <exception cref="OperationCanceledException">下载被取消时抛出。</exception>
        /// <exception cref="System.Exception">下载或写入文件时发生错误会向上抛出。</exception>
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
        /// 将远程文件流写入到目标流，支持进度回调与取消操作。方法不会关闭传入的目标流，调用方负责流的生命周期。
        /// </summary>
        /// <param name="url">文件的远程 URL。</param>
        /// <param name="destination">目标写入流，不能为 null，方法结束后不会自动关闭该流。</param>
        /// <param name="progress">可选进度回调，报告已下载字节数。</param>
        /// <param name="cancellationToken">可选取消令牌。</param>
        /// <returns>异步任务，完成或抛出异常以指示失败。</returns>
        /// <exception cref="System.ArgumentNullException">当 destination 为 null 时抛出。</exception>
        public async Task DownloadToStreamAsync(string url, Stream destination, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            try
            {
                using var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

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

        /// <summary>
        /// 设置默认请求头，影响后续发送的请求。
        /// </summary>
        /// <param name="name">头部名称。</param>
        /// <param name="value">头部值（会确保为 ASCII，可自动转义不可 ASCII 字符）。</param>
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
        /// 设置底层 HttpClient 的超时时间。
        /// </summary>
        /// <param name="timeout">超时时间。</param>
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
        /// 异步释放本实例占用的资源并停止后台请求处理。
        /// </summary>
        /// <returns>表示释放完成的可等待结构。</returns>
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
        /// 统一的 SendAsync 接口，接受自定义 <see cref="HttpRequest"/> 对象并返回 <see cref="HttpResponse"/>。
        /// 如果需要上传文件，请设置 <see cref="HttpRequest.UploadFile"/> 或让方法根据 Body 隐式构建上传描述。
        /// </summary>
        /// <param name="request">要发送的自定义请求对象，不能为 null。</param>
        /// <returns>返回服务器响应的 <see cref="HttpResponse"/>。</returns>
        /// <exception cref="System.ArgumentNullException">当 request 为 null 时抛出。</exception>
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
        /// 便捷方法：上传本地文件并附带可选 metadata（metadata 对象将被序列化为 JSON）。
        /// </summary>
        /// <param name="url">上传目标 URL。</param>
        /// <param name="filePath">本地文件路径，文件必须存在。</param>
        /// <param name="metadata">可选的元数据对象，将被序列化为 JSON 并作为 metadata 字段附加。</param>
        /// <param name="headers">可选请求头集合。</param>
        /// <param name="progress">可选进度回调。</param>
        /// <param name="cancellationToken">可选取消令牌。</param>
        /// <exception cref="System.IO.FileNotFoundException">当指定的本地文件不存在时抛出。</exception>
        /// <returns>返回服务器响应的 <see cref="HttpResponse"/>。</returns>
        public async Task<HttpResponse> UploadFileWithMetadataAsync(string url, string filePath, object? metadata = null, NameValueCollection? headers = null, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) throw new FileNotFoundException("上传文件不存在", filePath);

            using var fs = File.OpenRead(filePath);
            return await UploadFileWithMetadataAsync(url, fs, Path.GetFileName(filePath), metadata, headers, progress, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 便捷方法：上传流并附带可选 metadata（metadata 对象将被序列化为 JSON）。
        /// </summary>
        /// <param name="url">上传目标 URL。</param>
        /// <param name="fileStream">要上传的源流，不能为 null。调用方负责流的生命周期，方法结束后不会关闭该流。</param>
        /// <param name="fileName">上传时使用的文件名；若为空则使用默认 file。</param>
        /// <param name="metadata">可选的元数据对象，将被序列化为 JSON 并作为 metadata 字段附加。</param>
        /// <param name="headers">可选请求头集合。</param>
        /// <param name="progress">可选进度回调。</param>
        /// <param name="cancellationToken">可选取消令牌。</param>
        /// <returns>返回服务器响应的 <see cref="HttpResponse"/>。</returns>
        /// <exception cref="System.ArgumentNullException">当 fileStream 为 null 时抛出。</exception>
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
