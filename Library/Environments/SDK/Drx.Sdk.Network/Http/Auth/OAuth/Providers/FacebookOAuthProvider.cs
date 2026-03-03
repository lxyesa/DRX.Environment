using System;
using System.Text.Json;
using Drx.Sdk.Network.Http.Configs;

namespace Drx.Sdk.Network.Http.Auth.OAuth.Providers
{
    /// <summary>
    /// Facebook OAuth 2.0 提供商
    /// </summary>
    /// <remarks>
    /// 申请地址: https://developers.facebook.com/apps
    /// 文档: https://developers.facebook.com/docs/facebook-login/
    /// </remarks>
    public class FacebookOAuthProvider : OAuthProvider
    {
        public FacebookOAuthProvider(string clientId, string clientSecret, string redirectUri, string scopes = "public_profile email")
            : base(new OAuthProviderConfig(
                name: "facebook",
                displayName: "Facebook",
                clientId: clientId,
                clientSecret: clientSecret,
                authorizationEndpoint: "https://www.facebook.com/v19.0/dialog/oauth",
                tokenEndpoint: "https://graph.facebook.com/v19.0/oauth/access_token",
                userInfoEndpoint: "https://graph.facebook.com/v19.0/me?fields=id,name,email,picture.type(large)",
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

            if (root.TryGetProperty("name", out var name))
                info.Name = name.GetString();

            if (root.TryGetProperty("email", out var email))
                info.Email = email.GetString();

            // Facebook 头像结构: picture.data.url
            if (root.TryGetProperty("picture", out var picture)
                && picture.TryGetProperty("data", out var data)
                && data.TryGetProperty("url", out var url))
            {
                info.AvatarUrl = url.GetString();
            }

            return info;
        }
    }
}
