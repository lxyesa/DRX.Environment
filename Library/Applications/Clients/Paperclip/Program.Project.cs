using Drx.Sdk.Shared;
using Drx.Sdk.Shared.JavaScript;

namespace DrxPaperclip;

public partial class Program
{
    /// <summary>
    /// 若当前可执行文件目录尚未写入 PATH，则将其追加到用户级 PATH 中，
    /// 同时写入 PAPERCLIP_HOME 方便脚本侧获取主机位置。
    /// </summary>
    private static void EnsurePathRegistered()
    {
        var exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

        var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
        var segments = userPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        var alreadyInPath = segments.Any(s =>
            string.Equals(s.TrimEnd(Path.DirectorySeparatorChar), exeDir, StringComparison.OrdinalIgnoreCase));

        if (!alreadyInPath)
        {
            var newPath = string.IsNullOrEmpty(userPath) ? exeDir : $"{userPath}{Path.PathSeparator}{exeDir}";
            Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);
            Logger.Info($"[Paperclip] 已将自身路径写入用户 PATH: {exeDir}");
            Logger.Info("[Paperclip] 请重启终端后即可全局使用 `paperclip` 命令。");
        }

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvVarName, EnvironmentVariableTarget.User)))
        {
            Environment.SetEnvironmentVariable(EnvVarName, exeDir, EnvironmentVariableTarget.User);
        }
    }

    private static void CreateProject(string? name)
    {
        CreateProject(name, useTypeScript: false);
    }

    private static void CreateProject(string? name, bool useTypeScript)
    {
        var targetDir = string.IsNullOrWhiteSpace(name)
            ? Directory.GetCurrentDirectory()
            : Path.Combine(Directory.GetCurrentDirectory(), name);

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
            Logger.Info($"[Paperclip] 已创建目录: {targetDir}");
        }

        var projectFile = Path.Combine(targetDir, "project.json");
        if (File.Exists(projectFile))
        {
            throw new InvalidOperationException($"[Paperclip] project.json 已存在: {projectFile}");
        }

        var projectName = string.IsNullOrWhiteSpace(name)
            ? Path.GetFileName(targetDir)
            : name;

        var entryScriptFile = useTypeScript ? DefaultTypeScriptScript : DefaultScript;

        var json = $$"""
            {
              "name": "{{projectName}}",
              "version": "1.0.0",
              "main": "{{entryScriptFile}}"
            }
            """;

        File.WriteAllText(projectFile, json);
        Logger.Info($"[Paperclip] 已创建项目: {projectFile}");

        var mainScript = Path.Combine(targetDir, entryScriptFile);
        if (!File.Exists(mainScript))
        {
            var template = useTypeScript
                ? "// Paperclip TypeScript 入口脚本（运行时已自动预加载 Models 目录下模块）\nconst { log, fs } = Paperclip.use();\n\nlog.info('Hello, Paperclip TypeScript!');\nfs.writeText('./hello.txt', 'Created by Paperclip SDK module.');\n"
                : "// Paperclip 入口脚本（运行时已自动预加载 Models 目录下模块）\nconst { log, fs } = Paperclip.use();\n\nlog.info('Hello, Paperclip!');\nfs.writeText('./hello.txt', 'Created by Paperclip SDK module.');\n";

            File.WriteAllText(mainScript, template);
            Logger.Info($"[Paperclip] 已创建入口脚本: {mainScript}");
        }

        if (useTypeScript)
        {
                        JavaScript.EnsureTypeScriptScaffold(targetDir, projectName);
        }

        EnsureModelsReleased(targetDir);
    }
}
