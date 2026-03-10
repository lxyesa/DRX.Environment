using System.Text;
using Drx.Sdk.Shared.JavaScript;
using DrxPaperclip.Cli;
using DrxPaperclip.Formatting;

namespace DrxPaperclip.Hosting;

/// <summary>
/// REPL 交互宿主。负责循环读取输入、执行脚本、处理内置命令与中断信号。
/// 依赖：<see cref="EngineBootstrap"/>、<see cref="Drx.Sdk.Shared.JavaScript.Abstractions.IJavaScriptEngine"/>。
/// </summary>
public static class ReplHost
{
    private static readonly string[] TypeScriptExtensions = [".ts", ".mts", ".cts"];

    /// <summary>
    /// 启动 REPL 循环。
    /// </summary>
    /// <param name="bootstrap">已初始化的引擎启动栈。</param>
    /// <returns>退出码（0 = 正常退出，1 = 非预期错误）。</returns>
    public static int Start(EngineBootstrap bootstrap)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);

        var activeBootstrap = bootstrap;
        var ownsActiveBootstrap = false;
        var baselineGlobals = CaptureGlobalSnapshot(activeBootstrap);
        var cancelCurrentInput = false;

        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cancelCurrentInput = true;
            Console.WriteLine();
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            PrintWelcome();

            while (true)
            {
                var input = ReadSubmission(ref cancelCurrentInput);
                if (input is null)
                {
                    return 0;
                }

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                if (IsBuiltinCommand(input))
                {
                    var shouldExit = HandleBuiltinCommand(
                        input.Trim(),
                        ref activeBootstrap,
                        ref ownsActiveBootstrap,
                        bootstrap,
                        ref baselineGlobals);

                    if (shouldExit)
                    {
                        return 0;
                    }

                    continue;
                }

                ExecuteSnippet(activeBootstrap, input);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ErrorFormatter.Format(ex));
            return ErrorFormatter.GetExitCode(ex);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;

            if (ownsActiveBootstrap)
            {
                activeBootstrap.Dispose();
            }
        }
    }

    private static bool HandleBuiltinCommand(
        string command,
        ref EngineBootstrap activeBootstrap,
        ref bool ownsActiveBootstrap,
        EngineBootstrap originalBootstrap,
        ref HashSet<string> baselineGlobals)
    {
        if (string.Equals(command, ".exit", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(command, ".help", StringComparison.Ordinal))
        {
            PrintHelp();
            return false;
        }

        if (string.Equals(command, ".clear", StringComparison.Ordinal))
        {
            activeBootstrap = ResetReplBootstrap(activeBootstrap, originalBootstrap, ref ownsActiveBootstrap);
            baselineGlobals = CaptureGlobalSnapshot(activeBootstrap);
            Console.WriteLine("REPL 上下文已重置。");
            return false;
        }

        if (command.StartsWith(".load ", StringComparison.Ordinal))
        {
            var path = command[6..].Trim();
            if (string.IsNullOrEmpty(path))
            {
                Console.Error.WriteLine(".load 命令需要提供文件路径。用法：.load <file>");
                return false;
            }

            ExecuteFile(activeBootstrap, path);
            return false;
        }

        Console.Error.WriteLine($"未知命令: {command}。输入 .help 查看可用命令。");
        return false;
    }

    private static EngineBootstrap ResetReplBootstrap(
        EngineBootstrap activeBootstrap,
        EngineBootstrap originalBootstrap,
        ref bool ownsActiveBootstrap)
    {
        var options = new PaperclipOptions
        {
            IsRepl = true,
            Debug = originalBootstrap.Options.EnableDebugLogs,
            NoModules = originalBootstrap.ModuleLoader is null
        };

        foreach (var allowPath in originalBootstrap.Options.AllowedImportPathPrefixes)
        {
            options.AllowPaths.Add(allowPath);
        }

        var newBootstrap = EngineBootstrap.Create(options);

        if (ownsActiveBootstrap)
        {
            activeBootstrap.Dispose();
        }

        ownsActiveBootstrap = true;
        return newBootstrap;
    }

    private static void ExecuteFile(EngineBootstrap bootstrap, string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"未找到文件: {fullPath}", fullPath);
        }

        if (IsTypeScript(fullPath))
        {
            var projectRoot = Path.GetDirectoryName(fullPath) ?? bootstrap.Options.ProjectRoot;
            var jsSource = JavaScript.TranspileTypeScriptFile(fullPath, projectRoot);
            var result = bootstrap.Engine.Execute(jsSource);
            PrintResult(result);
            return;
        }

        var fileResult = bootstrap.Engine.ExecuteFile(fullPath);
        PrintResult(fileResult);
    }

    private static void ExecuteSnippet(EngineBootstrap bootstrap, string input)
    {
        try
        {
            var result = bootstrap.Engine.Execute(input);
            PrintResult(result);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ErrorFormatter.Format(ex));
        }
    }

    private static string? ReadSubmission(ref bool cancelCurrentInput)
    {
        var builder = new StringBuilder();

        while (true)
        {
            var prompt = builder.Length == 0 ? "paperclip> " : "... ";
            Console.Write(prompt);

            var line = Console.ReadLine();

            if (cancelCurrentInput)
            {
                cancelCurrentInput = false;
                builder.Clear();
                return string.Empty;
            }

            if (line is null)
            {
                // Ctrl+D / EOF
                return builder.Length == 0 ? null : builder.ToString();
            }

            builder.AppendLine(line);

            if (!NeedsMoreInput(builder))
            {
                return builder.ToString().TrimEnd();
            }
        }
    }

    private static bool NeedsMoreInput(StringBuilder builder)
    {
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inTemplate = false;
        var escape = false;
        var balance = 0;

        for (var i = 0; i < builder.Length; i++)
        {
            var c = builder[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\')
            {
                escape = true;
                continue;
            }

            if (!inDoubleQuote && !inTemplate && c == '\'')
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (!inSingleQuote && !inTemplate && c == '"')
            {
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && c == '`')
            {
                inTemplate = !inTemplate;
                continue;
            }

            if (inSingleQuote || inDoubleQuote || inTemplate)
            {
                continue;
            }

            if (c is '{' or '[' or '(')
            {
                balance++;
            }
            else if (c is '}' or ']' or ')')
            {
                balance--;
            }
        }

        return inSingleQuote || inDoubleQuote || inTemplate || balance > 0;
    }

    private static bool IsBuiltinCommand(string input)
    {
        var trimmed = input.Trim();
        return !trimmed.Contains("\n", StringComparison.Ordinal) && trimmed.StartsWith(".", StringComparison.Ordinal);
    }

    private static void PrintResult(object? result)
    {
        if (result is null)
        {
            return;
        }

        if (string.Equals(result.GetType().FullName, "Microsoft.ClearScript.Undefined", StringComparison.Ordinal))
        {
            return;
        }

        Console.WriteLine(result);
    }

    private static HashSet<string> CaptureGlobalSnapshot(EngineBootstrap bootstrap)
    {
        try
        {
            var json = bootstrap.Engine.Execute<string>("JSON.stringify(Object.getOwnPropertyNames(globalThis))");
            var names = System.Text.Json.JsonSerializer.Deserialize<string[]>(json)
                        ?? [];
            return new HashSet<string>(names, StringComparer.Ordinal);
        }
        catch
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static bool IsTypeScript(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        foreach (var tsExt in TypeScriptExtensions)
        {
            if (string.Equals(ext, tsExt, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void PrintWelcome()
    {
        Console.WriteLine("Paperclip REPL 已启动。输入 .help 查看命令，.exit 退出。");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("可用命令：");
        Console.WriteLine("  .help         显示帮助");
        Console.WriteLine("  .exit         退出 REPL");
        Console.WriteLine("  .clear        重置 REPL 上下文");
        Console.WriteLine("  .load <file>  加载并执行文件（支持 .js/.ts）");
    }
}
