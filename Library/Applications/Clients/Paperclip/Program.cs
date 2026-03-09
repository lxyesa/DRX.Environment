using DrxPaperclip.Cli;
using DrxPaperclip.Formatting;
using DrxPaperclip.Hosting;

namespace DrxPaperclip;

/// <summary>
/// Paperclip CLI 主入口。负责参数解析、命令路由与顶层异常处理。
/// 关键依赖：CliParser / EngineBootstrap / ScriptHost / ReplHost / ErrorFormatter。
/// </summary>
public partial class Program
{
    /// <summary>
    /// 程序入口：解析参数并路由到 run/repl/help/version 分支，最终以约定退出码结束进程。
    /// </summary>
    public static void Main(string[] args)
    {
        var exitCode = 0;

        try
        {
            var options = CliParser.Parse(args);

            if (!string.IsNullOrWhiteSpace(options.ErrorMessage))
            {
                Console.Error.WriteLine($"Error [CLI_INVALID]: {options.ErrorMessage}");
                Console.Error.WriteLine("Hint: 使用 --help 查看正确用法。");
                exitCode = 2;
            }
            else if (options.IsHelp)
            {
                Console.WriteLine(HelpText.GetHelpText());
            }
            else if (options.IsVersion)
            {
                Console.WriteLine(HelpText.GetVersion());
            }
            else if (options.IsRepl)
            {
                using var bootstrap = EngineBootstrap.Create(options);
                exitCode = ReplHost.Start(bootstrap);
            }
            else if (!string.IsNullOrWhiteSpace(options.ScriptPath))
            {
                using var bootstrap = EngineBootstrap.Create(options);
                exitCode = ScriptHost.Run(options, bootstrap);
            }
            else
            {
                Console.Error.WriteLine("Error [CLI_INVALID]: 缺少脚本路径或子命令。");
                Console.Error.WriteLine("Hint: 使用 run <path> 或 repl，或执行 --help 查看说明。");
                exitCode = 2;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ErrorFormatter.Format(ex));
            exitCode = ErrorFormatter.GetExitCode(ex);
        }

        Environment.Exit(exitCode);
    }
}
