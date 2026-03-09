namespace DrxPaperclip.Cli;

/// <summary>
/// 手写 CLI 参数解析器，解析子命令及选项标志，返回 <see cref="PaperclipOptions"/>。
/// </summary>
public static class CliParser
{
    /// <summary>
    /// 解析命令行参数，返回填充完毕的 <see cref="PaperclipOptions"/>。
    /// </summary>
    /// <param name="args">命令行参数数组。</param>
    /// <returns>解析后的选项对象；若解析失败，<see cref="PaperclipOptions.ErrorMessage"/> 非 null。</returns>
    public static PaperclipOptions Parse(string[] args)
    {
        var options = new PaperclipOptions();

        if (args.Length == 0)
        {
            options.IsHelp = true;
            return options;
        }

        int i = 0;

        // 识别子命令（第一个非 - 开头参数）
        if (args[0] is not ['-', ..])
        {
            switch (args[0])
            {
                case "run":
                    i = 1;
                    // run 后的下一个非 - 参数为脚本/目录路径
                    if (i < args.Length && args[i] is not ['-', ..])
                    {
                        options.ScriptPath = args[i];
                        i++;
                    }
                    break;

                case "repl":
                    options.IsRepl = true;
                    i = 1;
                    break;

                default:
                    // 单独传入非子命令名称，视为 run <path> 的简写
                    options.ScriptPath = args[0];
                    i = 1;
                    break;
            }
        }

        // 解析剩余的 --key [value] 选项
        for (; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    options.IsHelp = true;
                    break;

                case "--version":
                case "-v":
                    options.IsVersion = true;
                    break;

                case "--debug":
                    options.Debug = true;
                    break;

                case "--no-modules":
                    options.NoModules = true;
                    break;

                case "--allow-path":
                    if (!TryConsumeValue(args, ref i, out var allowPath))
                    {
                        options.ErrorMessage = "--allow-path requires a directory path argument.";
                        return options;
                    }
                    options.AllowPaths.Add(allowPath);
                    break;

                case "--plugin":
                    if (!TryConsumeValue(args, ref i, out var pluginPath))
                    {
                        options.ErrorMessage = "--plugin requires a DLL path argument.";
                        return options;
                    }
                    options.PluginPaths.Add(pluginPath);
                    break;

                case "--typescript-path":
                    if (!TryConsumeValue(args, ref i, out var tsPath))
                    {
                        options.ErrorMessage = "--typescript-path requires a directory path argument.";
                        return options;
                    }
                    options.TypeScriptPath = tsPath;
                    break;

                default:
                    options.ErrorMessage = $"Unknown option: {args[i]}";
                    return options;
            }
        }

        return options;
    }

    /// <summary>
    /// 消费下一个参数作为值。值不能以 - 开头（除非被用尽检查捕获）。
    /// </summary>
    private static bool TryConsumeValue(string[] args, ref int index, out string value)
    {
        int next = index + 1;
        if (next >= args.Length || args[next].StartsWith('-'))
        {
            value = string.Empty;
            return false;
        }

        index = next;
        value = args[next];
        return true;
    }
}
