using Drx.Sdk.Shared.JavaScript.Abstractions;
using Drx.Sdk.Shared.JavaScript.Engine;
using DrxPaperclip.Cli;
using DrxPaperclip.Diagnostics;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 引擎启动器。根据 <see cref="PaperclipOptions"/> 配置并创建 <see cref="IJavaScriptEngine"/>，
/// 管理模块运行时全部组件（Resolver/Cache/Loader/Security/Diagnostics）的生命周期。
/// </summary>
public sealed class EngineBootstrap : IDisposable
{
    private readonly List<IJavaScriptPlugin> _plugins;
    private readonly ModuleDiagnosticCollector? _diagnosticCollector;
    private bool _disposed;

    /// <summary>已配置的 JavaScript 引擎实例。</summary>
    public IJavaScriptEngine Engine { get; }

    /// <summary>模块加载器（<c>--no-modules</c> 时为 null）。</summary>
    public ModuleLoader? ModuleLoader { get; }

    /// <summary>模块缓存实例（<c>--no-modules</c> 时为 null）。</summary>
    public ModuleCache? ModuleCache { get; }

    /// <summary>诊断输出管道。</summary>
    public DiagnosticOutput DiagnosticOutput { get; }

    /// <summary>模块运行时配置选项。</summary>
    public ModuleRuntimeOptions Options { get; }

    private EngineBootstrap(
        IJavaScriptEngine engine,
        ModuleLoader? moduleLoader,
        ModuleCache? moduleCache,
        DiagnosticOutput diagnosticOutput,
        ModuleRuntimeOptions options,
        List<IJavaScriptPlugin> plugins,
        ModuleDiagnosticCollector? diagnosticCollector)
    {
        Engine = engine;
        ModuleLoader = moduleLoader;
        ModuleCache = moduleCache;
        DiagnosticOutput = diagnosticOutput;
        Options = options;
        _plugins = plugins;
        _diagnosticCollector = diagnosticCollector;
    }

    /// <summary>
    /// 根据 CLI 选项创建完整引擎启动栈。
    /// </summary>
    /// <param name="options">CLI 解析结果。</param>
    /// <returns>已初始化的 <see cref="EngineBootstrap"/> 实例。</returns>
    public static EngineBootstrap Create(PaperclipOptions options)
    {
        // 1. 确定项目根目录
        var projectRoot = DetermineProjectRoot(options);

        // 2. 创建安全默认配置
        var runtimeOptions = ModuleRuntimeOptions.CreateSecureDefaults(projectRoot);

        // 3. 添加 --allow-path 白名单
        foreach (var allowPath in options.AllowPaths)
        {
            runtimeOptions.AllowedImportPathPrefixes.Add(allowPath);
        }

        // 4. --debug → 启用诊断
        if (options.Debug)
        {
            runtimeOptions.EnableDebugLogs = true;
            runtimeOptions.EnableStructuredDebugEvents = true;
        }

        // 5. 脚本主机支持 npm 包
        runtimeOptions.AllowNodeModulesResolution = true;

        // 6. 校验并规范化
        runtimeOptions.ValidateAndNormalize();

        // 7. 创建模块运行时组件
        ModuleDiagnosticCollector? diagnosticCollector = null;
        ImportSecurityPolicy? securityPolicy = null;
        ModuleResolver? resolver = null;
        ModuleCache? cache = null;
        ModuleLoader? moduleLoader = null;

        if (!options.NoModules)
        {
            diagnosticCollector = new ModuleDiagnosticCollector(options.Debug);
            securityPolicy = new ImportSecurityPolicy(runtimeOptions, diagnosticCollector);
            resolver = new ModuleResolver(
                runtimeOptions,
                builtinSpecifierMap: null,
                workspaceImportsMap: runtimeOptions.WorkspaceImportsMap,
                securityPolicy: securityPolicy);
            cache = new ModuleCache();
            moduleLoader = new ModuleLoader(resolver, cache, runtimeOptions, securityPolicy, diagnosticCollector);
        }

        // 8. 加载插件
        var plugins = options.PluginPaths.Count > 0
            ? PluginLoader.Load(options.PluginPaths)
            : new List<IJavaScriptPlugin>();

        // 9. 构建引擎
        var builder = new JavaScriptEngineBuilder()
            .WithOption(opt =>
            {
                opt.EnableScriptCaching = true;
                opt.MaxRetry = 0;
            });

        foreach (var plugin in plugins)
        {
            builder.WithPlugin(plugin);
        }

        var engine = builder.Build();

        // 10. 创建诊断输出管道
        var diagnosticOutput = new DiagnosticOutput(
            diagnosticCollector ?? new ModuleDiagnosticCollector(false),
            options.Debug);

        return new EngineBootstrap(
            engine,
            moduleLoader,
            cache,
            diagnosticOutput,
            runtimeOptions,
            plugins,
            diagnosticCollector);
    }

    /// <summary>
    /// 根据 CLI 选项确定项目根目录。
    /// </summary>
    private static string DetermineProjectRoot(PaperclipOptions options)
    {
        if (options.IsRepl || string.IsNullOrEmpty(options.ScriptPath))
        {
            return Directory.GetCurrentDirectory();
        }

        var fullPath = Path.GetFullPath(options.ScriptPath);

        if (Directory.Exists(fullPath))
        {
            return fullPath;
        }

        if (File.Exists(fullPath))
        {
            return Path.GetDirectoryName(fullPath)!;
        }

        // 路径不存在时回退到当前工作目录，交由 ScriptHost 报错
        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// 释放资源：engine → plugins → diagnosticCollector。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Engine.Dispose();

        foreach (var plugin in _plugins)
        {
            try { plugin.Dispose(); }
            catch { /* 插件 Dispose 失败不影响流程 */ }
        }

        _diagnosticCollector?.Clear();
    }
}
