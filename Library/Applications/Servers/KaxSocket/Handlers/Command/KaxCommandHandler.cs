using System;
using Drx.Sdk.Network.V2.Web.Core;
using Drx.Sdk.Network.V2.Web.Http;
using Drx.Sdk.Shared;

namespace KaxSocket.Handlers.Command;

public class KaxCommandHandler
{
    
    [Command("unban <username>", "helper:解除用户封禁", "仅限开发者使用")]
    public static void Cmd_UnBanUser(string userName)
    {
        KaxGlobal.UnBanUser(userName).Wait();
    }

    [Command("ban <username> <reason> <duration>", "helper:封禁用户", "仅限开发者使用")]
    public static void Cmd_BanUser(string userName, string reason, long durationSeconds)
    {
        KaxGlobal.BanUser(userName, reason, durationSeconds).Wait();
    }

    [Command("setpermission <username> <group>", "helper:设置用户权限组", "仅限开发者使用")]
    public static void Cmd_SetUserPermissionGroup(string userName, int group)
    {
        if (!Enum.IsDefined(typeof(UserPermissionGroup), group))
        {
            Console.WriteLine($"无效的权限组: {group}");
            return;
        }

        KaxGlobal.SetUserPermissionGroup(userName, (UserPermissionGroup)group).Wait();
    }

    [Command("help", "helper:显示帮助信息", "所有用户可用")]
    public static void Cmd_Help()
    {
        Console.WriteLine("可用命令:");
        Console.WriteLine("  ban <username> <reason> <duration> - 封禁用户");
        Console.WriteLine("  unban <username> - 解除用户封禁");
        Console.WriteLine("  setpermission <username> <group> - 设置用户权限组");
        Console.WriteLine("    权限组: 0=Console, 1=Root, 2=Admin, 100=User");
        Console.WriteLine("  addasset <username> <assetId> <duration> - 为用户添加激活资源");
        Console.WriteLine("  help - 显示此帮助信息");
    }

    [Command("addasset <username> <assetId> <duration>", "helper:为用户添加激活资源", "仅限开发者使用")]
    public static void Cmd_AddAssetToUser(string userName, int assetId, long durationSeconds)
    {
        KaxGlobal.AddActiveAssetToUser(userName, assetId, durationSeconds).Wait();
    }

    [Command("usetactiveassetlist <username>", "helper:显示用户激活资源列表", "仅限开发者使用")]
    public static void Cmd_UseActiveAssetList(string userName)
    {
        var user = KaxGlobal.UserDatabase.SelectWhere("UserName", userName).FirstOrDefault();
        if (user != null)
        {
            Console.WriteLine($"用户 {userName} 的激活资源列表:");
            foreach (var asset in user.ActiveAssets)
            {
                Logger.Info($"  资源ID: {asset.AssetId}, 激活时间: {DateTimeOffset.FromUnixTimeSeconds(asset.ActivatedAt)}, 到期时间: {DateTimeOffset.FromUnixTimeSeconds(asset.ExpiresAt)}");
            }
        }
        else
        {
            Console.WriteLine($"未找到用户: {userName}");
        }
    }
}
