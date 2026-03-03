using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Drx.Sdk.Shared;
using KaxSocket.Handlers.Helpers;

namespace KaxSocket;

/// <summary>
/// KaxGlobal 扩展：密码重置令牌服务。
/// 提供高熵令牌签发、校验、消费与过期记录清理能力。
/// 明文令牌仅出现在邮件 URL 中，数据库仅存 hash/salt。
/// </summary>
public static partial class KaxGlobal
{
    // -----------------------------------------------------------------
    // 常量
    // -----------------------------------------------------------------

    /// <summary>重置令牌有效期（毫秒），默认 10 分钟。</summary>
    private const long PasswordResetTokenTtlMs = 10 * 60 * 1000L;

    /// <summary>发送冷却时间（毫秒），30 秒。</summary>
    private const long PasswordResetCooldownMs = 30 * 1000L;

    /// <summary>每小时最大申请次数。</summary>
    private const int PasswordResetMaxPerHour = 10;

    /// <summary>每日最大申请次数。</summary>
    private const int PasswordResetMaxPerDay = 20;

    // -----------------------------------------------------------------
    // 令牌签发
    // -----------------------------------------------------------------

    /// <summary>
    /// 为指定用户签发一次性密码重置令牌。
    /// </summary>
    /// <param name="userId">用户 Id</param>
    /// <param name="ip">请求来源 IP（用于审计）</param>
    /// <param name="ua">请求来源 User-Agent（用于审计）</param>
    /// <returns>明文令牌字符串（仅此一次，调用方负责写入邮件）</returns>
    public static async Task<(string? rawToken, PasswordResetToken? record, string? error)>
        IssuePasswordResetTokenAsync(int userId, string ip, string ua)
    {
        var user = await UserDatabase.SelectByIdAsync(userId);
        if (user == null) return (null, null, "user_not_found");

        EnsurePasswordResetTokenStore(user);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 读取最近的 pending 记录，用于频控窗口状态延续
        var latestPending = user.PasswordResetTokens
            .Where(t => t.Status == "pending")
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefault();

        // 冷却检查
        if (latestPending != null && (now - latestPending.LastSendAt) < PasswordResetCooldownMs)
        {
            var remaining = (int)Math.Ceiling((PasswordResetCooldownMs - (now - latestPending.LastSendAt)) / 1000.0);
            return (null, null, $"cooldown:{remaining}");
        }

        // 频控窗口（复用已有记录的窗口起点，否则新建）
        int hourlyCount = 0, dailyCount = 0;
        long windowHourStart = 0, windowDayStart = 0;

        if (latestPending != null)
        {
            // 继承窗口
            var h = latestPending;
            hourlyCount = IsInSameHourWindow(h.WindowHourStartAt, now) ? h.HourlyCount : 0;
            dailyCount = IsInSameDayWindow(h.WindowDayStartAt, now) ? h.DailyCount : 0;
            windowHourStart = IsInSameHourWindow(h.WindowHourStartAt, now) ? h.WindowHourStartAt : now;
            windowDayStart = IsInSameDayWindow(h.WindowDayStartAt, now) ? h.WindowDayStartAt : now;
        }
        else
        {
            windowHourStart = now;
            windowDayStart = now;
        }

        if (hourlyCount >= PasswordResetMaxPerHour)
            return (null, null, "rate_limit_hour");
        if (dailyCount >= PasswordResetMaxPerDay)
            return (null, null, "rate_limit_day");

        // 旧的 pending 令牌全部作废
        foreach (var old in user.PasswordResetTokens.Where(t => t.Status == "pending").ToList())
        {
            old.Status = "expired";
            old.UpdatedAt = now;
            user.PasswordResetTokens.Update(old);
        }

        // 生成高熵明文令牌（32 字节 = 256 位，Base64Url 编码）
        var rawBytes = new byte[32];
        RandomNumberGenerator.Fill(rawBytes);
        var rawToken = Convert.ToBase64String(rawBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        // 存储 hash/salt
        var salt = Api.GenerateVerificationSalt(16);
        var hash = HashToken(rawToken, salt);

        var record = new PasswordResetToken
        {
            Id = Guid.NewGuid().ToString(),
            ParentId = userId,
            TokenHash = hash,
            TokenSalt = salt,
            Status = "pending",
            Attempts = 0,
            MaxAttempts = 5,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now + PasswordResetTokenTtlMs,
            UsedAt = 0,
            RequestIp = ip ?? string.Empty,
            UserAgent = ua ?? string.Empty,
            LastSendAt = now,
            HourlyCount = hourlyCount + 1,
            DailyCount = dailyCount + 1,
            WindowHourStartAt = windowHourStart,
            WindowDayStartAt = windowDayStart
        };

        user.PasswordResetTokens.Add(record);
        await UserDatabase.UpdateAsync(user);

        return (rawToken, record, null);
    }

    // -----------------------------------------------------------------
    // 令牌校验（预检，不消费）
    // -----------------------------------------------------------------

    /// <summary>
    /// 校验重置令牌是否有效（未过期、未使用、未锁定），并返回关联用户。
    /// 不消费令牌、不更新 Attempts。
    /// </summary>
    public static async Task<(UserData? user, PasswordResetToken? token, string? error)>
        ValidatePasswordResetTokenAsync(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return (null, null, "invalid_token");

        var allUsers = await UserDatabase.SelectAllAsync();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var user in allUsers)
        {
            EnsurePasswordResetTokenStore(user);
            var match = user.PasswordResetTokens
                .Where(t => t.Status == "pending")
                .FirstOrDefault(t => HashToken(rawToken, t.TokenSalt) == t.TokenHash);

            if (match == null) continue;

            if (now > match.ExpiresAt)
            {
                // 惰性更新过期状态
                match.Status = "expired";
                match.UpdatedAt = now;
                user.PasswordResetTokens.Update(match);
                await UserDatabase.UpdateAsync(user);
                return (null, null, "token_expired");
            }

            if (match.Attempts >= match.MaxAttempts)
                return (null, null, "token_locked");

            return (user, match, null);
        }

        return (null, null, "invalid_token");
    }

    // -----------------------------------------------------------------
    // 令牌消费（提交阶段）
    // -----------------------------------------------------------------

    /// <summary>
    /// 消费重置令牌：校验令牌、更新密码、置令牌为 used，并更新会话失效基线。
    /// </summary>
    public static async Task<(bool success, string? error)>
        ConsumePasswordResetTokenAsync(string rawToken, string newPasswordHash)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var allUsers = await UserDatabase.SelectAllAsync();
        foreach (var user in allUsers)
        {
            EnsurePasswordResetTokenStore(user);
            var match = user.PasswordResetTokens
                .Where(t => t.Status == "pending")
                .FirstOrDefault(t => HashToken(rawToken, t.TokenSalt) == t.TokenHash);

            if (match == null) continue;

            // 累加尝试次数（防暴力枚举）
            match.Attempts++;
            match.UpdatedAt = now;

            if (now > match.ExpiresAt)
            {
                match.Status = "expired";
                user.PasswordResetTokens.Update(match);
                await UserDatabase.UpdateAsync(user);
                return (false, "token_expired");
            }

            if (match.Attempts > match.MaxAttempts)
            {
                match.Status = "locked";
                user.PasswordResetTokens.Update(match);
                await UserDatabase.UpdateAsync(user);
                return (false, "token_locked");
            }

            // 令牌消费
            match.Status = "used";
            match.UsedAt = now;
            user.PasswordResetTokens.Update(match);

            // 更新密码
            user.PasswordHash = newPasswordHash;

            // 更新会话失效基线（使所有旧 token 全部失效）
            user.TokenInvalidBefore = now;

            await UserDatabase.UpdateAsync(user);
            return (true, null);
        }

        return (false, "invalid_token");
    }

    // -----------------------------------------------------------------
    // 清理过期记录
    // -----------------------------------------------------------------

    /// <summary>
    /// 清理所有用户中超过保留期的过期/已使用密码重置令牌。
    /// 建议在 DoTicker 定时任务中调用。
    /// </summary>
    public static async Task CleanUpPasswordResetTokensAsync()
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            // 已使用/过期记录保留 24 小时后清理
            var retentionMs = 24 * 60 * 60 * 1000L;

            var allUsers = await UserDatabase.SelectAllAsync();
            foreach (var user in allUsers)
            {
                if (user.PasswordResetTokens == null || user.PasswordResetTokens.Count == 0)
                    continue;

                var toRemove = user.PasswordResetTokens
                    .Where(t => t.Status is "used" or "expired" or "locked"
                                && (now - t.UpdatedAt) > retentionMs)
                    .ToList();

                if (toRemove.Count == 0) continue;

                foreach (var r in toRemove)
                    user.PasswordResetTokens.Remove(r);

                await UserDatabase.UpdateAsync(user);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"CleanUpPasswordResetTokensAsync 失败: {ex.Message}");
        }
    }

    // -----------------------------------------------------------------
    // 私有工具
    // -----------------------------------------------------------------

    private static string HashToken(string rawToken, string salt)
    {
        var input = $"{rawToken}:{salt}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static bool IsInSameHourWindow(long windowStart, long now)
        => windowStart > 0 && (now - windowStart) < 3600_000L;

    private static bool IsInSameDayWindow(long windowStart, long now)
        => windowStart > 0 && (now - windowStart) < 86400_000L;
}
