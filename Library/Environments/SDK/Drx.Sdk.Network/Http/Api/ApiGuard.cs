using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http.Protocol;

namespace Drx.Sdk.Network.Http.Api
{
    /// <summary>
    /// API 认证守卫：从请求中提取并验证 Bearer Token，返回认证信息或标准错误。
    /// <para>
    /// 这是一个通用的 SDK 层工具，不依赖具体的用户模型。
    /// 通过委托注入验证逻辑，适应不同的应用场景。
    /// </para>
    /// <para>
    /// 使用示例：
    /// <code>
    /// // 配置（应用启动时）
    /// ApiGuard.Configure(token => MyJwtHelper.ValidateToken(token));
    /// 
    /// // 在 Handler 中使用
    /// if (!ApiGuard.TryAuthenticate(request, out var principal, out var error))
    ///     return error;
    /// var userName = principal.Identity?.Name;
    /// </code>
    /// </para>
    /// </summary>
    public static class ApiGuard
    {
        private static Func<string, ClaimsPrincipal?>? _tokenValidator;

        /// <summary>
        /// 配置全局的 Token 验证器。
        /// 必须在应用启动时调用一次。
        /// </summary>
        /// <param name="tokenValidator">
        /// Token 验证函数：接收 Bearer Token 字符串，返回 ClaimsPrincipal（验证成功）或 null（验证失败）。
        /// </param>
        public static void Configure(Func<string, ClaimsPrincipal?> tokenValidator)
        {
            _tokenValidator = tokenValidator ?? throw new ArgumentNullException(nameof(tokenValidator));
        }

        /// <summary>
        /// 从请求的 Authorization 头中提取 Bearer Token。
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>Bearer Token 字符串，未找到返回 null</returns>
        public static string? ExtractBearerToken(HttpRequest request)
        {
            var authHeader = request.Headers[HttpHeaders.Authorization];
            if (string.IsNullOrEmpty(authHeader)) return null;

            const string bearerPrefix = "Bearer ";
            if (authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
                return authHeader.Substring(bearerPrefix.Length).Trim();

            return authHeader.Trim();
        }

        /// <summary>
        /// 尝试从请求中认证用户。
        /// 提取 Bearer Token → 调用验证器 → 提取用户名。
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <param name="principal">验证成功时的 ClaimsPrincipal</param>
        /// <param name="error">验证失败时的标准错误响应（401 Unauthorized）</param>
        /// <returns>认证是否成功</returns>
        public static bool TryAuthenticate(HttpRequest request, out ClaimsPrincipal principal, out IActionResult? error)
        {
            principal = null!;
            error = null;

            if (_tokenValidator == null)
            {
                error = ApiResult.ServerError("认证系统未配置");
                return false;
            }

            var token = ExtractBearerToken(request);
            if (string.IsNullOrEmpty(token))
            {
                error = ApiResult.Unauthorized();
                return false;
            }

            var result = _tokenValidator(token);
            if (result == null)
            {
                error = ApiResult.Unauthorized("无效的登录令牌");
                return false;
            }

            principal = result;
            return true;
        }

        /// <summary>
        /// 尝试从请求中认证并提取用户名。
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <param name="userName">认证成功时的用户名</param>
        /// <param name="error">认证失败时的标准错误响应</param>
        /// <returns>认证是否成功</returns>
        public static bool TryGetUserName(HttpRequest request, out string userName, out IActionResult? error)
        {
            userName = null!;
            if (!TryAuthenticate(request, out var principal, out error))
                return false;

            var name = principal.Identity?.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                error = ApiResult.Unauthorized("令牌缺少用户信息");
                return false;
            }

            userName = name;
            return true;
        }
    }
}
