using System;
using System.Text.Json;
using Drx.Sdk.Network.Http.Configs;

namespace Drx.Sdk.Network.Http.Auth.OAuth.Providers
{
    /// <summary>
    /// Google OAuth 2.0 提供商（OpenID Connect）
    /// </summary>
    /// <remarks>
    /// 申请地址: https://console.developers.google.com/
    /// 文档: https://developers.google.com/identity/protocols/oauth2
    /// </remarks>
    public class GoogleOAuthProvider : OAuthProvider
    {
        public GoogleOAuthProvider(string clientId, string clientSecret, string redirectUri, string scopes = "openid email profile")
            : base(new OAuthProviderConfig(
                name: "google",
                displayName: "Google",
                clientId: clientId,
                clientSecret: clientSecret,
                authorizationEndpoint: "https://accounts.google.com/o/oauth2/v2/auth",
                tokenEndpoint: "https://oauth2.googleapis.com/token",
                userInfoEndpoint: "https://www.googleapis.com/oauth2/v3/userinfo",
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

            if (root.TryGetProperty("sub", out var sub))
                info.Id = sub.GetString() ?? "";

            if (root.TryGetProperty("name", out var name))
                info.Name = name.GetString();

            if (root.TryGetProperty("email", out var email))
                info.Email = email.GetString();

            if (root.TryGetProperty("picture", out var picture))
                info.AvatarUrl = picture.GetString();

            if (root.TryGetProperty("email_verified", out var ev))
                info.Extra["email_verified"] = ev.ToString();

            if (root.TryGetProperty("locale", out var locale))
                info.Extra["locale"] = locale.GetString() ?? "";

            if (root.TryGetProperty("given_name", out var gn))
                info.Extra["given_name"] = gn.GetString() ?? "";

            if (root.TryGetProperty("family_name", out var fn))
                info.Extra["family_name"] = fn.GetString() ?? "";

            return info;
        }
    }
}
