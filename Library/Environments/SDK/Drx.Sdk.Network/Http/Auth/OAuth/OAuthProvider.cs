using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.Auth.OAuth
{
    /// <summary>
    /// OAuth 2.0 提供商抽象基类。
    /// 子类只需覆写配置和用户信息解析即可快速接入新平台。
    /// </summary>
    public abstract class OAuthProvider : IDisposable
    {
        /// <summary>
        /// 内部复用的 HttpClient（高性能，避免 Socket 耗尽）
        /// </summary>
        private static readonly HttpClient _sharedHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        /// <summary>
        /// 提供商配置
        /// </summary>
        public OAuthProviderConfig Config { get; }

        /// <summary>
        /// 提供商唯一标识名（小写）
        /// </summary>
        public string Name => Config.Name;

        /// <summary>
        /// 提供商显示名称
        /// </summary>
        public string DisplayName => Config.DisplayName;

        protected OAuthProvider(OAuthProviderConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// 构建授权 URL（第一步：将用户重定向到此 URL）
        /// </summary>
        /// <param name="state">CSRF 防护随机值</param>
        /// <param name="codeChallenge">PKCE Code Challenge（可选）</param>
        /// <returns>完整的授权 URL</returns>
        public virtual string BuildAuthorizationUrl(string state, string? codeChallenge = null)
        {
            var parameters = new List<string>
            {
                $"client_id={Uri.EscapeDataString(Config.ClientId)}",
                $"redirect_uri={Uri.EscapeDataString(Config.RedirectUri)}",
                $"response_type=code",
                $"state={Uri.EscapeDataString(state)}"
            };

            if (!string.IsNullOrEmpty(Config.Scopes))
            {
                parameters.Add($"scope={Uri.EscapeDataString(Config.Scopes)}");
            }

            if (Config.UsePKCE && !string.IsNullOrEmpty(codeChallenge))
            {
                parameters.Add($"code_challenge={Uri.EscapeDataString(codeChallenge)}");
                parameters.Add("code_challenge_method=S256");
            }

            foreach (var extra in Config.ExtraParameters)
            {
                parameters.Add($"{Uri.EscapeDataString(extra.Key)}={Uri.EscapeDataString(extra.Value)}");
            }

            var separator = Config.AuthorizationEndpoint.Contains('?') ? "&" : "?";
            return $"{Config.AuthorizationEndpoint}{separator}{string.Join("&", parameters)}";
        }

        /// <summary>
        /// 用授权码交换 Access Token（第二步）
        /// </summary>
        /// <param name="code">授权回调返回的 code</param>
        /// <param name="codeVerifier">PKCE Code Verifier（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>OAuth Token</returns>
        public virtual async Task<OAuthToken> ExchangeCodeForTokenAsync(string code, string? codeVerifier = null, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, string>
            {
                ["client_id"] = Config.ClientId,
                ["client_secret"] = Config.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = Config.RedirectUri,
                ["grant_type"] = "authorization_code"
            };

            if (Config.UsePKCE && !string.IsNullOrEmpty(codeVerifier))
            {
                parameters["code_verifier"] = codeVerifier;
            }

            var content = new FormUrlEncodedContent(parameters);
            var request = new HttpRequestMessage(HttpMethod.Post, Config.TokenEndpoint)
            {
                Content = content
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var response = await _sharedHttpClient.SendAsync(request, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"[OAuth:{Name}] Token 交换失败: {response.StatusCode} - {json}");
                    return new OAuthToken { RawResponse = json };
                }

                return ParseTokenResponse(json);
            }
            catch (Exception ex)
            {
                Logger.Error($"[OAuth:{Name}] Token 交换异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 使用 Refresh Token 刷新 Access Token
        /// </summary>
        /// <param name="refreshToken">刷新令牌</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>新的 OAuth Token</returns>
        public virtual async Task<OAuthToken> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, string>
            {
                ["client_id"] = Config.ClientId,
                ["client_secret"] = Config.ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            };

            var content = new FormUrlEncodedContent(parameters);
            var request = new HttpRequestMessage(HttpMethod.Post, Config.TokenEndpoint)
            {
                Content = content
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var response = await _sharedHttpClient.SendAsync(request, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"[OAuth:{Name}] Token 刷新失败: {response.StatusCode} - {json}");
                    return new OAuthToken { RawResponse = json };
                }

                return ParseTokenResponse(json);
            }
            catch (Exception ex)
            {
                Logger.Error($"[OAuth:{Name}] Token 刷新异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取用户信息（第三步）
        /// </summary>
        /// <param name="accessToken">Access Token</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>用户信息</returns>
        public virtual async Task<OAuthUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, Config.UserInfoEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var response = await _sharedHttpClient.SendAsync(request, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"[OAuth:{Name}] 获取用户信息失败: {response.StatusCode} - {json}");
                    return new OAuthUserInfo { Provider = Name, RawJson = json };
                }

                var userInfo = ParseUserInfo(json);
                userInfo.Provider = Name;
                userInfo.RawJson = json;
                return userInfo;
            }
            catch (Exception ex)
            {
                Logger.Error($"[OAuth:{Name}] 获取用户信息异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 完整认证流程：Code → Token → UserInfo（一步到位）
        /// </summary>
        /// <param name="code">授权回调返回的 code</param>
        /// <param name="codeVerifier">PKCE Code Verifier（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>完整的认证结果</returns>
        public virtual async Task<OAuthResult> AuthenticateAsync(string code, string? codeVerifier = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var token = await ExchangeCodeForTokenAsync(code, codeVerifier, cancellationToken);
                if (string.IsNullOrEmpty(token.AccessToken))
                {
                    return OAuthResult.Fail(Name, $"Token 交换失败: {token.RawResponse}");
                }

                var user = await GetUserInfoAsync(token.AccessToken, cancellationToken);
                if (string.IsNullOrEmpty(user.Id))
                {
                    return OAuthResult.Fail(Name, $"获取用户信息失败: {user.RawJson}");
                }

                Logger.Info($"[OAuth:{Name}] 认证成功: {user.Name ?? user.Id}");
                return OAuthResult.Ok(Name, token, user);
            }
            catch (Exception ex)
            {
                Logger.Error($"[OAuth:{Name}] 认证流程异常: {ex.Message}");
                return OAuthResult.Fail(Name, ex.Message);
            }
        }

        /// <summary>
        /// 解析 Token 响应 JSON（子类可覆写以处理非标准格式）
        /// </summary>
        protected virtual OAuthToken ParseTokenResponse(string json)
        {
            var token = new OAuthToken { RawResponse = json, ObtainedAt = DateTime.UtcNow };

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("access_token", out var at))
                    token.AccessToken = at.GetString() ?? "";

                if (root.TryGetProperty("refresh_token", out var rt))
                    token.RefreshToken = rt.GetString();

                if (root.TryGetProperty("token_type", out var tt))
                    token.TokenType = tt.GetString() ?? "Bearer";

                if (root.TryGetProperty("expires_in", out var ei))
                {
                    if (ei.ValueKind == JsonValueKind.Number)
                        token.ExpiresIn = ei.GetInt32();
                    else if (ei.ValueKind == JsonValueKind.String && int.TryParse(ei.GetString(), out var parsed))
                        token.ExpiresIn = parsed;
                }

                if (root.TryGetProperty("scope", out var sc))
                    token.Scope = sc.GetString();

                if (root.TryGetProperty("id_token", out var id))
                    token.IdToken = id.GetString();
            }
            catch (Exception ex)
            {
                Logger.Error($"[OAuth:{Name}] Token 响应解析失败: {ex.Message}");
            }

            return token;
        }

        /// <summary>
        /// 解析用户信息 JSON（子类必须实现，各平台格式不同）
        /// </summary>
        protected abstract OAuthUserInfo ParseUserInfo(string json);

        /// <summary>
        /// 释放资源（子类可覆写）
        /// </summary>
        public virtual void Dispose()
        {
            // 默认不释放 _sharedHttpClient（全局复用）
            GC.SuppressFinalize(this);
        }
    }
}
