using System;
using System.Linq;
using Drx.Sdk.Shared;
using KaxSocket.Model;

namespace KaxSocket;

/// <summary>
/// KaxGlobal 用户查询与权限管理：权限组设置/读取、金币、资源数、活动计数、CDK 数量查询。
/// </summary>
public static partial class KaxGlobal
{
    public static async Task SetUserPermissionGroup(string userName, UserPermissionGroup group)
    {
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user != null)
        {
            user.PermissionGroup = group;
            await UserDatabase.UpdateAsync(user);
            Logger.Info($"已设置用户 {user.UserName} 的权限组为 {group}。");
        }
    }

    public static async Task<UserPermissionGroup> GetUserPermissionGroup(string userName)
    {
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        return user?.PermissionGroup ?? UserPermissionGroup.User;
    }

    /// <summary>
    /// 返回指定用户已使用的 CDK 数量（UsedBy == userName）。
    /// </summary>
    public static async Task<int> GetUserCdkCountAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return 0;
        try
        {
            var all = await CdkDatabase.SelectAllAsync();
            return all.Count(c => string.Equals(c.UsedBy ?? string.Empty, userName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Logger.Warn($"GetUserCdkCountAsync({userName}) 失败: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// 返回用户的资源计数（来自 UserData.ResourceCount）。
    /// </summary>
    public static async Task<int> GetUserResourceCountAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return 0;
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        return user?.ResourceCount ?? 0;
    }

    /// <summary>
    /// 返回用户的金币（来自 UserData.Gold）。
    /// </summary>
    public static async Task<int> GetUserGoldAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return 0;
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        return user?.Gold ?? 0;
    }

    /// <summary>
    /// 返回用户的最近活动计数（来自 UserData.RecentActivity）。
    /// </summary>
    public static async Task<int> GetUserRecentActivityAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return 0;
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        return user?.RecentActivity ?? 0;
    }
}
