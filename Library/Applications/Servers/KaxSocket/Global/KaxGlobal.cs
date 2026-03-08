using System;
using System.IO;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
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
    private const string TokenUseClaim = "token_use";
    private const string TokenUseClient = "client";
    private const string TokenUseWeb = "web";
    private const string ClientHidClaim = "hid";
    private const string ClientSeedClaim = "client_seed";
    private const string DeviceNameClaim = "device_name";
    private const string DeviceOsClaim = "device_os";
    private static readonly TimeSpan ClientTokenLifetime = TimeSpan.FromDays(36500); // 业务上近似不过期

    public static readonly SqliteV2<UserData> UserDatabase = new SqliteV2<UserData>("kax_users.db", AppDomain.CurrentDomain.BaseDirectory);
    public static readonly SqliteV2<CdkModel> CdkDatabase = new SqliteV2<CdkModel>("cdk.db", AppDomain.CurrentDomain.BaseDirectory);
    public static readonly SqliteV2<AssetModel> AssetDataBase = new SqliteV2<AssetModel>("assets.db", AppDomain.CurrentDomain.BaseDirectory);
    public static readonly SqliteV2<AssetAuditLog> AssetAuditDatabase = new SqliteV2<AssetAuditLog>("asset_audit.db", AppDomain.CurrentDomain.BaseDirectory);

    public static string GenerateLoginToken(UserData user)
    {
        var hid = string.IsNullOrWhiteSpace(user.ClientHid) ? "legacy" : user.ClientHid;
        return GenerateClientToken(user, hid, string.Empty, string.Empty);
    }

    /// <summary>
    /// 生成客户端专用 JWT（每次登录重签、长期有效）。
    /// 携带 hid、device_name、device_os 声明，用于设备标识与安全展示。
    /// </summary>
    /// <param name="user">登录用户。</param>
    /// <param name="hid">设备硬件唯一标识。</param>
    /// <param name="deviceName">设备名称，如 DESKTOP-ABC123。</param>
    /// <param name="deviceOs">操作系统名称，如 Windows 11。</param>
    public static string GenerateClientToken(UserData user, string hid, string deviceName, string deviceOs)
    {
        var normalizedHid = (hid ?? string.Empty).Trim();
        var clientSeed = ComputeClientSeed(user.UserName ?? string.Empty, normalizedHid);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
            new Claim(TokenUseClaim, TokenUseClient),
            new Claim(ClientHidClaim, normalizedHid),
            new Claim(ClientSeedClaim, clientSeed),
            new Claim(DeviceNameClaim, (deviceName ?? string.Empty).Trim()),
            new Claim(DeviceOsClaim, (deviceOs ?? string.Empty).Trim())
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        claims.Add(new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));
        return JwtHelper.GenerateToken(claims, ClientTokenLifetime);
    }

    /// <summary>
    /// 生成 Web 专用 JWT（遵循默认过期时间）。
    /// </summary>
    public static string GenerateWebToken(UserData user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
            new Claim(TokenUseClaim, TokenUseWeb)
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        claims.Add(new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));
        return JwtHelper.GenerateToken(claims);
    }

    /// <summary>
    /// 解析登录后应发放的双 Token。
    /// 策略：
    ///   - 客户端登录：重签 client token（单客户端，旧客户端 token 立即失效）；Web token 另行签发。
    ///   - Web 登录：每次签发全新 web token（支持多 web 端同时在线）；不触碰 client token。
    /// </summary>
    /// <param name="user">登录用户。</param>
    /// <param name="isClientLogin">是否为客户端登录（type=client）。</param>
    /// <param name="hid">设备 HID（客户端登录时提供）。</param>
    /// <param name="deviceName">设备名称（客户端登录时提供）。</param>
    /// <param name="deviceOs">操作系统（客户端登录时提供）。</param>
    public static (string clientToken, string webToken, bool webTokenRotated) ResolveLoginTokens(
        UserData user, bool isClientLogin, string? hid, string? deviceName = null, string? deviceOs = null)
    {
        string clientToken;
        if (isClientLogin)
        {
            // 客户端登录：重签 client token，绑定新 hid，旧客户端会话因 user.ClientToken 精确匹配失败而失效。
            var normalizedHid = (hid ?? string.Empty).Trim();
            user.ClientHid = normalizedHid;
            clientToken = GenerateClientToken(user, normalizedHid, deviceName ?? string.Empty, deviceOs ?? string.Empty);
        }
        else
        {
            // Web 登录：保持 client token 不变，避免踢下已在线的客户端。
            clientToken = user.ClientToken ?? string.Empty;
        }

        // Web 多端并存：每次登录都签发全新的 web token，不复用旧 token。
        // GetUserAsync 不再做精确匹配校验，各 web 端各持自己的有效 token 即可。
        return (clientToken, GenerateWebToken(user), true);
    }

    public static ClaimsPrincipal ValidateToken(string token)
    {
        return JwtHelper.ValidateToken(token);
    }

    /// <summary>
    /// 检查用户现有 Web Token 是否仍可复用（未过期、用途正确、归属用户一致、未被失效基线淘汰）。
    /// </summary>
    private static bool TryReuseValidWebToken(UserData user, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(user.WebToken)) return false;

        var principal = JwtHelper.ValidateToken(user.WebToken);
        if (principal == null) return false;

        var tokenUse = principal.FindFirst(TokenUseClaim)?.Value;
        if (!string.Equals(tokenUse, TokenUseWeb, StringComparison.OrdinalIgnoreCase)) return false;

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.Equals(userId, user.Id.ToString(), StringComparison.Ordinal)) return false;

        // 若该 token 的签发时间早于失效基线，则不复用（避免返回一个 GetUserAsync 必然拒绝的旧 token）
        if (user.TokenInvalidBefore > 0)
        {
            var iatClaim = principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Iat)?.Value;
            if (long.TryParse(iatClaim, out var iatSeconds) && iatSeconds * 1000L < user.TokenInvalidBefore)
                return false;
        }

        token = user.WebToken;
        return true;
    }

    /// <summary>
    /// 根据用户名与设备 HID 计算客户端绑定种子。
    /// </summary>
    private static string ComputeClientSeed(string userName, string hid)
    {
        var value = $"{hid}:{userName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
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
