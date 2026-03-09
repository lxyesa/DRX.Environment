namespace DrxPaperclip.Cli;

/// <summary>
/// CLI 参数解析结果，纯数据持有类。
/// </summary>
public sealed class PaperclipOptions
{
    /// <summary>run 子命令的目标路径（文件或目录）。</summary>
    public string? ScriptPath { get; set; }

    /// <summary>是否为 repl 子命令。</summary>
    public bool IsRepl { get; set; }

    /// <summary>是否请求帮助信息 (--help)。</summary>
    public bool IsHelp { get; set; }

    /// <summary>是否请求版本信息 (--version)。</summary>
    public bool IsVersion { get; set; }

    /// <summary>是否启用调试诊断输出 (--debug)。</summary>
    public bool Debug { get; set; }

    /// <summary>是否禁用模块系统 (--no-modules)。</summary>
    public bool NoModules { get; set; }

    /// <summary>自定义 TypeScript 编译器路径 (--typescript-path)。</summary>
    public string? TypeScriptPath { get; set; }

    /// <summary>安全白名单目录列表 (--allow-path，可多次指定)。</summary>
    public List<string> AllowPaths { get; } = [];

    /// <summary>插件 DLL 路径列表 (--plugin，可多次指定)。</summary>
    public List<string> PluginPaths { get; } = [];

    /// <summary>解析错误信息，非 null 时表示参数解析失败。</summary>
    public string? ErrorMessage { get; set; }
}
