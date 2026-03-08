using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Api;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Results;
using Drx.Sdk.Shared;
using KaxSocket.Handlers.Helpers;

namespace KaxSocket.Handlers;

/// <summary>
/// 用户管理模块（System）
/// </summary>
public partial class KaxHttp
{
    private static string PermissionGroupText(UserPermissionGroup group) => group switch
    {
        UserPermissionGroup.System => "System",
        UserPermissionGroup.Console => "Console",
        UserPermissionGroup.Admin => "Admin",
        _ => "User"
    };

    /// <summary>
    /// System 用户列表
    /// 支持筛选：q(关键字)、permissionGroup(权限组)、page、pageSize
    /// </summary>
    [HttpHandle("/api/system/users", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_SystemUsers(HttpRequest request)
    {
        var (_, authError) = await Api.RequirePolicyAsync(request, ApiAccessPolicy.System);
        if (authError != null) return authError;

        try
        {
            var (page, pageSize) = ApiPagination.Parse(request, defaultPageSize: 20, maxPageSize: 100);
            var keyword = (request.Query["q"] ?? string.Empty).Trim();
            var permissionGroupRaw = request.Query["permissionGroup"];

            var users = await KaxGlobal.UserDatabase.SelectAllAsync();
            var filtered = users.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                filtered = filtered.Where(u =>
                    (!string.IsNullOrWhiteSpace(u.UserName) && u.UserName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(u.DisplayName) && u.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(u.Email) && u.Email.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                );
            }

            if (!string.IsNullOrWhiteSpace(permissionGroupRaw)
                && int.TryParse(permissionGroupRaw, out var groupVal)
                && Enum.IsDefined(typeof(UserPermissionGroup), groupVal))
            {
                var group = (UserPermissionGroup)groupVal;
                filtered = filtered.Where(u => u.PermissionGroup == group);
            }

            var total = filtered.Count();
            var items = filtered
                .OrderByDescending(u => u.LastLoginAt)
                .ThenByDescending(u => u.RegisteredAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    id = u.Id,
                    userName = u.UserName,
                    displayName = string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName,
                    email = u.Email,
                    permissionGroup = (int)u.PermissionGroup,
                    permissionGroupText = PermissionGroupText(u.PermissionGroup),
                    emailVerified = u.EmailVerified,
                    isBanned = u.Status?.IsBanned ?? false,
                    banExpiresAt = u.Status?.BanExpiresAt ?? 0,
                    registeredAt = u.RegisteredAt,
                    lastLoginAt = u.LastLoginAt,
                    recentActivity = u.RecentActivity,
                    resourceCount = u.ResourceCount,
                    gold = u.Gold
                })
                .ToList<object>();

            return Api.EnvelopePaged(request, items, page, pageSize, total, "成功");
        }
        catch (Exception ex)
        {
            Logger.Error($"获取系统用户列表失败: {ex.Message}");
            return Api.EnvelopeFail(request, 500, 500, "服务器错误");
        }
    }

    /// <summary>
    /// 获取单个用户完整详情（System）
    /// </summary>
    [HttpHandle("/api/system/user/{userId}", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_SystemUserDetail(HttpRequest request)
    {
        var (_, authError) = await Api.RequirePolicyAsync(request, ApiAccessPolicy.System);
        if (authError != null) return authError;

        if (!request.PathParameters.TryGetValue("userId", out var idStr) || !int.TryParse(idStr, out var userId) || userId <= 0)
            return ApiResult.BadRequest("userId 参数无效");

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("Id", userId)).FirstOrDefault();
            if (user == null) return ApiResult.NotFound("用户不存在");

            return ApiResult.Ok(new
            {
                id = user.Id,
                userName = user.UserName,
                displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName,
                email = user.Email,
                emailVerified = user.EmailVerified,
                permissionGroup = (int)user.PermissionGroup,
                permissionGroupText = PermissionGroupText(user.PermissionGroup),
                registeredAt = user.RegisteredAt,
                lastLoginAt = user.LastLoginAt,
                signature = user.Signature ?? string.Empty,
                bio = user.Bio ?? string.Empty,
                badges = BadgeHelper.ParseBadges(user.Badges),
                gold = user.Gold,
                recentActivity = user.RecentActivity,
                resourceCount = user.ResourceCount,
                isBanned = user.Status?.IsBanned ?? false,
                bannedAt = user.Status?.BannedAt ?? 0,
                banExpiresAt = user.Status?.BanExpiresAt ?? 0,
                banReason = user.Status?.BanReason ?? string.Empty,
                tokenInvalidBefore = user.TokenInvalidBefore,
                activeAssetsCount = user.ActiveAssets?.Count ?? 0,
                favoriteAssetsCount = user.FavoriteAssets?.Count ?? 0,
                cartItemsCount = user.CartItems?.Count ?? 0,
                orderRecordsCount = user.OrderRecords?.Count ?? 0
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"获取用户详情失败: {ex.Message}");
            return ApiResult.Error(500, "服务器错误");
        }
    }

    /// <summary>
    /// 封禁用户（System）
    /// Body: { reason: string, durationHours?: number (0=永久) }
    /// </summary>
    [HttpHandle("/api/system/user/{userId}/ban", "POST", RateLimitMaxRequests = 30, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_SystemBanUser(HttpRequest request)
    {
        var (operator_, authError) = await Api.RequirePolicyAsync(request, ApiAccessPolicy.System);
        if (authError != null) return authError;

        if (!request.PathParameters.TryGetValue("userId", out var idStr) || !int.TryParse(idStr, out var userId) || userId <= 0)
            return ApiResult.BadRequest("userId 参数无效");

        if (!ApiBody.TryParse(request, out var body, out var bodyError))
            return bodyError!;

        var reason = body.TrimmedString("reason") ?? string.Empty;
        var durationHours = body.Int("durationHours", 0);

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("Id", userId)).FirstOrDefault();
            if (user == null) return ApiResult.NotFound("用户不存在");

            if (user.PermissionGroup == UserPermissionGroup.System)
                return ApiResult.Forbidden("无法封禁 System 用户");

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            user.Status ??= new UserStatus();
            user.Status.IsBanned = true;
            user.Status.BannedAt = now;
            user.Status.BanReason = reason;
            user.Status.BanExpiresAt = durationHours > 0
                ? now + (long)durationHours * 3600_000L
                : 0;

            await KaxGlobal.UserDatabase.UpdateAsync(user);
            Logger.Info($"[UserManagement] 用户 {user.UserName}(Id={user.Id}) 被 {operator_!.UserName} 封禁, 原因: {reason}");

            return ApiResult.Ok("封禁成功");
        }
        catch (Exception ex)
        {
            Logger.Error($"封禁用户失败: {ex.Message}");
            return ApiResult.Error(500, "服务器错误");
        }
    }

    /// <summary>
    /// 解封用户（System）
    /// </summary>
    [HttpHandle("/api/system/user/{userId}/unban", "POST", RateLimitMaxRequests = 30, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_SystemUnbanUser(HttpRequest request)
    {
        var (operator_, authError) = await Api.RequirePolicyAsync(request, ApiAccessPolicy.System);
        if (authError != null) return authError;

        if (!request.PathParameters.TryGetValue("userId", out var idStr) || !int.TryParse(idStr, out var userId) || userId <= 0)
            return ApiResult.BadRequest("userId 参数无效");

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("Id", userId)).FirstOrDefault();
            if (user == null) return ApiResult.NotFound("用户不存在");

            user.Status ??= new UserStatus();
            user.Status.IsBanned = false;
            user.Status.BannedAt = 0;
            user.Status.BanExpiresAt = 0;
            user.Status.BanReason = string.Empty;

            await KaxGlobal.UserDatabase.UpdateAsync(user);
            Logger.Info($"[UserManagement] 用户 {user.UserName}(Id={user.Id}) 被 {operator_!.UserName} 解封");

            return ApiResult.Ok("解封成功");
        }
        catch (Exception ex)
        {
            Logger.Error($"解封用户失败: {ex.Message}");
            return ApiResult.Error(500, "服务器错误");
        }
    }

    /// <summary>
    /// 修改用户权限组（System）
    /// Body: { permissionGroup: int }
    /// </summary>
    [HttpHandle("/api/system/user/{userId}/permission", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_SystemChangePermission(HttpRequest request)
    {
        var (operator_, authError) = await Api.RequirePolicyAsync(request, ApiAccessPolicy.System);
        if (authError != null) return authError;

        if (!request.PathParameters.TryGetValue("userId", out var idStr) || !int.TryParse(idStr, out var userId) || userId <= 0)
            return ApiResult.BadRequest("userId 参数无效");

        if (!ApiBody.TryParse(request, out var body, out var bodyError))
            return bodyError!;

        var groupVal = body.Int("permissionGroup", -1);
        if (!Enum.IsDefined(typeof(UserPermissionGroup), groupVal))
            return ApiResult.BadRequest("permissionGroup 值无效");

        var newGroup = (UserPermissionGroup)groupVal;

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("Id", userId)).FirstOrDefault();
            if (user == null) return ApiResult.NotFound("用户不存在");

            if (user.Id == operator_!.Id)
                return ApiResult.BadRequest("无法修改自己的权限组");

            var oldGroup = user.PermissionGroup;
            user.PermissionGroup = newGroup;

            await KaxGlobal.UserDatabase.UpdateAsync(user);
            Logger.Info($"[UserManagement] 用户 {user.UserName}(Id={user.Id}) 权限组从 {PermissionGroupText(oldGroup)} 改为 {PermissionGroupText(newGroup)} (操作者: {operator_.UserName})");

            return ApiResult.Ok("权限组修改成功");
        }
        catch (Exception ex)
        {
            Logger.Error($"修改用户权限组失败: {ex.Message}");
            return ApiResult.Error(500, "服务器错误");
        }
    }

    /// <summary>
    /// 调整用户金币（System）
    /// Body: { amount: int, reason?: string }
    /// </summary>
    [HttpHandle("/api/system/user/{userId}/gold", "POST", RateLimitMaxRequests = 30, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_SystemAdjustGold(HttpRequest request)
    {
        var (operator_, authError) = await Api.RequirePolicyAsync(request, ApiAccessPolicy.System);
        if (authError != null) return authError;

        if (!request.PathParameters.TryGetValue("userId", out var idStr) || !int.TryParse(idStr, out var userId) || userId <= 0)
            return ApiResult.BadRequest("userId 参数无效");

        if (!ApiBody.TryParse(request, out var body, out var bodyError))
            return bodyError!;

        var amount = body.Int("amount", 0);
        if (amount == 0) return ApiResult.BadRequest("amount 不能为 0");

        var reason = body.TrimmedString("reason") ?? "System 手动调整";

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("Id", userId)).FirstOrDefault();
            if (user == null) return ApiResult.NotFound("用户不存在");

            var oldGold = user.Gold;
            user.Gold += amount;
            if (user.Gold < 0) user.Gold = 0;

            await KaxGlobal.UserDatabase.UpdateAsync(user);
            Logger.Info($"[UserManagement] 用户 {user.UserName}(Id={user.Id}) 金币 {oldGold} -> {user.Gold} (变动: {amount}, 原因: {reason}, 操作者: {operator_!.UserName})");

            return ApiResult.Ok(new { gold = user.Gold }, "金币调整成功");
        }
        catch (Exception ex)
        {
            Logger.Error($"调整用户金币失败: {ex.Message}");
            return ApiResult.Error(500, "服务器错误");
        }
    }

    /// <summary>
    /// 强制登出用户（使该用户所有已发放 Token 失效）（System）
    /// </summary>
    [HttpHandle("/api/system/user/{userId}/force-logout", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_SystemForceLogout(HttpRequest request)
    {
        var (operator_, authError) = await Api.RequirePolicyAsync(request, ApiAccessPolicy.System);
        if (authError != null) return authError;

        if (!request.PathParameters.TryGetValue("userId", out var idStr) || !int.TryParse(idStr, out var userId) || userId <= 0)
            return ApiResult.BadRequest("userId 参数无效");

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("Id", userId)).FirstOrDefault();
            if (user == null) return ApiResult.NotFound("用户不存在");

            user.TokenInvalidBefore = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            await KaxGlobal.UserDatabase.UpdateAsync(user);
            Logger.Info($"[UserManagement] 用户 {user.UserName}(Id={user.Id}) 被 {operator_!.UserName} 强制登出");

            return ApiResult.Ok("强制登出成功");
        }
        catch (Exception ex)
        {
            Logger.Error($"强制登出用户失败: {ex.Message}");
            return ApiResult.Error(500, "服务器错误");
        }
    }
}
