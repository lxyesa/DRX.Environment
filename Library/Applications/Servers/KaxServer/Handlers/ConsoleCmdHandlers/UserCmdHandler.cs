using System.Threading.Tasks;
using Drx.Sdk.Shared.ConsoleCommand;
using DRX.Framework;
using KaxServer.Services;

[Command("user", "用户相关命令")]
public class UserCmdHandler : ICommandHandler
{
    public Exception Execute(string[] args)
    {
        return null;
    }

    [SubCommand("list", "列出所有用户")]
    public Exception List(string[] parentArgs, string[] args)
    {
        Logger.Debug("尚未实现用户相关命令。");
        return null;
    }

    [SubCommand("kick", "踢出用户", 1, "用户名")]
    public async Task<Exception> Kick(string[] parentArgs, string[] args)
    {
        if (args.Length < 1)
        {
            Logger.Error("请提供要踢出的用户名。");
            return new ArgumentException("缺少用户名参数。");
        }

        string username = args[0];
        var user = await UserManager.GetUserByUsernameAsync(username);
        if (user == null)
        {
            Logger.Error($"用户 {username} 不存在。");
            return new KeyNotFoundException($"用户 {username} 不存在。");
        }

        var result = await UserManager.KickUserAsync(user);
        if (result)
        {
            Logger.Info($"用户 {username} 已被踢出。");
            return null;
        }
        else
        {
            Logger.Error($"无法踢出用户 {username}，请稍后再试。");
            return new InvalidOperationException($"无法踢出用户 {username}，请稍后再试。");
        }
    }
}