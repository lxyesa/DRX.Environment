using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.Auth.OAuth.Providers
{
    /// <summary>
    /// Twitter (X) OAuth 2.0 提供商（使用 PKCE）
    /// </summary>
    /// <remarks>
    /// 申请地址: https://developer.twitter.com/en/portal/dashboard
    /// 文档: https://developer.twitter.com/en/docs/authentication/oauth-2-0/authorization-code
    /// 注意: Twitter OAuth 2.0 强制使用 PKCE，Token 端点使用 Basic Auth
    /// </remarks>
    public class TwitterOAuthProvider : OAuthProvider
    {
        public TwitterOAuthProvider(string clientId, string clientSecret, string redirectUri, string scopes = "tweet.read users.read offline.access")
            : base(new OAuthProviderConfig(
                name: "twitter",
                displayName: "Twitter (X)",
                clientId: clientId,
                clientSecret: clientSecret,
                authorizationEndpoint: "https://twitter.com/i/oauth2/authorize",
                tokenEndpoint: "https://api.twitter.com/2/oauth2/token",
                userInfoEndpoint: "https://api.twitter.com/2/users/me?user.fields=profile_image_url,description,location,verified",
                redirectUri: redirectUri,
                scopes: scopes,
                usePKCE: true)) // Twitter OAuth 2.0 强制 PKCE
        {
        }

        /// <summary>
        /// Twitter Token 端点使用 Basic Auth（clientId:clientSecret）
        /// </summary>
        public override async Task<OAuthToken> ExchangeCodeForTokenAsync(string code, string? codeVerifier = null, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, string>
            {
                ["code"] = code,
                ["redirect_uri"] = Config.RedirectUri,
                ["grant_type"] = "authorization_code"
            };

            if (!string.IsNullOrEmpty(codeVerifier))
            {
                parameters["code_verifier"] = codeVerifier;
            }

            var content = new FormUrlEncodedContent(parameters);
            var request = new HttpRequestMessage(HttpMethod.Post, Config.TokenEndpoint)
            {
                Content = content
            };

            // Twitter 使用 Basic Auth
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Config.ClientId}:{Config.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var response = await httpClient.SendAsync(request, cancellationToken);
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

        protected override OAuthUserInfo ParseUserInfo(string json)
        {
            var info = new OAuthUserInfo();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Twitter v2 API 将用户数据放在 "data" 字段中
            var data = root.TryGetProperty("data", out var d) ? d : root;

            if (data.TryGetProperty("id", out var id))
                info.Id = id.GetString() ?? "";

            if (data.TryGetProperty("name", out var name))
                info.Name = name.GetString();

            if (data.TryGetProperty("username", out var username))
                info.Extra["username"] = username.GetString() ?? "";

            if (data.TryGetProperty("profile_image_url", out var avatar))
                info.AvatarUrl = avatar.GetString();

            if (data.TryGetProperty("description", out var desc) && desc.ValueKind != JsonValueKind.Null)
                info.Extra["description"] = desc.GetString() ?? "";

            if (data.TryGetProperty("location", out var location) && location.ValueKind != JsonValueKind.Null)
                info.Extra["location"] = location.GetString() ?? "";

            if (data.TryGetProperty("verified", out var verified))
                info.Extra["verified"] = verified.ToString();

            return info;
        }
    }
}
