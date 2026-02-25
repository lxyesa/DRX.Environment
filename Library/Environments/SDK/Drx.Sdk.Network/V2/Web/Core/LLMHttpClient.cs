using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;using Drx.Sdk.Network.V2.Web.Http;
namespace Drx.Sdk.Network.V2.Web.Core
{
    /// <summary>
    /// 面向大语言模型的 HTTP 客户端，继承自框架内的 HttpClient，提供便捷的 POST 请求与流式响应读取能力。
    /// - 默认以 POST 发送请求
    /// - 会自动填充默认的模型与温度字段（可被传入的 body 覆盖）
    /// - 提供基于 IAsyncEnumerable 的流式读取接口，适用于模型的实时输出（SSE / chunked 等）
    /// </summary>
    public class LLMHttpClient : DrxHttpClient
    {
        public LLMHttpClient() : base()
        {
        }

        public LLMHttpClient(string baseAddress) : base(baseAddress)
        {
        }

        /// <summary>
        /// 以流式方式向大模型服务发送请求并逐块返回响应文本（原始文本片段）。
        /// 该方法会使用独立的 System.Net.Http.HttpClient 发起请求以便支持每次调用自定义超时和取消。
        /// 默认请求体会包含 model 和 temperature 字段，传入的 body 会被放到 "input" 字段（如果为 string）或合并到 body 对象中（如果为字典式对象）。
        /// </summary>
        /// <param name="url">请求 URL 或相对路径</param>
        /// <param name="body">请求体，可以为 string（当作输入文本）或任意对象（将被序列化为 JSON）</param>
        /// <param name="headers">可选请求头</param>
        /// <param name="query">可选查询参数</param>
        /// <param name="timeout">可选超时（null 表示不指定，由调用方或全局设置控制）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>IAsyncEnumerable<string>：按读取到的文本块异步返回</returns>
        public async IAsyncEnumerable<string> StreamAsync(string url,
            object? body = null,
            NameValueCollection? headers = null,
            NameValueCollection? query = null,
            TimeSpan? timeout = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // 构建请求 URL（含 query）
            string requestUrl = BuildUrlLocal(url, query);

            // 构建请求体 JSON，带默认字段并允许被传入 body 覆盖/扩展
            object requestPayload = BuildDefaultPayload(body);

            var json = JsonSerializer.Serialize(requestPayload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var httpClient = new System.Net.Http.HttpClient();
            if (timeout.HasValue)
            {
                httpClient.Timeout = Timeout.InfiniteTimeSpan; // 使用 CTS 控制超时
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeout.HasValue)
            {
                cts.CancelAfter(timeout.Value);
            }

            using var requestMessage = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, requestUrl)
            {
                Content = content
            };

            // 默认接受流式文本（可根据后端需要调整）
            requestMessage.Headers.Accept.TryParseAdd("text/event-stream");
            requestMessage.Headers.Accept.TryParseAdd("application/json");

            // 附加自定义请求头（确保 ASCII）
            if (headers != null)
            {
                foreach (string key in headers)
                {
                    if (string.IsNullOrEmpty(key)) continue;
                    try
                    {
                        requestMessage.Headers.TryAddWithoutValidation(key, EnsureAsciiHeaderValue(headers[key]));
                    }
                    catch { }
                }
            }

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            response.EnsureSuccessStatusCode();

            // 逐块读取响应流并以 UTF8 解码为字符串片段返回
            using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            var buffer = ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                int read;
                var decoder = Encoding.UTF8.GetDecoder();
                var charBuffer = new char[8192];
                var leftover = new StringBuilder();

                while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token).ConfigureAwait(false)) > 0)
                {
                    int charsDecoded = decoder.GetChars(buffer, 0, read, charBuffer, 0, flush: false);
                    var chunk = new string(charBuffer, 0, charsDecoded);

                    // 合并可能的残留并逐行/逐段输出 —— 简化处理：直接返回接收到的文本片段
                    if (leftover.Length > 0)
                    {
                        leftover.Append(chunk);
                        var output = leftover.ToString();
                        leftover.Clear();
                        yield return output;
                    }
                    else
                    {
                        yield return chunk;
                    }
                }

                // flush decoder
                int finalChars = decoder.GetChars(Array.Empty<byte>(), 0, 0, charBuffer, 0, flush: true);
                if (finalChars > 0)
                {
                    yield return new string(charBuffer, 0, finalChars);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                try { response.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// 发送一个非流式的 LLM 请求并返回框架内的 HttpResponse（便捷方法）。
        /// </summary>
        public Task<HttpResponse> SendLLMRequestAsync(string url, object? body = null, NameValueCollection? headers = null, NameValueCollection? query = null)
        {
            // 使用基类的 PostAsync（基类会将对象序列化为 JSON）
            var payload = BuildDefaultPayload(body);
            return PostAsync(url, payload, headers, query);
        }

        // 构建默认请求体并合并传入的 body（传入 string 时按 input 字段处理）
        private static object BuildDefaultPayload(object? body)
        {
            var defaults = new Dictionary<string, object>
            {
                ["model"] = "gpt-3.5-turbo",
                ["temperature"] = 0.7
            };

            if (body == null) return defaults;

            if (body is string s)
            {
                defaults["input"] = s;
                return defaults;
            }

            // 如果 body 是字典类型，则合并
            if (body is IDictionary<string, object> dictObj)
            {
                foreach (var kv in dictObj)
                {
                    defaults[kv.Key] = kv.Value!;
                }
                return defaults;
            }

            // 其他对象：将其作为 "input" 或直接作为 payload 的 "data" 字段
            // 为了兼容性，把对象序列化后放到 data 字段，且保留 defaults
            defaults["data"] = body;
            return defaults;
        }

        // 局部实现：构建带查询字符串的 URL（与基类行为一致）
        private static string BuildUrlLocal(string url, NameValueCollection? query)
        {
            try
            {
                if (query == null || query.Count == 0) return url;
                var items = new List<string>();
                foreach (var key in query.AllKeys)
                {
                    if (key == null) continue;
                    var val = query[key];
                    if (val == null) continue;
                    items.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(val)}");
                }
                var queryString = string.Join("&", items);
                return url.Contains("?") ? $"{url}&{queryString}" : $"{url}?{queryString}";
            }
            catch
            {
                return url;
            }
        }

        // 复用基类中的 EnsureAsciiHeaderValue（基类为 private static），因此在此重复实现以保证头部安全
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
    }
}
