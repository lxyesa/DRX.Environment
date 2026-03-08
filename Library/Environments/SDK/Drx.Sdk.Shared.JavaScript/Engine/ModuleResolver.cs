using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Drx.Sdk.Shared.JavaScript.Abstractions;

namespace Drx.Sdk.Shared.JavaScript.Engine
{
    /// <summary>
    /// 模块 specifier 基础解析器，按 deterministic 顺序支持 builtin / relative / absolute 解析。
    /// </summary>
    public sealed class ModuleResolver
    {
        private static readonly string[] ExtensionCandidates = [".js", ".mjs", ".cjs", ".ts", ".mts", ".cts"];
        private static readonly Regex ImportFromRegex = new(@"\bimport\s+(?:[^\""']+?\s+from\s+)?[\""'](?<spec>[^\""']+)[\""']", RegexOptions.Compiled);
        private static readonly Regex ExportFromRegex = new(@"\bexport\s+[^\""']*?\s+from\s+[\""'](?<spec>[^\""']+)[\""']", RegexOptions.Compiled);

        private readonly ModuleRuntimeOptions _options;
        private readonly IReadOnlyDictionary<string, string> _builtinSpecifierMap;
        private readonly IReadOnlyDictionary<string, string> _workspaceImportsMap;

        /// <summary>
        /// 初始化 <see cref="ModuleResolver"/>。
        /// </summary>
        public ModuleResolver(
            ModuleRuntimeOptions options,
            IReadOnlyDictionary<string, string>? builtinSpecifierMap = null,
            IReadOnlyDictionary<string, string>? workspaceImportsMap = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _builtinSpecifierMap = builtinSpecifierMap ?? new Dictionary<string, string>(StringComparer.Ordinal);
            _workspaceImportsMap = workspaceImportsMap ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// 解析入口文件路径。
        /// </summary>
        public ModuleResolutionResult ResolveForEntry(string entryFilePath)
        {
            return Resolve(entryFilePath, fromFilePath: null);
        }

        /// <summary>
        /// 解析单个 specifier，并输出标准化结果。
        /// </summary>
        public ModuleResolutionResult Resolve(string specifier, string? fromFilePath)
        {
            if (string.IsNullOrWhiteSpace(specifier))
            {
                throw new ModuleResolutionException(
                    code: "PC_RES_000",
                    specifier: specifier,
                    from: fromFilePath,
                    attempts: Array.Empty<string>(),
                    reason: "Specifier 不能为空。",
                    hint: "请检查 import/export 语句中的模块路径。");
            }

            var normalizedSpecifier = specifier.Trim();
            var attempts = new List<string>();
            var kind = Classify(normalizedSpecifier);

            if (kind == ModuleSpecifierKind.Builtin)
            {
                attempts.Add($"builtin:{normalizedSpecifier}");
                if (_builtinSpecifierMap.TryGetValue(normalizedSpecifier, out var mappedPath))
                {
                    return ResolvePathCandidate(
                        normalizedSpecifier,
                        fromFilePath,
                        kind,
                        ModuleResolutionSource.BuiltinMap,
                        mappedPath,
                        attempts);
                }

                throw new ModuleResolutionException(
                    code: "PC_RES_003",
                    specifier: normalizedSpecifier,
                    from: fromFilePath,
                    attempts: attempts,
                    reason: "Builtin specifier 未注册。",
                    hint: "请确认 builtin 映射表包含该 specifier。"
                );
            }

            if (kind == ModuleSpecifierKind.Relative)
            {
                if (string.IsNullOrWhiteSpace(fromFilePath))
                {
                    throw new ModuleResolutionException(
                        code: "PC_RES_002",
                        specifier: normalizedSpecifier,
                        from: fromFilePath,
                        attempts: attempts,
                        reason: "Relative specifier 缺少来源文件上下文。",
                        hint: "请为 relative import 提供 from 文件路径。");
                }

                var fromDirectory = Path.GetDirectoryName(Path.GetFullPath(fromFilePath));
                if (string.IsNullOrWhiteSpace(fromDirectory))
                {
                    throw new ModuleResolutionException(
                        code: "PC_RES_002",
                        specifier: normalizedSpecifier,
                        from: fromFilePath,
                        attempts: attempts,
                        reason: "无法从来源文件推导目录。",
                        hint: "请确认 from 文件路径有效。");
                }

                var candidate = Path.GetFullPath(Path.Combine(fromDirectory, normalizedSpecifier));
                return ResolvePathCandidate(
                    normalizedSpecifier,
                    fromFilePath,
                    kind,
                    ModuleResolutionSource.RelativePath,
                    candidate,
                    attempts);
            }

            if (kind == ModuleSpecifierKind.Absolute)
            {
                var candidate = ResolveAbsoluteSpecifier(normalizedSpecifier);
                return ResolvePathCandidate(
                    normalizedSpecifier,
                    fromFilePath,
                    kind,
                    ModuleResolutionSource.AbsolutePath,
                    candidate,
                    attempts);
            }

            if (kind == ModuleSpecifierKind.Bare)
            {
                attempts.Add($"imports-map:{normalizedSpecifier}");
                if (_workspaceImportsMap.TryGetValue(normalizedSpecifier, out var mappedPath))
                {
                    return ResolvePathCandidate(
                        normalizedSpecifier,
                        fromFilePath,
                        kind,
                        ModuleResolutionSource.WorkspaceImportsMap,
                        mappedPath,
                        attempts);
                }

                // node_modules 逐级查找（需启用 AllowNodeModulesResolution）
                if (_options.AllowNodeModulesResolution)
                {
                    var nodeResult = ResolveNodeModulesPackage(normalizedSpecifier, fromFilePath, attempts);
                    if (nodeResult is not null)
                    {
                        return nodeResult;
                    }
                }
            }

            throw new ModuleResolutionException(
                code: "PC_RES_004",
                specifier: normalizedSpecifier,
                from: fromFilePath,
                attempts: attempts,
                reason: _options.AllowNodeModulesResolution
                    ? "裸包 specifier 未命中 workspace imports map，且 node_modules 查找失败。"
                    : "裸包 specifier 未命中 workspace imports map，且 node_modules 解析未启用。",
                hint: _options.AllowNodeModulesResolution
                    ? "请确认包已安装到 node_modules，或在 paperclip.json#imports 中配置别名。"
                    : "请在 ModuleRuntimeOptions 中设置 AllowNodeModulesResolution = true，或在 paperclip.json#imports 中配置别名。\n");
        }

        /// <summary>
        /// 递归解析入口与其静态导入（import/export from），用于基础解析流水线验证。
        /// </summary>
        public IReadOnlyList<ModuleResolutionResult> ResolveStaticImportsRecursively(string entryFilePath)
        {
            var orderedResults = new List<ModuleResolutionResult>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();

            var entry = ResolveForEntry(entryFilePath);
            if (entry.ResolvedPath is null)
            {
                throw new ModuleResolutionException(
                    code: "PC_RES_001",
                    specifier: entryFilePath,
                    from: null,
                    attempts: entry.Attempts,
                    reason: "入口文件解析失败。",
                    hint: "请确认入口路径存在且可访问。");
            }

            queue.Enqueue(entry.ResolvedPath);
            visited.Add(entry.ResolvedPath);
            orderedResults.Add(entry);

            while (queue.Count > 0)
            {
                var currentFile = queue.Dequeue();
                var source = File.ReadAllText(currentFile);
                var staticImportSpecifiers = ExtractStaticImportSpecifiers(source);

                foreach (var importSpecifier in staticImportSpecifiers)
                {
                    var result = Resolve(importSpecifier, currentFile);
                    orderedResults.Add(result);

                    if (result.Kind == ModuleSpecifierKind.Builtin || string.IsNullOrWhiteSpace(result.ResolvedPath))
                    {
                        continue;
                    }

                    if (visited.Add(result.ResolvedPath))
                    {
                        queue.Enqueue(result.ResolvedPath);
                    }
                }
            }

            return orderedResults;
        }

        /// <summary>
        /// 分类模块 specifier。
        /// </summary>
        public ModuleSpecifierKind Classify(string specifier)
        {
            if (string.IsNullOrWhiteSpace(specifier))
            {
                return ModuleSpecifierKind.Unknown;
            }

            if (_builtinSpecifierMap.ContainsKey(specifier))
            {
                return ModuleSpecifierKind.Builtin;
            }

            if (specifier.StartsWith("./", StringComparison.Ordinal) || specifier.StartsWith("../", StringComparison.Ordinal))
            {
                return ModuleSpecifierKind.Relative;
            }

            if (Path.IsPathRooted(specifier) || specifier.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                return ModuleSpecifierKind.Absolute;
            }

            return ModuleSpecifierKind.Bare;
        }

        private ModuleResolutionResult ResolvePathCandidate(
            string specifier,
            string? fromFilePath,
            ModuleSpecifierKind kind,
            ModuleResolutionSource source,
            string basePath,
            List<string> attempts)
        {
            foreach (var candidate in EnumeratePathCandidates(basePath))
            {
                attempts.Add(candidate);

                if (!File.Exists(candidate))
                {
                    continue;
                }

                if (!_options.IsPathAllowed(candidate))
                {
                    throw new ModuleResolutionException(
                        code: "PC_SEC_001",
                        specifier: specifier,
                        from: fromFilePath,
                        attempts: attempts,
                        reason: "解析到的模块路径超出安全边界。",
                        hint: "请调整导入路径，或在 moduleRuntime.allowImportPaths 中添加白名单。");
                }

                return new ModuleResolutionResult(specifier, fromFilePath, kind, source, candidate, attempts);
            }

            throw new ModuleResolutionException(
                code: "PC_RES_001",
                specifier: specifier,
                from: fromFilePath,
                attempts: attempts,
                reason: "未找到可用模块文件。",
                hint: "请确认文件路径、扩展名或 index 文件是否存在。");
        }

        private static string ResolveAbsoluteSpecifier(string specifier)
        {
            if (specifier.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(specifier, UriKind.Absolute);
                if (!uri.IsFile)
                {
                    throw new ModuleResolutionException(
                        code: "PC_RES_005",
                        specifier: specifier,
                        from: null,
                        attempts: [specifier],
                        reason: "仅支持 file:// 协议的绝对 URL。",
                        hint: "请使用本地文件路径或 file URL。");
                }

                return Path.GetFullPath(uri.LocalPath);
            }

            return Path.GetFullPath(specifier);
        }

        private static IEnumerable<string> EnumeratePathCandidates(string basePath)
        {
            var normalizedBasePath = Path.GetFullPath(basePath);
            yield return normalizedBasePath;

            if (!Path.HasExtension(normalizedBasePath))
            {
                foreach (var extension in ExtensionCandidates)
                {
                    yield return normalizedBasePath + extension;
                }
            }

            if (Directory.Exists(normalizedBasePath))
            {
                foreach (var extension in ExtensionCandidates)
                {
                    yield return Path.Combine(normalizedBasePath, "index" + extension);
                }
            }
        }

        private static IReadOnlyList<string> ExtractStaticImportSpecifiers(string source)
        {
            var matches = new List<Match>();
            matches.AddRange(ImportFromRegex.Matches(source).Cast<Match>());
            matches.AddRange(ExportFromRegex.Matches(source).Cast<Match>());

            return matches
                .OrderBy(match => match.Index)
                .Select(match => match.Groups["spec"].Value.Trim())
                .Where(spec => !string.IsNullOrWhiteSpace(spec))
                .ToList();
        }

        /// <summary>
        /// Node-style node_modules 逐级向上查找裸包，解析 package.json main 字段与 index.* 入口。
        /// </summary>
        internal ModuleResolutionResult? ResolveNodeModulesPackage(
            string specifier,
            string? fromFilePath,
            List<string> attempts)
        {
            var (packageName, subpath) = SplitBareSpecifier(specifier);
            var startDir = !string.IsNullOrWhiteSpace(fromFilePath)
                ? Path.GetDirectoryName(Path.GetFullPath(fromFilePath))
                : _options.ProjectRoot;

            if (string.IsNullOrWhiteSpace(startDir))
            {
                return null;
            }

            var current = startDir;
            var projectRootNormalized = Path.GetFullPath(_options.ProjectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            while (current is not null)
            {
                // 避免在 node_modules 自身内再嵌套查找
                if (Path.GetFileName(current).Equals("node_modules", StringComparison.OrdinalIgnoreCase))
                {
                    current = Path.GetDirectoryName(current);
                    continue;
                }

                var nodeModulesDir = Path.Combine(current, "node_modules");
                var packageDir = Path.Combine(nodeModulesDir, packageName);
                attempts.Add($"node_modules:{packageDir}");

                if (Directory.Exists(packageDir))
                {
                    if (subpath is not null)
                    {
                        // 带子路径：先尝试 exports 子路径映射，再回退直接路径候选
                        var packageJsonPath = Path.Combine(packageDir, "package.json");
                        if (File.Exists(packageJsonPath))
                        {
                            try
                            {
                                var jsonContent = File.ReadAllText(packageJsonPath);
                                using var doc = JsonDocument.Parse(jsonContent);
                                var root = doc.RootElement;

                                if (root.TryGetProperty("exports", out var exportsProp))
                                {
                                    var conditions = new[] { "import", "require", "default" };
                                    attempts.Add($"exports:subpath-resolve (subpath={subpath}, conditions=[{string.Join(",", conditions)}])");

                                    var exportsResult = ResolvePackageExports(
                                        exportsProp, packageDir, specifier, subpath,
                                        fromFilePath, conditions, attempts);

                                    if (exportsResult is not null)
                                    {
                                        return exportsResult;
                                    }

                                    // exports 存在但子路径未命中 — 按 Node.js 规范不回退到直接路径
                                    attempts.Add($"exports:subpath '{subpath}' not matched in exports; direct path fallback blocked by exports field");
                                    continue;
                                }
                            }
                            catch (JsonException)
                            {
                                attempts.Add($"package.json:parse-error:{packageJsonPath}");
                            }
                        }

                        // 无 exports 字段时回退到直接路径候选
                        var subCandidate = Path.GetFullPath(Path.Combine(packageDir, subpath));
                        var subResult = TryResolvePathCandidate(
                            specifier, fromFilePath, ModuleSpecifierKind.Bare,
                            ModuleResolutionSource.NodeModules, subCandidate, attempts);
                        if (subResult is not null)
                        {
                            return subResult;
                        }
                    }
                    else
                    {
                        // 无子路径：解析包入口
                        var entryResult = ResolvePackageEntry(packageDir, specifier, fromFilePath, attempts);
                        if (entryResult is not null)
                        {
                            return entryResult;
                        }
                    }
                }

                // 逐级向上查找，但不超出项目根
                var parentDir = Path.GetDirectoryName(current);

                if (parentDir is null || parentDir.Equals(current, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                var parentNormalized = parentDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (parentNormalized.Length < projectRootNormalized.Length)
                {
                    break;
                }

                current = parentDir;
            }

            return null;
        }

        /// <summary>
        /// 读取 package.json 的 exports/conditions → module → main 字段解析入口，如果都缺失则回退到 index.* 候选。
        /// exports 具有最高优先级，命中后不再回退 module/main。
        /// </summary>
        internal ModuleResolutionResult? ResolvePackageEntry(
            string packageDir,
            string specifier,
            string? fromFilePath,
            List<string> attempts)
        {
            var packageJsonPath = Path.Combine(packageDir, "package.json");

            if (File.Exists(packageJsonPath))
            {
                attempts.Add($"package.json:{packageJsonPath}");

                try
                {
                    var jsonContent = File.ReadAllText(packageJsonPath);
                    using var doc = JsonDocument.Parse(jsonContent);
                    var root = doc.RootElement;

                    // 拆分 specifier 获取 subpath
                    var (_, subpath) = SplitBareSpecifier(specifier);

                    // exports 最高优先级（REQ-PKG-004）
                    if (root.TryGetProperty("exports", out var exportsProp))
                    {
                        var conditions = new[] { "import", "require", "default" };
                        attempts.Add($"exports:resolve-start (conditions=[{string.Join(",", conditions)}], subpath={subpath ?? "."})");

                        var exportsResult = ResolvePackageExports(
                            exportsProp, packageDir, specifier, subpath,
                            fromFilePath, conditions, attempts);

                        if (exportsResult is not null)
                        {
                            return exportsResult;
                        }

                        // exports 字段存在但未命中 — 按 Node.js 规范，不回退到 main
                        attempts.Add("exports:no-match — exports field present but no condition matched; will NOT fallback to module/main per Node.js spec");
                    }
                    else
                    {
                        // 无 exports 字段时，走 module → main 回退链
                        // 尝试 "module" 字段（ESM 优先）
                        if (root.TryGetProperty("module", out var moduleProp) &&
                            moduleProp.ValueKind == JsonValueKind.String)
                        {
                            var moduleEntry = moduleProp.GetString();
                            if (!string.IsNullOrWhiteSpace(moduleEntry))
                            {
                                var moduleCandidate = Path.GetFullPath(Path.Combine(packageDir, moduleEntry));
                                attempts.Add($"module:{moduleCandidate}");
                                var moduleResult = TryResolvePathCandidate(
                                    specifier, fromFilePath, ModuleSpecifierKind.Bare,
                                    ModuleResolutionSource.NodeModules, moduleCandidate, attempts);
                                if (moduleResult is not null)
                                {
                                    return moduleResult;
                                }
                            }
                        }

                        // 尝试 "main" 字段
                        if (root.TryGetProperty("main", out var mainProp) &&
                            mainProp.ValueKind == JsonValueKind.String)
                        {
                            var mainEntry = mainProp.GetString();
                            if (!string.IsNullOrWhiteSpace(mainEntry))
                            {
                                var mainCandidate = Path.GetFullPath(Path.Combine(packageDir, mainEntry));
                                attempts.Add($"main:{mainCandidate}");
                                var mainResult = TryResolvePathCandidate(
                                    specifier, fromFilePath, ModuleSpecifierKind.Bare,
                                    ModuleResolutionSource.NodeModules, mainCandidate, attempts);
                                if (mainResult is not null)
                                {
                                    return mainResult;
                                }
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    attempts.Add($"package.json:parse-error:{packageJsonPath}");
                }
            }

            // 回退：查找 index.*
            foreach (var candidate in EnumerateIndexCandidates(packageDir))
            {
                attempts.Add(candidate);
                if (File.Exists(candidate) && _options.IsPathAllowed(candidate))
                {
                    return new ModuleResolutionResult(
                        specifier, fromFilePath, ModuleSpecifierKind.Bare,
                        ModuleResolutionSource.NodeModules, candidate, attempts);
                }
            }

            return null;
        }

        /// <summary>
        /// 尝试按路径候选列表解析文件（不抛异常，未找到返回 null）。
        /// </summary>
        private ModuleResolutionResult? TryResolvePathCandidate(
            string specifier,
            string? fromFilePath,
            ModuleSpecifierKind kind,
            ModuleResolutionSource source,
            string basePath,
            List<string> attempts)
        {
            foreach (var candidate in EnumeratePathCandidates(basePath))
            {
                if (!attempts.Contains(candidate))
                {
                    attempts.Add(candidate);
                }

                if (!File.Exists(candidate))
                {
                    continue;
                }

                if (!_options.IsPathAllowed(candidate))
                {
                    return null;
                }

                return new ModuleResolutionResult(specifier, fromFilePath, kind, source, candidate, attempts);
            }

            return null;
        }

        /// <summary>
        /// 拆分裸包 specifier 为包名与子路径。
        /// 支持 scoped 包：<c>@scope/pkg</c> → packageName=<c>@scope/pkg</c>，subpath=null。
        /// 带子路径：<c>@scope/pkg/utils</c> → packageName=<c>@scope/pkg</c>，subpath=<c>utils</c>。
        /// </summary>
        internal static (string packageName, string? subpath) SplitBareSpecifier(string specifier)
        {
            if (specifier.StartsWith("@", StringComparison.Ordinal))
            {
                // @scope/pkg[/subpath]
                var slashIndex = specifier.IndexOf('/', 1);
                if (slashIndex < 0)
                {
                    return (specifier, null);
                }

                var secondSlash = specifier.IndexOf('/', slashIndex + 1);
                if (secondSlash < 0)
                {
                    return (specifier, null);
                }

                return (specifier[..secondSlash], specifier[(secondSlash + 1)..]);
            }
            else
            {
                // pkg[/subpath]
                var slashIndex = specifier.IndexOf('/');
                if (slashIndex < 0)
                {
                    return (specifier, null);
                }

                return (specifier[..slashIndex], specifier[(slashIndex + 1)..]);
            }
        }

        /// <summary>
        /// 解析 package.json 的 exports 字段。
        /// 支持形式：字符串直接映射、条件对象（import/require/default）、子路径映射。
        /// 严格记录每一步条件命中/跳过过程到 attempts，不会默默降级。
        /// </summary>
        internal ModuleResolutionResult? ResolvePackageExports(
            JsonElement exportsElement,
            string packageDir,
            string specifier,
            string? subpath,
            string? fromFilePath,
            string[] conditions,
            List<string> attempts)
        {
            var exportKey = subpath is null ? "." : $"./{subpath}";

            // Case 1: exports 是字符串 — 仅匹配根入口 "."
            if (exportsElement.ValueKind == JsonValueKind.String)
            {
                if (subpath is not null)
                {
                    attempts.Add($"exports:string-shorthand — subpath '{exportKey}' requested but exports is a plain string (root-only); skip");
                    return null;
                }

                var target = exportsElement.GetString();
                attempts.Add($"exports:string-shorthand → {target}");
                return TryResolveExportsTarget(target, packageDir, specifier, fromFilePath, attempts);
            }

            // Case 2: exports 是对象
            if (exportsElement.ValueKind == JsonValueKind.Object)
            {
                // 判断是子路径映射还是条件对象：
                // 如果所有 key 以 "." 开头 → 子路径映射
                // 否则 → 条件对象（适用于根入口）
                var isSubpathMap = true;
                foreach (var prop in exportsElement.EnumerateObject())
                {
                    if (!prop.Name.StartsWith(".", StringComparison.Ordinal))
                    {
                        isSubpathMap = false;
                        break;
                    }
                }

                if (isSubpathMap)
                {
                    attempts.Add($"exports:subpath-map — looking for key '{exportKey}'");

                    if (exportsElement.TryGetProperty(exportKey, out var subpathValue))
                    {
                        attempts.Add($"exports:subpath-map — matched key '{exportKey}'");
                        return ResolveExportsValue(subpathValue, packageDir, specifier, fromFilePath, conditions, attempts);
                    }

                    // 未命中子路径 — 不降级
                    attempts.Add($"exports:subpath-map — key '{exportKey}' not found in exports; available keys: [{string.Join(", ", exportsElement.EnumerateObject().Select(p => p.Name))}]");
                    return null;
                }
                else
                {
                    // 条件对象 — 仅适用于根入口
                    if (subpath is not null)
                    {
                        attempts.Add($"exports:conditions-object — subpath '{exportKey}' requested but exports is a conditions object (root-only); skip");
                        return null;
                    }

                    attempts.Add("exports:conditions-object — resolving root entry conditions");
                    return ResolveExportsConditions(exportsElement, packageDir, specifier, fromFilePath, conditions, attempts);
                }
            }

            // Case 3: exports 是数组 — 按顺序尝试（deterministic first-match）
            if (exportsElement.ValueKind == JsonValueKind.Array)
            {
                if (subpath is not null)
                {
                    attempts.Add($"exports:array — subpath '{exportKey}' requested but exports is an array (root-only); skip");
                    return null;
                }

                attempts.Add($"exports:array — trying {exportsElement.GetArrayLength()} candidates in order");
                var index = 0;
                foreach (var item in exportsElement.EnumerateArray())
                {
                    attempts.Add($"exports:array[{index}] — trying candidate");
                    var result = ResolveExportsValue(item, packageDir, specifier, fromFilePath, conditions, attempts);
                    if (result is not null)
                    {
                        attempts.Add($"exports:array[{index}] — matched");
                        return result;
                    }
                    attempts.Add($"exports:array[{index}] — no match, continuing");
                    index++;
                }

                attempts.Add("exports:array — exhausted all candidates without match");
                return null;
            }

            attempts.Add($"exports:unsupported-type — ValueKind={exportsElement.ValueKind}; skip");
            return null;
        }

        /// <summary>
        /// 解析 exports 值：可能是字符串、条件对象或数组，递归处理。
        /// </summary>
        private ModuleResolutionResult? ResolveExportsValue(
            JsonElement value,
            string packageDir,
            string specifier,
            string? fromFilePath,
            string[] conditions,
            List<string> attempts)
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                var target = value.GetString();
                attempts.Add($"exports:value → {target}");
                return TryResolveExportsTarget(target, packageDir, specifier, fromFilePath, attempts);
            }

            if (value.ValueKind == JsonValueKind.Object)
            {
                return ResolveExportsConditions(value, packageDir, specifier, fromFilePath, conditions, attempts);
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    attempts.Add($"exports:nested-array[{index}]");
                    var result = ResolveExportsValue(item, packageDir, specifier, fromFilePath, conditions, attempts);
                    if (result is not null) return result;
                    index++;
                }
                return null;
            }

            if (value.ValueKind == JsonValueKind.Null)
            {
                attempts.Add("exports:value — null (explicitly excluded)");
                return null;
            }

            attempts.Add($"exports:value — unsupported ValueKind={value.ValueKind}");
            return null;
        }

        /// <summary>
        /// 按 deterministic 优先级遍历条件对象，记录每个条件的匹配与跳过过程。
        /// 条件优先级由调用方通过 <paramref name="conditions"/> 数组顺序决定（默认 import → require → default）。
        /// </summary>
        internal ModuleResolutionResult? ResolveExportsConditions(
            JsonElement conditionsObj,
            string packageDir,
            string specifier,
            string? fromFilePath,
            string[] conditions,
            List<string> attempts)
        {
            // 按调用方指定的优先级顺序遍历条件
            foreach (var condition in conditions)
            {
                if (conditionsObj.TryGetProperty(condition, out var conditionValue))
                {
                    attempts.Add($"exports:condition '{condition}' — found, resolving target");

                    var result = ResolveExportsValue(conditionValue, packageDir, specifier, fromFilePath, conditions, attempts);
                    if (result is not null)
                    {
                        attempts.Add($"exports:condition '{condition}' — resolved successfully");
                        return result;
                    }

                    // 条件命中但目标文件不存在 — 记录并继续下一个条件
                    attempts.Add($"exports:condition '{condition}' — target not found on disk, trying next condition");
                }
                else
                {
                    attempts.Add($"exports:condition '{condition}' — not present in exports object, skip");
                }
            }

            // 记录 exports 对象中存在但不在请求条件集中的 key
            var unmatchedKeys = new List<string>();
            foreach (var prop in conditionsObj.EnumerateObject())
            {
                if (!conditions.Contains(prop.Name, StringComparer.Ordinal))
                {
                    unmatchedKeys.Add(prop.Name);
                }
            }

            if (unmatchedKeys.Count > 0)
            {
                attempts.Add($"exports:conditions — unmatched keys in exports object: [{string.Join(", ", unmatchedKeys)}]; these were not in the requested condition set");
            }

            attempts.Add($"exports:conditions — no condition matched from [{string.Join(", ", conditions)}]");
            return null;
        }

        /// <summary>
        /// 将 exports 目标路径（相对于包目录）解析为物理文件。
        /// </summary>
        private ModuleResolutionResult? TryResolveExportsTarget(
            string? target,
            string packageDir,
            string specifier,
            string? fromFilePath,
            List<string> attempts)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                attempts.Add("exports:target — empty or null target");
                return null;
            }

            var resolvedPath = Path.GetFullPath(Path.Combine(packageDir, target));
            return TryResolvePathCandidate(
                specifier, fromFilePath, ModuleSpecifierKind.Bare,
                ModuleResolutionSource.PackageExports, resolvedPath, attempts);
        }

        private static IEnumerable<string> EnumerateIndexCandidates(string directory)
        {
            foreach (var extension in ExtensionCandidates)
            {
                yield return Path.Combine(directory, "index" + extension);
            }
        }
    }

    /// <summary>
    /// 模块 specifier 分类。
    /// </summary>
    public enum ModuleSpecifierKind
    {
        /// <summary>未知类型。</summary>
        Unknown = 0,
        /// <summary>内建模块映射。</summary>
        Builtin = 1,
        /// <summary>相对路径。</summary>
        Relative = 2,
        /// <summary>绝对路径。</summary>
        Absolute = 3,
        /// <summary>裸包名。</summary>
        Bare = 4
    }

    /// <summary>
    /// 模块解析结果。
    /// </summary>
    public sealed record ModuleResolutionResult(
        string Specifier,
        string? From,
        ModuleSpecifierKind Kind,
        ModuleResolutionSource Source,
        string? ResolvedPath,
        IReadOnlyList<string> Attempts);

    /// <summary>
    /// 模块解析来源。
    /// </summary>
    public enum ModuleResolutionSource
    {
        /// <summary>来源于 builtin 映射。</summary>
        BuiltinMap = 0,
        /// <summary>来源于 relative 路径解析。</summary>
        RelativePath = 1,
        /// <summary>来源于 absolute 路径解析。</summary>
        AbsolutePath = 2,
        /// <summary>来源于 workspace imports map（paperclip.json#imports）。</summary>
        WorkspaceImportsMap = 3,
        /// <summary>来源于 node_modules 逐级查找。</summary>
        NodeModules = 4,
        /// <summary>来源于 package.json exports 条件解析。</summary>
        PackageExports = 5
    }

    /// <summary>
    /// 标准化的模块解析异常，包含 specifier/from/attempts/reason 等结构化字段。
    /// </summary>
    public sealed class ModuleResolutionException : Exception
    {
        /// <summary>业务错误码。</summary>
        public string Code { get; }

        /// <summary>原始模块标识符。</summary>
        public string Specifier { get; }

        /// <summary>来源文件。</summary>
        public string? From { get; }

        /// <summary>解析尝试轨迹。</summary>
        public IReadOnlyList<string> Attempts { get; }

        /// <summary>失败原因。</summary>
        public string Reason { get; }

        /// <summary>修复建议。</summary>
        public string? Hint { get; }

        /// <summary>
        /// 创建一个结构化模块解析异常。
        /// </summary>
        public ModuleResolutionException(
            string code,
            string specifier,
            string? from,
            IReadOnlyList<string> attempts,
            string reason,
            string? hint = null,
            Exception? innerException = null)
            : base($"[{code}] {reason} (specifier: {specifier}, from: {from ?? "<entry>"})", innerException)
        {
            Code = code;
            Specifier = specifier;
            From = from;
            Attempts = attempts ?? Array.Empty<string>();
            Reason = reason;
            Hint = hint;
        }

        /// <summary>
        /// 输出标准化错误结构。
        /// </summary>
        public object ToStructuredError()
        {
            return new
            {
                code = Code,
                specifier = Specifier,
                from = From,
                attempts = Attempts,
                reason = Reason,
                hint = Hint
            };
        }
    }
}
