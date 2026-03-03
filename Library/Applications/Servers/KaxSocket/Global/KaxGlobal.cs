using System;
using System.IO;
using System.Security.Claims;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Drx.Sdk.Network.DataBase;
using Drx.Sdk.Shared;
using KaxSocket.Model;
using Drx.Sdk.Network.Http.Auth;

namespace KaxSocket;

/// <summary>
/// KaxGlobal 主文件：持有全局静态数据库实例以及 Token、头像基础方法。
/// </summary>
public static partial class KaxGlobal
{
    public static readonly SqliteV2<UserData> UserDatabase = new SqliteV2<UserData>("kax_users.db", AppDomain.CurrentDomain.BaseDirectory);
    public static readonly SqliteV2<CdkModel> CdkDatabase = new SqliteV2<CdkModel>("cdk.db", AppDomain.CurrentDomain.BaseDirectory);
    public static readonly SqliteV2<AssetModel> AssetDataBase = new SqliteV2<AssetModel>("assets.db", AppDomain.CurrentDomain.BaseDirectory);

    public static string GenerateLoginToken(UserData user)
    {
        return JwtHelper.GenerateToken(user.Id.ToString(), user.UserName, user.Email);
    }

    public static ClaimsPrincipal ValidateToken(string token)
    {
        return JwtHelper.ValidateToken(token);
    }

    /// <summary>
    /// 确保用户的邮箱变更验证码子表已初始化。
    /// </summary>
    public static void EnsureEmailChangeVerificationStore(UserData user)
    {
        if (user.EmailChangeVerifications == null)
        {
            user.EmailChangeVerifications = new TableList<EmailChangeVerification>();
        }
    }

    /// <summary>
    /// 获取指定用户的邮箱变更验证码记录列表（若未初始化则返回空列表）。
    /// </summary>
    public static async Task<List<EmailChangeVerification>> GetEmailChangeVerificationsAsync(int userId)
    {
        var user = await UserDatabase.SelectByIdAsync(userId);
        if (user == null) return new List<EmailChangeVerification>();

        EnsureEmailChangeVerificationStore(user);
        return user.EmailChangeVerifications.ToList();
    }

    /// <summary>
    /// 为指定用户新增一条邮箱变更验证码记录并持久化。
    /// </summary>
    public static async Task<bool> AddEmailChangeVerificationAsync(int userId, EmailChangeVerification record)
    {
        var user = await UserDatabase.SelectByIdAsync(userId);
        if (user == null) return false;

        EnsureEmailChangeVerificationStore(user);
        record.ParentId = user.Id;
        user.EmailChangeVerifications.Add(record);
        await UserDatabase.UpdateAsync(user);
        return true;
    }

    /// <summary>
    /// 更新指定用户已有验证码记录并持久化。
    /// </summary>
    public static async Task<bool> UpdateEmailChangeVerificationAsync(int userId, EmailChangeVerification record)
    {
        var user = await UserDatabase.SelectByIdAsync(userId);
        if (user == null) return false;

        EnsureEmailChangeVerificationStore(user);
        var existing = user.EmailChangeVerifications.FirstOrDefault(x => x.Id == record.Id);
        if (existing == null) return false;

        record.ParentId = user.Id;
        user.EmailChangeVerifications.Update(record);
        await UserDatabase.UpdateAsync(user);
        return true;
    }

    /// <summary>
    /// 提交邮箱变更与验证码状态更新。
    /// 说明：调用 UserDatabase.UpdateAsync 时会在同一数据库事务内同步主表与 TableList 子表变更。
    /// </summary>
    public static async Task<bool> CommitEmailChangeAsync(UserData user, string newEmail)
    {
        if (user == null || string.IsNullOrWhiteSpace(newEmail)) return false;

        EnsureEmailChangeVerificationStore(user);
        user.Email = newEmail.Trim();
        await UserDatabase.UpdateAsync(user);
        return true;
    }

    /// <summary>
    /// 确保用户的密码重置令牌子表已初始化。
    /// </summary>
    public static void EnsurePasswordResetTokenStore(UserData user)
    {
        if (user.PasswordResetTokens == null)
        {
            user.PasswordResetTokens = new TableList<PasswordResetToken>();
        }
    }

    /// <summary>
    /// 为指定用户新增一条密码重置令牌记录并持久化。
    /// </summary>
    public static async Task<bool> AddPasswordResetTokenAsync(int userId, PasswordResetToken record)
    {
        var user = await UserDatabase.SelectByIdAsync(userId);
        if (user == null) return false;

        EnsurePasswordResetTokenStore(user);
        record.ParentId = user.Id;
        user.PasswordResetTokens.Add(record);
        await UserDatabase.UpdateAsync(user);
        return true;
    }

    /// <summary>
    /// 更新指定用户密码重置令牌记录并持久化。
    /// </summary>
    public static async Task<bool> UpdatePasswordResetTokenAsync(int userId, PasswordResetToken record)
    {
        var user = await UserDatabase.SelectByIdAsync(userId);
        if (user == null) return false;

        EnsurePasswordResetTokenStore(user);
        var existing = user.PasswordResetTokens.FirstOrDefault(x => x.Id == record.Id);
        if (existing == null) return false;

        record.ParentId = user.Id;
        user.PasswordResetTokens.Update(record);
        await UserDatabase.UpdateAsync(user);
        return true;
    }

    /// <summary>
    /// 通过用户 Id 返回已存在的头像文件的本地路径（优先 .png，其次 .jpg）。
    /// 未找到返回 null。路径位于 {AppBase}/resources/user/icon/{uid}.png|jpg
    /// </summary>
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
}
