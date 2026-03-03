using System;
using System.Text.Json;
using Drx.Sdk.Network.Http.Configs;

namespace Drx.Sdk.Network.Http.Auth.OAuth.Providers
{
    /// <summary>
    /// Microsoft OAuth 2.0 提供商（Azure AD / Microsoft Identity）
    /// </summary>
    /// <remarks>
    /// 申请地址: https://portal.azure.com/#blade/Microsoft_AAD_RegisteredApps
    /// 文档: https://learn.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-auth-code-flow
    /// </remarks>
    public class MicrosoftOAuthProvider : OAuthProvider
    {
        /// <summary>
        /// 创建 Microsoft OAuth 提供商
        /// </summary>
        /// <param name="clientId">客户端 ID</param>
        /// <param name="clientSecret">客户端密钥</param>
        /// <param name="redirectUri">回调 URL</param>
        /// <param name="tenant">租户（默认 "common" 支持所有账户类型）</param>
        /// <param name="scopes">权限范围</param>
        public MicrosoftOAuthProvider(string clientId, string clientSecret, string redirectUri, string tenant = "common", string scopes = "openid email profile User.Read")
            : base(new OAuthProviderConfig(
                name: "microsoft",
                displayName: "Microsoft",
                clientId: clientId,
                clientSecret: clientSecret,
                authorizationEndpoint: $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize",
                tokenEndpoint: $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token",
                userInfoEndpoint: "https://graph.microsoft.com/v1.0/me",
                redirectUri: redirectUri,
                scopes: scopes,
                usePKCE: true))
        {
        }

        protected override OAuthUserInfo ParseUserInfo(string json)
        {
            var info = new OAuthUserInfo();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("id", out var id))
                info.Id = id.GetString() ?? "";

            if (root.TryGetProperty("displayName", out var name))
                info.Name = name.GetString();

            if (root.TryGetProperty("mail", out var mail) && mail.ValueKind != JsonValueKind.Null)
                info.Email = mail.GetString();
            else if (root.TryGetProperty("userPrincipalName", out var upn))
                info.Email = upn.GetString();

            if (root.TryGetProperty("givenName", out var gn) && gn.ValueKind != JsonValueKind.Null)
                info.Extra["given_name"] = gn.GetString() ?? "";

            if (root.TryGetProperty("surname", out var sn) && sn.ValueKind != JsonValueKind.Null)
                info.Extra["surname"] = sn.GetString() ?? "";

            if (root.TryGetProperty("jobTitle", out var jt) && jt.ValueKind != JsonValueKind.Null)
                info.Extra["job_title"] = jt.GetString() ?? "";

            if (root.TryGetProperty("officeLocation", out var ol) && ol.ValueKind != JsonValueKind.Null)
                info.Extra["office_location"] = ol.GetString() ?? "";

            return info;
        }
    }
}
