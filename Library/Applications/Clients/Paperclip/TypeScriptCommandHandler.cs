using Drx.Sdk.Shared;

namespace DrxPaperclip;

/// <summary>
/// TypeScript 命令处理器，封装 ts 子命令解析与执行。
/// 依赖：Program 内部执行方法（RunTypeScriptAsync/CreateProject/ParseRunOptions）。
/// </summary>
public partial class Program
{
    private static class TypeScriptCommandHandler
    {
        public static void Execute(string[] args)
        {
            if (args.Length == 0)
            {
                Logger.Warn("[Paperclip] 缺少 ts 子命令，可用: create/run");
                PrintUsage();
                return;
            }

            var subCommand = args[0].ToLowerInvariant();
            var argument = args.Length > 1 ? args[1] : null;

            switch (subCommand)
            {
                case CommandRun:
                    var runOptions = ParseRunOptions(args, DefaultTypeScriptScript, "ts run");
                    EnsureModelsReleasedForRun(runOptions.ScriptName);
                    RunTypeScriptAsync(runOptions.ScriptName, runOptions.EnableDebugLogs).GetAwaiter().GetResult();
                    break;
                case CommandCreate:
                    CreateProject(argument, useTypeScript: true);
                    break;
                default:
                    Logger.Warn($"[Paperclip] 未知 ts 子命令: {args[0]}");
                    PrintUsage();
                    break;
            }
        }
    }
}
