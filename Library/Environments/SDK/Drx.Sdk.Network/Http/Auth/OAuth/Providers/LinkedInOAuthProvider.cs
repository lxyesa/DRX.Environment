using System;
using System.Text.Json;
using Drx.Sdk.Network.Http.Configs;

namespace Drx.Sdk.Network.Http.Auth.OAuth.Providers
{
    /// <summary>
    /// LinkedIn OAuth 2.0 提供商
    /// </summary>
    /// <remarks>
    /// 申请地址: https://www.linkedin.com/developers/apps
    /// 文档: https://learn.microsoft.com/en-us/linkedin/shared/authentication/authorization-code-flow
    /// </remarks>
    public class LinkedInOAuthProvider : OAuthProvider
    {
        public LinkedInOAuthProvider(string clientId, string clientSecret, string redirectUri, string scopes = "openid profile email")
            : base(new OAuthProviderConfig(
                name: "linkedin",
                displayName: "LinkedIn",
                clientId: clientId,
                clientSecret: clientSecret,
                authorizationEndpoint: "https://www.linkedin.com/oauth/v2/authorization",
                tokenEndpoint: "https://www.linkedin.com/oauth/v2/accessToken",
                userInfoEndpoint: "https://api.linkedin.com/v2/userinfo",
                redirectUri: redirectUri,
                scopes: scopes))
        {
        }

        protected override OAuthUserInfo ParseUserInfo(string json)
        {
            var info = new OAuthUserInfo();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("sub", out var sub))
                info.Id = sub.GetString() ?? "";

            if (root.TryGetProperty("name", out var name))
                info.Name = name.GetString();

            if (root.TryGetProperty("email", out var email))
                info.Email = email.GetString();

            if (root.TryGetProperty("picture", out var picture))
                info.AvatarUrl = picture.GetString();

            if (root.TryGetProperty("given_name", out var gn))
                info.Extra["given_name"] = gn.GetString() ?? "";

            if (root.TryGetProperty("family_name", out var fn))
                info.Extra["family_name"] = fn.GetString() ?? "";

            if (root.TryGetProperty("email_verified", out var ev))
                info.Extra["email_verified"] = ev.ToString();

            if (root.TryGetProperty("locale", out var locale))
            {
                if (locale.ValueKind == JsonValueKind.Object && locale.TryGetProperty("language", out var lang))
                    info.Extra["locale"] = lang.GetString() ?? "";
                else if (locale.ValueKind == JsonValueKind.String)
                    info.Extra["locale"] = locale.GetString() ?? "";
            }

            return info;
        }
    }
}
