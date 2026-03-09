namespace DrxPaperclip.Cli;

/// <summary>
/// 帮助文本与版本号常量。纯常量，无逻辑。
/// </summary>
public static class HelpText
{
    private const string Version = "1.0.0";

    private const string Usage = """
        Paperclip — JS/TS modular script host

        Usage:
          paperclip run <path> [options]     Execute a script file or project directory
          paperclip repl [options]           Start interactive REPL mode
          paperclip --help                   Show this help message
          paperclip --version                Show version information

        Subcommands:
          run <path>        Run a .js/.ts file or a project directory
                            (directory resolves via project.json → index.js → index.ts)
          repl              Enter interactive JavaScript/TypeScript REPL

        Options:
          -h, --help                Show this help message
          -v, --version             Show version information
          --debug                   Enable diagnostic output to stderr
          --no-modules              Disable the module system (direct execution only)
          --allow-path <dir>        Add a directory to the import security whitelist
                                    (can be specified multiple times)
          --plugin <path>           Load a plugin assembly (.dll) implementing IJavaScriptPlugin
                                    (can be specified multiple times)
          --typescript-path <dir>   Custom path to the TypeScript compiler directory

        Examples:
          paperclip run script.js
          paperclip run ./my-project/
          paperclip run app.ts --debug --allow-path ./lib
          paperclip repl --plugin ./myplugin.dll
        """;

    /// <summary>
    /// 返回格式化的完整使用说明。
    /// </summary>
    public static string GetHelpText() => Usage;

    /// <summary>
    /// 返回版本字符串。
    /// </summary>
    public static string GetVersion() => $"paperclip {Version}";
}
