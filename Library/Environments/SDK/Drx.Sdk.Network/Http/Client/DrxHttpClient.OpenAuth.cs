using System;
using System.Text.Json;
using Drx.Sdk.Network.Http.Configs;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpClient OpenAuth 快速封装：构建授权 URL 与交换 code。
    /// </summary>
    public partial class DrxHttpClient
    {
        private static string ResolveApiUrl(string serverBaseUrl, string? apiUrl, string defaultPath)
        {
            if (string.IsNullOrWhiteSpace(serverBaseUrl))
                throw new ArgumentException("serverBaseUrl 不能为空", nameof(serverBaseUrl));

            if (!string.IsNullOrWhiteSpace(apiUrl))
            {
                if (Uri.TryCreate(apiUrl, UriKind.Absolute, out var absoluteUri))
                    return absoluteUri.ToString();

                var baseUriFromApi = new Uri(serverBaseUrl.TrimEnd('/') + "/");
                var relativeApiPath = apiUrl.StartsWith("/") ? apiUrl[1..] : apiUrl;
                return new Uri(baseUriFromApi, relativeApiPath).ToString();
            }

            var baseUri = new Uri(serverBaseUrl.TrimEnd('/') + "/");
            var path = defaultPath.StartsWith("/") ? defaultPath[1..] : defaultPath;
            return new Uri(baseUri, path).ToString();
        }

        /// <summary>
        /// 生成随机 state（建议每次授权请求使用新的 state）。
        /// </summary>
        public static string CreateOpenAuthState()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 快速构建 OpenAuth 授权页 URL。
        /// </summary>
        /// <param name="serverBaseUrl">授权服务器基地址，例如 https://auth.example.com</param>
        /// <param name="clientId">客户端标识</param>
        /// <param name="redirectUri">回调地址</param>
        /// <param name="state">防 CSRF 随机值</param>
        /// <param name="scope">授权范围</param>
        /// <param name="appName">展示用应用名</param>
        /// <param name="appDescription">展示用应用描述</param>
        /// <param name="authorizePath">授权路径，默认 /oauth/authorize</param>
        /// <returns>完整可跳转 URL</returns>
        public static string BuildOpenAuthAuthorizeUrl(
            string serverBaseUrl,
            string clientId,
            string redirectUri,
            string state,
            string scope = "profile",
            string? appName = null,
            string? appDescription = null,
            string authorizePath = "/oauth/authorize")
        {
            if (string.IsNullOrWhiteSpace(serverBaseUrl)) throw new ArgumentException("serverBaseUrl 不能为空", nameof(serverBaseUrl));
            if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("clientId 不能为空", nameof(clientId));
            if (string.IsNullOrWhiteSpace(redirectUri)) throw new ArgumentException("redirectUri 不能为空", nameof(redirectUri));

            var baseUri = new Uri(serverBaseUrl.TrimEnd('/') + "/");
            var path = authorizePath.StartsWith("/") ? authorizePath[1..] : authorizePath;
            var uri = new Uri(baseUri, path);

            var query = $"client_id={Uri.EscapeDataString(clientId)}" +
                        $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                        $"&state={Uri.EscapeDataString(state ?? string.Empty)}" +
                        $"&scope={Uri.EscapeDataString(scope ?? string.Empty)}";

            if (!string.IsNullOrWhiteSpace(appName))
                query += $"&app_name={Uri.EscapeDataString(appName)}";

            if (!string.IsNullOrWhiteSpace(appDescription))
                query += $"&app_desc={Uri.EscapeDataString(appDescription)}";

            var sep = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
            return uri + sep + query;
        }

        /// <summary>
        /// 快速使用授权码交换 OpenAuth Token。
        /// </summary>
        /// <param name="serverBaseUrl">授权服务器基地址，例如 https://auth.example.com</param>
        /// <param name="code">回调返回的授权码</param>
        /// <param name="clientId">客户端标识</param>
        /// <param name="redirectUri">授权时使用的回调地址（用于服务端校验）</param>
        /// <param name="clientSecret">客户端密钥（如果是机密客户端）</param>
        /// <param name="tokenApiUrl">Token API 地址。可传完整 URL；若为空则使用 serverBaseUrl + /api/oauth/token</param>
        /// <returns>OAuthToken（失败时 RawResponse 可查看服务端错误详情）</returns>
        public async Task<OAuthToken> ExchangeOpenAuthCodeAsync(
            string serverBaseUrl,
            string code,
            string clientId,
            string redirectUri,
            string? clientSecret = null,
            string? tokenApiUrl = null)
        {
            if (string.IsNullOrWhiteSpace(serverBaseUrl)) throw new ArgumentException("serverBaseUrl 不能为空", nameof(serverBaseUrl));
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("code 不能为空", nameof(code));
            if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("clientId 不能为空", nameof(clientId));
            if (string.IsNullOrWhiteSpace(redirectUri)) throw new ArgumentException("redirectUri 不能为空", nameof(redirectUri));

            var tokenUrl = ResolveApiUrl(serverBaseUrl, tokenApiUrl, "/api/oauth/token");

            var payload = new
            {
                grant_type = "authorization_code",
                code = code,
                client_id = clientId,
                client_secret = clientSecret,
                redirect_uri = redirectUri
            };

            var response = await PostAsync(tokenUrl, payload).ConfigureAwait(false);
            var raw = response.Body ?? string.Empty;

            var token = new OAuthToken
            {
                RawResponse = raw,
                ObtainedAt = DateTime.UtcNow
            };

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                token.AccessToken = root.TryGetProperty("access_token", out var at) ? (at.GetString() ?? string.Empty) : string.Empty;
                token.TokenType = root.TryGetProperty("token_type", out var tt) ? (tt.GetString() ?? "Bearer") : "Bearer";
                token.RefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
                token.Scope = root.TryGetProperty("scope", out var sc) ? sc.GetString() : null;
                token.IdToken = root.TryGetProperty("id_token", out var idt) ? idt.GetString() : null;
                token.ExpiresIn = root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var sec) ? sec : -1;
            }
            catch
            {
                // 忽略解析异常，调用方可使用 RawResponse 进行错误处理
            }

            return token;
        }

        /// <summary>
        /// 快速获取授权登录 URL（简化模式）。
        /// <para>典型用法：<c>var url = await client.AuthLogin("https://auth.example.com", "my-app-id");</c></para>
        /// 服务端将根据已注册的 Auth App 自动补全 redirect_uri / app_name 等参数。
        /// </summary>
        /// <param name="serverBaseUrl">认证服务器地址</param>
        /// <param name="appId">已注册的 AppId（client_id）</param>
        /// <param name="scope">可选 scope，默认 profile</param>
        /// <returns>可直接跳转的授权 URL</returns>
        public Task<string> AuthLogin(string serverBaseUrl, string appId, string scope = "profile")
        {
            return AuthLoginAsync(serverBaseUrl, appId, scope);
        }

        /// <summary>
        /// 快速获取授权登录 URL（异步）。
        /// </summary>
        public async Task<string> AuthLoginAsync(
            string serverBaseUrl,
            string appId,
            string scope = "profile",
            string? quickLoginApiUrl = null)
        {
            if (string.IsNullOrWhiteSpace(serverBaseUrl))
                throw new ArgumentException("serverBaseUrl 不能为空", nameof(serverBaseUrl));
            if (string.IsNullOrWhiteSpace(appId))
                throw new ArgumentException("appId 不能为空", nameof(appId));

            var endpoint = ResolveApiUrl(serverBaseUrl, quickLoginApiUrl, "/api/oauth/apps/quick-login-url");

            endpoint += endpoint.Contains("?") ? "&" : "?";
            endpoint += $"clientId={Uri.EscapeDataString(appId)}&scope={Uri.EscapeDataString(scope ?? "profile")}";

            var response = await GetAsync(endpoint).ConfigureAwait(false);
            var raw = response.Body ?? string.Empty;

            if (response.StatusCode != 200)
            {
                try
                {
                    using var errDoc = JsonDocument.Parse(raw);
                    if (errDoc.RootElement.TryGetProperty("message", out var msgEl))
                        throw new InvalidOperationException(msgEl.GetString() ?? $"AuthLogin 失败: {response.StatusCode}");
                }
                catch (JsonException)
                {
                    // ignore parse error and fallback below
                }

                throw new InvalidOperationException($"AuthLogin 失败: HTTP {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (!root.TryGetProperty("authorizeUrl", out var urlEl))
                throw new InvalidOperationException("AuthLogin 响应缺少 authorizeUrl 字段");

            var authorizeUrl = urlEl.GetString();
            if (string.IsNullOrWhiteSpace(authorizeUrl))
                throw new InvalidOperationException("AuthLogin 返回的 authorizeUrl 为空");

            return authorizeUrl;
        }
    }
}
