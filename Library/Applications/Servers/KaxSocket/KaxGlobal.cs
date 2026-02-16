using System;
using System.Security.Claims;
using System.Linq;
using Microsoft.Data.Sqlite;
using Drx.Sdk.Network.DataBase.Sqlite.V2;
using Drx.Sdk.Network.V2.Web;
using Drx.Sdk.Shared;
using KaxSocket.Model;

namespace KaxSocket;

public static class KaxGlobal
{
    public static readonly Sqlite<UserData> UserDatabase = new Sqlite<UserData>("kax_users.db", AppDomain.CurrentDomain.BaseDirectory);
    public static readonly Sqlite<CdkModel> CdkDatabase = new Sqlite<CdkModel>("cdk.db", AppDomain.CurrentDomain.BaseDirectory);
    public static readonly Sqlite<AssetModel> AssetDataBase = new Sqlite<AssetModel>("assets.db", AppDomain.CurrentDomain.BaseDirectory);

    public static async Task BanUser(string userName, string reason, long durationSeconds)
    {
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user != null && !user.Status.IsBanned)
        {
            user.Status.IsBanned = true;
            user.Status.BannedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            user.Status.BanExpiresAt = user.Status.BannedAt + durationSeconds;
            user.Status.BanReason = reason;
            await UserDatabase.UpdateAsync(user);
            Logger.Info($"已封禁用户 {user.UserName}，原因：{reason}，持续时间：{durationSeconds} 秒。");
        }
    }

    public static async Task UnBanUser(string userName)
    {
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user != null && user.Status.IsBanned)
        {
            user.Status.IsBanned = false;
            user.Status.BanExpiresAt = 0;
            user.Status.BanReason = string.Empty;
            await UserDatabase.UpdateAsync(user);
            Logger.Info($"已解除用户 {user.UserName} 的封禁状态。");
        }
    }

    
    public static string GenerateLoginToken(UserData user)
    {
        return JwtHelper.GenerateToken(user.Id.ToString(), user.UserName, user.Email);
    }

    public static ClaimsPrincipal ValidateToken(string token)
    {
        return JwtHelper.ValidateToken(token);
    }


    public static async Task<bool> IsUserBanned(string userName)
    {
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user != null)
        {
            Logger.Info($"检查用户 {userName} 的封禁状态: 状态={user.Status.IsBanned}, 到期时间={user.Status.BanExpiresAt}, 当前时间={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}, 原因={user.Status.BanReason ?? "无"}");

            if (user.Status.IsBanned)
            {
                // 检查封禁是否已过期
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Logger.Info($"当前时间戳: {currentTime}, 用户封禁到期时间戳: {user.Status.BanExpiresAt}");
                if (user.Status.BanExpiresAt > 0 && currentTime >= user.Status.BanExpiresAt)
                {
                    KaxGlobal.UnBanUser(userName).Wait();
                    Logger.Info($"用户 {user.UserName} 的封禁已过期，已自动解除封禁。");
                    return false;
                }
                return true;
            }
        }
        else
        {
            Logger.Warn($"检查封禁状态时未找到用户：{userName}");
        }
        return false;
    }

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

    public static async Task AddActiveAssetToUser(string userName, int assetId, long durationSeconds)
    {
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user != null)
        {
            // 检查资源数据库中是否存在该资源id
            var asset = (await AssetDataBase.SelectWhereAsync("Id", assetId)).FirstOrDefault();
            if (asset == null)
            {
                Logger.Warn($"为用户 {user.UserName} 激活资源时未找到资源 ID {assetId}。");
                return;
            }
            var activeAsset = new ActiveAssets
            {
                AssetId = assetId,
                ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExpiresAt = durationSeconds > 0 ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() + durationSeconds : 0
            };
            user.ActiveAssets.Add(activeAsset);
            await UserDatabase.UpdateAsync(user);
            Logger.Info($"已为用户 {user.UserName} 添加激活资源 ID {assetId}，持续时间 {durationSeconds} 秒。");
        }
    }

    public static async Task<bool> VerifyUserHasActiveAsset(string userName, int assetId)
    {
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user != null)
        {
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var hasActiveAsset = user.ActiveAssets.Any(a => a.AssetId == assetId && (a.ExpiresAt == 0 || a.ExpiresAt > currentTime));
            Logger.Info($"验证用户 {user.UserName} 是否拥有激活资源 ID {assetId}：{hasActiveAsset}");
            return hasActiveAsset;
        }
        Logger.Warn($"验证激活资源时未找到用户：{userName}");
        return false;
    }

    /// <summary>
    /// 内部方法：安全地删除 ActiveAssets 子表中的过期记录
    /// 通过框架提供的参数化 API 防止注入攻击
    /// </summary>
    private static async Task<int> DeleteExpiredActiveAssetsAsync()
    {
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var childTable = "UserData_ActiveAssets";
        using var conn = new SqliteConnection(UserDatabase.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM [{childTable}] WHERE [ExpiresAt] <= @now";
        cmd.Parameters.AddWithValue("@now", currentTime);
        var deletedCount = await cmd.ExecuteNonQueryAsync();
        return deletedCount;
    }

    /// <summary>
    /// 内部方法：安全地删除 ActiveAssets 中对应资源不存在的记录
    /// 使用框架 API（先读取 AssetDB 的 id 列，再用参数化的 NOT IN 删除），避免跨 DB 的子查询
    /// </summary>
    private static async Task<int> DeleteInvalidActiveAssetsAsync()
    {
        var assets = await AssetDataBase.SelectAllAsync();
        var assetIds = assets.Select(a => a.Id).ToArray();
        if (assetIds.Length == 0) return 0;

        var childTable = "UserData_ActiveAssets";
        var placeholders = string.Join(",", assetIds.Select((_, i) => $"@id{i}"));
        var sql = $"DELETE FROM [{childTable}] WHERE [AssetId] NOT IN ({placeholders})";

        using var conn = new SqliteConnection(UserDatabase.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        for (int i = 0; i < assetIds.Length; i++) cmd.Parameters.AddWithValue($"@id{i}", assetIds[i]);
        var deletedCount = await cmd.ExecuteNonQueryAsync();
        return deletedCount;
    }

    /// <summary>
    /// 内部方法：安全地删除 ActiveAssets 中对应资源已被标记为删除的记录
    /// 使用框架 API（先从 AssetDB 获取已删除的 id 列，再按 IN 删除）
    /// </summary>
    private static async Task<int> DeleteMarkedDeletedAssetsAsync()
    {
        var deletedAssets = await AssetDataBase.SelectWhereAsync("IsDeleted", true);
        var deletedIds = deletedAssets.Select(a => a.Id).ToArray();
        if (deletedIds.Length == 0) return 0;

        var childTable = "UserData_ActiveAssets";
        var placeholders = string.Join(",", deletedIds.Select((_, i) => $"@id{i}"));
        var sql = $"DELETE FROM [{childTable}] WHERE [AssetId] IN ({placeholders})";

        using var conn = new SqliteConnection(UserDatabase.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        for (int i = 0; i < deletedIds.Length; i++) cmd.Parameters.AddWithValue($"@id{i}", deletedIds[i]);
        var deletedCount = await cmd.ExecuteNonQueryAsync();
        return deletedCount;
    }

    public static async Task CleanUpExpiredAssets()
    {
        // 清理过期激活资源：使用内部构建的安全命令
        var deletedCount = await DeleteExpiredActiveAssetsAsync();
        if (deletedCount > 0)
        {
            Logger.Info($"已清理 {deletedCount} 条过期激活资源。");
        }
    }

    public static async Task CleanNotFoundAssets()
    {
        // 清理对应资源不存在的激活资源：使用内部构建的安全命令
        var deletedCount = await DeleteInvalidActiveAssetsAsync();
        if (deletedCount > 0)
        {
            Logger.Info($"已清理 {deletedCount} 条无效激活资源（对应资源已不存在）。");
        }
    }

    public static async Task CleanUpDeletedAssets()
    {
        // 清理对应资源已删除的激活资源：使用内部构建的安全命令
        var deletedCount = await DeleteMarkedDeletedAssetsAsync();
        if (deletedCount > 0)
        {
            Logger.Info($"已清理 {deletedCount} 条已删除激活资源。");
        }
    }

    public static async Task CleanUpAssets()
    {
        await CleanUpExpiredAssets();
        await CleanNotFoundAssets();
        await CleanUpDeletedAssets();
    }
}
