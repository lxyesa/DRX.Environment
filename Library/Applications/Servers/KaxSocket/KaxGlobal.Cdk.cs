using System;
using System.Linq;
using Drx.Sdk.Shared;
using KaxSocket.Model;

namespace KaxSocket;

/// <summary>
/// KaxGlobal CDK 管理：激活 CDK、校验 CDK 有效性。
/// </summary>
public static partial class KaxGlobal
{
    /// <summary>
    /// 激活 CDK 并为用户添加对应资源。
    /// 返回值：(0,"成功激活 CDK") | (1,"CDK为空/用户名为空") | (2,"CDK错误") | (3,"CDK已使用") | (500,"服务器错误")
    /// </summary>
    public static async Task<(int code, string message)> ActivateCdkAsync(string cdkCode, string userName)
    {
        if (string.IsNullOrWhiteSpace(cdkCode))
            return (1, "CDK为空");

        if (string.IsNullOrWhiteSpace(userName))
            return (1, "用户名为空");

        try
        {
            var normalizedCode = cdkCode.Trim().ToUpperInvariant();

            var all = await CdkDatabase.SelectAllAsync();
            var cdk = all.FirstOrDefault(c => string.Equals(c.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));

            if (cdk == null)
            {
                Logger.Warn($"用户 {userName} 尝试激活不存在的 CDK: {normalizedCode}");
                return (2, "CDK错误");
            }

            if (cdk.IsUsed)
            {
                Logger.Warn($"用户 {userName} 尝试激活已使用的 CDK: {cdk.Code}（已被 {cdk.UsedBy} 于 {cdk.UsedAt} 激活）");
                return (3, "CDK已使用");
            }

            var user = (await UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
            if (user == null)
            {
                Logger.Warn($"尝试为不存在的用户激活 CDK: {userName}");
                return (500, "用户不存在");
            }

            cdk.IsUsed = true;
            cdk.UsedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            cdk.UsedBy = userName;
            await CdkDatabase.UpdateAsync(cdk);

            if (cdk.AssetId > 0)
                await AddActiveAssetToUser(userName, cdk.AssetId, cdk.ExpiresInSeconds);

            if (cdk.GoldValue > 0)
            {
                user.Gold += cdk.GoldValue;
                await UserDatabase.UpdateAsync(user);
                Logger.Info($"用户 {userName} 激活CDK后增加金币 {cdk.GoldValue}");
            }

            Logger.Info($"用户 {userName} 成功激活 CDK {cdk.Code}（关联资源: {cdk.AssetId}, 金币: {cdk.GoldValue}）");
            return (0, "成功激活 CDK");
        }
        catch (Exception ex)
        {
            Logger.Error($"激活 CDK 失败（用户: {userName}, CDK: {cdkCode}）: {ex.Message}, {ex.StackTrace}");
            return (500, "服务器错误");
        }
    }

    /// <summary>
    /// 校验 CDK 是否有效（存在且未使用）。
    /// </summary>
    public static async Task<bool> ValidateCdkAsync(string cdkCode)
    {
        if (string.IsNullOrWhiteSpace(cdkCode))
            return false;

        try
        {
            var normalizedCode = cdkCode.Trim().ToUpperInvariant();
            var all = await CdkDatabase.SelectAllAsync();
            var cdk = all.FirstOrDefault(c => string.Equals(c.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));
            return cdk != null && !cdk.IsUsed;
        }
        catch (Exception ex)
        {
            Logger.Warn($"校验 CDK 失败 ({cdkCode}): {ex.Message}");
            return false;
        }
    }
}
