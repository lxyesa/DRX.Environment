using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpClient Cookie 管理部分：会话管理、Cookie 导入/导出
    /// </summary>
    public partial class DrxHttpClient
    {
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
                var newContainer = new System.Net.CookieContainer();
                try
                {
                }
                catch { }
                try
                {
                    foreach (var table in GetAllCookies(_cookieContainer))
                    {
                        newContainer.Add(table.Key, table.Value);
                    }
                }
                catch { }
                _cookieContainer = newContainer;
            }
            catch { }
        }

        /// <summary>
        /// Helper 返回所有 domain->CookieCollection 项（用于复制）
        /// </summary>
        private IEnumerable<KeyValuePair<Uri, CookieCollection>> GetAllCookies(CookieContainer container)
        {
            var list = new List<KeyValuePair<Uri, CookieCollection>>();
            try
            {
                if (_httpClient.BaseAddress != null)
                {
                    list.Add(new KeyValuePair<Uri, CookieCollection>(_httpClient.BaseAddress, container.GetCookies(_httpClient.BaseAddress)));
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
                return JsonSerializer.Serialize(list);
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
                var arr = JsonSerializer.Deserialize<JsonElement[]>(json);
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

        /// <summary>
        /// 将 header 值保证为 ASCII，不可 ASCII 的字节会以百分号转义形式编码。
        /// </summary>
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
