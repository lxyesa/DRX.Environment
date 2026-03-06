using System;
using System.Linq;
using System.Text.Json;
using Drx.Sdk.Network.Http.Commands;
using Drx.Sdk.Shared;

namespace KaxSocket.Handlers.Command;

public class UserCommandHandler
{
    private static UserData? FindUserByNameOrId(string userNameOrId)
    {
        if (string.IsNullOrWhiteSpace(userNameOrId)) return null;

        var key = userNameOrId.Trim();
        if (int.TryParse(key, out var userId) && userId > 0)
        {
            return KaxGlobal.UserDatabase.SelectWhere("Id", userId).FirstOrDefault();
        }

        return KaxGlobal.UserDatabase.SelectWhere("UserName", key).FirstOrDefault();
    }

    private static int ClampRgb(int value)
    {
        if (value < 0) return 0;
        if (value > 255) return 255;
        return value;
    }

    private static bool TryParseBanDurationToSeconds(string rawDuration, out long durationSeconds, out string? error)
    {
        durationSeconds = 0;
        error = null;

        if (string.IsNullOrWhiteSpace(rawDuration))
        {
            error = "duration 不能为空。";
            return false;
        }

        var input = rawDuration.Trim().ToLowerInvariant();
        if (input == "0")
        {
            durationSeconds = 0; // 永久封禁
            return true;
        }

        long factor = 1;
        var numberPart = input;

        var unit = input[^1];
        if (char.IsLetter(unit))
        {
            numberPart = input[..^1];
            factor = unit switch
            {
                's' => 1,
                'm' => 60,
                'd' => 24 * 60 * 60,
                'y' => 365L * 24 * 60 * 60,
                _ => -1
            };

            if (factor < 0)
            {
                error = "不支持的时间单位。仅支持 s/m/d/y，例如 30s、15m、7d、1y。";
                return false;
            }
        }

        if (!long.TryParse(numberPart, out var value) || value <= 0)
        {
            error = "duration 数值无效。示例：30s、15m、7d、1y，或 0（永久）。";
            return false;
        }

        try
        {
            checked
            {
                durationSeconds = value * factor;
            }
            return true;
        }
        catch (OverflowException)
        {
            error = "duration 过大，无法处理。";
            return false;
        }
    }

    [Command("ban <userName> <reason> <duration>", "user:管理用户", "封禁指定用户，duration 支持 s/m/d/y（如 30s/15m/7d/1y），0 表示永久封禁")]
    public static void Cmd_BanUser(string userName, string reason, string duration)
    {
        if (!TryParseBanDurationToSeconds(duration, out var durationSeconds, out var parseError))
        {
            Console.WriteLine($"封禁时长解析失败: {parseError}");
            Console.WriteLine("用法：ban <userName> <reason> <duration>");
            return;
        }

        KaxGlobal.BanUser(userName, reason, durationSeconds).Wait();
    }

    [Command("userinfo <username>", "user:管理用户", "显示用户基础信息、封禁状态与资源概览")]
    public static void Cmd_UserInfo(string userName)
    {
        var user = KaxGlobal.UserDatabase.SelectWhere("UserName", userName).FirstOrDefault();
        if (user == null)
        {
            Console.WriteLine($"未找到用户: {userName}");
            return;
        }

        var cdkCount = KaxGlobal.GetUserCdkCountAsync(user.UserName).Result;

        Console.WriteLine("用户信息");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine($"ID: {user.Id}");
        Console.WriteLine($"用户名: {user.UserName}");
        Console.WriteLine($"邮箱: {user.Email}");
        Console.WriteLine($"权限组: {user.PermissionGroup} ({(int)user.PermissionGroup})");
        Console.WriteLine($"金币: {user.Gold}");
        Console.WriteLine($"资源数: {user.ResourceCount}");
        Console.WriteLine($"激活资产数: {user.ActiveAssets.Count}");
        Console.WriteLine($"已使用CDK数: {cdkCount}");
        Console.WriteLine($"徽章: {(string.IsNullOrWhiteSpace(user.Badges) ? "(无)" : user.Badges)}");
        Console.WriteLine($"封禁状态: {(user.Status.IsBanned ? "是" : "否")}");
        if (user.Status.IsBanned)
        {
            Console.WriteLine($"封禁原因: {user.Status.BanReason}");
            Console.WriteLine($"封禁到期: {(user.Status.BanExpiresAt > 0 ? DateTimeOffset.FromUnixTimeSeconds(user.Status.BanExpiresAt).ToString("yyyy-MM-dd HH:mm:ss") : "永久")}");
        }
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
    }

    [Command("listusers [limit]", "user:管理用户", "按注册时间倒序显示用户列表（默认20）")]
    public static void Cmd_ListUsers(int? limit = null)
    {
        var take = limit.GetValueOrDefault(20);
        if (take <= 0) take = 20;

        var users = KaxGlobal.UserDatabase
            .SelectAll()
            .OrderByDescending(u => u.RegisteredAt)
            .Take(take)
            .ToList();

        if (users.Count == 0)
        {
            Console.WriteLine("当前没有用户记录。");
            return;
        }

        Console.WriteLine($"用户列表（显示 {users.Count} 条）");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine("{0,-5} {1,-20} {2,-10} {3,-8} {4,-6}", "ID", "用户名", "权限组", "金币", "封禁");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        foreach (var user in users)
        {
            Console.WriteLine("{0,-5} {1,-20} {2,-10} {3,-8} {4,-6}",
                user.Id,
                user.UserName.Length > 20 ? user.UserName[..17] + "..." : user.UserName,
                user.PermissionGroup,
                user.Gold,
                user.Status.IsBanned ? "是" : "否");
        }
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
    }

    [Command("addgold <username> <amount>", "user:管理用户", "为用户增加/扣减金币（可填负数）")]
    public static void Cmd_AddGold(string userName, int amount)
    {
        var user = KaxGlobal.UserDatabase.SelectWhere("UserName", userName).FirstOrDefault();
        if (user == null)
        {
            Console.WriteLine($"未找到用户: {userName}");
            return;
        }

        var before = user.Gold;
        user.Gold += amount;
        KaxGlobal.UserDatabase.UpdateAsync(user).Wait();

        Console.WriteLine($"用户 {user.UserName} 金币已更新: {before} -> {user.Gold} (变更 {amount:+#;-#;0})");
    }

    [Command("addbadge <userNameOrId> <badgeText> <r> <g> <b>", "user:管理用户", "给用户添加一个徽章（JSON 格式存储）")]
    public static void Cmd_AddBadge(string userNameOrId, string badgeText, int r, int g, int b)
    {
        if (string.IsNullOrWhiteSpace(userNameOrId))
        {
            Console.WriteLine("用户参数为空。用法：addbadge <userNameOrId> <badgeText> <r> <g> <b>");
            return;
        }

        var user = FindUserByNameOrId(userNameOrId);
        if (user == null)
        {
            Console.WriteLine($"未找到用户: {userNameOrId.Trim()}");
            return;
        }

        var text = (badgeText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            Console.WriteLine("徽章名称不能为空。示例：addbadge alice MVP 59 130 246");
            return;
        }

        var badges = BadgeHelper.ParseBadges(user.Badges);
        badges.Add(new BadgeEntry { Text = text, Color = new[] { ClampRgb(r), ClampRgb(g), ClampRgb(b) } });
        user.Badges = BadgeHelper.Serialize(badges);

        KaxGlobal.UserDatabase.UpdateAsync(user).Wait();

        Console.WriteLine($"已为用户 {user.UserName}(ID:{user.Id}) 添加徽章: {text}[{ClampRgb(r)},{ClampRgb(g)},{ClampRgb(b)}]");
        Console.WriteLine($"当前徽章: {user.Badges}");
    }

    [Command("listbanned [limit]", "user:管理用户", "显示封禁用户列表（默认20）")]
    public static void Cmd_ListBannedUsers(int? limit = null)
    {
        var take = limit.GetValueOrDefault(20);
        if (take <= 0) take = 20;

        var users = KaxGlobal.UserDatabase
            .SelectAll()
            .Where(u => u.Status.IsBanned)
            .OrderByDescending(u => u.Status.BanExpiresAt)
            .Take(take)
            .ToList();

        if (users.Count == 0)
        {
            Console.WriteLine("当前没有封禁用户。\n");
            return;
        }

        Console.WriteLine($"封禁用户列表（显示 {users.Count} 条）");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine("{0,-5} {1,-20} {2,-22} {3}", "ID", "用户名", "到期时间", "原因");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        foreach (var user in users)
        {
            var userName = user.UserName.Length > 20 ? user.UserName[..17] + "..." : user.UserName;
            var expires = user.Status.BanExpiresAt > 0
                ? DateTimeOffset.FromUnixTimeSeconds(user.Status.BanExpiresAt).ToString("yyyy-MM-dd HH:mm:ss")
                : "永久";
            var reason = string.IsNullOrWhiteSpace(user.Status.BanReason) ? "(无)" : user.Status.BanReason;
            if (reason.Length > 24) reason = reason[..21] + "...";

            Console.WriteLine("{0,-5} {1,-20} {2,-22} {3}", user.Id, userName, expires, reason);
        }
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
    }

    [Command("searchusers <keyword> [limit]", "user:管理用户", "按用户名/邮箱关键字搜索用户")]
    public static void Cmd_SearchUsers(string keyword, int? limit = null)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            Console.WriteLine("关键字不能为空。");
            return;
        }

        var take = limit.GetValueOrDefault(20);
        if (take <= 0) take = 20;

        var q = keyword.Trim();
        var users = KaxGlobal.UserDatabase
            .SelectAll()
            .Where(u =>
                (!string.IsNullOrWhiteSpace(u.UserName) && u.UserName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(u.Email) && u.Email.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(u => u.RegisteredAt)
            .Take(take)
            .ToList();

        if (users.Count == 0)
        {
            Console.WriteLine($"未找到关键字为 '{q}' 的用户。");
            return;
        }

        Console.WriteLine($"搜索结果（关键字: {q}，显示 {users.Count} 条）");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine("{0,-5} {1,-20} {2,-24} {3,-8}", "ID", "用户名", "邮箱", "封禁");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        foreach (var user in users)
        {
            var userName = user.UserName.Length > 20 ? user.UserName[..17] + "..." : user.UserName;
            var email = string.IsNullOrWhiteSpace(user.Email) ? "(无)" : user.Email;
            if (email.Length > 24) email = email[..21] + "...";
            Console.WriteLine("{0,-5} {1,-20} {2,-24} {3,-8}", user.Id, userName, email, user.Status.IsBanned ? "是" : "否");
        }
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
    }

    [Command("unban <username>", "user:管理用户", "解除用户的封禁状态")]
    public static void Cmd_UnBanUser(string userName)
    {
        KaxGlobal.UnBanUser(userName).Wait();
    }

    [Command("setpermission <username> <group>", "user:管理用户", "为用户设置权限组")]
    public static void Cmd_SetUserPermissionGroup(string userName, int group)
    {
        if (!Enum.IsDefined(typeof(UserPermissionGroup), group))
        {
            Console.WriteLine($"无效的权限组: {group}");
            return;
        }

        KaxGlobal.SetUserPermissionGroup(userName, (UserPermissionGroup)group).Wait();
    }
}
