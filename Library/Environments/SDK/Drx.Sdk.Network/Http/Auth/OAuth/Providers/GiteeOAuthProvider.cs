using System;
using System.Text.Json;
using Drx.Sdk.Network.Http.Configs;

namespace Drx.Sdk.Network.Http.Auth.OAuth.Providers
{
    /// <summary>
    /// Gitee OAuth 2.0 提供商（码云）
    /// </summary>
    /// <remarks>
    /// 申请地址: https://gitee.com/oauth/applications
    /// 文档: https://gitee.com/api/v5/oauth_doc
    /// </remarks>
    public class GiteeOAuthProvider : OAuthProvider
    {
        public GiteeOAuthProvider(string clientId, string clientSecret, string redirectUri, string scopes = "user_info emails")
            : base(new OAuthProviderConfig(
                name: "gitee",
                displayName: "Gitee（码云）",
                clientId: clientId,
                clientSecret: clientSecret,
                authorizationEndpoint: "https://gitee.com/oauth/authorize",
                tokenEndpoint: "https://gitee.com/oauth/token",
                userInfoEndpoint: "https://gitee.com/api/v5/user",
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

            if (root.TryGetProperty("html_url", out var url))
                info.Extra["profile_url"] = url.GetString() ?? "";

            if (root.TryGetProperty("name", out var name) && name.ValueKind != JsonValueKind.Null)
                info.Extra["display_name"] = name.GetString() ?? "";

            if (root.TryGetProperty("bio", out var bio) && bio.ValueKind != JsonValueKind.Null)
                info.Extra["bio"] = bio.GetString() ?? "";

            if (root.TryGetProperty("blog", out var blog) && blog.ValueKind != JsonValueKind.Null)
                info.Extra["blog"] = blog.GetString() ?? "";

            return info;
        }
    }
}
