using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Api;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Protocol;
using KaxSocket;
using KaxSocket.Handlers.Helpers;

namespace KaxSocket.Handlers;

/// <summary>
/// OAuth(OpenAuth) 授权流程：授权确认页 + 授权码换取访问令牌
/// </summary>
public partial class KaxHttp
{
    [HttpHandle("/api/oauth/apps", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> GetOAuthApps(HttpRequest request, DrxHttpServer server)
    {
        var (user, authError) = await Api.GetUserAsync(request).ConfigureAwait(false);
        if (authError != null) return authError;

        if (user!.PermissionGroup != UserPermissionGroup.System)
            return new JsonResult(new { message = "权限不足：仅权限组 0 可管理 Auth App" }, 403);

        var q = (request.Query["q"] ?? string.Empty).Trim();
        var all = server.AuthAppDatabase.SelectAll()
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.ClientId)
            .ToList();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var kw = q.ToLowerInvariant();
            all = all.Where(x =>
                    (x.ClientId ?? string.Empty).ToLowerInvariant().Contains(kw)
                    || (x.ApplicationName ?? string.Empty).ToLowerInvariant().Contains(kw)
                    || (x.RedirectUri ?? string.Empty).ToLowerInvariant().Contains(kw)
                )
                .ToList();
        }

        return new JsonResult(new
        {
            data = all.Select(ToAuthAppDto).ToList(),
            total = all.Count
        });
    }

    [HttpHandle("/api/oauth/apps/save", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> PostOAuthAppSave(HttpRequest request, DrxHttpServer server)
    {
        var (user, authError) = await Api.GetUserAsync(request).ConfigureAwait(false);
        if (authError != null) return authError;

        if (user!.PermissionGroup != UserPermissionGroup.System)
            return new JsonResult(new { message = "权限不足：仅权限组 0 可管理 Auth App" }, 403);

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        var id = body.Int("id", 0);
        var clientId = body.TrimmedString("clientId", string.Empty) ?? string.Empty;
        var appName = body.TrimmedString("applicationName", string.Empty) ?? string.Empty;
        var appDesc = body.String("applicationDescription", string.Empty) ?? string.Empty;
        var redirectUri = body.TrimmedString("redirectUri", string.Empty) ?? string.Empty;
        var scopes = body.TrimmedString("scopes", "profile") ?? "profile";
        var clientSecret = body.String("clientSecret", string.Empty) ?? string.Empty;
        var isEnabled = body.Bool("isEnabled", true);

        if (string.IsNullOrWhiteSpace(clientId))
            return new JsonResult(new { message = "clientId 不能为空" }, 400);

        if (string.IsNullOrWhiteSpace(appName))
            return new JsonResult(new { message = "applicationName 不能为空" }, 400);

        if (string.IsNullOrWhiteSpace(redirectUri) || !IsValidRedirectUri(redirectUri))
            return new JsonResult(new { message = "redirectUri 非法，仅支持 http/https" }, 400);

        // 创建场景：直接复用服务器内置注册逻辑
        if (id <= 0)
        {
            var created = server.RegisterAuthApp(
                clientId,
                redirectUri,
                appName,
                appDesc,
                scopes,
                string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret,
                isEnabled
            );

            return new JsonResult(new
            {
                message = "Auth App 已创建",
                data = ToAuthAppDto(created)
            });
        }

        // 更新场景：按 ID 找到并更新，支持修改 clientId
        var existing = server.AuthAppDatabase.SelectWhere("Id", id).FirstOrDefault();
        if (existing == null)
            return new JsonResult(new { message = "Auth App 不存在" }, 404);

        var duplicate = server.AuthAppDatabase.SelectWhere("ClientId", clientId).FirstOrDefault();
        if (duplicate != null && duplicate.Id != existing.Id)
            return new JsonResult(new { message = "clientId 已存在" }, 409);

        existing.ClientId = clientId;
        existing.ApplicationName = appName;
        existing.ApplicationDescription = appDesc;
        existing.RedirectUri = redirectUri;
        existing.Scopes = scopes;
        existing.IsEnabled = isEnabled;
        existing.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            // 复用 RegisterAuthApp 的安全哈希逻辑
            var updated = server.RegisterAuthApp(
                existing.ClientId,
                existing.RedirectUri,
                existing.ApplicationName,
                existing.ApplicationDescription,
                existing.Scopes,
                clientSecret,
                existing.IsEnabled
            );

            // RegisterAuthApp 会按 clientId 更新一条记录；若 id 改动过则再确保字段一致
            updated.ClientId = existing.ClientId;
            updated.ApplicationName = existing.ApplicationName;
            updated.ApplicationDescription = existing.ApplicationDescription;
            updated.RedirectUri = existing.RedirectUri;
            updated.Scopes = existing.Scopes;
            updated.IsEnabled = existing.IsEnabled;
            updated.UpdatedAt = existing.UpdatedAt;
            server.AuthAppDatabase.Update(updated);

            return new JsonResult(new
            {
                message = "Auth App 已更新",
                data = ToAuthAppDto(updated)
            });
        }

        server.AuthAppDatabase.Update(existing);
        return new JsonResult(new
        {
            message = "Auth App 已更新",
            data = ToAuthAppDto(existing)
        });
    }

    [HttpHandle("/api/oauth/apps/quick-login-url", "GET", RateLimitMaxRequests = 120, RateLimitWindowSeconds = 60)]
    public static Task<IActionResult> GetOAuthQuickLoginUrl(HttpRequest request, DrxHttpServer server)
    {
        var clientId = (request.Query["clientId"] ?? string.Empty).Trim();
        var scope = (request.Query["scope"] ?? "profile").Trim();
        var state = (request.Query["state"] ?? Guid.NewGuid().ToString("N")).Trim();

        if (string.IsNullOrWhiteSpace(clientId))
            return Task.FromResult<IActionResult>(new JsonResult(new { message = "clientId 不能为空" }, 400));

        var app = server.GetAuthApp(clientId);
        if (app == null || !app.IsEnabled)
            return Task.FromResult<IActionResult>(new JsonResult(new { message = "Auth App 未注册或未启用" }, 404));

        if (string.IsNullOrWhiteSpace(app.RedirectUri) || !IsValidRedirectUri(app.RedirectUri))
            return Task.FromResult<IActionResult>(new JsonResult(new { message = "Auth App 回调地址无效" }, 400));

        var authorizeUrl = BuildAuthorizeUrl(
            request,
            app.ClientId,
            app.RedirectUri,
            state,
            string.IsNullOrWhiteSpace(scope) ? (app.Scopes ?? "profile") : scope,
            app.ApplicationName,
            app.ApplicationDescription
        );

        return Task.FromResult<IActionResult>(new JsonResult(new
        {
            clientId = app.ClientId,
            redirectUri = app.RedirectUri,
            authorizeUrl,
            state,
            scope = string.IsNullOrWhiteSpace(scope) ? (app.Scopes ?? "profile") : scope
        }));
    }

    [HttpHandle("/api/oauth/authorize/confirm", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> PostOAuthAuthorizeConfirm(HttpRequest request, DrxHttpServer server)
    {
        if (request.Body == null)
            return new JsonResult(new { message = "请求体不能为空" }, 400);

        JsonNode? bodyJson;
        try
        {
            bodyJson = JsonNode.Parse(request.Body);
        }
        catch
        {
            return new JsonResult(new { message = "请求体不是合法的 JSON" }, 400);
        }

        if (bodyJson == null)
            return new JsonResult(new { message = "请求体不能为空" }, 400);

        var clientId = bodyJson["clientId"]?.ToString()?.Trim();
        var redirectUri = bodyJson["redirectUri"]?.ToString()?.Trim();
        var state = bodyJson["state"]?.ToString()?.Trim();
        var scope = bodyJson["scope"]?.ToString()?.Trim() ?? string.Empty;
        var appName = bodyJson["applicationName"]?.ToString()?.Trim();
        var appDescription = bodyJson["applicationDescription"]?.ToString()?.Trim() ?? string.Empty;
        var approve = bodyJson["approve"]?.GetValue<bool?>() ?? true;

        if (!approve)
            return new JsonResult(new { message = "用户拒绝授权" }, 400);

        if (string.IsNullOrWhiteSpace(clientId))
            return new JsonResult(new { message = "clientId 不能为空" }, 400);

        if (string.IsNullOrWhiteSpace(redirectUri) || !IsValidRedirectUri(redirectUri))
            return new JsonResult(new { message = "redirectUri 无效，仅支持 http/https" }, 400);

        if (!server.ValidateAuthApp(clientId!, redirectUri!, null, out var authApp, out var authAppError))
        {
            return new JsonResult(new { message = authAppError ?? "Auth App 校验失败" }, 403);
        }

        var (user, authError) = await Api.GetUserAsync(request).ConfigureAwait(false);
        if (authError != null) return authError;

        var finalAppName = string.IsNullOrWhiteSpace(appName)
            ? (string.IsNullOrWhiteSpace(authApp?.ApplicationName) ? clientId : authApp!.ApplicationName)
            : appName!;

        var finalScope = string.IsNullOrWhiteSpace(scope)
            ? (authApp?.Scopes ?? string.Empty)
            : scope;

        var code = server.AuthorizationManager.GenerateAuthorizationCode(
            user!.UserName,
            clientId!,
            appDescription,
            finalScope
        );

        // 用户在授权页已完成确认：标记授权完成，供 /api/oauth/token 校验
        _ = server.AuthorizationManager.CompleteAuthorization(code);

        var redirectUrl = AppendQueryValue(redirectUri!, "code", code);
        if (!string.IsNullOrWhiteSpace(state))
        {
            redirectUrl = AppendQueryValue(redirectUrl, "state", state!);
        }

        return new JsonResult(new
        {
            message = "授权成功",
            code,
            state,
            redirectUrl
        });
    }

    /// <summary>
    /// 注册 OpenAuth Token 交换 API。
    /// </summary>
    /// <param name="server">HTTP 服务器实例</param>
    /// <param name="apiUrl">Token API 路径；为空时默认 /api/oauth/token</param>
    public static void RegisterOAuthTokenApi(DrxHttpServer server, string? apiUrl = null)
    {
        if (server == null) throw new ArgumentNullException(nameof(server));

        var route = string.IsNullOrWhiteSpace(apiUrl) ? "/api/oauth/token" : apiUrl.Trim();
        if (!route.StartsWith('/')) route = "/" + route;

        server.AddRoute(System.Net.Http.HttpMethod.Post, route,
            async (request, currentServer) => await PostOAuthToken(request, currentServer).ConfigureAwait(false));
    }

    public static async Task<IActionResult> PostOAuthToken(HttpRequest request, DrxHttpServer server)
    {
        if (request.Body == null)
            return new JsonResult(new { error = "invalid_request", error_description = "请求体不能为空" }, 400);

        JsonNode? bodyJson;
        try
        {
            bodyJson = JsonNode.Parse(request.Body);
        }
        catch
        {
            return new JsonResult(new { error = "invalid_request", error_description = "请求体不是合法的 JSON" }, 400);
        }

        if (bodyJson == null)
            return new JsonResult(new { error = "invalid_request", error_description = "请求体不能为空" }, 400);

        var grantType = bodyJson["grant_type"]?.ToString()?.Trim() ?? "authorization_code";
        var code = bodyJson["code"]?.ToString()?.Trim();
        var clientId = bodyJson["client_id"]?.ToString()?.Trim();
        var clientSecret = bodyJson["client_secret"]?.ToString();
        var redirectUri = bodyJson["redirect_uri"]?.ToString()?.Trim();

        if (!string.Equals(grantType, "authorization_code", StringComparison.OrdinalIgnoreCase))
            return new JsonResult(new { error = "unsupported_grant_type", error_description = "仅支持 authorization_code" }, 400);

        if (string.IsNullOrWhiteSpace(code))
            return new JsonResult(new { error = "invalid_request", error_description = "code 不能为空" }, 400);

        if (string.IsNullOrWhiteSpace(clientId))
            return new JsonResult(new { error = "invalid_request", error_description = "client_id 不能为空" }, 400);

        if (string.IsNullOrWhiteSpace(redirectUri))
            return new JsonResult(new { error = "invalid_request", error_description = "redirect_uri 不能为空" }, 400);

        if (!server.ValidateAuthApp(clientId!, redirectUri!, clientSecret, out _, out var authAppError))
            return new JsonResult(new { error = "invalid_client", error_description = authAppError ?? "Auth App 校验失败" }, 400);

        var record = server.AuthorizationManager.GetAuthorizationRecord(code!);
        if (record == null)
            return new JsonResult(new { error = "invalid_grant", error_description = "授权码无效或已过期" }, 400);

        if (!record.IsAuthorized)
            return new JsonResult(new { error = "invalid_grant", error_description = "授权码尚未完成授权或已被使用" }, 400);

        if (!string.Equals(record.ApplicationName, clientId, StringComparison.OrdinalIgnoreCase))
        {
            return new JsonResult(new { error = "invalid_client", error_description = "client_id 不匹配" }, 400);
        }

        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", record.UserName).ConfigureAwait(false)).FirstOrDefault();
        if (user == null)
            return new JsonResult(new { error = "invalid_grant", error_description = "授权用户不存在" }, 400);

        if (await KaxGlobal.IsUserBanned(user).ConfigureAwait(false))
            return ApiResult.Forbidden("授权用户已被封禁");

        // 令牌签发后标记为已消费，防止重复换取
        record.IsAuthorized = false;

        var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // 单端登录策略：OAuth 换取令牌同样推进失效基线，防止绕过普通登录约束。
        user.TokenInvalidBefore = nowSeconds * 1000L;
        user.LastLoginAt = nowSeconds;
        var (clientToken, webToken, webTokenRotated) = KaxGlobal.ResolveLoginTokens(user, false, null);
        user.ClientToken = clientToken;
        user.WebToken = webToken;
        await KaxGlobal.UserDatabase.UpdateAsync(user).ConfigureAwait(false);

        return new JsonResult(new
        {
            access_token = clientToken,
            login_token = clientToken,
            client_token = clientToken,
            web_token = webToken,
            web_token_rotated = webTokenRotated,
            token_type = "Bearer",
            expires_in = 3600,
            scope = record.Scopes,
            user = new
            {
                id = user.Id,
                userName = user.UserName,
                email = user.Email
            }
        });
    }

    private static bool IsValidRedirectUri(string redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    private static string AppendQueryValue(string url, string key, string value)
    {
        var separator = url.Contains('?') ? '&' : '?';
        return $"{url}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }

    private static string BuildAuthorizeUrl(
        HttpRequest request,
        string clientId,
        string redirectUri,
        string state,
        string scope,
        string? appName,
        string? appDescription)
    {
        var host = request.Headers?["Host"] ?? "localhost";
        var proto = request.Headers?["X-Forwarded-Proto"]
                    ?? request.Headers?["X-Scheme"]
                    ?? "http";

        var baseUrl = $"{proto}://{host}".TrimEnd('/');
        var authorizeUrl = $"{baseUrl}/oauth/authorize";

        authorizeUrl = AppendQueryValue(authorizeUrl, "client_id", clientId);
        authorizeUrl = AppendQueryValue(authorizeUrl, "redirect_uri", redirectUri);
        authorizeUrl = AppendQueryValue(authorizeUrl, "state", state);
        authorizeUrl = AppendQueryValue(authorizeUrl, "scope", scope);

        if (!string.IsNullOrWhiteSpace(appName))
            authorizeUrl = AppendQueryValue(authorizeUrl, "app_name", appName!);

        if (!string.IsNullOrWhiteSpace(appDescription))
            authorizeUrl = AppendQueryValue(authorizeUrl, "app_desc", appDescription!);

        return authorizeUrl;
    }

    private static object ToAuthAppDto(Drx.Sdk.Network.Http.Models.AuthAppDataModel app)
    {
        return new
        {
            id = app.Id,
            clientId = app.ClientId,
            applicationName = app.ApplicationName,
            applicationDescription = app.ApplicationDescription,
            redirectUri = app.RedirectUri,
            scopes = app.Scopes,
            isEnabled = app.IsEnabled,
            hasClientSecret = !string.IsNullOrWhiteSpace(app.ClientSecretHash),
            createdAt = app.CreatedAt,
            updatedAt = app.UpdatedAt
        };
    }
}
