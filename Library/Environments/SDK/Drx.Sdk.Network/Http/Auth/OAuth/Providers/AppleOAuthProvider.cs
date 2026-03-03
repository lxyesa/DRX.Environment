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
    /// Apple Sign In OAuth 2.0 提供商
    /// </summary>
    /// <remarks>
    /// 申请地址: https://developer.apple.com/account/resources/identifiers/list/serviceId
    /// 文档: https://developer.apple.com/documentation/sign_in_with_apple
    /// 注意: Apple 的用户信息在首次授权时通过 id_token 返回，后续不再返回
    /// </remarks>
    public class AppleOAuthProvider : OAuthProvider
    {
        public AppleOAuthProvider(string clientId, string clientSecret, string redirectUri, string scopes = "name email")
            : base(new OAuthProviderConfig(
                name: "apple",
                displayName: "Apple",
                clientId: clientId,
                clientSecret: clientSecret,
                authorizationEndpoint: "https://appleid.apple.com/auth/authorize",
                tokenEndpoint: "https://appleid.apple.com/auth/token",
                userInfoEndpoint: "", // Apple 不提供标准用户信息端点
                redirectUri: redirectUri,
                scopes: scopes,
                usePKCE: true))
        {
            // Apple 需要 response_mode=form_post
            Config.ExtraParameters["response_mode"] = "form_post";
        }

        /// <summary>
        /// Apple 的用户信息从 id_token 中解析（不走标准 API 端点）
        /// </summary>
        public override async Task<OAuthUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            // Apple 在首次登录时通过 id_token 提供用户信息
            // 这里解析 id_token（JWT 格式）
            return await Task.FromResult(new OAuthUserInfo
            {
                Provider = Name,
                Id = "从 id_token 解析",
                Extra = { ["note"] = "Apple 用户信息需从 OAuthToken.IdToken 中解析 JWT payload" }
            });
        }

        /// <summary>
        /// 覆写认证流程：从 id_token 解析用户信息
        /// </summary>
        public override async Task<OAuthResult> AuthenticateAsync(string code, string? codeVerifier = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var token = await ExchangeCodeForTokenAsync(code, codeVerifier, cancellationToken);
                if (string.IsNullOrEmpty(token.AccessToken))
                {
                    return OAuthResult.Fail(Name, $"Token 交换失败: {token.RawResponse}");
                }

                // 从 id_token 解析用户信息
                var user = ParseUserInfoFromIdToken(token.IdToken);
                user.Provider = Name;

                Logger.Info($"[OAuth:{Name}] 认证成功: {user.Id}");
                return OAuthResult.Ok(Name, token, user);
            }
            catch (Exception ex)
            {
                Logger.Error($"[OAuth:{Name}] 认证流程异常: {ex.Message}");
                return OAuthResult.Fail(Name, ex.Message);
            }
        }

        /// <summary>
        /// 从 Apple id_token (JWT) 中解析用户信息
        /// </summary>
        private OAuthUserInfo ParseUserInfoFromIdToken(string? idToken)
        {
            var info = new OAuthUserInfo();

            if (string.IsNullOrEmpty(idToken))
            {
                return info;
            }

            try
            {
                // JWT 由三部分组成: header.payload.signature
                var parts = idToken.Split('.');
                if (parts.Length >= 2)
                {
                    var payload = parts[1];
                    // 补齐 Base64 padding
                    payload = payload.Replace('-', '+').Replace('_', '/');
                    switch (payload.Length % 4)
                    {
                        case 2: payload += "=="; break;
                        case 3: payload += "="; break;
                    }

                    var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("sub", out var sub))
                        info.Id = sub.GetString() ?? "";

                    if (root.TryGetProperty("email", out var email))
                        info.Email = email.GetString();

                    if (root.TryGetProperty("email_verified", out var ev))
                        info.Extra["email_verified"] = ev.ToString();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[OAuth:{Name}] id_token 解析失败: {ex.Message}");
            }

            return info;
        }

        protected override OAuthUserInfo ParseUserInfo(string json)
        {
            // Apple 不走标准 UserInfo 端点，此方法通过 AuthenticateAsync 覆写绕过
            return new OAuthUserInfo { Provider = Name };
        }
    }
}
