using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Api;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Shared.Utility;

namespace KaxSocket.Handlers.Helpers
{
    /// <summary>
    /// KaxSocket 业务层 API 快捷封装。
    /// 将 JWT 认证、封禁检查、用户查询、管理员验证等重复逻辑整合为单行调用。
    /// <para>
    /// 使用示例：
    /// <code>
    /// // 认证 + 封禁检查 + 获取用户对象（原来需要 8 行，现在只需 2 行）：
    /// var (user, error) = await Api.GetUserAsync(request);
    /// if (error != null) return error;
    /// 
    /// // 管理员接口（认证 + 封禁 + 权限检查）：
    /// var (user, error) = await Api.RequireAdminAsync(request);
    /// if (error != null) return error;
    /// 
    /// // 仅认证（不查数据库）：
    /// var (userName, error) = Api.Authenticate(request);
    /// if (error != null) return error;
    /// </code>
    /// </para>
    /// </summary>
    public static class Api
    {
        private const string VerificationCodeCharset = "0123456789"; // 纯数字验证码，避免大小写混淆

        #region 认证（不涉及数据库查询）

        /// <summary>
        /// 从请求中认证用户，提取用户名。
        /// 验证步骤：Bearer Token → JWT 验证 → 提取用户名
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>(userName, error) — 成功时 error 为 null</returns>
        public static (string? userName, IActionResult? error) Authenticate(HttpRequest request)
        {
            if (!ApiGuard.TryGetUserName(request, out var userName, out var error))
                return (null, error);
            return (userName, null);
        }

        #endregion

        #region 安全工具（验证码与邮箱）

        /// <summary>
        /// 生成指定长度的验证码（默认 8 位，字母数字，排除易混淆字符）。
        /// </summary>
        public static string GenerateVerificationCode(int length = 8)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

            var buffer = new byte[length];
            RandomNumberGenerator.Fill(buffer);
            var chars = new char[length];

            for (var i = 0; i < length; i++)
            {
                chars[i] = VerificationCodeCharset[buffer[i] % VerificationCodeCharset.Length];
            }

            return new string(chars);
        }

        /// <summary>
        /// 生成随机盐（Base64 URL 友好）。
        /// </summary>
        public static string GenerateVerificationSalt(int byteLength = 16)
        {
            if (byteLength <= 0) throw new ArgumentOutOfRangeException(nameof(byteLength));
            var saltBytes = new byte[byteLength];
            RandomNumberGenerator.Fill(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }

        /// <summary>
        /// 计算验证码哈希（SHA-256，包含盐），返回小写十六进制串。
        /// </summary>
        public static string HashVerificationCode(string code, string salt)
        {
            var input = $"{code ?? string.Empty}:{salt ?? string.Empty}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// 校验验证码明文是否与目标哈希一致（固定时序比较）。
        /// </summary>
        public static bool VerifyVerificationCode(string inputCode, string salt, string expectedHash)
        {
            var actual = HashVerificationCode(inputCode, salt);
            var actualBytes = Encoding.UTF8.GetBytes(actual);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedHash ?? string.Empty);
            return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }

        /// <summary>
        /// 邮箱脱敏：保留首字符与域名，其余替换为 *。
        /// </summary>
        public static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return string.Empty;
            var at = email.IndexOf('@');
            if (at <= 0 || at == email.Length - 1) return "***";

            var local = email[..at];
            var domain = email[(at + 1)..];
            var head = local[0];
            var maskedLocal = local.Length <= 1 ? "*" : head + new string('*', Math.Max(2, local.Length - 1));
            return $"{maskedLocal}@{domain}";
        }

        /// <summary>
        /// 统一邮箱格式校验入口。
        /// </summary>
        public static bool IsValidEmailFormat(string? email)
        {
            return !string.IsNullOrWhiteSpace(email) && CommonUtility.IsValidEmail(email);
        }

        #endregion

        #region 认证 + 封禁检查

        /// <summary>
        /// 认证用户并检查封禁状态（不查询完整用户对象）。
        /// 验证步骤：Bearer Token → JWT 验证 → 提取用户名 → 封禁检查
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>(userName, error) — 成功时 error 为 null</returns>
        public static async Task<(string? userName, IActionResult? error)> AuthenticateAndCheckBanAsync(HttpRequest request)
        {
            if (!ApiGuard.TryGetUserName(request, out var userName, out var error))
                return (null, error);

            if (await KaxGlobal.IsUserBanned(userName))
                return (null, ApiResult.Banned());

            return (userName, null);
        }

        #endregion

        #region 认证 + 封禁 + 获取用户对象

        /// <summary>
        /// 认证用户、检查封禁状态、查询完整用户对象。
        /// 这是最常用的方法，替代 Handler 中重复的 8 行样板代码。
        /// <para>
        /// 验证步骤：Bearer Token → JWT 验证 → 提取用户名 → 封禁检查 → 查询 UserData
        /// </para>
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>(user, error) — 成功时 error 为 null，user 保证非 null</returns>
        public static async Task<(UserData? user, IActionResult? error)> GetUserAsync(HttpRequest request)
        {
            if (!ApiGuard.TryAuthenticate(request, out var principal, out var error))
                return (null, error);

            var userName = principal.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName))
                return (null, ApiResult.Unauthorized("令牌缺少用户信息"));

            if (await KaxGlobal.IsUserBanned(userName))
                return (null, ApiResult.Banned());

            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
            if (user == null)
                return (null, ApiResult.NotFound("用户不存在"));

            // 密码重置后强制所有旧会话失效：比较 JWT iat（秒）与 TokenInvalidBefore（毫秒基准）
            if (user.TokenInvalidBefore > 0)
            {
                var iatClaim = principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Iat)?.Value;
                if (long.TryParse(iatClaim, out var iatSeconds))
                {
                    var iatMs = iatSeconds * 1000L; // 转毫秒比较
                    if (iatMs < user.TokenInvalidBefore)
                        return (null, ApiResult.Unauthorized("登录状态已过期，请重新登录"));
                }
            }

            return (user, null);
        }

        #endregion

        #region 管理员验证

        /// <summary>
        /// 检查用户是否拥有管理员权限（System / Console / Admin）。
        /// </summary>
        private static bool IsAdmin(UserPermissionGroup group)
        {
            return group == UserPermissionGroup.System
                || group == UserPermissionGroup.Console
                || group == UserPermissionGroup.Admin;
        }

        /// <summary>
        /// 认证用户并验证管理员权限。
        /// 验证步骤：Bearer Token → JWT 验证 → 提取用户名 → 封禁检查 → 查询 UserData → 权限验证
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>(user, error) — 成功时 error 为 null，user 保证非 null 且是管理员</returns>
        public static async Task<(UserData? user, IActionResult? error)> RequireAdminAsync(HttpRequest request)
        {
            var (user, error) = await GetUserAsync(request);
            if (error != null) return (null, error);

            if (!IsAdmin(user!.PermissionGroup))
                return (null, ApiResult.Forbidden("权限不足"));

            return (user, null);
        }

        /// <summary>
        /// 仅做认证 + 封禁 + 管理员检查，不返回用户对象（用于不需要 user 的管理接口）。
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>(userName, error) — 成功时 error 为 null</returns>
        public static async Task<(string? userName, IActionResult? error)> RequireAdminNameAsync(HttpRequest request)
        {
            var (user, error) = await RequireAdminAsync(request);
            if (error != null) return (null, error);
            return (user!.UserName, null);
        }

        #endregion
    }
}
