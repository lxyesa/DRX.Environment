using System;
using System.Linq;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Api;
using Drx.Sdk.Network.Http.Protocol;

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
            if (!ApiGuard.TryGetUserName(request, out var userName, out var error))
                return (null, error);

            if (await KaxGlobal.IsUserBanned(userName))
                return (null, ApiResult.Banned());

            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
            if (user == null)
                return (null, ApiResult.NotFound("用户不存在"));

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
