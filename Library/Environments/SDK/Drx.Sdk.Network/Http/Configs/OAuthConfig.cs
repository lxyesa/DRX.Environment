using System;
using System.Collections.Generic;

namespace Drx.Sdk.Network.Http.Configs
{
    /// <summary>
    /// OAuth 提供商配置
    /// </summary>
    public class OAuthProviderConfig
    {
        /// <summary>
        /// 提供商唯一标识名（小写，如 "github", "google"）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 提供商显示名称
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 客户端 ID（从第三方平台获取）
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// 客户端密钥（从第三方平台获取）
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// 授权端点 URL
        /// </summary>
        public string AuthorizationEndpoint { get; set; }

        /// <summary>
        /// Token 交换端点 URL
        /// </summary>
        public string TokenEndpoint { get; set; }

        /// <summary>
        /// 用户信息端点 URL
        /// </summary>
        public string UserInfoEndpoint { get; set; }

        /// <summary>
        /// 回调重定向 URL
        /// </summary>
        public string RedirectUri { get; set; }

        /// <summary>
        /// 请求的权限范围（空格分隔）
        /// </summary>
        public string Scopes { get; set; }

        /// <summary>
        /// 是否启用 PKCE（推荐公开客户端启用）
        /// </summary>
        public bool UsePKCE { get; set; }

        /// <summary>
        /// 自定义参数（部分提供商需要额外参数）
        /// </summary>
        public Dictionary<string, string> ExtraParameters { get; set; } = new();

        public OAuthProviderConfig(
            string name,
            string displayName,
            string clientId,
            string clientSecret,
            string authorizationEndpoint,
            string tokenEndpoint,
            string userInfoEndpoint,
            string redirectUri,
            string scopes = "",
            bool usePKCE = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DisplayName = displayName ?? name;
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            ClientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
            AuthorizationEndpoint = authorizationEndpoint ?? throw new ArgumentNullException(nameof(authorizationEndpoint));
            TokenEndpoint = tokenEndpoint ?? throw new ArgumentNullException(nameof(tokenEndpoint));
            UserInfoEndpoint = userInfoEndpoint ?? throw new ArgumentNullException(nameof(userInfoEndpoint));
            RedirectUri = redirectUri ?? throw new ArgumentNullException(nameof(redirectUri));
            Scopes = scopes ?? "";
            UsePKCE = usePKCE;
        }
    }

    /// <summary>
    /// OAuth Token 响应数据
    /// </summary>
    public class OAuthToken
    {
        /// <summary>
        /// 访问令牌
        /// </summary>
        public string AccessToken { get; set; } = "";

        /// <summary>
        /// 刷新令牌（部分提供商返回）
        /// </summary>
        public string? RefreshToken { get; set; }

        /// <summary>
        /// 令牌类型（通常为 "Bearer"）
        /// </summary>
        public string TokenType { get; set; } = "Bearer";

        /// <summary>
        /// 过期时间（秒），-1 表示未知
        /// </summary>
        public int ExpiresIn { get; set; } = -1;

        /// <summary>
        /// 授权的权限范围
        /// </summary>
        public string? Scope { get; set; }

        /// <summary>
        /// ID Token（OpenID Connect 提供商返回）
        /// </summary>
        public string? IdToken { get; set; }

        /// <summary>
        /// Token 获取时间（UTC）
        /// </summary>
        public DateTime ObtainedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 原始响应 JSON
        /// </summary>
        public string? RawResponse { get; set; }

        /// <summary>
        /// 令牌是否已过期
        /// </summary>
        public bool IsExpired => ExpiresIn > 0 && (DateTime.UtcNow - ObtainedAt).TotalSeconds > ExpiresIn;
    }

    /// <summary>
    /// OAuth 用户信息
    /// </summary>
    public class OAuthUserInfo
    {
        /// <summary>
        /// 用户在第三方平台的唯一 ID
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// 用户名
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 邮箱
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// 头像 URL
        /// </summary>
        public string? AvatarUrl { get; set; }

        /// <summary>
        /// 提供商标识名
        /// </summary>
        public string Provider { get; set; } = "";

        /// <summary>
        /// 原始用户信息 JSON
        /// </summary>
        public string? RawJson { get; set; }

        /// <summary>
        /// 扩展字段（不同提供商的特有信息）
        /// </summary>
        public Dictionary<string, string> Extra { get; set; } = new();
    }

    /// <summary>
    /// OAuth 认证结果（Token + 用户信息的聚合）
    /// </summary>
    public class OAuthResult
    {
        /// <summary>
        /// 是否认证成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误信息（认证失败时）
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// OAuth 令牌
        /// </summary>
        public OAuthToken? Token { get; set; }

        /// <summary>
        /// 用户信息
        /// </summary>
        public OAuthUserInfo? User { get; set; }

        /// <summary>
        /// 提供商标识名
        /// </summary>
        public string Provider { get; set; } = "";

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static OAuthResult Ok(string provider, OAuthToken token, OAuthUserInfo user) => new()
        {
            Success = true,
            Provider = provider,
            Token = token,
            User = user
        };

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static OAuthResult Fail(string provider, string error) => new()
        {
            Success = false,
            Provider = provider,
            Error = error
        };
    }

    /// <summary>
    /// OAuth State 记录（防 CSRF）
    /// </summary>
    internal class OAuthStateRecord
    {
        /// <summary>
        /// State 值
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// 对应的提供商名称
        /// </summary>
        public string ProviderName { get; set; }

        /// <summary>
        /// PKCE Code Verifier（启用 PKCE 时）
        /// </summary>
        public string? CodeVerifier { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 过期时间（分钟）
        /// </summary>
        public int ExpirationMinutes { get; set; } = 10;

        /// <summary>
        /// 用户自定义附加数据（如 returnUrl 等）
        /// </summary>
        public string? UserData { get; set; }

        /// <summary>
        /// 检查是否已过期
        /// </summary>
        public bool IsExpired => (DateTime.UtcNow - CreatedAt).TotalMinutes > ExpirationMinutes;

        public OAuthStateRecord(string state, string providerName, string? codeVerifier = null, string? userData = null)
        {
            State = state;
            ProviderName = providerName;
            CodeVerifier = codeVerifier;
            UserData = userData;
        }
    }
}
