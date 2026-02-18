using System;
using System.IO;
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
    public static readonly SqliteV2<UserData> UserDatabase = new SqliteV2<UserData>("kax_users.db", AppDomain.CurrentDomain.BaseDirectory);
    public static readonly SqliteV2<CdkModel> CdkDatabase = new SqliteV2<CdkModel>("cdk.db", AppDomain.CurrentDomain.BaseDirectory);
    public static readonly SqliteV2<AssetModel> AssetDataBase = new SqliteV2<AssetModel>("assets.db", AppDomain.CurrentDomain.BaseDirectory);

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

    /// <summary>
    /// 通过用户 Id 返回已存在的头像文件的本地路径（优先 .png，其次 .jpg）。
    /// 未找到返回 null。路径位于 {AppBase}/resources/user/icon/{uid}.png|jpg
    /// </summary>
    /// <param name="userId">用户 Id</param>
    /// <returns>本地文件绝对路径或 null</returns>
    public static string? GetUserAvatarPathById(int userId)
    {
        try
        {
            var iconsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "user", "icon");
            var png = Path.Combine(iconsDir, $"{userId}.png");
            var jpg = Path.Combine(iconsDir, $"{userId}.jpg");
            if (File.Exists(png)) return png;
            if (File.Exists(jpg)) return jpg;
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warn($"GetUserAvatarPathById({userId}) 读取失败: {ex.Message}");
            return null;
        }
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
            
            // 使用新的 ActiveAssets 数据模型（TableList），所有时间戳使用毫秒单位
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var activeAsset = new ActiveAssets
            {
                AssetId = assetId,
                ActivatedAt = now,
                ExpiresAt = durationSeconds > 0 ? now + (durationSeconds * 1000) : 0  // 秒转毫秒
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
            // 使用毫秒时间戳进行过期检查
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var hasActiveAsset = user.ActiveAssets.Any(a => a.AssetId == assetId && (a.ExpiresAt == 0 || a.ExpiresAt > currentTime));
            Logger.Info($"验证用户 {user.UserName} 是否拥有激活资源 ID {assetId}：{hasActiveAsset}");
            return hasActiveAsset;
        }
        Logger.Warn($"验证激活资源时未找到用户：{userName}");
        return false;
    }

    /// <summary>
    /// 公共 API：获取用户指定 asset 的原始激活记录（activatedAt / expiresAt）。
    /// 如果未找到返回 null（调用方可据此判断“未拥有”或“无记录”）。
    /// </summary>
    public static async Task<ActiveAssets?> GetUserActiveAssetRawAsync(string userName, int assetId)
    {
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null)
        {
            Logger.Warn($"GetUserActiveAssetRawAsync: 未找到用户：{userName}");
            return null;
        }
        var entry = user.ActiveAssets.FirstOrDefault(a => a.AssetId == assetId);
        if (entry == null)
        {
            Logger.Info($"GetUserActiveAssetRawAsync: 用户 {userName} 没有资产 {assetId} 的激活记录。");
            return null;
        }
        return entry;
    }

    /// <summary>
    /// 公共 API：返回用户指定 asset 的剩余秒数。
    /// 返回值含义：
    /// - null ：用户没有该资产的激活记录
    /// - -1   ：永久有效（永不过期）
    /// - 0    ：已过期（或刚好到期）
    /// - >0   ：剩余秒数
    /// </summary>
    public static async Task<long?> GetUserAssetRemainingSecondsAsync(string userName, int assetId)
    {
        var entry = await GetUserActiveAssetRawAsync(userName, assetId);
        if (entry == null) return null;
        if (entry.ExpiresAt == 0) return -1L; // 永久有效
        
        // 使用毫秒时间戳计算剩余秒数
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var remainingMs = entry.ExpiresAt - currentTime;
        var remainingSeconds = remainingMs / 1000;  // 转换为秒
        return remainingSeconds > 0 ? remainingSeconds : 0L;
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
        // 仅删除有明确过期时间且已到期的记录，ExpiresAt == 0 表示永久有效，不能被视为已过期
        cmd.CommandText = $"DELETE FROM [{childTable}] WHERE [ExpiresAt] > 0 AND [ExpiresAt] <= @now";
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

    /// <summary>
    /// 公共 API：返回指定用户的 CDK 已被使用的数量（UsedBy == userName）
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
    /// 公共 API：返回用户的资源计数（来自 UserData.ResourceCount）
    /// </summary>
    public static async Task<int> GetUserResourceCountAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return 0;
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        return user?.ResourceCount ?? 0;
    }

    /// <summary>
    /// 公共 API：返回用户的贡献值（来自 UserData.Contribution）
    /// </summary>
    public static async Task<int> GetUserContributionAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return 0;
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        return user?.Contribution ?? 0;
    }

    /// <summary>
    /// 公共 API：返回用户的最近活动计数（来自 UserData.RecentActivity）
    /// </summary>
    public static async Task<int> GetUserRecentActivityAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return 0;
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        return user?.RecentActivity ?? 0;
    }

    /// <summary>
    /// 公共 API：激活 CDK 并为用户添加对应资源
    /// 返回值：
    /// - (0, "成功激活 CDK") - 激活成功
    /// - (1, "CDK为空") - CDK 代码为空
    /// - (2, "CDK错误") - CDK 不存在
    /// - (3, "CDK已使用") - CDK 已被使用
    /// - (500, "服务器错误") - 激活过程中发生错误
    /// </summary>
    public static async Task<(int code, string message)> ActivateCdkAsync(string cdkCode, string userName)
    {
        if (string.IsNullOrWhiteSpace(cdkCode))
        {
            return (1, "CDK为空");
        }

        if (string.IsNullOrWhiteSpace(userName))
        {
            return (1, "用户名为空");
        }

        try
        {
            // 规范化 CDK 代码（大写）
            var normalizedCode = cdkCode.Trim().ToUpperInvariant();

            // 查询 CDK 是否存在（大小写不敏感）
            var all = await CdkDatabase.SelectAllAsync();
            var cdk = all.FirstOrDefault(c => string.Equals(c.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));

            if (cdk == null)
            {
                Logger.Warn($"用户 {userName} 尝试激活不存在的 CDK: {normalizedCode}");
                return (2, "CDK错误");
            }

            // 检查 CDK 是否已被使用
            if (cdk.IsUsed)
            {
                Logger.Warn($"用户 {userName} 尝试激活已使用的 CDK: {cdk.Code}（已被 {cdk.UsedBy} 于 {cdk.UsedAt} 激活）");
                return (3, "CDK已使用");
            }

            // 检查用户是否存在
            var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
            if (user == null)
            {
                Logger.Warn($"尝试为不存在的用户激活 CDK: {userName}");
                return (500, "用户不存在");
            }

            // 激活 CDK：标记为已使用，记录激活信息
            cdk.IsUsed = true;
            cdk.UsedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            cdk.UsedBy = userName;
            await CdkDatabase.UpdateAsync(cdk);

            // 如果 CDK 关联了资源，添加到用户的活跃资源中（使用 CDK 的 ExpiresInSeconds，0 表示永久有效）
            if (cdk.AssetId > 0)
            {
                await AddActiveAssetToUser(userName, cdk.AssetId, cdk.ExpiresInSeconds);
            }

            // 如果 CDK 有贡献值，添加到用户的贡献值中（可选功能，暂未实现）
            if (cdk.ContributionValue > 0)
            {
                user.Contribution += cdk.ContributionValue;
                await UserDatabase.UpdateAsync(user);
                Logger.Info($"用户 {userName} 激活CDK后增加贡献值 {cdk.ContributionValue}");
            }

            Logger.Info($"用户 {userName} 成功激活 CDK {cdk.Code}（关联资源: {cdk.AssetId}, 贡献值: {cdk.ContributionValue}）");
            return (0, "成功激活 CDK");
        }
        catch (Exception ex)
        {
            Logger.Error($"激活 CDK 失败（用户: {userName}, CDK: {cdkCode}）: {ex.Message}, {ex.StackTrace}");
            return (500, "服务器错误");
        }
    }

    /// <summary>
    /// 公共 API：校验 CDK 是否有效（检查存在性和使用状态）
    /// 返回值 true：CDK 有效且未使用；false：CDK 无效或已使用
    /// </summary>
    public static async Task<bool> ValidateCdkAsync(string cdkCode)
    {
        if (string.IsNullOrWhiteSpace(cdkCode))
        {
            return false;
        }

        try
        {
            var normalizedCode = cdkCode.Trim().ToUpperInvariant();
            var all = await CdkDatabase.SelectAllAsync();
            var cdk = all.FirstOrDefault(c => string.Equals(c.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));

            if (cdk == null || cdk.IsUsed)
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"校验 CDK 失败 ({cdkCode}): {ex.Message}");
            return false;
        }
    }
}

