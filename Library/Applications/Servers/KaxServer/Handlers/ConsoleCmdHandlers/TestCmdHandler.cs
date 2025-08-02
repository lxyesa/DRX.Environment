using Drx.Sdk.Shared.ConsoleCommand;
using DRX.Framework;

[Command("test", "测试命令，用于演示命令补全功能")]
public class TestCmdHandler : ICommandHandler
{
    public Exception Execute(string[] args)
    {
        Logger.Info("测试命令已执行");
        return null;
    }

    [SubCommand("echo", "回显消息")]
    public Exception Echo(string[] parentArgs, string[] args)
    {
        var message = args.Length > 0 ? string.Join(" ", args) : "Hello World";
        Logger.Info($"Echo: {message}");
        return null;
    }

    [SubCommand("status", "显示系统状态",2)]
    public Exception Status(string[] parentArgs, string[] args)
    {
        Logger.Info("系统状态: 正常运行");
        return null;
    }

    [SubCommand("config", "配置管理")]
    public Exception Config(string[] parentArgs, string[] args)
    {
        Logger.Info("配置管理命令");
        return null;
    }

    [BranchCommand("set", "config", "设置配置值", "键名", "值")]
    public Exception ConfigSet(string[] parentArgs, string[] args)
    {
        if (args.Length < 2)
        {
            Logger.Error("用法: /test -config -set <key> <value>");
            return null;
        }
        
        Logger.Info($"设置配置: {args[0]} = {args[1]}");
        return null;
    }

    [BranchCommand("get", "config", "获取配置值", "键名")]
    public Exception ConfigGet(string[] parentArgs, string[] args)
    {
        if (args.Length < 1)
        {
            Logger.Error("用法: /test -config -get <key>");
            return null;
        }
        
        Logger.Info($"获取配置: {args[0]} = 示例值");
        return null;
    }
}