using System;
using System.Linq;
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
        var (_, authError) = await Api.RequireSystemAsync(request);
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

            return new JsonResult(new
            {
                code = 0,
                message = "成功",
                data = new
                {
                    total,
                    page,
                    pageSize,
                    items
                }
            }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"获取系统用户列表失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }
}
