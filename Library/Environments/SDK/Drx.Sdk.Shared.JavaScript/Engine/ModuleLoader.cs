using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Drx.Sdk.Shared.JavaScript.Abstractions;

namespace Drx.Sdk.Shared.JavaScript.Engine
{
    /// <summary>
    /// 模块加载器：递归构建模块依赖图，驱动状态机从 Loading → Loaded/Failed，
    /// 集成 <see cref="ModuleResolver"/> 解析与 <see cref="ModuleCache"/> 缓存。
    /// 支持循环依赖检测、single-flight 并发合并与诊断事件。
    /// </summary>
    public sealed class ModuleLoader
    {
        private static readonly Regex ImportFromRegex =
            new(@"\bimport\s+(?:[^\""']+?\s+from\s+)?[\""'](?<spec>[^\""']+)[\""']", RegexOptions.Compiled);

        private static readonly Regex ExportFromRegex =
            new(@"\bexport\s+[^\""']*?\s+from\s+[\""'](?<spec>[^\""']+)[\""']", RegexOptions.Compiled);

        private readonly ModuleResolver _resolver;
        private readonly ModuleCache _cache;
        private readonly ModuleRuntimeOptions _options;
        private readonly InteropBridge _interop;
        private readonly ImportSecurityPolicy? _securityPolicy;
        private readonly ModuleDiagnosticCollector? _diagnosticCollector;

        /// <summary>
        /// 加载过程中产生的诊断事件（仅 debug 模式收集）。
        /// </summary>
        public IReadOnlyList<ModuleLoaderEvent> DiagnosticEvents => _diagnosticEvents;

        /// <summary>
        /// 统一诊断收集器（若已注入），供外部获取全链路事件。
        /// </summary>
        public ModuleDiagnosticCollector? DiagnosticCollector => _diagnosticCollector;

        /// <summary>
        /// 互操作桥接层实例，供外部查看诊断事件。
        /// </summary>
        public InteropBridge Interop => _interop;

        /// <summary>
        /// 安全策略实例（若已注入），供外部读取审计日志。
        /// </summary>
        public ImportSecurityPolicy? SecurityPolicy => _securityPolicy;

        private readonly List<ModuleLoaderEvent> _diagnosticEvents = new();

        /// <summary>
        /// 初始化 <see cref="ModuleLoader"/>。
        /// </summary>
        public ModuleLoader(ModuleResolver resolver, ModuleCache cache, ModuleRuntimeOptions options, ImportSecurityPolicy? securityPolicy = null, ModuleDiagnosticCollector? diagnosticCollector = null)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _interop = new InteropBridge(options, diagnosticCollector);
            _securityPolicy = securityPolicy;
            _diagnosticCollector = diagnosticCollector;
        }

        /// <summary>
        /// 从入口文件开始递归加载模块依赖图。
        /// </summary>
        /// <param name="entryFilePath">入口文件绝对路径。</param>
        /// <param name="executeModule">执行模块源码并返回 namespace（由引擎层提供）。</param>
        /// <returns>入口模块记录。</returns>
        public ModuleRecord LoadModuleGraph(string entryFilePath, Func<string, string, object?> executeModule)
        {
            if (string.IsNullOrWhiteSpace(entryFilePath))
            {
                throw new ArgumentException("入口文件路径不能为空。", nameof(entryFilePath));
            }

            if (executeModule is null)
            {
                throw new ArgumentNullException(nameof(executeModule));
            }

            var resolvedEntry = _resolver.ResolveForEntry(entryFilePath);
            if (resolvedEntry.ResolvedPath is null)
            {
                throw new ModuleLoadException(
                    code: "PC_LOAD_001",
                    moduleUrl: entryFilePath,
                    phase: "resolve",
                    reason: "入口文件解析失败。",
                    hint: "请确认入口路径存在且可访问。");
            }

            var loadingStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return LoadModuleRecursive(resolvedEntry.ResolvedPath, null, executeModule, loadingStack, depth: 0);
        }

        /// <summary>
        /// 异步动态导入模块（对应 JS 的 <c>import()</c> 表达式）。
        /// 复用统一的 <see cref="ModuleResolver"/>、<see cref="ModuleCache"/> 与 <see cref="InteropBridge"/>，
        /// 错误模型与静态导入一致（<see cref="ModuleLoadException"/>）。
        /// </summary>
        /// <param name="specifier">导入标识符（相对路径 / 绝对路径 / 裸包名 / builtin）。</param>
        /// <param name="fromFilePath">发起 import() 调用的文件路径（用于相对路径解析），可为 null。</param>
        /// <param name="executeModule">执行模块源码并返回 namespace（由引擎层提供）。</param>
        /// <returns>已加载的模块记录（含 namespace 与导出）。</returns>
        public Task<ModuleRecord> DynamicImportAsync(
            string specifier,
            string? fromFilePath,
            Func<string, string, object?> executeModule)
        {
            if (string.IsNullOrWhiteSpace(specifier))
            {
                throw new ArgumentException("动态导入 specifier 不能为空。", nameof(specifier));
            }

            if (executeModule is null)
            {
                throw new ArgumentNullException(nameof(executeModule));
            }

            EmitEvent("dynamic.import.start", specifier, new { from = fromFilePath });

            return Task.Run(() =>
            {
                try
                {
                    // 1. 统一解析
                    ModuleResolutionResult resolved;
                    try
                    {
                        resolved = _resolver.Resolve(specifier, fromFilePath);
                    }
                    catch (ModuleResolutionException ex)
                    {
                        var loadEx = new ModuleLoadException(
                            code: "PC_DYN_001",
                            moduleUrl: specifier,
                            phase: "dynamic-resolve",
                            reason: $"动态导入解析失败：{ex.Reason}",
                            hint: ex.Hint,
                            innerException: ex);

                        EmitEvent("dynamic.import.fail", specifier, new { phase = "resolve", reason = ex.Reason });
                        throw loadEx;
                    }

                    if (resolved.ResolvedPath is null)
                    {
                        var loadEx = new ModuleLoadException(
                            code: "PC_DYN_002",
                            moduleUrl: specifier,
                            phase: "dynamic-resolve",
                            reason: $"动态导入无法解析 specifier '{specifier}'。",
                            hint: fromFilePath is not null
                                ? $"请确认相对于 '{fromFilePath}' 的路径是否正确。"
                                : "请确认 specifier 是否正确。");

                        EmitEvent("dynamic.import.fail", specifier, new { phase = "resolve", reason = "resolved path is null" });
                        throw loadEx;
                    }

                    // 2. 复用递归加载（同步部分，共享缓存）
                    var loadingStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var record = LoadModuleRecursive(resolved.ResolvedPath, fromFilePath, executeModule, loadingStack, depth: 0);

                    if (record.State == ModuleRecordState.Failed)
                    {
                        EmitEvent("dynamic.import.fail", specifier, new { phase = "load", cacheKey = record.CacheKey });
                        throw record.Error ?? new ModuleLoadException(
                            code: "PC_DYN_003",
                            moduleUrl: resolved.ResolvedPath,
                            phase: "dynamic-load",
                            reason: "动态导入的模块处于 Failed 状态。");
                    }

                    EmitEvent("dynamic.import.end", specifier, new
                    {
                        resolvedPath = resolved.ResolvedPath,
                        cacheKey = record.CacheKey,
                        state = record.State.ToString(),
                        cached = _cache.TryGet(record.CacheKey, out _)
                    });

                    return record;
                }
                catch (ModuleLoadException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var loadEx = new ModuleLoadException(
                        code: "PC_DYN_004",
                        moduleUrl: specifier,
                        phase: "dynamic-import",
                        reason: $"动态导入异常：{ex.Message}",
                        hint: "请检查 specifier 与目标模块。",
                        innerException: ex);

                    EmitEvent("dynamic.import.fail", specifier, new { phase = "unexpected", reason = ex.Message });
                    throw loadEx;
                }
            });
        }

        /// <summary>
        /// 递归加载单个模块及其依赖，通过状态机和缓存避免重复执行。
        /// </summary>
        private ModuleRecord LoadModuleRecursive(
            string absolutePath,
            string? fromPath,
            Func<string, string, object?> executeModule,
            HashSet<string> loadingStack,
            int depth)
        {
            var cacheKey = ModuleCache.NormalizeCacheKey(absolutePath);

            EmitEvent("module.load.start", cacheKey, new { depth, from = fromPath });

            // 1. 缓存命中（已加载或已失败）：直接返回
            if (_cache.TryGet(cacheKey, out var cached))
            {
                if (cached!.State == ModuleRecordState.Loaded)
                {
                    EmitEvent("cache.hit", cacheKey, new { state = "loaded" });
                    return cached;
                }

                if (cached.State == ModuleRecordState.Failed)
                {
                    EmitEvent("cache.hit", cacheKey, new { state = "failed" });
                    return cached;
                }

                // Loading 状态 = 循环依赖
                if (cached.State == ModuleRecordState.Loading)
                {
                    EmitEvent("module.circular", cacheKey, new { from = fromPath, depth });
                    return cached;
                }
            }

            EmitEvent("cache.miss", cacheKey, null);

            // 2. 循环依赖检测（递归栈）
            if (!loadingStack.Add(cacheKey))
            {
                EmitEvent("module.circular", cacheKey, new { from = fromPath, depth });
                // 返回缓存中的 loading 占位
                if (_cache.TryGet(cacheKey, out var circularRecord))
                {
                    return circularRecord!;
                }

                // 不应发生：栈中有但缓存中没有
                throw new ModuleLoadException(
                    code: "PC_LOAD_005",
                    moduleUrl: absolutePath,
                    phase: "circular-check",
                    reason: "循环依赖检测异常：加载栈与缓存状态不一致。");
            }

            // 3. 创建 Loading 占位并注册缓存
            var moduleKind = DetermineModuleKind(absolutePath);
            var record = new ModuleRecord(cacheKey, absolutePath, moduleKind);
            var sw = Stopwatch.StartNew();

            if (!_cache.TryRegisterLoading(cacheKey, record, out var existingRecord))
            {
                // 并发中已被其他线程注册
                loadingStack.Remove(cacheKey);
                return existingRecord!;
            }

            try
            {
                // 4a. 安全策略复查（对符号链接做完整验证）
                if (_securityPolicy is not null)
                {
                    try
                    {
                        _securityPolicy.ValidateAccess(absolutePath, absolutePath, fromPath);
                        EmitEvent("security.check.pass", cacheKey, new { path = absolutePath });
                    }
                    catch (ImportSecurityException secEx)
                    {
                        throw new ModuleLoadException(
                            code: secEx.Code,
                            moduleUrl: absolutePath,
                            phase: "security-check",
                            reason: secEx.Message,
                            hint: secEx.Hint,
                            innerException: secEx);
                    }
                }

                // 4b. 读取源码
                if (!File.Exists(absolutePath))
                {
                    throw new ModuleLoadException(
                        code: "PC_LOAD_002",
                        moduleUrl: absolutePath,
                        phase: "read",
                        reason: "模块文件不存在。",
                        hint: "请确认模块文件路径正确。");
                }

                var source = File.ReadAllText(absolutePath);

                // 5. 解析并递归加载依赖
                var specifiers = ExtractStaticImportSpecifiers(source);
                var depKeys = new List<string>();

                foreach (var spec in specifiers)
                {
                    try
                    {
                        var resolved = _resolver.Resolve(spec, absolutePath);

                        if (resolved.Kind == ModuleSpecifierKind.Builtin || string.IsNullOrWhiteSpace(resolved.ResolvedPath))
                        {
                            continue;
                        }

                        var depRecord = LoadModuleRecursive(resolved.ResolvedPath, absolutePath, executeModule, loadingStack, depth + 1);
                        depKeys.Add(depRecord.CacheKey);
                    }
                    catch (ModuleResolutionException ex)
                    {
                        throw new ModuleLoadException(
                            code: "PC_LOAD_003",
                            moduleUrl: absolutePath,
                            phase: "resolve-dependency",
                            reason: $"依赖 '{spec}' 解析失败：{ex.Reason}",
                            hint: ex.Hint,
                            innerException: ex);
                    }
                }

                // 6. 执行模块
                EmitEvent("module.execute.start", cacheKey, new { kind = moduleKind.ToString() });
                var moduleNamespace = executeModule(absolutePath, source);
                EmitEvent("module.execute.end", cacheKey, null);

                // 7. 状态转为 Loaded
                sw.Stop();
                record.MarkLoaded(
                    moduleNamespace: moduleNamespace,
                    exports: ExtractExports(moduleNamespace),
                    dependencies: depKeys,
                    loadDuration: sw.Elapsed);

                _cache.Update(cacheKey, record);
                EmitEvent("module.load.end", cacheKey, new { durationMs = sw.Elapsed.TotalMilliseconds, state = "loaded" });

                return record;
            }
            catch (ModuleLoadException)
            {
                sw.Stop();
                if (record.State == ModuleRecordState.Loading)
                {
                    record.MarkFailed(
                        new ModuleLoadException(
                            code: "PC_LOAD_004",
                            moduleUrl: absolutePath,
                            phase: "load",
                            reason: "模块加载管道异常。"),
                        sw.Elapsed);
                    _cache.Update(cacheKey, record);
                }

                EmitEvent("module.load.end", cacheKey, new { durationMs = sw.Elapsed.TotalMilliseconds, state = "failed" });
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                var loadEx = new ModuleLoadException(
                    code: "PC_LOAD_004",
                    moduleUrl: absolutePath,
                    phase: "execute",
                    reason: $"模块执行失败：{ex.Message}",
                    hint: "请检查模块源码是否有语法或运行时错误。",
                    innerException: ex);

                if (record.State == ModuleRecordState.Loading)
                {
                    record.MarkFailed(loadEx, sw.Elapsed);
                    _cache.Update(cacheKey, record);
                }

                EmitEvent("module.load.end", cacheKey, new { durationMs = sw.Elapsed.TotalMilliseconds, state = "failed" });
                throw loadEx;
            }
            finally
            {
                loadingStack.Remove(cacheKey);
            }
        }

        /// <summary>
        /// 根据文件扩展名与约定推断模块类型。
        /// </summary>
        internal static ModuleKind DetermineModuleKind(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            return ext.ToLowerInvariant() switch
            {
                ".mjs" or ".mts" => ModuleKind.Esm,
                ".cjs" or ".cts" => ModuleKind.Cjs,
                ".json" => ModuleKind.Json,
                _ => ModuleKind.Esm // .js/.ts 默认 ESM（Module Runtime 上下文）
            };
        }

        /// <summary>
        /// 提取静态导入 specifier 列表。
        /// </summary>
        private static IReadOnlyList<string> ExtractStaticImportSpecifiers(string source)
        {
            var matches = new List<Match>();
            matches.AddRange(ImportFromRegex.Matches(source).Cast<Match>());
            matches.AddRange(ExportFromRegex.Matches(source).Cast<Match>());

            return matches
                .OrderBy(m => m.Index)
                .Select(m => m.Groups["spec"].Value.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// 尝试从模块命名空间提取导出字典（最佳努力）。
        /// </summary>
        private static IReadOnlyDictionary<string, object?>? ExtractExports(object? moduleNamespace)
        {
            if (moduleNamespace is IDictionary<string, object?> dict)
            {
                return new Dictionary<string, object?>(dict, StringComparer.Ordinal);
            }

            return null;
        }

        /// <summary>
        /// 获取依赖模块的导出，自动应用 ESM↔CJS 互操作包装。
        /// 当导入方与目标模块类型不同时（如 ESM import CJS），返回经互操作转换后的 namespace。
        /// </summary>
        /// <param name="importerKind">导入方模块类型。</param>
        /// <param name="dependencyCacheKey">依赖模块的缓存键。</param>
        /// <param name="targetSource">依赖模块源码（CJS→ESM 时用于静态推导命名导出）。</param>
        /// <returns>互操作结果，含方向、是否应用包装、最终导出字典。</returns>
        /// <exception cref="InteropException">互操作语义错误时抛出（不隐式吞错）。</exception>
        public InteropResult GetDependencyExports(
            ModuleKind importerKind,
            string dependencyCacheKey,
            string? targetSource = null)
        {
            if (!_cache.TryGet(dependencyCacheKey, out var targetRecord) || targetRecord is null)
            {
                throw new ModuleLoadException(
                    code: "PC_LOAD_006",
                    moduleUrl: dependencyCacheKey,
                    phase: "interop",
                    reason: "依赖模块未在缓存中找到。",
                    hint: "请确认依赖模块已先通过 LoadModuleGraph 加载。");
            }

            return _interop.ResolveInterop(importerKind, targetRecord, targetSource);
        }

        /// <summary>
        /// 尝试获取已加载模块的命名空间对象（供 CJS require 桥接使用）。
        /// </summary>
        /// <param name="absolutePath">模块绝对路径。</param>
        /// <param name="moduleNamespace">命中时返回模块 namespace（即 module.exports 或 ESM 导出对象），否则 null。</param>
        /// <returns>找到并已 Loaded 时返回 true。</returns>
        public bool TryGetLoadedExports(string absolutePath, out object? moduleNamespace)
        {
            moduleNamespace = null;
            var cacheKey = ModuleCache.NormalizeCacheKey(absolutePath);
            if (!_cache.TryGet(cacheKey, out var record) || record is null || record.State != ModuleRecordState.Loaded)
            {
                return false;
            }

            moduleNamespace = record.Namespace;
            return true;
        }

        private void EmitEvent(string eventName, string cacheKey, object? data)
        {
            if (!_options.EnableDebugLogs && !_options.EnableStructuredDebugEvents)
            {
                return;
            }

            _diagnosticEvents.Add(new ModuleLoaderEvent(eventName, cacheKey, data));

            // 向统一收集器推送（带类别分类）
            if (_diagnosticCollector is not null)
            {
                var category = ClassifyEventCategory(eventName);
                var severity = eventName.Contains("fail") || eventName.Contains("error")
                    ? DiagnosticSeverity.Error
                    : eventName.Contains("circular") || eventName.Contains("static-only")
                        ? DiagnosticSeverity.Warning
                        : DiagnosticSeverity.Debug;
                _diagnosticCollector.Emit(eventName, category, severity, cacheKey, data);
            }
        }

        private static DiagnosticCategory ClassifyEventCategory(string eventName)
        {
            if (eventName.StartsWith("cache.", StringComparison.Ordinal))
                return DiagnosticCategory.Cache;
            if (eventName.StartsWith("security.", StringComparison.Ordinal))
                return DiagnosticCategory.Security;
            if (eventName.StartsWith("dynamic.", StringComparison.Ordinal))
                return DiagnosticCategory.DynamicImport;
            if (eventName.StartsWith("module.circular", StringComparison.Ordinal))
                return DiagnosticCategory.Load;
            return DiagnosticCategory.Load;
        }
    }

    /// <summary>
    /// 模块加载器诊断事件。
    /// </summary>
    public sealed record ModuleLoaderEvent(string EventName, string CacheKey, object? Data)
    {
        /// <summary>
        /// 事件产生时间戳。
        /// </summary>
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    }
}
