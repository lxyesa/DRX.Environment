using System;
using System.Text.Json;
using Drx.Sdk.Network.Http.Configs;

namespace Drx.Sdk.Network.Http.Auth.OAuth.Providers
{
    /// <summary>
    /// GitHub OAuth 2.0 提供商
    /// </summary>
    /// <remarks>
    /// 申请地址: https://github.com/settings/developers
    /// 文档: https://docs.github.com/en/apps/oauth-apps
    /// </remarks>
    public class GitHubOAuthProvider : OAuthProvider
    {
        public GitHubOAuthProvider(string clientId, string clientSecret, string redirectUri, string scopes = "read:user user:email")
            : base(new OAuthProviderConfig(
                name: "github",
                displayName: "GitHub",
                clientId: clientId,
                clientSecret: clientSecret,
                authorizationEndpoint: "https://github.com/login/oauth/authorize",
                tokenEndpoint: "https://github.com/login/oauth/access_token",
                userInfoEndpoint: "https://api.github.com/user",
                redirectUri: redirectUri,
                scopes: scopes))
        {
        }

        protected override OAuthUserInfo ParseUserInfo(string json)
        {
            var info = new OAuthUserInfo();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("id", out var id))
                info.Id = id.ToString();

            if (root.TryGetProperty("login", out var login))
                info.Name = login.GetString();

            if (root.TryGetProperty("email", out var email) && email.ValueKind != JsonValueKind.Null)
                info.Email = email.GetString();

            if (root.TryGetProperty("avatar_url", out var avatar))
                info.AvatarUrl = avatar.GetString();

            if (root.TryGetProperty("bio", out var bio) && bio.ValueKind != JsonValueKind.Null)
                info.Extra["bio"] = bio.GetString() ?? "";

            if (root.TryGetProperty("html_url", out var url))
                info.Extra["profile_url"] = url.GetString() ?? "";

            if (root.TryGetProperty("company", out var company) && company.ValueKind != JsonValueKind.Null)
                info.Extra["company"] = company.GetString() ?? "";

            if (root.TryGetProperty("location", out var location) && location.ValueKind != JsonValueKind.Null)
                info.Extra["location"] = location.GetString() ?? "";

            return info;
        }
    }
}
