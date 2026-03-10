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
          paperclip run <path> [function] [options]
                                            Execute a script file or project directory
          paperclip repl [options]           Start interactive REPL mode
          paperclip project cr <name>        Create a new Paperclip project
          paperclip project cr -http <name>  Create an HTTP server project
          paperclip project de <name>        Delete a Paperclip project
          paperclip --help                   Show this help message
          paperclip --version                Show version information

        Subcommands:
          run <path> [fn]   Run a .js/.ts file or a project directory
                            (directory resolves via project.json entryModule/main)
                            if fn is provided, invoke globalThis[fn]() after loading entry
          repl              Enter interactive JavaScript/TypeScript REPL
          project cr <name> Create project folder with main.ts + project.json
          project cr -http <name>
                          Create HTTP server project with routes + index.html
          project de <name> Delete project folder recursively

        Options:
          -h, --help                Show this help message
          -v, --version             Show version information
          --debug                   Enable diagnostic output to stderr
          -w, --watch               Watch script files and rerun on changes (run only)
          --no-cache                Disable TypeScript transpile cache (run only)
          --no-modules              Disable the module system (direct execution only)
          --allow-path <dir>        Add a directory to the import security whitelist
                                    (can be specified multiple times)
          --plugin <path>           Load a plugin assembly (.dll) implementing IJavaScriptPlugin
                                    (can be specified multiple times)
          --typescript-path <dir>   Custom path to the TypeScript compiler directory

        Examples:
          paperclip run script.js
          paperclip run script.js boot
          paperclip run ./my-project/
          paperclip run app.ts --debug --allow-path ./lib
          paperclip run app.ts --watch --no-cache
          paperclip project cr demo
          paperclip project cr -http myserver
          paperclip project de demo
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
