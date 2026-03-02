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
        if (user == null)
        {
            Logger.Warn($"封禁失败：未找到用户 {userName}。");
            Console.WriteLine($"封禁失败：未找到用户 {userName}。");
            return;
        }

        user.Status.IsBanned = true;
        user.Status.BannedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // durationSeconds == 0 表示永久封禁，BanExpiresAt 保持 0；否则设置到期时间戳
        user.Status.BanExpiresAt = durationSeconds == 0 ? 0 : user.Status.BannedAt + durationSeconds;
        user.Status.BanReason = reason;
        await UserDatabase.UpdateAsync(user);
        var expireDesc = durationSeconds == 0 ? "永久" : $"{durationSeconds} 秒";
        Logger.Info($"已封禁用户 {user.UserName}，原因：{reason}，持续时间：{expireDesc}。");
        Console.WriteLine($"已封禁用户 {user.UserName}，原因：{reason}，持续时间：{expireDesc}。");
    }

    public static async Task UnBanUser(string userName)
    {
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null)
        {
            Logger.Warn($"解封失败：未找到用户 {userName}。");
            Console.WriteLine($"解封失败：未找到用户 {userName}。");
            return;
        }

        if (!user.Status.IsBanned)
        {
            Console.WriteLine($"用户 {userName} 当前未被封禁。");
            return;
        }

        user.Status.IsBanned = false;
        user.Status.BanExpiresAt = 0;
        user.Status.BanReason = string.Empty;
        await UserDatabase.UpdateAsync(user);
        Logger.Info($"已解除用户 {user.UserName} 的封禁状态。");
        Console.WriteLine($"已解除用户 {user.UserName} 的封禁状态。");
    }

    public static async Task<bool> IsUserBanned(string userName)
    {
        var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null)
        {
            Logger.Warn($"检查封禁状态时未找到用户：{userName}");
            return false;
        }

        return await IsUserBanned(user).ConfigureAwait(false);
    }

    public static async Task<bool> IsUserBanned(UserData user)
    {
        if (!user.Status.IsBanned)
        {
            return false;
        }

        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Logger.Info($"当前时间戳: {currentTime}, 用户封禁到期时间戳: {user.Status.BanExpiresAt}");
        if (user.Status.BanExpiresAt > 0 && currentTime >= user.Status.BanExpiresAt)
        {
            user.Status.IsBanned = false;
            user.Status.BanExpiresAt = 0;
            user.Status.BanReason = string.Empty;
            await UserDatabase.UpdateAsync(user).ConfigureAwait(false);
            Logger.Info($"用户 {user.UserName} 的封禁已过期，已自动解除封禁。");
            return false;
        }

        return true;
    }
}
