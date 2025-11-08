using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;
using Drx.Sdk.Network.V2.Web;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Drx.Sdk.Network.V2.Web.Asp
{
    /// <summary>
    /// DrxHttpAspClient - 一个轻量的 HttpClient 封装，便于快速向 DrxHttpAspServer 发送请求。
    /// </summary>
    public class DrxHttpAspClient : IDisposable
    {
        private readonly HttpClient _client;
        private bool _disposed;
        private System.Collections.Specialized.NameValueCollection _defaultHeaders = new System.Collections.Specialized.NameValueCollection();

        /// <summary>
        /// 使用指定的基地址创建客户端（例如："http://localhost:5000"）。
        /// </summary>
        /// <param name="baseAddress">服务器基地址，包含协议和端口。</param>
        public DrxHttpAspClient(string? baseAddress = null)
        {
            _client = new HttpClient();
            if (!string.IsNullOrEmpty(baseAddress))
                _client.BaseAddress = new Uri(baseAddress);
        }

        /// <summary>
        /// 发送 GET 请求并以字符串形式返回响应体。
        /// </summary>
        /// <param name="path">要请求的路径（相对于 BaseAddress 或完整 URL）。</param>
        /// <returns>响应的字符串内容。</returns>
        public async Task<string> GetStringAsync(string path)
        {
            var resp = await _client.GetAsync(path).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 发送 POST 请求，内容为 JSON 字符串，并返回响应字符串。
        /// </summary>
        /// <param name="path">请求路径或完整 URL。</param>
        /// <param name="json">要发送的 JSON 字符串。</param>
        /// <returns>响应的字符串内容。</returns>
        public async Task<string> PostJsonAsync(string path, string json)
        {
            using var content = new StringContent(json ?? string.Empty, Encoding.UTF8, "application/json");
            var resp = await _client.PostAsync(path, content).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 获取底层 HttpClient 的实例以执行更高级的操作。
        /// </summary>
        public HttpClient HttpClient => _client;

        /// <summary>
        /// 发送封装的 HttpRequest 并返回 HttpResponse。
        /// </summary>
        /// <param name="request">框架层的 HttpRequest 对象。</param>
        /// <returns>框架层的 HttpResponse 对象。</returns>
        public async Task<HttpResponse> SendAsync(HttpRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // 合并客户端默认头到请求（仅当请求中不存在该键时）
            if (_defaultHeaders != null)
            {
                try
                {
                    if (request.Headers == null) request.Headers = new System.Collections.Specialized.NameValueCollection();
                    foreach (string k in _defaultHeaders)
                    {
                        if (string.IsNullOrEmpty(k)) continue;
                        if (request.Headers[k] == null)
                            request.Headers.Add(k, _defaultHeaders[k]);
                    }
                }
                catch { }
            }

            var method = new HttpMethod(string.IsNullOrEmpty(request.Method) ? "GET" : request.Method);

            // 构建请求 URI：优先使用完整 Url，否则使用 Path（相对或绝对）
            Uri? uri = null;
            if (!string.IsNullOrEmpty(request.Url))
                uri = new Uri(request.Url);
            else if (!string.IsNullOrEmpty(request.Path))
            {
                if (_client.BaseAddress != null)
                {
                    uri = new Uri(_client.BaseAddress, request.Path);
                }
                else
                {
                    // 允许传入相对或完整路径
                    uri = new Uri(request.Path, UriKind.RelativeOrAbsolute);
                }
            }

            var msg = new HttpRequestMessage(method, uri ?? new Uri("/", UriKind.Relative));

            // 添加请求体（优先 BodyObject -> BodyBytes -> Body 字符串）
            if (request.BodyObject != null)
            {
                var json = JsonSerializer.Serialize(request.BodyObject);
                msg.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            else if (request.BodyBytes != null && request.BodyBytes.Length > 0)
            {
                msg.Content = new ByteArrayContent(request.BodyBytes);
            }
            else if (!string.IsNullOrEmpty(request.Body))
            {
                msg.Content = new StringContent(request.Body, Encoding.UTF8, "text/plain");
            }

            // 添加请求头（NameValueCollection）到 HttpRequestMessage
            if (request.Headers != null)
            {
                foreach (string key in request.Headers)
                {
                    if (string.IsNullOrEmpty(key)) continue;
                    var val = request.Headers[key];
                    try
                    {
                        // 尝试添加到通用请求头
                        if (!msg.Headers.TryAddWithoutValidation(key, val))
                        {
                            // 如果无法添加到请求头，则尝试加入到内容头
                            if (msg.Content != null)
                            {
                                msg.Content.Headers.TryAddWithoutValidation(key, val);
                            }
                        }
                    }
                    catch { }
                }
            }

            var respMsg = await _client.SendAsync(msg).ConfigureAwait(false);

            // 读取响应内容
            var bytes = await respMsg.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            string text = string.Empty;
            try { text = Encoding.UTF8.GetString(bytes); } catch { text = string.Empty; }

            var response = new HttpResponse((int)respMsg.StatusCode, text);

            // 填充 Headers
            var headers = new NameValueCollection();
            try
            {
                foreach (var h in respMsg.Headers)
                {
                    headers.Add(h.Key, string.Join(",", h.Value));
                }
                foreach (var h in respMsg.Content.Headers)
                {
                    headers.Add(h.Key, string.Join(",", h.Value));
                }
            }
            catch { }
            response.Headers = headers;
            response.BodyBytes = bytes;
            response.Body = text;
            try { ((IDictionary<string, object>)response.Content)["Text"] = text; } catch { }

            return response;
        }

        /// <summary>
        /// 为客户端设置一个默认请求头（会被添加到所有后续 Send 操作中，除非请求本身已包含该头）。
        /// </summary>
        public void SetDefaultHeader(string name, string value)
        {
            if (string.IsNullOrEmpty(name)) return;
            try
            {
                _defaultHeaders[name] = value;
            }
            catch { }
        }

        /// <summary>
        /// 批量设置默认头（替换/添加）。
        /// </summary>
        public void SetDefaultHeaders(System.Collections.Specialized.NameValueCollection headers)
        {
            if (headers == null) return;
            try
            {
                foreach (string k in headers)
                {
                    if (string.IsNullOrEmpty(k)) continue;
                    _defaultHeaders[k] = headers[k];
                }
            }
            catch { }
        }

        /// <summary>
        /// 方便重载：以 method/path/headers/body 方式发送请求并返回 HttpResponse。
        /// </summary>
        public async Task<HttpResponse> SendAsync(string method, string path, System.Collections.Specialized.NameValueCollection? headers = null, string? body = null)
        {
            var req = new HttpRequest();
            req.Method = method;
            req.Path = path;
            if (headers != null) req.Headers = headers;
            if (!string.IsNullOrEmpty(body)) req.Body = body;
            return await SendAsync(req).ConfigureAwait(false);
        }

        /// <summary>
        /// 释放 HttpClient。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _client.Dispose();
            _disposed = true;
        }
    }
}
