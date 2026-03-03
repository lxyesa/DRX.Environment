using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.Auth.OAuth.Providers
{
    /// <summary>
    /// 微信开放平台 OAuth 2.0 提供商
    /// </summary>
    /// <remarks>
    /// 申请地址: https://open.weixin.qq.com/
    /// 文档: https://developers.weixin.qq.com/doc/oplatform/Website_App/WeChat_Login/Wechat_Login.html
    /// 注意: 微信 OAuth 使用非标准参数名（appid/secret 替代 client_id/client_secret）
    /// </remarks>
    public class WeChatOAuthProvider : OAuthProvider
    {
        public WeChatOAuthProvider(string appId, string appSecret, string redirectUri, string scopes = "snsapi_login")
            : base(new OAuthProviderConfig(
                name: "wechat",
                displayName: "微信",
                clientId: appId,
                clientSecret: appSecret,
                authorizationEndpoint: "https://open.weixin.qq.com/connect/qrconnect",
                tokenEndpoint: "https://api.weixin.qq.com/sns/oauth2/access_token",
                userInfoEndpoint: "https://api.weixin.qq.com/sns/userinfo",
                redirectUri: redirectUri,
                scopes: scopes))
        {
        }

        /// <summary>
        /// 微信授权 URL 使用 appid 参数（非标准 client_id）
        /// </summary>
        public override string BuildAuthorizationUrl(string state, string? codeChallenge = null)
        {
            var parameters = new List<string>
            {
                $"appid={Uri.EscapeDataString(Config.ClientId)}",
                $"redirect_uri={Uri.EscapeDataString(Config.RedirectUri)}",
                $"response_type=code",
                $"scope={Uri.EscapeDataString(Config.Scopes)}",
                $"state={Uri.EscapeDataString(state)}"
            };

            return $"{Config.AuthorizationEndpoint}?{string.Join("&", parameters)}#wechat_redirect";
        }

        /// <summary>
        /// 微信 Token 交换使用 appid/secret 参数
        /// </summary>
        public override async Task<OAuthToken> ExchangeCodeForTokenAsync(string code, string? codeVerifier = null, CancellationToken cancellationToken = default)
        {
            var url = $"{Config.TokenEndpoint}?appid={Uri.EscapeDataString(Config.ClientId)}" +
                      $"&secret={Uri.EscapeDataString(Config.ClientSecret)}" +
                      $"&code={Uri.EscapeDataString(code)}" +
                      $"&grant_type=authorization_code";

            try
            {
                var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var response = await httpClient.GetAsync(url, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"[OAuth:{Name}] Token 交换失败: {response.StatusCode} - {json}");
                    return new OAuthToken { RawResponse = json };
                }

                return ParseWeChatTokenResponse(json);
            }
            catch (Exception ex)
            {
                Logger.Error($"[OAuth:{Name}] Token 交换异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 微信获取用户信息需要 access_token 和 openid 参数
        /// </summary>
        public override async Task<OAuthUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            // 需要从 Token 响应中获取 openid，暂存在 Extra
            var url = $"{Config.UserInfoEndpoint}?access_token={Uri.EscapeDataString(accessToken)}&openid={Uri.EscapeDataString(_lastOpenId ?? "")}";

            try
            {
                var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var response = await httpClient.GetAsync(url, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"[OAuth:{Name}] 获取用户信息失败: {response.StatusCode} - {json}");
                    return new OAuthUserInfo { Provider = Name, RawJson = json };
                }

                var info = ParseUserInfo(json);
                info.Provider = Name;
                info.RawJson = json;
                return info;
            }
            catch (Exception ex)
            {
                Logger.Error($"[OAuth:{Name}] 获取用户信息异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 微信 Token 最后获取的 openid（用于获取用户信息）
        /// </summary>
        private string? _lastOpenId;

        /// <summary>
        /// 解析微信特殊的 Token 响应
        /// </summary>
        private OAuthToken ParseWeChatTokenResponse(string json)
        {
            var token = new OAuthToken { RawResponse = json, ObtainedAt = DateTime.UtcNow };

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // 检查错误
                if (root.TryGetProperty("errcode", out var errcode) && errcode.GetInt32() != 0)
                {
                    Logger.Error($"[OAuth:{Name}] 微信返回错误: {json}");
                    return token;
                }

                if (root.TryGetProperty("access_token", out var at))
                    token.AccessToken = at.GetString() ?? "";

                if (root.TryGetProperty("refresh_token", out var rt))
                    token.RefreshToken = rt.GetString();

                if (root.TryGetProperty("expires_in", out var ei))
                    token.ExpiresIn = ei.GetInt32();

                if (root.TryGetProperty("scope", out var sc))
                    token.Scope = sc.GetString();

                if (root.TryGetProperty("openid", out var openid))
                    _lastOpenId = openid.GetString();

                if (root.TryGetProperty("unionid", out var unionid))
                    token.Scope = (token.Scope ?? "") + $" unionid:{unionid.GetString()}";
            }
            catch (Exception ex)
            {
                Logger.Error($"[OAuth:{Name}] Token 响应解析失败: {ex.Message}");
            }

            return token;
        }

        protected override OAuthUserInfo ParseUserInfo(string json)
        {
            var info = new OAuthUserInfo();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("openid", out var openid))
                info.Id = openid.GetString() ?? "";

            if (root.TryGetProperty("nickname", out var nickname))
                info.Name = nickname.GetString();

            if (root.TryGetProperty("headimgurl", out var avatar))
                info.AvatarUrl = avatar.GetString();

            if (root.TryGetProperty("sex", out var sex))
                info.Extra["sex"] = sex.GetInt32() == 1 ? "男" : (sex.GetInt32() == 2 ? "女" : "未知");

            if (root.TryGetProperty("province", out var province))
                info.Extra["province"] = province.GetString() ?? "";

            if (root.TryGetProperty("city", out var city))
                info.Extra["city"] = city.GetString() ?? "";

            if (root.TryGetProperty("country", out var country))
                info.Extra["country"] = country.GetString() ?? "";

            if (root.TryGetProperty("unionid", out var unionid))
                info.Extra["unionid"] = unionid.GetString() ?? "";

            return info;
        }
    }
}
