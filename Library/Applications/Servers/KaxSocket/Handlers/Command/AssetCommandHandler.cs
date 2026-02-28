using System;
using System.Linq;
using Drx.Sdk.Network.Http.Commands;
using Drx.Sdk.Shared;

namespace KaxSocket.Handlers.Command;

/// <summary>
/// 资源管理相关命令处理器
/// </summary>
public class AssetCommandHandler
{
    [Command("addasset <username> <assetId> <duration>", "asset:资源管理", "为用户添加激活资源")]
    public static void Cmd_AddAssetToUser(string userName, int assetId, long durationSeconds)
    {
        KaxGlobal.AddActiveAssetToUser(userName, assetId, durationSeconds).Wait();
    }

    [Command("listassets <username>", "asset:资源管理", "显示用户的激活资源列表")]
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
