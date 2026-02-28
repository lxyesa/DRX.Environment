using System;
using Drx.Sdk.Network.Http.Commands;
using Drx.Sdk.Shared;

namespace KaxSocket.Handlers.Command;

/// <summary>
/// 用户管理相关命令处理器
/// </summary>
public class UserCommandHandler
{
    [Command("ban <username> <reason> <duration>", "user:管理用户", "封禁指定用户，禁止其访问服务器，duration单位为秒，0表示永久封禁")]
    public static void Cmd_BanUser(string userName, string reason, long durationSeconds)
    {
        KaxGlobal.BanUser(userName, reason, durationSeconds).Wait();
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
