using System.Text.Json;

namespace DrxPaperclip.Hosting;

/// <summary>
/// project 子命令处理器。负责创建与删除 Paperclip 项目目录。
/// 关键依赖：System.IO / System.Text.Json。
/// </summary>
public static class ProjectHost
{
    /// <summary>
    /// 从程序集嵌入资源中读取 global.d.ts 内容。
    /// </summary>
    private static string LoadEmbeddedGlobalDts()
    {
        var assembly = typeof(ProjectHost).Assembly;
        using var stream = assembly.GetManifestResourceStream("DrxPaperclip.Resources.global.d.ts")
            ?? throw new InvalidOperationException("嵌入资源 global.d.ts 未找到。");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 从程序集嵌入资源中读取指定模板文件内容。
    /// </summary>
    private static string LoadEmbeddedResource(string logicalName)
    {
        var assembly = typeof(ProjectHost).Assembly;
        using var stream = assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"嵌入资源 {logicalName} 未找到。");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 创建一个新的 Paperclip 项目目录，并生成 <c>project.json</c>、<c>main.ts</c> 与 <c>tsconfig.json</c>。
    /// </summary>
    /// <param name="projectName">项目目录名（相对当前工作目录）。</param>
    public static void CreateProject(string projectName)
    {
        ValidateProjectName(projectName);

        var projectDirectory = Path.GetFullPath(Directory.GetCurrentDirectory());

        var projectJsonPath = Path.Combine(projectDirectory, "project.json");
        if (File.Exists(projectJsonPath))
        {
            throw new IOException($"当前目录已存在 project.json，无法重复初始化。");
        }

        var mainFilePath = Path.Combine(projectDirectory, "main.ts");
        if (!File.Exists(mainFilePath))
        {
            var template =
                $"function main(): void {{{Environment.NewLine}" +
                $"    console.log(\"Hello from {projectName}!\");{Environment.NewLine}" +
                $"}}{Environment.NewLine}";
            File.WriteAllText(mainFilePath, template);
        }

        var globalDtsPath = Path.Combine(projectDirectory, "global.d.ts");
        if (!File.Exists(globalDtsPath))
        {
            File.WriteAllText(globalDtsPath, LoadEmbeddedGlobalDts());
        }

        var tsconfigPath = Path.Combine(projectDirectory, "tsconfig.json");
        if (!File.Exists(tsconfigPath))
        {
            var tsconfig =
                                """
                                {
                                    "compilerOptions": {
                                        "target": "ES2020",
                                        "module": "ESNext",
                                        "moduleResolution": "Bundler",
                                        "strict": true,
                                        "skipLibCheck": true,
                                        "types": []
                                    },
                                    "include": [
                                        "*.ts",
                                        "**/*.ts"
                                    ],
                                    "exclude": [
                                        "node_modules"
                                    ]
                                }
                                """;
            File.WriteAllText(tsconfigPath, tsconfig + Environment.NewLine);
        }

        var projectConfig = new
        {
            entryModule = "main.ts",
            entryFunction = "main"
        };
        var json = JsonSerializer.Serialize(projectConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(projectJsonPath, json + Environment.NewLine);

        var batFilePath = Path.Combine(projectDirectory, $"{projectName}.bat");
        var batContent =
            $"@echo off{Environment.NewLine}" +
            $"paperclip run .{Environment.NewLine}";
        File.WriteAllText(batFilePath, batContent);
    }

    /// <summary>
    /// 创建一个基于 HTTP 服务器模板的 Paperclip 项目目录，
    /// 包含 <c>main.ts</c>、<c>project.json</c>、<c>tsconfig.json</c>、<c>global.d.ts</c> 及 <c>public/index.html</c>。
    /// </summary>
    /// <param name="projectName">项目目录名（相对当前工作目录）。</param>
    public static void CreateHttpProject(string projectName)
    {
        ValidateProjectName(projectName);

        var projectDirectory = Path.GetFullPath(Directory.GetCurrentDirectory());

        var projectJsonPath = Path.Combine(projectDirectory, "project.json");
        if (File.Exists(projectJsonPath))
        {
            throw new IOException($"当前目录已存在 project.json，无法重复初始化。");
        }

        // main.ts — HTTP 服务器入口
        var mainFilePath = Path.Combine(projectDirectory, "main.ts");
        if (!File.Exists(mainFilePath))
        {
            File.WriteAllText(mainFilePath, LoadEmbeddedResource("DrxPaperclip.Resources.templates.http_server.main.ts"));
        }

        // global.d.ts — 类型声明
        var globalDtsPath = Path.Combine(projectDirectory, "global.d.ts");
        if (!File.Exists(globalDtsPath))
        {
            File.WriteAllText(globalDtsPath, LoadEmbeddedGlobalDts());
        }

        // tsconfig.json
        var tsconfigPath = Path.Combine(projectDirectory, "tsconfig.json");
        if (!File.Exists(tsconfigPath))
        {
            File.WriteAllText(tsconfigPath, LoadEmbeddedResource("DrxPaperclip.Resources.templates.http_server.tsconfig.json"));
        }

        // project.json
        File.WriteAllText(projectJsonPath, LoadEmbeddedResource("DrxPaperclip.Resources.templates.http_server.project.json"));

        // public/index.html — 默认首页
        var publicDir = Path.Combine(projectDirectory, "public");
        Directory.CreateDirectory(publicDir);
        var indexHtmlPath = Path.Combine(publicDir, "index.html");
        if (!File.Exists(indexHtmlPath))
        {
            File.WriteAllText(indexHtmlPath, LoadEmbeddedResource("DrxPaperclip.Resources.templates.http_server.public.index.html"));
        }

        // 启动批处理
        var batFilePath = Path.Combine(projectDirectory, $"{projectName}.bat");
        var batContent =
            $"@echo off{Environment.NewLine}" +
            $"paperclip run .{Environment.NewLine}";
        File.WriteAllText(batFilePath, batContent);
    }

    /// <summary>
    /// 删除指定的 Paperclip 项目目录。
    /// </summary>
    /// <param name="projectName">项目目录名（相对当前工作目录）。</param>
    public static void DeleteProject(string projectName)
    {
        ValidateProjectName(projectName);

        var currentDirectory = Path.GetFullPath(Directory.GetCurrentDirectory());
        var projectDirectory = Path.GetFullPath(Path.Combine(currentDirectory, projectName));

        if (!projectDirectory.StartsWith(currentDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("禁止删除当前工作目录之外的路径。请仅提供项目名。");
        }

        if (!Directory.Exists(projectDirectory))
        {
            throw new DirectoryNotFoundException($"项目不存在: {projectName}");
        }

        Directory.Delete(projectDirectory, recursive: true);
    }

    /// <summary>
    /// 校验项目名，避免空值、根路径与目录穿越输入。
    /// </summary>
    private static void ValidateProjectName(string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            throw new ArgumentException("项目名不能为空。", nameof(projectName));
        }

        if (Path.IsPathRooted(projectName) ||
            projectName.Contains("..", StringComparison.Ordinal) ||
            projectName.Contains(Path.DirectorySeparatorChar) ||
            projectName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("项目名必须是单级目录名，不能包含路径分隔符。", nameof(projectName));
        }

        if (projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("项目名包含非法字符。", nameof(projectName));
        }
    }
}
