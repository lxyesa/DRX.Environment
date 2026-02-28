using System;
using System.Linq;
using Drx.Sdk.Shared;
using KaxSocket.Model;

namespace KaxSocket;

/// <summary>
/// KaxGlobal 资产管理：为用户新增/累加激活资产、校验资产有效性、查询资产原始记录及剩余时长。
/// </summary>
public static partial class KaxGlobal
{
    /// <summary>
    /// 为用户激活指定资产。
    /// 若用户已拥有该资产（记录存在），则将新的时长累加到剩余有效期上（基准为 max(当前时间, 现有到期时间)）；
    /// 若为永久资产（ExpiresAt == 0），累加无效，保持永久；
    /// 若用户尚未拥有该资产，则新建记录。
    /// durationSeconds == 0 表示永久有效。
    /// </summary>
    public static async Task AddActiveAssetToUser(string userName, int assetId, long durationSeconds)
    {
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return;

        var asset = (await AssetDataBase.SelectWhereAsync("Id", assetId)).FirstOrDefault();
        if (asset == null)
        {
            Logger.Warn($"为用户 {user.UserName} 激活资源时未找到资源 ID {assetId}。");
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var durationMs = durationSeconds * 1000;

        var existing = user.ActiveAssets.FirstOrDefault(a => a.AssetId == assetId);
        if (existing != null)
        {
            if (existing.ExpiresAt == 0)
            {
                Logger.Info($"用户 {user.UserName} 的资产 ID {assetId} 已是永久有效，无需更新。");
                return;
            }

            if (durationSeconds == 0)
            {
                existing.ExpiresAt = 0;
                existing.UpdatedAt = now;
                await UserDatabase.UpdateAsync(user);
                Logger.Info($"用户 {user.UserName} 的资产 ID {assetId} 已升级为永久有效。");
                return;
            }

            var baseTime = Math.Max(now, existing.ExpiresAt);
            existing.ExpiresAt = baseTime + durationMs;
            existing.UpdatedAt = now;
            await UserDatabase.UpdateAsync(user);
            Logger.Info($"用户 {user.UserName} 的资产 ID {assetId} 累加时长 {durationSeconds} 秒，新到期时间戳: {existing.ExpiresAt}。");
            return;
        }

        var activeAsset = new ActiveAssets
        {
            AssetId = assetId,
            ActivatedAt = now,
            ExpiresAt = durationSeconds > 0 ? now + durationMs : 0
        };

        user.ActiveAssets.Add(activeAsset);
        await UserDatabase.UpdateAsync(user);
        Logger.Info($"已为用户 {user.UserName} 新增激活资产 ID {assetId}，持续时间 {durationSeconds} 秒。");
    }

    public static async Task<bool> VerifyUserHasActiveAsset(string userName, int assetId)
    {
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user != null)
        {
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var hasActiveAsset = user.ActiveAssets.Any(a => a.AssetId == assetId && (a.ExpiresAt == 0 || a.ExpiresAt > currentTime));
            Logger.Info($"验证用户 {user.UserName} 是否拥有激活资源 ID {assetId}：{hasActiveAsset}");
            return hasActiveAsset;
        }
        Logger.Warn($"验证激活资源时未找到用户：{userName}");
        return false;
    }

    /// <summary>
    /// 获取用户指定 asset 的原始激活记录（activatedAt / expiresAt）。
    /// 如果未找到返回 null。
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
    /// 返回用户指定 asset 的剩余秒数。
    /// 返回值含义：null=无记录；-1=永久有效；0=已过期；>0=剩余秒数。
    /// </summary>
    public static async Task<long?> GetUserAssetRemainingSecondsAsync(string userName, int assetId)
    {
        var entry = await GetUserActiveAssetRawAsync(userName, assetId);
        if (entry == null) return null;
        if (entry.ExpiresAt == 0) return -1L;

        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var remainingMs = entry.ExpiresAt - currentTime;
        var remainingSeconds = remainingMs / 1000;
        return remainingSeconds > 0 ? remainingSeconds : 0L;
    }
}
