using System;
using System.Linq;
using Drx.Sdk.Network.Http.Commands;
using Drx.Sdk.Shared;

namespace KaxSocket.Handlers.Command;

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

    [Command("assetremaining <username> <assetId>", "asset:资源管理", "显示用户某资产剩余有效时长")]
    public static void Cmd_AssetRemaining(string userName, int assetId)
    {
        var remaining = KaxGlobal.GetUserAssetRemainingSecondsAsync(userName, assetId).Result;
        if (remaining == null)
        {
            Console.WriteLine($"用户 {userName} 没有资产 {assetId} 的激活记录。");
            return;
        }

        if (remaining == -1)
        {
            Console.WriteLine($"用户 {userName} 的资产 {assetId} 为永久有效。");
            return;
        }

        Console.WriteLine($"用户 {userName} 的资产 {assetId} 剩余 {remaining} 秒。");
    }

    [Command("listassetcatalog [limit]", "asset:资源管理", "显示资产库中的资产列表（默认20）")]
    public static void Cmd_ListAssetCatalog(int? limit = null)
    {
        var take = limit.GetValueOrDefault(20);
        if (take <= 0) take = 20;

        var assets = KaxGlobal.AssetDataBase
            .SelectAll()
            .OrderByDescending(a => a.Id)
            .Take(take)
            .ToList();

        if (assets.Count == 0)
        {
            Console.WriteLine("当前没有资产记录。");
            return;
        }

        Console.WriteLine($"资产列表（显示 {assets.Count} 条）");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine("{0,-5} {1,-24} {2,-12} {3,-10}", "ID", "名称", "状态", "作者ID");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");

        foreach (var asset in assets)
        {
            var name = string.IsNullOrWhiteSpace(asset.Name) ? "(未命名)" : asset.Name;
            Console.WriteLine("{0,-5} {1,-24} {2,-12} {3,-10}",
                asset.Id,
                name.Length > 24 ? name[..21] + "..." : name,
                asset.Status,
                asset.AuthorId);
        }

        Console.WriteLine("─────────────────────────────────────────────────────────────────");
    }

    [Command("assetinfo <assetId>", "asset:资源管理", "显示资产的详细信息")]
    public static void Cmd_AssetInfo(int assetId)
    {
        var asset = KaxGlobal.AssetDataBase.SelectWhere("Id", assetId).FirstOrDefault();
        if (asset == null)
        {
            Console.WriteLine($"未找到资产: {assetId}");
            return;
        }

        Console.WriteLine("资产信息");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine($"ID: {asset.Id}");
        Console.WriteLine($"名称: {asset.Name}");
        Console.WriteLine($"作者ID: {asset.AuthorId}");
        Console.WriteLine($"状态: {asset.Status}");
        Console.WriteLine($"描述: {(string.IsNullOrWhiteSpace(asset.Description) ? "(无)" : asset.Description)}");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
    }

    [Command("listassetexpiring [hours] [limit]", "asset:资源管理", "显示即将在指定小时内到期的激活资产（默认24小时，最多50条）")]
    public static void Cmd_ListAssetExpiring(int? hours = null, int? limit = null)
    {
        var inHours = hours.GetValueOrDefault(24);
        if (inHours <= 0) inHours = 24;

        var take = limit.GetValueOrDefault(50);
        if (take <= 0) take = 50;
        if (take > 200) take = 200;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var deadline = now + inHours * 3600L;

        var rows = KaxGlobal.UserDatabase.SelectAll()
            .SelectMany(u => u.ActiveAssets.Select(a => new
            {
                UserName = u.UserName,
                AssetId = a.AssetId,
                ExpiresAt = a.ExpiresAt
            }))
            .Where(x => x.ExpiresAt > 0 && x.ExpiresAt <= deadline)
            .OrderBy(x => x.ExpiresAt)
            .Take(take)
            .ToList();

        if (rows.Count == 0)
        {
            Console.WriteLine($"未来 {inHours} 小时内无即将到期资产。");
            return;
        }

        Console.WriteLine($"即将到期资产（未来 {inHours} 小时，显示 {rows.Count} 条）");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine("{0,-20} {1,-8} {2,-22} {3}", "用户名", "资产ID", "到期时间", "剩余");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        foreach (var row in rows)
        {
            var expires = DateTimeOffset.FromUnixTimeSeconds(row.ExpiresAt);
            var left = Math.Max(0, row.ExpiresAt - now);
            Console.WriteLine("{0,-20} {1,-8} {2,-22} {3}",
                row.UserName.Length > 20 ? row.UserName[..17] + "..." : row.UserName,
                row.AssetId,
                expires.ToString("yyyy-MM-dd HH:mm:ss"),
                FormatLeftSeconds(left));
        }
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
    }

    private static string FormatLeftSeconds(long seconds)
    {
        if (seconds <= 0) return "0s";
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }
}
