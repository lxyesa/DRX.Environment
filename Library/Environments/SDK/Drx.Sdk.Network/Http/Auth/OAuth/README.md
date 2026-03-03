# OAuth 2.0 模块

> `Drx.Sdk.Network.Http.Auth.OAuth` — 为 DRX Http 框架提供统一、高性能的 OAuth 2.0 社交登录能力。

## 特性

- **极简 API** — 3 行代码完成 OAuth 接入
- **10+ 预置提供商** — GitHub、Google、Microsoft、Discord、Apple、Facebook、Twitter(X)、Gitee、微信、LinkedIn
- **高性能** — 全局 HttpClient 复用、ConcurrentDictionary 无锁状态管理、Timer 自动清理
- **安全增强** — 内置 State 防 CSRF、PKCE (S256) 支持、Token 自动过期
- **易于扩展** — 继承 `OAuthProvider` 即可接入任意 OAuth 2.0 平台

## 快速开始

### 1. 注册提供商（应用启动时配置一次）

```csharp
using Drx.Sdk.Network.Http.Auth.OAuth;
using Drx.Sdk.Network.Http.Auth.OAuth.Providers;

// 单个注册
OAuthManager.AddProvider(new GitHubOAuthProvider(
    clientId: "your_client_id",
    clientSecret: "your_client_secret",
    redirectUri: "https://yourapp.com/oauth/callback"
));

// 批量注册
OAuthManager.AddProviders(
    new GoogleOAuthProvider("id", "secret", "https://yourapp.com/oauth/callback"),
    new MicrosoftOAuthProvider("id", "secret", "https://yourapp.com/oauth/callback"),
    new DiscordOAuthProvider("id", "secret", "https://yourapp.com/oauth/callback"),
    new GiteeOAuthProvider("id", "secret", "https://yourapp.com/oauth/callback"),
    new WeChatOAuthProvider("appid", "appsecret", "https://yourapp.com/oauth/callback")
);
```

### 2. 获取授权 URL（引导用户登录）

```csharp
// 自动处理 State + PKCE
var url = OAuthManager.GetAuthorizationUrl("github");
// → 将用户重定向到此 URL

// 携带自定义数据（如登录后跳转地址）
var url = OAuthManager.GetAuthorizationUrl("google", userData: "/dashboard");
```

### 3. 处理回调（完成认证）

```csharp
// 方式一：指定提供商
var result = await OAuthManager.AuthenticateAsync("github", code, state);

// 方式二：从 State 自动推断提供商（统一回调 URL 场景）
var result = await OAuthManager.AuthenticateAsync(code, state);

if (result.Success)
{
    Console.WriteLine($"登录成功！");
    Console.WriteLine($"  用户: {result.User.Name}");
    Console.WriteLine($"  邮箱: {result.User.Email}");
    Console.WriteLine($"  头像: {result.User.AvatarUrl}");
    Console.WriteLine($"  来源: {result.Provider}");
}
else
{
    Console.WriteLine($"登录失败: {result.Error}");
}
```

### 4. 与 DrxHttpServer 集成

```csharp
// 登录入口路由
server.Get("/login/{provider}", (req) =>
{
    var provider = req.PathParams["provider"];
    var url = OAuthManager.GetAuthorizationUrl(provider);
    return new HttpResponse { StatusCode = 302, Headers = { { "Location", url } } };
});

// OAuth 回调路由
server.Get("/oauth/callback", async (req) =>
{
    var code = req.QueryParams["code"];
    var state = req.QueryParams["state"];

    var result = await OAuthManager.AuthenticateAsync(code, state);

    if (result.Success)
    {
        // 创建本站 Session 或 JWT
        var jwt = JwtHelper.GenerateToken(result.User.Id, result.User.Name ?? "", result.User.Email);
        return new HttpResponse { Body = new { token = jwt, user = result.User } };
    }

    return new HttpResponse { StatusCode = 401, Body = result.Error };
});
```

## 预置提供商列表

| 提供商 | 类名 | 默认权限 | PKCE | 备注 |
|--------|------|----------|------|------|
| GitHub | `GitHubOAuthProvider` | `read:user user:email` | ❌ | |
| Google | `GoogleOAuthProvider` | `openid email profile` | ✅ | OpenID Connect |
| Microsoft | `MicrosoftOAuthProvider` | `openid email profile User.Read` | ✅ | 支持多租户 |
| Discord | `DiscordOAuthProvider` | `identify email` | ❌ | |
| Apple | `AppleOAuthProvider` | `name email` | ✅ | 用户信息从 id_token 解析 |
| Facebook | `FacebookOAuthProvider` | `public_profile email` | ❌ | Graph API v19.0 |
| Twitter (X) | `TwitterOAuthProvider` | `tweet.read users.read offline.access` | ✅ (强制) | Basic Auth Token 端点 |
| Gitee（码云） | `GiteeOAuthProvider` | `user_info emails` | ❌ | |
| 微信 | `WeChatOAuthProvider` | `snsapi_login` | ❌ | 非标准参数（appid/secret） |
| LinkedIn | `LinkedInOAuthProvider` | `openid profile email` | ❌ | |

## 自定义提供商

继承 `OAuthProvider`，只需实现 `ParseUserInfo` 方法：

```csharp
public class MyOAuthProvider : OAuthProvider
{
    public MyOAuthProvider(string clientId, string clientSecret, string redirectUri)
        : base(new OAuthProviderConfig(
            name: "myprovider",
            displayName: "My Provider",
            clientId: clientId,
            clientSecret: clientSecret,
            authorizationEndpoint: "https://auth.myprovider.com/authorize",
            tokenEndpoint: "https://auth.myprovider.com/token",
            userInfoEndpoint: "https://api.myprovider.com/userinfo",
            redirectUri: redirectUri,
            scopes: "profile email"))
    {
    }

    protected override OAuthUserInfo ParseUserInfo(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new OAuthUserInfo
        {
            Id = root.GetProperty("uid").GetString() ?? "",
            Name = root.GetProperty("display_name").GetString(),
            Email = root.GetProperty("email").GetString()
        };
    }
}
```

如果提供商使用非标准协议（如微信），可覆写 `BuildAuthorizationUrl`、`ExchangeCodeForTokenAsync`、`GetUserInfoAsync` 等方法。

## Token 刷新

```csharp
var newToken = await OAuthManager.RefreshTokenAsync("google", oldToken.RefreshToken);
```

## 与自有 OpenAuth 服务集成（AuthApp 模式）

除第三方社交登录外，框架还支持自有 OpenAuth 授权码流程。

### 服务端（DrxHttpServer）

在启动时注册允许接入的 AuthApp：

```csharp
server.RegisterAuthApp(
    clientId: "demo_client",
    redirectUri: "https://client.example.com/callback",
    applicationName: "Demo Client",
    applicationDescription: "示例客户端",
    scopes: "profile",
    clientSecret: "optional-secret",
    enabled: true
);
```

### Auth App 管理 API（需 System 权限 = 0）

服务端内置了以下管理端点，供具有 System 权限的用户通过 API 动态管理 AuthApp：

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| `GET`  | `/api/oauth/apps` | 获取所有 AuthApp 列表 | System (0) |
| `POST` | `/api/oauth/apps/save` | 创建或更新 AuthApp | System (0) |
| `GET`  | `/api/oauth/apps/quick-login-url` | 根据 clientId 生成授权 URL | 公开 |

**`POST /api/oauth/apps/save` 请求体：**

```json
{
  "id": "",
  "clientId": "demo_client",
  "applicationName": "Demo Client",
  "applicationDescription": "示例客户端",
  "redirectUri": "https://client.example.com/callback",
  "scopes": "profile",
  "clientSecret": "optional-secret",
  "isEnabled": true
}
```
> `id` 为空时创建新 App，非空时更新已有记录（通过数据库 Id 定位）。  
> `clientSecret` 留空表示不修改密钥（更新场景）。

**`GET /api/oauth/apps/quick-login-url` 查询参数：**

| 参数 | 说明 |
|------|------|
| `clientId` | AuthApp 的 clientId |
| `scope` | 请求权限范围（默认 `profile`） |

### 客户端（DrxHttpClient.OpenAuth）

**完整流程（手动管理 state）：**

```csharp
var state = DrxHttpClient.CreateOpenAuthState();

var authUrl = DrxHttpClient.BuildOpenAuthAuthorizeUrl(
    serverBaseUrl: "https://auth.example.com",
    clientId: "demo_client",
    redirectUri: "https://client.example.com/callback",
    state: state,
    scope: "profile"
);

// 浏览器跳转 authUrl，回调后拿到 code
var token = await client.ExchangeOpenAuthCodeAsync(
    serverBaseUrl: "https://auth.example.com",
    code: code,
    clientId: "demo_client",
    redirectUri: "https://client.example.com/callback"
);
```

**快捷方式 `AuthLogin`（推荐，服务端自动查找配置）：**

```csharp
// 一行获取完整授权 URL，服务端自动拼装 redirectUri、state 等参数
var authUrl = await client.AuthLogin(
    serverBaseUrl: "https://auth.example.com",
    appId: "demo_client",
    scope: "profile"          // 可选，默认 "profile"
);

// 将 authUrl 重定向给用户浏览器即可完成登录引导
```

## PKCE 工具

```csharp
using Drx.Sdk.Network.Http.Auth.OAuth;

// 手动生成 PKCE 对
var (verifier, challenge) = OAuthPKCE.GeneratePair();

// 或分开生成
var verifier = OAuthPKCE.GenerateCodeVerifier();
var challenge = OAuthPKCE.GenerateCodeChallenge(verifier);
```

## 高级配置

```csharp
// 修改 State 过期时间（默认 10 分钟）
OAuthManager.SetStateExpiration(15);

// 查询已注册提供商
var providers = OAuthManager.GetRegisteredProviders(); // ["github", "google", ...]

// 检查提供商
if (OAuthManager.HasProvider("github")) { ... }

// 重置（清除所有提供商和 State）
OAuthManager.Reset();
```

## 架构

```
Auth/OAuth/
├── OAuthProvider.cs              ← 抽象基类（标准 OAuth 2.0 流程）
├── OAuthManager.cs               ← 静态管理器（State、Provider 注册）
├── OAuthPKCE.cs                  ← PKCE 工具（S256）
├── README.md                     ← 本文档
└── Providers/
    ├── GitHubOAuthProvider.cs
    ├── GoogleOAuthProvider.cs
    ├── MicrosoftOAuthProvider.cs
    ├── DiscordOAuthProvider.cs
    ├── AppleOAuthProvider.cs
    ├── FacebookOAuthProvider.cs
    ├── TwitterOAuthProvider.cs
    ├── GiteeOAuthProvider.cs
    ├── WeChatOAuthProvider.cs
    └── LinkedInOAuthProvider.cs

Configs/
└── OAuthConfig.cs                ← 数据模型（OAuthProviderConfig, OAuthToken, OAuthUserInfo, OAuthResult）
```

## 性能要点

| 特性 | 实现 |
|------|------|
| HttpClient 复用 | 全局 `static readonly HttpClient`，避免 Socket 耗尽 |
| State 管理 | `ConcurrentDictionary` + `Timer` 每 2 分钟清理过期记录 |
| Provider 注册 | `ConcurrentDictionary`（大小写不敏感），O(1) 查找 |
| 全异步 | 所有网络操作使用 `async/await`，无阻塞调用 |
| 零反射 | 不使用反射或动态代理，全静态分派 |
