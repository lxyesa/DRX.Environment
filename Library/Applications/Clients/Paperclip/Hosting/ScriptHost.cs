using System.Text.Json;
using Drx.Sdk.Shared.JavaScript;
using DrxPaperclip.Cli;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 脚本执行器。处理 <c>run</c> 子命令：入口检测、TypeScript 转译、模块图加载或直接执行。
/// </summary>
public static class ScriptHost
{
    private static readonly string[] TypeScriptExtensions = [".ts", ".mts", ".cts"];
    private static readonly string[] IndexFileCandidates = ["index.js", "index.ts", "index.mjs"];

    /// <summary>
    /// 执行脚本并返回退出码。
    /// </summary>
    /// <param name="options">CLI 解析结果。</param>
    /// <param name="bootstrap">已初始化的引擎启动栈。</param>
    /// <returns>进程退出码（0 = 成功）。</returns>
    public static int Run(PaperclipOptions options, EngineBootstrap bootstrap)
    {
        var entryFile = DetectEntryPoint(options.ScriptPath!);
        var projectRoot = bootstrap.Options.ProjectRoot;

        if (options.NoModules)
        {
            ExecuteWithoutModules(entryFile, projectRoot, bootstrap);
        }
        else
        {
            ExecuteWithModules(entryFile, projectRoot, bootstrap);
        }

        // 输出诊断摘要（--debug 模式）
        bootstrap.DiagnosticOutput.Flush();
        if (bootstrap.ModuleCache != null)
        {
            bootstrap.DiagnosticOutput.PrintSummary(bootstrap.ModuleCache);
        }

        return 0;
    }

    /// <summary>
    /// 检测入口文件路径。文件直接返回；目录按 project.json → index 文件探测。
    /// </summary>
    private static string DetectEntryPoint(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (File.Exists(fullPath))
        {
            return fullPath;
        }

        if (Directory.Exists(fullPath))
        {
            // 1. 尝试 project.json
            var projectJsonPath = Path.Combine(fullPath, "project.json");
            if (File.Exists(projectJsonPath))
            {
                var mainEntry = ReadProjectJsonMain(projectJsonPath);
                if (mainEntry != null)
                {
                    var mainPath = Path.GetFullPath(Path.Combine(fullPath, mainEntry));
                    if (File.Exists(mainPath))
                    {
                        return mainPath;
                    }

                    throw new FileNotFoundException(
                        $"project.json 指定的入口文件不存在: {mainEntry}", mainPath);
                }
            }

            // 2. 回退到 index 文件
            foreach (var candidate in IndexFileCandidates)
            {
                var candidatePath = Path.Combine(fullPath, candidate);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            throw new FileNotFoundException(
                $"目录中找不到入口文件（project.json、index.js、index.ts 或 index.mjs）: {fullPath}");
        }

        throw new FileNotFoundException($"路径不存在: {fullPath}", fullPath);
    }

    /// <summary>
    /// 从 project.json 读取 main 字段。
    /// </summary>
    private static string? ReadProjectJsonMain(string projectJsonPath)
    {
        var json = File.ReadAllText(projectJsonPath);
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        if (doc.RootElement.TryGetProperty("main", out var mainElement) &&
            mainElement.ValueKind == JsonValueKind.String)
        {
            return mainElement.GetString();
        }

        return null;
    }

    /// <summary>
    /// 非模块模式执行（--no-modules）。
    /// </summary>
    private static void ExecuteWithoutModules(string entryFile, string projectRoot, EngineBootstrap bootstrap)
    {
        if (IsTypeScript(entryFile))
        {
            var jsSource = JavaScript.TranspileTypeScriptFile(entryFile, projectRoot);
            bootstrap.Engine.Execute(jsSource);
        }
        else
        {
            bootstrap.Engine.ExecuteFile(entryFile);
        }
    }

    /// <summary>
    /// 模块模式执行：通过 ModuleLoader 加载模块图。
    /// </summary>
    private static void ExecuteWithModules(string entryFile, string projectRoot, EngineBootstrap bootstrap)
    {
        var moduleLoader = bootstrap.ModuleLoader
            ?? throw new InvalidOperationException("模块系统未初始化，但未指定 --no-modules。");

        moduleLoader.LoadModuleGraph(entryFile, (filePath, source) =>
        {
            if (IsTypeScript(filePath))
            {
                var jsSource = JavaScript.TranspileTypeScriptFile(filePath, projectRoot);
                return bootstrap.Engine.Execute(jsSource);
            }

            return bootstrap.Engine.Execute(source);
        });
    }

    /// <summary>
    /// 判断文件是否为 TypeScript。
    /// </summary>
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
}
