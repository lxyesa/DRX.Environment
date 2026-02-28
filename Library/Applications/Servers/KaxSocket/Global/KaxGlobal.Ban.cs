using System;
using System.Linq;
using Drx.Sdk.Shared;
using KaxSocket.Model;

namespace KaxSocket;

/// <summary>
/// KaxGlobal 用户封禁管理：封禁、解封、检查封禁状态。
/// </summary>
public static partial class KaxGlobal
{
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

    public static async Task<bool> IsUserBanned(string userName)
    {
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user != null)
        {
            if (user.Status.IsBanned)
            {
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
}
