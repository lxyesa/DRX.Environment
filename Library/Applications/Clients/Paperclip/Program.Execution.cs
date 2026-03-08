using Drx.Sdk.Shared;
using Drx.Sdk.Shared.JavaScript;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Drx.Sdk.Shared.JavaScript.Engine;
using Microsoft.ClearScript;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace DrxPaperclip;

public partial class Program
{
    private static void ExecuteTypeScriptCommand(string[] args)
    {
        TypeScriptCommandHandler.Execute(args);
    }

    private readonly record struct ModuleRunOptions(
        string ScriptName,
        bool EnableDebugLogs,
        string? ConfigPath,
        IReadOnlyList<string> AdditionalAllowedImportPaths,
        bool EnableStructuredDebugEvents);

    /// <summary>
    /// Module Runtime 执行入口。按 CLI + 配置文件装载 ModuleRuntimeOptions，
    /// 在脚本执行前应用安全边界与调试输出策略。
    /// </summary>
    private static async Task RunModuleScriptAsync(ModuleRunOptions runOptions)
    {
        var scriptPath = ResolveScriptPath(runOptions.ScriptName);

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"[Paperclip] 找不到脚本文件: {scriptPath}", scriptPath);
        }

        var scriptDirectory = Path.GetDirectoryName(scriptPath);
        var workingDirectory = string.IsNullOrWhiteSpace(scriptDirectory)
            ? Directory.GetCurrentDirectory()
            : scriptDirectory;

        var moduleRuntimeOptions = BuildModuleRuntimeOptions(runOptions, workingDirectory);
        EnsurePathAllowedByModuleSecurity(scriptPath, moduleRuntimeOptions);

        var previousDebugEnvValue = Environment.GetEnvironmentVariable(ExportRegistrationDebugEnvVar);
        Environment.SetEnvironmentVariable(
            ExportRegistrationDebugEnvVar,
            moduleRuntimeOptions.EnableDebugLogs ? "1" : null);

        try
        {
            Logger.Info($"[Paperclip] Module Runtime 已激活，项目根目录: {moduleRuntimeOptions.ProjectRoot}");
            RegisterDirectDotNetAccess();
            PreloadPresetModules(workingDirectory);

            var resolver = CreateModuleResolver(moduleRuntimeOptions, workingDirectory);
            var resolveResults = resolver.ResolveStaticImportsRecursively(scriptPath);
            var entryResolution = resolveResults[0];
            var resolvedScriptPath = entryResolution.ResolvedPath ?? scriptPath;

            EmitModuleDebugEvent(
                moduleRuntimeOptions,
                "runtime.options",
                new
                {
                    moduleRuntimeOptions.ProjectRoot,
                    allowNodeModulesResolution = moduleRuntimeOptions.AllowNodeModulesResolution,
                    allowImportPathCount = moduleRuntimeOptions.AllowedImportPathPrefixes.Count,
                    moduleRuntimeOptions.EnableStructuredDebugEvents
                });

            EmitModuleDebugEvent(
                moduleRuntimeOptions,
                "resolve.summary",
                new
                {
                    entry = resolvedScriptPath,
                    resolvedCount = resolveResults.Count,
                    resolved = resolveResults.Select(result => new
                    {
                        result.Specifier,
                        from = result.From,
                        kind = result.Kind.ToString(),
                        source = result.Source.ToString(),
                        result.ResolvedPath,
                        attempts = result.Attempts
                    })
                });

            var extension = Path.GetExtension(resolvedScriptPath).ToLowerInvariant();
            if (extension == ".ts" || extension == ".mts")
            {
                var transpiledScript = JavaScript.TranspileTypeScriptFile(resolvedScriptPath, workingDirectory);
                await Task.Run(() => JavaScript.Execute(transpiledScript));
            }
            else
            {
                await Task.Run(() => JavaScript.ExecuteFile(resolvedScriptPath));
            }
        }
        catch (ModuleResolutionException ex)
        {
            var structuredError = JsonSerializer.Serialize(ex.ToStructuredError());
            throw new InvalidOperationException($"[Paperclip] Module 解析失败: {structuredError}", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"[Paperclip] Module Runtime 脚本执行错误: {ex.Message}", ex);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ExportRegistrationDebugEnvVar, previousDebugEnvValue);
        }
    }

    private static ModuleRunOptions ParseModuleRunOptions(string[] args)
    {
        string? configPath = null;
        var additionalAllowedImportPaths = new List<string>();
        var enableStructuredDebugEvents = false;

        var runOptions = ParseRunOptions(
            args,
            defaultScriptName: DefaultScript,
            commandDisplayName: CommandModuleRun,
            tryHandleOption: token =>
            {
                if (token.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
                {
                    configPath = GetOptionValueOrThrow(token, "--config", CommandModuleRun);
                    return true;
                }

                if (token.StartsWith("--allow-import=", StringComparison.OrdinalIgnoreCase))
                {
                    additionalAllowedImportPaths.Add(GetOptionValueOrThrow(token, "--allow-import", CommandModuleRun));
                    return true;
                }

                if (string.Equals(token, "--debug-events", StringComparison.OrdinalIgnoreCase))
                {
                    enableStructuredDebugEvents = true;
                    return true;
                }

                return false;
            });

        return new ModuleRunOptions(
            ScriptName: runOptions.ScriptName,
            EnableDebugLogs: runOptions.EnableDebugLogs,
            ConfigPath: configPath,
            AdditionalAllowedImportPaths: additionalAllowedImportPaths,
            EnableStructuredDebugEvents: enableStructuredDebugEvents);
    }

    private static ModuleRuntimeOptions BuildModuleRuntimeOptions(ModuleRunOptions runOptions, string workingDirectory)
    {
        var options = ModuleRuntimeOptions.CreateSecureDefaults(workingDirectory);
        var configPath = ResolveModuleConfigPath(runOptions.ConfigPath, workingDirectory);

        if (File.Exists(configPath))
        {
            ApplyModuleRuntimeConfigFromFile(configPath, options);
        }
        else if (!string.IsNullOrWhiteSpace(runOptions.ConfigPath))
        {
            throw new FileNotFoundException(
                $"[Paperclip] mrun 配置文件不存在: {configPath}。请确认 --config 路径正确，或移除该参数以使用默认安全配置。",
                configPath);
        }

        if (runOptions.EnableDebugLogs)
        {
            options.EnableDebugLogs = true;
        }

        if (runOptions.EnableStructuredDebugEvents)
        {
            options.EnableStructuredDebugEvents = true;
        }

        foreach (var path in runOptions.AdditionalAllowedImportPaths)
        {
            options.AllowedImportPathPrefixes.Add(path);
        }

        options.ValidateAndNormalize();
        return options;
    }

    private static void ApplyModuleRuntimeConfigFromFile(string configPath, ModuleRuntimeOptions options)
    {
        try
        {
            using var stream = File.OpenRead(configPath);
            using var document = JsonDocument.Parse(stream);

            var root = document.RootElement;
            var moduleRuntimeSection = root;

            if (root.TryGetProperty("moduleRuntime", out var nestedSection))
            {
                moduleRuntimeSection = nestedSection;
            }

            var parsedImportsMap = TryReadStringDictionary(root, "imports", configPath);
            if (parsedImportsMap != null)
            {
                options.WorkspaceImportsMap = parsedImportsMap;
            }

            if (moduleRuntimeSection.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"[Paperclip] 配置文件格式错误: {configPath}。\n" +
                    "请将 moduleRuntime 配置为对象，例如:\n" +
                    "{\"moduleRuntime\":{\"enableDebugLogs\":true,\"allowImportPaths\":[\"../shared\"]}}。");
            }

            var parsedAllowImportPaths = TryReadStringArray(moduleRuntimeSection, "allowImportPaths", configPath);
            if (parsedAllowImportPaths != null)
            {
                options.AllowedImportPathPrefixes = parsedAllowImportPaths;
            }

            var parsedEnableDebugLogs = TryReadBoolean(moduleRuntimeSection, "enableDebugLogs", configPath);
            if (parsedEnableDebugLogs.HasValue)
            {
                options.EnableDebugLogs = parsedEnableDebugLogs.Value;
            }

            var parsedEnableStructuredEvents = TryReadBoolean(moduleRuntimeSection, "enableStructuredDebugEvents", configPath);
            if (parsedEnableStructuredEvents.HasValue)
            {
                options.EnableStructuredDebugEvents = parsedEnableStructuredEvents.Value;
            }

            var parsedAllowNodeModules = TryReadBoolean(moduleRuntimeSection, "allowNodeModulesResolution", configPath);
            if (parsedAllowNodeModules.HasValue)
            {
                options.AllowNodeModulesResolution = parsedAllowNodeModules.Value;
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"[Paperclip] 解析配置文件失败: {configPath}（行 {ex.LineNumber}, 字节 {ex.BytePositionInLine}）。\n" +
                "请检查 JSON 语法，建议先用 JSON 校验工具修复后重试。",
                ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                $"[Paperclip] 无法读取配置文件（权限不足）: {configPath}。\n" +
                "请授予读取权限，或改用 --config 指向可访问路径。",
                ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"[Paperclip] 读取配置文件失败: {configPath}。\n" +
                "请确认文件存在且未被占用。",
                ex);
        }
    }

    private static List<string>? TryReadStringArray(JsonElement section, string propertyName, string configPath)
    {
        if (!section.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                $"[Paperclip] 配置项 moduleRuntime.{propertyName} 类型错误（{configPath}）。应为字符串数组。\n" +
                "示例: \"allowImportPaths\": [\"../shared\", \"D:/safe-lib\"]");
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"[Paperclip] 配置项 moduleRuntime.{propertyName} 仅允许字符串元素（{configPath}）。");
            }

            var value = item.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            values.Add(value.Trim());
        }

        return values;
    }

    private static Dictionary<string, string>? TryReadStringDictionary(JsonElement section, string propertyName, string configPath)
    {
        if (!section.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"[Paperclip] 配置项 {propertyName} 类型错误（{configPath}）。应为对象字典。\n" +
                "示例: \"imports\": { \"@app/utils\": \"./src/utils/index.ts\" }");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in property.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"[Paperclip] 配置项 {propertyName}.{entry.Name} 类型错误（{configPath}）。应为字符串路径。");
            }

            var mappedPath = entry.Value.GetString();
            if (string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(mappedPath))
            {
                continue;
            }

            values[entry.Name.Trim()] = mappedPath.Trim();
        }

        return values;
    }

    private static bool? TryReadBoolean(JsonElement section, string propertyName, string configPath)
    {
        if (!section.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new InvalidOperationException(
                $"[Paperclip] 配置项 moduleRuntime.{propertyName} 类型错误（{configPath}）。应为布尔值 true/false。");
        }

        return property.GetBoolean();
    }

    private static string ResolveModuleConfigPath(string? configPath, string workingDirectory)
    {
        var effectivePath = string.IsNullOrWhiteSpace(configPath)
            ? Path.Combine(workingDirectory, "paperclip.json")
            : configPath;

        return Path.IsPathRooted(effectivePath)
            ? Path.GetFullPath(effectivePath)
            : Path.GetFullPath(Path.Combine(workingDirectory, effectivePath));
    }

    private static string GetOptionValueOrThrow(string token, string optionName, string commandDisplayName)
    {
        var separatorIndex = token.IndexOf('=');
        if (separatorIndex < 0 || separatorIndex == token.Length - 1)
        {
            throw new ArgumentException(
                $"[Paperclip] {commandDisplayName} 参数 {optionName} 缺少值。\n" +
                $"示例: {optionName}=<path>");
        }

        var value = token[(separatorIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"[Paperclip] {commandDisplayName} 参数 {optionName} 不能为空。\n" +
                $"示例: {optionName}=<path>");
        }

        return value;
    }

    private static void EnsurePathAllowedByModuleSecurity(string scriptPath, ModuleRuntimeOptions options)
    {
        if (options.IsPathAllowed(scriptPath))
        {
            return;
        }

        throw new UnauthorizedAccessException(
            $"[Paperclip] 入口脚本超出 Module Runtime 安全边界: {scriptPath}。\n" +
            $"当前项目根目录: {options.ProjectRoot}\n" +
            "请将脚本移动到项目目录内，或在 paperclip.json 的 moduleRuntime.allowImportPaths 中加入白名单，\n" +
            "也可临时通过 --allow-import=<path> 指定额外白名单。"
        );
    }

    private static void EmitModuleDebugEvent(ModuleRuntimeOptions options, string eventName, object payload)
    {
        if (!options.EnableDebugLogs)
        {
            return;
        }

        if (!options.EnableStructuredDebugEvents)
        {
            Logger.Info($"[Paperclip][mrun] {eventName}");
            return;
        }

        var envelope = new
        {
            @event = eventName,
            timestamp = DateTimeOffset.UtcNow,
            payload
        };

        Logger.Info($"[Paperclip][mrun] {JsonSerializer.Serialize(envelope)}");
    }

    private static ModuleResolver CreateModuleResolver(ModuleRuntimeOptions options, string workingDirectory)
    {
        var sdkModulePath = Path.Combine(workingDirectory, ModelsDirectoryName, CoreModuleFileName);
        if (!File.Exists(sdkModulePath))
        {
            sdkModulePath = Path.Combine(workingDirectory, CoreModuleFileName);
        }

        var builtinSpecifierMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["@paperclip/sdk"] = sdkModulePath
        };

        var workspaceImportsMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in options.WorkspaceImportsMap.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (builtinSpecifierMap.ContainsKey(pair.Key))
            {
                EmitModuleDebugEvent(
                    options,
                    "resolve.imports.conflict",
                    new
                    {
                        specifier = pair.Key,
                        source = "paperclip.json#imports",
                        ignoredTarget = pair.Value,
                        winner = "builtin"
                    });

                continue;
            }

            workspaceImportsMap[pair.Key] = pair.Value;
        }

        return new ModuleResolver(options, builtinSpecifierMap, workspaceImportsMap);
    }

    private static async Task RunScriptAsync(string scriptName, bool enableDebugLogs)
    {
        var scriptPath = ResolveScriptPath(scriptName);

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"[Paperclip] 找不到脚本文件: {scriptPath}", scriptPath);
        }

        var previousDebugEnvValue = Environment.GetEnvironmentVariable(ExportRegistrationDebugEnvVar);
        Environment.SetEnvironmentVariable(ExportRegistrationDebugEnvVar, enableDebugLogs ? "1" : null);

        var scriptDirectory = Path.GetDirectoryName(scriptPath);
        var workingDirectory = string.IsNullOrWhiteSpace(scriptDirectory)
            ? Directory.GetCurrentDirectory()
            : scriptDirectory;

        try
        {
            RegisterDirectDotNetAccess();
            PreloadPresetModules(workingDirectory);
            await Task.Run(() => JavaScript.ExecuteFile(scriptPath));
        }
        catch (Exception ex)
        {
            throw new Exception($"[Paperclip] 脚本执行错误: {ex.Message}", ex);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ExportRegistrationDebugEnvVar, previousDebugEnvValue);
        }
    }

    private static async Task RunTypeScriptAsync(string scriptName, bool enableDebugLogs)
    {
        var scriptPath = ResolveScriptPath(scriptName);

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"[Paperclip] 找不到脚本文件: {scriptPath}", scriptPath);
        }

        var previousDebugEnvValue = Environment.GetEnvironmentVariable(ExportRegistrationDebugEnvVar);
        Environment.SetEnvironmentVariable(ExportRegistrationDebugEnvVar, enableDebugLogs ? "1" : null);

        var scriptDirectory = Path.GetDirectoryName(scriptPath);
        var workingDirectory = string.IsNullOrWhiteSpace(scriptDirectory)
            ? Directory.GetCurrentDirectory()
            : scriptDirectory;

        try
        {
            RegisterDirectDotNetAccess();
            PreloadPresetModules(workingDirectory);
            var transpiledScript = JavaScript.TranspileTypeScriptFile(scriptPath, workingDirectory);
            await Task.Run(() => JavaScript.Execute(transpiledScript));
        }
        catch (Exception ex)
        {
            throw new Exception($"[Paperclip] TypeScript 脚本执行错误: {ex.Message}", ex);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ExportRegistrationDebugEnvVar, previousDebugEnvValue);
        }
    }

    private static void EnsureModelsReleasedForRun(string scriptName)
    {
        var scriptPath = ResolveScriptPath(scriptName);
        var targetDir = Path.GetDirectoryName(scriptPath);

        EnsureModelsReleased(string.IsNullOrWhiteSpace(targetDir)
            ? Directory.GetCurrentDirectory()
            : targetDir);
    }

    private static void EnsureModelsReleased(string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            targetDirectory = Directory.GetCurrentDirectory();
        }

        var modelsDirectory = Path.Combine(targetDirectory, ModelsDirectoryName);
        Directory.CreateDirectory(modelsDirectory);

        var resources = EmbeddedModelResources.Value;
        if (resources.Count == 0)
        {
            Logger.Warn("[Paperclip] 未找到嵌入 Models 资源，跳过释放。");
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        foreach (var resource in resources)
        {
            var destinationPath = Path.Combine(modelsDirectory, resource.RelativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            using var stream = assembly.GetManifestResourceStream(resource.ResourceName);
            if (stream == null)
            {
                Logger.Warn($"[Paperclip] 无法读取嵌入资源: {resource.ResourceName}");
                continue;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var sourceContent = reader.ReadToEnd();

            if (File.Exists(destinationPath))
            {
                var existingContent = File.ReadAllText(destinationPath, Encoding.UTF8);
                if (string.Equals(existingContent, sourceContent, StringComparison.Ordinal))
                {
                    continue;
                }
            }

            File.WriteAllText(destinationPath, sourceContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Logger.Info($"[Paperclip] 已释放模块资源: {destinationPath}");
        }
    }

    private static void PreloadPresetModules(string targetDirectory)
    {
        var modelsDirectory = Path.Combine(targetDirectory, ModelsDirectoryName);
        if (!Directory.Exists(modelsDirectory))
        {
            modelsDirectory = targetDirectory;
        }

        var sdkModulePath = Path.Combine(modelsDirectory, CoreModuleFileName);
        ExecutePresetIfExists(sdkModulePath, required: true);
    }

    private static void RegisterDirectDotNetAccess()
    {
        var globals = JavaScript.GetRegisteredGlobals();
        if (!globals.ContainsKey(HostFunctionsGlobalName))
        {
            JavaScript.RegisterGlobal(HostFunctionsGlobalName, new HostFunctions());
        }

        JavaScript.RegisterHostType("NetPath", typeof(Path));
        JavaScript.RegisterHostType("NetFile", typeof(File));
        JavaScript.RegisterHostType("NetDirectory", typeof(Directory));
        JavaScript.RegisterHostType("NetFileInfo", typeof(FileInfo));
        JavaScript.RegisterHostType("NetDirectoryInfo", typeof(DirectoryInfo));
        JavaScript.RegisterHostType("NetConsole", typeof(Console));
        JavaScript.RegisterHostType("NetUri", typeof(Uri));
        JavaScript.RegisterHostType("NetRandom", typeof(Random));
        JavaScript.RegisterHostType("NetEncoding", typeof(Encoding));
        JavaScript.RegisterHostType("NetStringBuilder", typeof(StringBuilder));
        JavaScript.RegisterHostType("NetRegex", typeof(System.Text.RegularExpressions.Regex));
        JavaScript.RegisterHostType("NetJsonSerializer", typeof(System.Text.Json.JsonSerializer));
        JavaScript.RegisterHostType("NetCultureInfo", typeof(System.Globalization.CultureInfo));
        JavaScript.RegisterHostType("NetEnumerable", typeof(System.Linq.Enumerable));
        JavaScript.RegisterHostType("NetEnvironment", typeof(Environment));
        JavaScript.RegisterHostType("NetDateTime", typeof(DateTime));
        JavaScript.RegisterHostType("NetDateOnly", typeof(DateOnly));
        JavaScript.RegisterHostType("NetTimeOnly", typeof(TimeOnly));
        JavaScript.RegisterHostType("NetGuid", typeof(Guid));
        JavaScript.RegisterHostType("NetTimeSpan", typeof(TimeSpan));
        JavaScript.RegisterHostType("NetConvert", typeof(Convert));
        JavaScript.RegisterHostType("NetMath", typeof(Math));
        JavaScript.RegisterHostType("NetProcess", typeof(System.Diagnostics.Process));
        JavaScript.RegisterHostType("NetStopwatch", typeof(System.Diagnostics.Stopwatch));
        JavaScript.RegisterHostType("NetTask", typeof(Task));
        JavaScript.RegisterHostType("NetCancellationTokenSource", typeof(System.Threading.CancellationTokenSource));
        JavaScript.RegisterHostType("NetHttpClient", typeof(System.Net.Http.HttpClient));
        JavaScript.RegisterHostType("NetHttpRequestMessage", typeof(System.Net.Http.HttpRequestMessage));
        JavaScript.RegisterHostType("NetHttpMethod", typeof(System.Net.Http.HttpMethod));
        JavaScript.RegisterHostType("NetHttpStatusCode", typeof(System.Net.HttpStatusCode));
    }

    private static void ExecutePresetIfExists(string filePath, bool required)
    {
        if (!File.Exists(filePath))
        {
            if (required)
            {
                throw new FileNotFoundException($"[Paperclip] 运行前预加载失败，找不到文件: {filePath}", filePath);
            }

            return;
        }

        JavaScript.ExecuteFile(filePath);
    }

    private static RunOptions ParseRunOptions(
        string[] args,
        string defaultScriptName = DefaultScript,
        string commandDisplayName = CommandRun,
        Func<string, bool>? tryHandleOption = null)
    {
        string? scriptName = null;
        var enableDebugLogs = false;

        foreach (var token in args.Skip(1))
        {
            if (string.Equals(token, DebugFlag, StringComparison.OrdinalIgnoreCase))
            {
                enableDebugLogs = true;
                continue;
            }

            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                if (tryHandleOption != null && tryHandleOption(token))
                {
                    continue;
                }

                throw new ArgumentException($"[Paperclip] 未知 {commandDisplayName} 参数: {token}");
            }

            if (scriptName != null)
            {
                throw new ArgumentException($"[Paperclip] {commandDisplayName} 命令仅接受一个脚本路径参数，重复参数: {token}");
            }

            scriptName = token;
        }

        return new RunOptions(scriptName ?? defaultScriptName, enableDebugLogs);
    }

    private static string ResolveScriptPath(string scriptName)
    {
        return Path.IsPathRooted(scriptName)
            ? scriptName
            : Path.Combine(Directory.GetCurrentDirectory(), scriptName);
    }

}
