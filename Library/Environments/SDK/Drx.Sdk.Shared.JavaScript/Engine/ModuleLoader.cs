using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        /// <summary>
        /// 加载过程中产生的诊断事件（仅 debug 模式收集）。
        /// </summary>
        public IReadOnlyList<ModuleLoaderEvent> DiagnosticEvents => _diagnosticEvents;

        private readonly List<ModuleLoaderEvent> _diagnosticEvents = new();

        /// <summary>
        /// 初始化 <see cref="ModuleLoader"/>。
        /// </summary>
        public ModuleLoader(ModuleResolver resolver, ModuleCache cache, ModuleRuntimeOptions options)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _options = options ?? throw new ArgumentNullException(nameof(options));
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
                // 4. 读取源码
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

        private void EmitEvent(string eventName, string cacheKey, object? data)
        {
            if (!_options.EnableDebugLogs && !_options.EnableStructuredDebugEvents)
            {
                return;
            }

            _diagnosticEvents.Add(new ModuleLoaderEvent(eventName, cacheKey, data));
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
