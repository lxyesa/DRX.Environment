using System;
using System.Text.Json;
using Drx.Sdk.Network.Http.Configs;

namespace Drx.Sdk.Network.Http.Auth.OAuth.Providers
{
    /// <summary>
    /// Discord OAuth 2.0 提供商
    /// </summary>
    /// <remarks>
    /// 申请地址: https://discord.com/developers/applications
    /// 文档: https://discord.com/developers/docs/topics/oauth2
    /// </remarks>
    public class DiscordOAuthProvider : OAuthProvider
    {
        public DiscordOAuthProvider(string clientId, string clientSecret, string redirectUri, string scopes = "identify email")
            : base(new OAuthProviderConfig(
                name: "discord",
                displayName: "Discord",
                clientId: clientId,
                clientSecret: clientSecret,
                authorizationEndpoint: "https://discord.com/api/oauth2/authorize",
                tokenEndpoint: "https://discord.com/api/oauth2/token",
                userInfoEndpoint: "https://discord.com/api/users/@me",
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
                info.Id = id.GetString() ?? "";

            if (root.TryGetProperty("username", out var username))
                info.Name = username.GetString();

            if (root.TryGetProperty("global_name", out var gn) && gn.ValueKind != JsonValueKind.Null)
                info.Extra["global_name"] = gn.GetString() ?? "";

            if (root.TryGetProperty("email", out var email) && email.ValueKind != JsonValueKind.Null)
                info.Email = email.GetString();

            if (root.TryGetProperty("avatar", out var avatar) && avatar.ValueKind != JsonValueKind.Null)
            {
                var avatarHash = avatar.GetString();
                info.AvatarUrl = $"https://cdn.discordapp.com/avatars/{info.Id}/{avatarHash}.png";
            }

            if (root.TryGetProperty("discriminator", out var disc))
                info.Extra["discriminator"] = disc.GetString() ?? "";

            if (root.TryGetProperty("verified", out var verified))
                info.Extra["verified"] = verified.ToString();

            if (root.TryGetProperty("locale", out var locale))
                info.Extra["locale"] = locale.GetString() ?? "";

            return info;
        }
    }
}
