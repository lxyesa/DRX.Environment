using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Drx.Sdk.Shared.JavaScript;
using DrxPaperclip.Cli;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 脚本执行器。处理 <c>run</c> 子命令：入口检测、TypeScript 转译、模块图加载或直接执行。
/// </summary>
public static class ScriptHost
{
    private sealed record ResolvedEntryPoint(string EntryFile, string? EntryFunction);
    private sealed class PrecompileMapPayload
    {
        public int Version { get; set; } = 2;

        public Dictionary<string, PrecompileMapEntry> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class PrecompileMapEntry
    {
        public string SourceHash { get; set; } = string.Empty;

        public string TypeScriptVersion { get; set; } = string.Empty;

        /// <summary>转译配置指纹，对应 <c>JavaScript.TranspileConfigTag</c>。</summary>
        public string TranspileConfigTag { get; set; } = string.Empty;

        public string JsRelativePath { get; set; } = string.Empty;
    }

    private static readonly string[] TypeScriptExtensions = [".ts", ".mts", ".cts"];
    private static readonly string[] DefaultTypeScriptIncludePatterns = ["**/*.ts", "**/*.mts", "**/*.cts"];
    private static readonly string[] DefaultTypeScriptExcludePatterns = ["node_modules/**"];
    private static readonly string[] IndexFileCandidates = ["index.js", "index.ts", "index.mjs"];
    private const string PrecompileOutDirectoryName = "out";
    private const string PrecompileMapFileName = "map";

    /// <summary>
    /// 执行脚本并返回退出码。
    /// </summary>
    /// <param name="options">CLI 解析结果。</param>
    /// <param name="bootstrap">已初始化的引擎启动栈。</param>
    /// <param name="cancellationToken">取消令牌（watch 模式重载时触发）。</param>
    /// <returns>进程退出码（0 = 成功）。</returns>
    public static int Run(PaperclipOptions options, EngineBootstrap bootstrap, CancellationToken cancellationToken = default)
    {
        var resolvedEntry = DetectEntryPoint(options.ScriptPath!);
        var entryFile = resolvedEntry.EntryFile;
        var entryFunction = !string.IsNullOrWhiteSpace(options.RunFunctionName)
            ? options.RunFunctionName
            : resolvedEntry.EntryFunction;
        var projectRoot = bootstrap.Options.ProjectRoot;

        PrecompileTrackedTypeScriptScripts(projectRoot, bootstrap, options.Debug);

        if (options.NoModules)
        {
            var executionResult = ExecuteWithoutModules(entryFile, projectRoot, bootstrap, options.Debug, entryFunction);
            WaitForPendingTask(executionResult, "入口脚本执行结果", cancellationToken);
        }
        else
        {
            var executionResult = ExecuteWithModules(entryFile, projectRoot, bootstrap, options.Debug, entryFunction);
            WaitForPendingTask(executionResult, "模块入口执行结果", cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(entryFunction))
        {
            var entryResult = ExecuteEntryFunction(bootstrap, entryFunction!);
            WaitForPendingTask(entryResult, $"入口函数 '{entryFunction}' 返回值", cancellationToken);
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
    /// 按 tsconfig 追踪集合执行预编译（仅编译，不执行脚本）。
    /// </summary>
    private static void PrecompileTrackedTypeScriptScripts(string projectRoot, EngineBootstrap bootstrap, bool debug)
    {
        var trackedScripts = ResolveTsConfigTrackedTypeScriptFiles(projectRoot);
        if (trackedScripts.Count == 0)
        {
            return;
        }

        var transpileCache = bootstrap.TranspileCache;
        var typeScriptVersion = ResolveTypeScriptVersionTag(projectRoot);

        foreach (var scriptPath in trackedScripts)
        {
            if (IsTypeScriptDeclaration(scriptPath))
            {
                continue;
            }

            var sourceContent = File.ReadAllText(scriptPath);

            if (TryGetPrecompiledJs(scriptPath, sourceContent, typeScriptVersion, projectRoot, out var cachedOutputPath))
            {
                if (debug)
                {
                    Console.Error.WriteLine($"[precompile] HIT  {scriptPath} -> {cachedOutputPath}");
                }

                continue;
            }

            if (transpileCache != null &&
                transpileCache.TryGet(scriptPath, sourceContent, typeScriptVersion, JavaScript.TranspileConfigTag, out var cachedCode) &&
                !string.IsNullOrEmpty(cachedCode))
            {
                var persistedPath = PersistPrecompiledJs(scriptPath, cachedCode, sourceContent, typeScriptVersion, projectRoot);
                if (debug)
                {
                    Console.Error.WriteLine($"[precompile] BUILD {scriptPath} -> {persistedPath ?? "<memory>"} (from transpile-cache)");
                }

                continue;
            }

            var jsSource = JavaScript.TranspileTypeScriptFile(scriptPath, projectRoot);
            transpileCache?.Set(scriptPath, sourceContent, typeScriptVersion, JavaScript.TranspileConfigTag, jsSource);
            var outputPath = PersistPrecompiledJs(scriptPath, jsSource, sourceContent, typeScriptVersion, projectRoot);

            if (debug)
            {
                Console.Error.WriteLine($"[precompile] BUILD {scriptPath} -> {outputPath ?? "<memory>"}");
            }
        }
    }

    /// <summary>
    /// 解析 tsconfig 追踪的 TypeScript 脚本集合（files/include/exclude）。
    /// </summary>
    private static List<string> ResolveTsConfigTrackedTypeScriptFiles(string projectRoot)
    {
        var tsconfigPath = Path.Combine(projectRoot, "tsconfig.json");
        if (!File.Exists(tsconfigPath))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(tsconfigPath), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        var root = doc.RootElement;
        var includePatterns = ReadStringArray(root, "include");
        var excludePatterns = ReadStringArray(root, "exclude");
        var fileEntries = ReadStringArray(root, "files");

        if (includePatterns.Count == 0 && fileEntries.Count == 0)
        {
            includePatterns.AddRange(DefaultTypeScriptIncludePatterns);
        }

        if (excludePatterns.Count == 0)
        {
            excludePatterns.AddRange(DefaultTypeScriptExcludePatterns);
        }

        var tracked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fileEntry in fileEntries)
        {
            if (string.IsNullOrWhiteSpace(fileEntry))
            {
                continue;
            }

            var candidate = Path.GetFullPath(Path.Combine(projectRoot, fileEntry));
            if (!File.Exists(candidate) || !IsTypeScript(candidate) || IsTypeScriptDeclaration(candidate))
            {
                continue;
            }

            tracked.Add(candidate);
        }

        var projectRootFullPath = Path.GetFullPath(projectRoot);
        foreach (var candidate in Directory.EnumerateFiles(projectRootFullPath, "*", SearchOption.AllDirectories))
        {
            if (!IsTypeScript(candidate) || IsTypeScriptDeclaration(candidate))
            {
                continue;
            }

            var normalizedRelativePath = NormalizeRelativePath(Path.GetRelativePath(projectRootFullPath, candidate));
            if (normalizedRelativePath.StartsWith("../", StringComparison.Ordinal) ||
                string.Equals(normalizedRelativePath, "..", StringComparison.Ordinal))
            {
                continue;
            }

            var isIncluded = includePatterns.Exists(pattern => IsGlobMatch(normalizedRelativePath, pattern));
            if (!isIncluded)
            {
                continue;
            }

            var isExcluded = excludePatterns.Exists(pattern => IsGlobMatch(normalizedRelativePath, pattern));
            if (isExcluded)
            {
                continue;
            }

            tracked.Add(candidate);
        }

        return tracked.OrderBy(static p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ReadStringArray(JsonElement root, string propertyName)
    {
        var result = new List<string>();
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var text = item.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            result.Add(text!);
        }

        return result;
    }

    private static bool IsTypeScriptDeclaration(string filePath)
    {
        return filePath.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith(".d.mts", StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith(".d.cts", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static bool IsGlobMatch(string relativePath, string pattern)
    {
        var normalizedPath = NormalizeRelativePath(relativePath).TrimStart('/');
        var normalizedPattern = NormalizeRelativePath(pattern).Trim();

        if (normalizedPattern.StartsWith("./", StringComparison.Ordinal))
        {
            normalizedPattern = normalizedPattern[2..];
        }

        if (normalizedPattern.Length == 0)
        {
            return false;
        }

        if (normalizedPattern.EndsWith("/", StringComparison.Ordinal))
        {
            normalizedPattern += "**";
        }

        var regexPattern = "^" + Regex.Escape(normalizedPattern)
            .Replace("\\*\\*", "__DOUBLE_STAR__", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal)
            .Replace("\\?", "[^/]", StringComparison.Ordinal)
            .Replace("__DOUBLE_STAR__", ".*", StringComparison.Ordinal)
            + "$";

        return Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// 检测入口文件路径。文件直接返回；目录按 project.json → index 文件探测。
    /// </summary>
    private static ResolvedEntryPoint DetectEntryPoint(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (File.Exists(fullPath))
        {
            return new ResolvedEntryPoint(fullPath, null);
        }

        if (Directory.Exists(fullPath))
        {
            // 1. 尝试 project.json
            var projectJsonPath = Path.Combine(fullPath, "project.json");
            if (File.Exists(projectJsonPath))
            {
                var projectEntry = ReadProjectEntry(projectJsonPath);
                if (string.IsNullOrWhiteSpace(projectEntry.EntryModule))
                {
                    throw new InvalidDataException(
                        $"project.json 缺少入口模块字段（entryModule 或 main）: {projectJsonPath}");
                }

                var mainPath = Path.GetFullPath(Path.Combine(fullPath, projectEntry.EntryModule));
                if (!File.Exists(mainPath))
                {
                    throw new FileNotFoundException(
                        $"project.json 指定的入口文件不存在: {projectEntry.EntryModule}", mainPath);
                }

                return new ResolvedEntryPoint(mainPath, projectEntry.EntryFunction);
            }

            // 2. 回退到 index 文件
            foreach (var candidate in IndexFileCandidates)
            {
                var candidatePath = Path.Combine(fullPath, candidate);
                if (File.Exists(candidatePath))
                {
                    return new ResolvedEntryPoint(candidatePath, null);
                }
            }

            throw new FileNotFoundException(
                $"目录中找不到入口文件（project.json、index.js、index.ts 或 index.mjs）: {fullPath}");
        }

        // 路径不存在时，回退到当前工作目录的 project.json（就地项目）
        var cwdProjectJson = Path.Combine(Directory.GetCurrentDirectory(), "project.json");
        if (File.Exists(cwdProjectJson))
        {
            var cwd = Path.GetFullPath(Directory.GetCurrentDirectory());
            var projectEntry = ReadProjectEntry(cwdProjectJson);
            if (string.IsNullOrWhiteSpace(projectEntry.EntryModule))
            {
                throw new InvalidDataException(
                    $"project.json 缺少入口模块字段（entryModule 或 main）: {cwdProjectJson}");
            }

            var mainPath = Path.GetFullPath(Path.Combine(cwd, projectEntry.EntryModule));
            if (!File.Exists(mainPath))
            {
                throw new FileNotFoundException(
                    $"project.json 指定的入口文件不存在: {projectEntry.EntryModule}", mainPath);
            }

            return new ResolvedEntryPoint(mainPath, projectEntry.EntryFunction);
        }

        throw new FileNotFoundException($"路径不存在: {fullPath}", fullPath);
    }

    /// <summary>
    /// 从 project.json 读取 main 字段。
    /// </summary>
    private static (string? EntryModule, string? EntryFunction) ReadProjectEntry(string projectJsonPath)
    {
        var json = File.ReadAllText(projectJsonPath);
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        string? entryModule = null;
        string? entryFunction = null;

        if (doc.RootElement.TryGetProperty("entryModule", out var entryModuleElement) &&
            entryModuleElement.ValueKind == JsonValueKind.String)
        {
            entryModule = entryModuleElement.GetString();
        }

        // 兼容历史字段
        if (string.IsNullOrWhiteSpace(entryModule) &&
            doc.RootElement.TryGetProperty("main", out var mainElement) &&
            mainElement.ValueKind == JsonValueKind.String)
        {
            entryModule = mainElement.GetString();
        }

        if (doc.RootElement.TryGetProperty("entryFunction", out var entryFunctionElement) &&
            entryFunctionElement.ValueKind == JsonValueKind.String)
        {
            entryFunction = entryFunctionElement.GetString();
        }

        return (entryModule, entryFunction);
    }

    /// <summary>
    /// 非模块模式执行（--no-modules）。
    /// </summary>
    private static object? ExecuteWithoutModules(string entryFile, string projectRoot, EngineBootstrap bootstrap, bool debug, string? entryFunction = null)
    {
        if (IsTypeScript(entryFile))
        {
            return ExecuteTypeScriptWithOptionalCache(entryFile, projectRoot, bootstrap, debug, entryFunctionBinding: entryFunction);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(entryFunction))
            {
                var source = InjectEntryFunctionBinding(File.ReadAllText(entryFile), entryFunction!);
                return bootstrap.Engine.Execute(source);
            }

            return bootstrap.Engine.ExecuteFile(entryFile);
        }
    }

    /// <summary>
    /// 模块模式执行：通过 ModuleLoader 加载模块图。
    /// </summary>
    private static object? ExecuteWithModules(string entryFile, string projectRoot, EngineBootstrap bootstrap, bool debug, string? entryFunction = null)
    {
        var moduleLoader = bootstrap.ModuleLoader
            ?? throw new InvalidOperationException("模块系统未初始化，但未指定 --no-modules。");

        // 注册 CJS require 桥接函数：将 specifier + 当前文件路径解析为绝对路径，
        // 然后从模块缓存中查找已加载模块的 Namespace 作为 require() 返回值。
        string currentExecutingFile = entryFile;
        Func<string, object?> requireBridge = (specifier) =>
        {
            string resolvedPath;
            if (specifier.StartsWith("./", StringComparison.Ordinal) ||
                specifier.StartsWith("../", StringComparison.Ordinal))
            {
                var baseDir = Path.GetDirectoryName(currentExecutingFile) ?? projectRoot;
                resolvedPath = Path.GetFullPath(Path.Combine(baseDir, specifier));

                // 若无扩展名，尝试补充 .ts / .js
                if (!File.Exists(resolvedPath))
                {
                    foreach (var ext in new[] { ".ts", ".js", ".mjs" })
                    {
                        var candidate = resolvedPath + ext;
                        if (File.Exists(candidate)) { resolvedPath = candidate; break; }
                    }
                }
            }
            else
            {
                // 非相对路径（内置模块等）：不支持
                throw new InvalidOperationException($"[CJS] require() 仅支持相对路径，不支持: {specifier}");
            }

            if (moduleLoader.TryGetLoadedExports(resolvedPath, out var moduleNamespace) && moduleNamespace is not null)
            {
                return moduleNamespace;
            }

            // 模块尚未加载（不应发生，依赖已在 LoadModuleGraph 中预先递归加载）
            throw new InvalidOperationException($"[CJS] require() 目标模块未加载: {resolvedPath}");
        };

        bootstrap.Engine.RegisterGlobal("__drxRequireNative", requireBridge);

        var entryRecord = moduleLoader.LoadModuleGraph(entryFile, (filePath, source) =>
        {
            currentExecutingFile = filePath;
            var isEntryFile = string.Equals(
                Path.GetFullPath(filePath), Path.GetFullPath(entryFile),
                StringComparison.OrdinalIgnoreCase);
            var bindingName = isEntryFile ? entryFunction : null;

            if (IsTypeScript(filePath))
            {
                return ExecuteTypeScriptWithOptionalCache(
                    filePath,
                    projectRoot,
                    bootstrap,
                    debug,
                    sourceContentOverride: source,
                    entryFunctionBinding: bindingName);
            }

            var effectiveSource = !string.IsNullOrWhiteSpace(bindingName)
                ? InjectEntryFunctionBinding(source, bindingName!)
                : source;
            return bootstrap.Engine.Execute(effectiveSource);
        });

        return entryRecord.Namespace;
    }

    /// <summary>
    /// 执行 TypeScript 文件，并在启用时复用转译缓存。
    /// </summary>
    private static object? ExecuteTypeScriptWithOptionalCache(
        string filePath,
        string projectRoot,
        EngineBootstrap bootstrap,
        bool debug,
        string? sourceContentOverride = null,
        string? entryFunctionBinding = null)
    {
        var sourceContent = sourceContentOverride ?? File.ReadAllText(filePath);
        var transpileCache = bootstrap.TranspileCache;
        var typeScriptVersion = ResolveTypeScriptVersionTag(projectRoot);

        if (TryGetPrecompiledJs(filePath, sourceContent, typeScriptVersion, projectRoot, out var precompiledJsFile))
        {
            if (debug)
            {
                Console.Error.WriteLine($"[precompile] HIT  {filePath} -> {precompiledJsFile}");
            }

            if (!string.IsNullOrWhiteSpace(entryFunctionBinding))
            {
                var precompiledSource = InjectEntryFunctionBinding(File.ReadAllText(precompiledJsFile), entryFunctionBinding!);
                return bootstrap.Engine.Execute(precompiledSource);
            }

            return bootstrap.Engine.ExecuteFile(precompiledJsFile);
        }

        if (transpileCache != null &&
            transpileCache.TryGet(filePath, sourceContent, typeScriptVersion, JavaScript.TranspileConfigTag, out var cachedCode) &&
            !string.IsNullOrEmpty(cachedCode))
        {
            if (debug)
            {
                Console.Error.WriteLine($"[transpile-cache] HIT  {filePath}");
            }

            var jsOutputPath = PersistPrecompiledJs(filePath, cachedCode, sourceContent, typeScriptVersion, projectRoot);
            if (!string.IsNullOrEmpty(jsOutputPath) && File.Exists(jsOutputPath))
            {
                if (!string.IsNullOrWhiteSpace(entryFunctionBinding))
                {
                    var cached = InjectEntryFunctionBinding(File.ReadAllText(jsOutputPath), entryFunctionBinding!);
                    return bootstrap.Engine.Execute(cached);
                }

                return bootstrap.Engine.ExecuteFile(jsOutputPath);
            }

            return bootstrap.Engine.Execute(
                !string.IsNullOrWhiteSpace(entryFunctionBinding)
                    ? InjectEntryFunctionBinding(cachedCode, entryFunctionBinding!)
                    : cachedCode);
        }

        if (debug && transpileCache != null)
        {
            Console.Error.WriteLine($"[transpile-cache] MISS {filePath}");
        }

        var jsSource = JavaScript.TranspileTypeScriptFile(filePath, projectRoot);
        transpileCache?.Set(filePath, sourceContent, typeScriptVersion, JavaScript.TranspileConfigTag, jsSource);
        var outputPath = PersistPrecompiledJs(filePath, jsSource, sourceContent, typeScriptVersion, projectRoot);
        if (!string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))
        {
            if (debug)
            {
                Console.Error.WriteLine($"[precompile] BUILD {filePath} -> {outputPath}");
            }

            if (!string.IsNullOrWhiteSpace(entryFunctionBinding))
            {
                var built = InjectEntryFunctionBinding(File.ReadAllText(outputPath), entryFunctionBinding!);
                return bootstrap.Engine.Execute(built);
            }

            return bootstrap.Engine.ExecuteFile(outputPath);
        }

        return bootstrap.Engine.Execute(
            !string.IsNullOrWhiteSpace(entryFunctionBinding)
                ? InjectEntryFunctionBinding(jsSource, entryFunctionBinding!)
                : jsSource);
    }

    /// <summary>
    /// 生成入口函数自动绑定脚本片段，将同名函数声明挂载到 globalThis。
    /// </summary>
    private static string GenerateEntryFunctionBinding(string functionName)
    {
        var escaped = functionName
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal);
        return $"\ntry {{ if (typeof {escaped} === 'function') globalThis[\"{escaped}\"] = {escaped}; }} catch(e) {{}}\n";
    }

    /// <summary>
    /// 将入口函数绑定片段注入到 JS 源码中。
    /// 若源码包含 CJS 包装（<c>return module.exports;</c>），则在其前方注入，保证在 IIFE 作用域内；
    /// 否则追加到末尾。
    /// </summary>
    private static string InjectEntryFunctionBinding(string jsSource, string functionName)
    {
        var binding = GenerateEntryFunctionBinding(functionName);
        const string cjsReturnMarker = "return module.exports;";
        var insertIndex = jsSource.LastIndexOf(cjsReturnMarker, StringComparison.Ordinal);
        if (insertIndex >= 0)
        {
            return jsSource.Insert(insertIndex, binding);
        }
        return jsSource + binding;
    }

    private static bool TryGetPrecompiledJs(
        string scriptPath,
        string sourceContent,
        string typeScriptVersion,
        string projectRoot,
        out string jsFilePath)
    {
        jsFilePath = string.Empty;

        var map = LoadPrecompileMap(projectRoot);
        if (map is null || map.Version != 2)
        {
            return false;
        }

        var scriptKey = GetPrecompileMapKey(scriptPath, projectRoot);
        if (!map.Entries.TryGetValue(scriptKey, out var entry))
        {
            return false;
        }

        var currentHash = ComputeSha256(sourceContent);
        if (!string.Equals(entry.SourceHash, currentHash, StringComparison.Ordinal) ||
            !string.Equals(entry.TypeScriptVersion, typeScriptVersion, StringComparison.Ordinal) ||
            !string.Equals(entry.TranspileConfigTag, JavaScript.TranspileConfigTag, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(entry.JsRelativePath))
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(GetPrecompileOutDirectory(projectRoot), entry.JsRelativePath));
        if (!File.Exists(candidate))
        {
            return false;
        }

        jsFilePath = candidate;
        return true;
    }

    private static string? PersistPrecompiledJs(
        string scriptPath,
        string jsSource,
        string sourceContent,
        string typeScriptVersion,
        string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(scriptPath) ||
            string.IsNullOrEmpty(jsSource) ||
            string.IsNullOrEmpty(sourceContent) ||
            string.IsNullOrWhiteSpace(typeScriptVersion) ||
            string.IsNullOrWhiteSpace(projectRoot))
        {
            return null;
        }

        try
        {
            var outDirectory = GetPrecompileOutDirectory(projectRoot);
            Directory.CreateDirectory(outDirectory);

            var jsFilePath = GetPrecompiledJsPath(scriptPath, projectRoot);
            var parent = Path.GetDirectoryName(jsFilePath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllText(jsFilePath, jsSource, Encoding.UTF8);

            var map = LoadPrecompileMap(projectRoot) ?? new PrecompileMapPayload();
            var scriptKey = GetPrecompileMapKey(scriptPath, projectRoot);
            var relativeJs = Path.GetRelativePath(outDirectory, jsFilePath).Replace('\\', '/');
            map.Entries[scriptKey] = new PrecompileMapEntry
            {
                SourceHash = ComputeSha256(sourceContent),
                TypeScriptVersion = typeScriptVersion,
                TranspileConfigTag = JavaScript.TranspileConfigTag,
                JsRelativePath = relativeJs
            };

            SavePrecompileMap(projectRoot, map);
            return jsFilePath;
        }
        catch
        {
            return null;
        }
    }

    private static string GetPrecompiledJsPath(string scriptPath, string projectRoot)
    {
        var outDirectory = GetPrecompileOutDirectory(projectRoot);
        var fullScriptPath = Path.GetFullPath(scriptPath);
        var fullProjectRoot = Path.GetFullPath(projectRoot);
        var relativePath = Path.GetRelativePath(fullProjectRoot, fullScriptPath);

        if (relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            var externalDir = Path.Combine(outDirectory, "external");
            Directory.CreateDirectory(externalDir);
            var hash = ComputeSha256(fullScriptPath);
            return Path.Combine(externalDir, $"{hash}.js");
        }

        var jsRelative = Path.ChangeExtension(relativePath, ".js");
        return Path.GetFullPath(Path.Combine(outDirectory, jsRelative));
    }

    private static string GetPrecompileMapKey(string scriptPath, string projectRoot)
    {
        var fullScriptPath = Path.GetFullPath(scriptPath);
        var fullProjectRoot = Path.GetFullPath(projectRoot);
        var relativePath = Path.GetRelativePath(fullProjectRoot, fullScriptPath);

        if (relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            return fullScriptPath.Replace('\\', '/');
        }

        return relativePath.Replace('\\', '/');
    }

    private static string GetPrecompileOutDirectory(string projectRoot)
    {
        return Path.Combine(Path.GetFullPath(projectRoot), PrecompileOutDirectoryName);
    }

    private static string GetPrecompileMapFilePath(string projectRoot)
    {
        return Path.Combine(GetPrecompileOutDirectory(projectRoot), PrecompileMapFileName);
    }

    private static PrecompileMapPayload? LoadPrecompileMap(string projectRoot)
    {
        var mapPath = GetPrecompileMapFilePath(projectRoot);
        if (!File.Exists(mapPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(mapPath, Encoding.UTF8);
            var payload = JsonSerializer.Deserialize<PrecompileMapPayload>(json);
            if (payload == null)
            {
                return null;
            }

            payload.Entries ??= new Dictionary<string, PrecompileMapEntry>(StringComparer.OrdinalIgnoreCase);
            return payload;
        }
        catch
        {
            return null;
        }
    }

    private static void SavePrecompileMap(string projectRoot, PrecompileMapPayload payload)
    {
        var mapPath = GetPrecompileMapFilePath(projectRoot);
        var tempPath = mapPath + ".tmp";

        var directory = Path.GetDirectoryName(mapPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(payload);
        File.WriteAllText(tempPath, json, Encoding.UTF8);

        if (File.Exists(mapPath))
        {
            File.Copy(tempPath, mapPath, overwrite: true);
            File.Delete(tempPath);
        }
        else
        {
            File.Move(tempPath, mapPath);
        }
    }

    private static string ComputeSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// 解析 TypeScript 版本标识；失败时返回可稳定比较的降级标识。
    /// </summary>
    private static string ResolveTypeScriptVersionTag(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current != null)
        {
            var packageJson = Path.Combine(current.FullName, "node_modules", "typescript", "package.json");
            if (File.Exists(packageJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(packageJson));
                    if (doc.RootElement.TryGetProperty("version", out var versionElement) &&
                        versionElement.ValueKind == JsonValueKind.String)
                    {
                        var version = versionElement.GetString();
                        if (!string.IsNullOrWhiteSpace(version))
                        {
                            return version!;
                        }
                    }
                }
                catch
                {
                    // 忽略解析错误，继续回退。
                }
            }

            current = current.Parent;
        }

        return "unknown";
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

    /// <summary>
    /// 执行已加载入口脚本中的全局入口函数。
    /// </summary>
    private static object? ExecuteEntryFunction(EngineBootstrap bootstrap, string functionName)
    {
        var escapedName = functionName
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal);

        var script =
            "(() => {\n" +
            $"  const fn = globalThis[\"{escapedName}\"];\n" +
            "  if (typeof fn !== \"function\") {\n" +
            $"    throw new Error(\"入口函数不存在: {escapedName}\");\n" +
            "  }\n" +
            "  return fn();\n" +
            "})();";

        return bootstrap.Engine.Execute(script);
    }

    /// <summary>
    /// 若脚本返回 Task（通常来自 JS Promise），则在当前线程等待完成，
    /// 防止 CLI 在异步链结束前提前退出。
    /// 支持 CancellationToken 以在 watch 模式重载时中断等待。
    /// </summary>
    private static void WaitForPendingTask(object? value, string context, CancellationToken cancellationToken = default)
    {
        if (value is not Task task)
        {
            return;
        }

        try
        {
            if (cancellationToken.CanBeCanceled)
            {
                // watch 模式：等待 task 完成或取消信号
                task.Wait(cancellationToken);
            }
            else
            {
                task.GetAwaiter().GetResult();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 && ex.InnerException is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"等待{context}失败：{ex.Message}", ex);
        }
    }
}
