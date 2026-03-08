using Drx.Sdk.Shared;

namespace DrxPaperclip;

/// <summary>
/// Paperclip 命令行入口，负责 PATH 自注册、项目初始化与脚本执行。
/// </summary>
public partial class Program
{
    private const string EnvVarName = "PAPERCLIP_HOME";
    private const string DefaultScript = "main.js";
    private const string DefaultTypeScriptScript = "main.ts";
    private const string CommandRun = "run";
    private const string CommandModuleRun = "mrun";
    private const string CommandCreate = "create";
    private const string CommandTypeScript = "ts";
    private const string DebugFlag = "--debug";
    private const string ExportRegistrationDebugEnvVar = "DRX_JS_EXPORT_REG_DEBUG";
    private const string ModelsDirectoryName = "Models";
    private const string CoreModuleFileName = "paperclip.module.js";
    private const string HostFunctionsGlobalName = "host";
    private const string ModelResourcePrefix = "PaperclipModels/";
    private static readonly Lazy<IReadOnlyList<EmbeddedModelResource>> EmbeddedModelResources =
        new(DiscoverEmbeddedModelResources);

    public static void Main(string[] args)
    {
        try
        {
            EnsurePathRegistered();

            ExecuteCommand(args);
        }
        catch (FileNotFoundException ex)
        {
            ExitWithError(ex.Message);
        }
        catch (Exception ex)
        {
            ExitWithError($"[Paperclip] 未处理的异常: {ex.Message}");
        }

        Logger.Flush();
    }

    private static void ExecuteCommand(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        var command = args[0].ToLowerInvariant();
        var argument = args.Length > 1 ? args[1] : null;

        switch (command)
        {
            case CommandRun:
                var runOptions = ParseRunOptions(args);
                EnsureModelsReleasedForRun(runOptions.ScriptName);
                RunScriptAsync(runOptions.ScriptName, runOptions.EnableDebugLogs).GetAwaiter().GetResult();
                break;
            case CommandModuleRun:
                var mrunOptions = ParseModuleRunOptions(args);
                EnsureModelsReleasedForRun(mrunOptions.ScriptName);
                RunModuleScriptAsync(mrunOptions).GetAwaiter().GetResult();
                break;
            case CommandCreate:
                CreateProject(argument);
                break;
            case CommandTypeScript:
                ExecuteTypeScriptCommand(args.Skip(1).ToArray());
                break;
            default:
                Logger.Warn($"未知命令: {args[0]}");
                PrintUsage();
                break;
        }
    }

    private static void ExitWithError(string message)
    {
        Logger.Error(message);
        Logger.Flush();
        Environment.Exit(1);
    }

    private readonly record struct RunOptions(string ScriptName, bool EnableDebugLogs);

    private readonly record struct EmbeddedModelResource(string ResourceName, string RelativePath);

    private static void PrintUsage()
    {
        Console.WriteLine("""
            Paperclip — DRX JS 脚本主机

            用法:
              paperclip run              执行当前目录下的 main.js
              paperclip run <file.js>    执行指定的 JS 脚本文件
              paperclip run <file.js> --debug
                                   显示 SDK 导出注册调试日志
              paperclip mrun             以 Module Runtime 执行当前目录下的 main.js
              paperclip mrun <file>      以 Module Runtime 执行指定脚本（支持 .js/.ts/.mts）
              paperclip mrun <file> --debug
                                   以 Module Runtime 执行并显示调试日志
              paperclip mrun <file> --config=paperclip.json
                                   从配置文件加载 moduleRuntime 选项
              paperclip mrun <file> --allow-import=../shared
                                   追加允许导入白名单路径（可重复）
              paperclip mrun <file> --debug-events
                                   输出结构化调试事件（建议配合 --debug）
              paperclip create           在当前目录初始化一个 Paperclip 项目
              paperclip create <name>    在当前目录下创建名为 <name> 的子项目

              paperclip ts create         在当前目录初始化一个 TypeScript Paperclip 项目
              paperclip ts create <name>  在当前目录下创建 TypeScript 子项目
              paperclip ts run            执行当前目录下的 main.ts（运行时转译）
              paperclip ts run <file.ts>  执行指定 TypeScript 脚本文件（运行时转译）
              paperclip ts run <file.ts> --debug
                                   显示 SDK 导出注册调试日志
            """);
    }
}
