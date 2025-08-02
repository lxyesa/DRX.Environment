using System;
using System.Linq;
using Drx.Sdk.Shared.ConsoleCommand;
namespace Drx.Sdk.Shared.ConsoleCommand
{
    [Command("help", "显示所有可用命令的帮助信息")]
    public class HelpHandler : ICommandHandler
    {
        public Exception Execute(string[] args)
        {
            var all = ConsoleCommandProcessor.GetAllCommands();
            if (args.Length == 0)
            {
                Console.WriteLine("可用命令：");
                foreach (var cmd in all)
                {
                    var mainArgs = cmd.ArgDescriptions.Length > 0
                        ? " " + string.Join(" ", cmd.ArgDescriptions.Select(a => $"<{a}>"))
                        : "";
                    Console.WriteLine($"/{cmd.Name}{mainArgs} - {cmd.Description}");
                    foreach (var sub in cmd.SubCommands)
                    {
                        var subArgs = sub.ArgDescriptions.Length > 0
                            ? " " + string.Join(" ", sub.ArgDescriptions.Select(a => $"<{a}>"))
                            : "";
                        Console.WriteLine($"    -{sub.Name}{subArgs} : {sub.Description}");
                    }
                    foreach (var branch in cmd.BranchCommands)
                    {
                        var branchArgs = branch.ArgDescriptions.Length > 0
                            ? " " + string.Join(" ", branch.ArgDescriptions.Select(a => $"<{a}>"))
                            : "";
                        var parentInfo = string.IsNullOrEmpty(branch.ParentName) ? "" : $" (父命令: {branch.ParentName})";
                        Console.WriteLine($"    -{branch.Name}{branchArgs} : {branch.Description}{parentInfo}");
                    }
                }
            }
            else
            {
                var cmdName = args[0].ToLowerInvariant();
                var cmd = all.FirstOrDefault(c => c.Name == cmdName);
                if (cmd == null)
                {
                    Console.WriteLine($"未找到命令: {cmdName}");
                    return null;
                }
                Console.WriteLine($"/{cmd.Name} - {cmd.Description}");
                if (cmd.SubCommands.Count > 0)
                {
                    Console.WriteLine("子命令：");
                    foreach (var sub in cmd.SubCommands)
                    {
                        Console.WriteLine($"    -{sub.Name} : {sub.Description}");
                    }
                    if (cmd.BranchCommands.Count > 0)
                    {
                        Console.WriteLine("分支命令：");
                        foreach (var branch in cmd.BranchCommands)
                        {
                            var parentInfo = string.IsNullOrEmpty(branch.ParentName) ? "" : $" (父命令: {branch.ParentName})";
                            Console.WriteLine($"    -{branch.Name} : {branch.Description}{parentInfo}");
                        }
                    }
                }
            }
            return null;
        }
    }
}
