using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Drx.Sdk.Shared.JavaScript.Engine
{
    /// <summary>
    /// 统一模块诊断事件——所有 resolve/load/cache/security/interop 链路共用的结构化事件模型。
    /// 设计目标：机器可解析（JSONL）、人可读（text），零开销原则——非 debug 路径不分配任何对象。
    /// </summary>
    public sealed class ModuleDiagnosticEvent
    {
        /// <summary>事件名称（如 resolve.start、cache.hit、security.check.pass）。</summary>
        public string EventName { get; }

        /// <summary>事件类别。</summary>
        public DiagnosticCategory Category { get; }

        /// <summary>事件严重级别。</summary>
        public DiagnosticSeverity Severity { get; }

        /// <summary>关联模块键（cache key 或 specifier）。</summary>
        public string ModuleKey { get; }

        /// <summary>事件负载（结构化数据）。</summary>
        public object? Data { get; }

        /// <summary>事件产生时间戳（UTC）。</summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// 创建诊断事件。
        /// </summary>
        public ModuleDiagnosticEvent(
            string eventName,
            DiagnosticCategory category,
            DiagnosticSeverity severity,
            string moduleKey,
            object? data)
        {
            EventName = eventName;
            Category = category;
            Severity = severity;
            ModuleKey = moduleKey;
            Data = data;
            Timestamp = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// 转为 JSONL 行（机器解析格式）。
        /// </summary>
        public string ToJsonLine()
        {
            var dict = new Dictionary<string, object?>
            {
                ["event"] = EventName,
                ["category"] = Category.ToString(),
                ["severity"] = Severity.ToString(),
                ["moduleKey"] = ModuleKey,
                ["timestamp"] = Timestamp,
                ["data"] = Data
            };
            return JsonSerializer.Serialize(dict);
        }

        /// <summary>
        /// 转为可读文本行（人类友好格式）。
        /// </summary>
        public string ToReadableString()
        {
            var dataStr = Data is not null ? $" | {FormatDataBrief(Data)}" : "";
            return $"[{Timestamp:HH:mm:ss.fff}] [{Category}] [{Severity}] {EventName} — {ModuleKey}{dataStr}";
        }

        private static string FormatDataBrief(object data)
        {
            try
            {
                return JsonSerializer.Serialize(data);
            }
            catch
            {
                return data.ToString() ?? "";
            }
        }
    }

    /// <summary>
    /// 诊断事件类别。
    /// </summary>
    public enum DiagnosticCategory
    {
        /// <summary>解析阶段。</summary>
        Resolve,
        /// <summary>加载阶段。</summary>
        Load,
        /// <summary>缓存操作。</summary>
        Cache,
        /// <summary>安全策略。</summary>
        Security,
        /// <summary>互操作桥接。</summary>
        Interop,
        /// <summary>动态导入。</summary>
        DynamicImport,
        /// <summary>运行时生命周期。</summary>
        Runtime
    }

    /// <summary>
    /// 诊断事件严重级别。
    /// </summary>
    public enum DiagnosticSeverity
    {
        /// <summary>跟踪信息（最低级别）。</summary>
        Trace,
        /// <summary>调试信息。</summary>
        Debug,
        /// <summary>一般信息。</summary>
        Info,
        /// <summary>警告。</summary>
        Warning,
        /// <summary>错误。</summary>
        Error
    }

    /// <summary>
    /// 统一诊断事件收集器——线程安全，零开销守卫。
    /// 所有模块系统组件（resolver/loader/cache/security/interop）共享同一实例。
    /// </summary>
    public sealed class ModuleDiagnosticCollector
    {
        private readonly List<ModuleDiagnosticEvent> _events = new();
        private readonly object _lock = new();
        private volatile bool _enabled;

        /// <summary>
        /// 是否启用收集。非 debug 模式下为 false，所有 Emit 调用直接返回，零分配。
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>
        /// 已收集的事件快照。
        /// </summary>
        public IReadOnlyList<ModuleDiagnosticEvent> Events
        {
            get
            {
                lock (_lock)
                {
                    return _events.ToArray();
                }
            }
        }

        /// <summary>
        /// 已收集事件数量。
        /// </summary>
        public int Count => _events.Count;

        /// <summary>
        /// 创建收集器。
        /// </summary>
        /// <param name="enabled">初始是否启用。</param>
        public ModuleDiagnosticCollector(bool enabled)
        {
            _enabled = enabled;
        }

        /// <summary>
        /// 发出诊断事件。非 debug 路径零开销（直接返回）。
        /// </summary>
        public void Emit(
            string eventName,
            DiagnosticCategory category,
            DiagnosticSeverity severity,
            string moduleKey,
            object? data = null)
        {
            if (!_enabled) return;

            var evt = new ModuleDiagnosticEvent(eventName, category, severity, moduleKey, data);
            lock (_lock)
            {
                _events.Add(evt);
            }
        }

        /// <summary>
        /// 快捷：发出 Resolve 类别事件。
        /// </summary>
        public void EmitResolve(string eventName, string moduleKey, object? data = null)
        {
            Emit(eventName, DiagnosticCategory.Resolve, DiagnosticSeverity.Debug, moduleKey, data);
        }

        /// <summary>
        /// 快捷：发出 Load 类别事件。
        /// </summary>
        public void EmitLoad(string eventName, string moduleKey, object? data = null)
        {
            Emit(eventName, DiagnosticCategory.Load, DiagnosticSeverity.Debug, moduleKey, data);
        }

        /// <summary>
        /// 快捷：发出 Cache 类别事件。
        /// </summary>
        public void EmitCache(string eventName, string moduleKey, object? data = null)
        {
            Emit(eventName, DiagnosticCategory.Cache, DiagnosticSeverity.Debug, moduleKey, data);
        }

        /// <summary>
        /// 快捷：发出 Security 类别事件。
        /// </summary>
        public void EmitSecurity(string eventName, string moduleKey, object? data = null)
        {
            Emit(eventName, DiagnosticCategory.Security, DiagnosticSeverity.Info, moduleKey, data);
        }

        /// <summary>
        /// 快捷：发出 Interop 类别事件。
        /// </summary>
        public void EmitInterop(string eventName, string moduleKey, object? data = null)
        {
            Emit(eventName, DiagnosticCategory.Interop, DiagnosticSeverity.Debug, moduleKey, data);
        }

        /// <summary>
        /// 快捷：发出 DynamicImport 类别事件。
        /// </summary>
        public void EmitDynamic(string eventName, string moduleKey, object? data = null)
        {
            Emit(eventName, DiagnosticCategory.DynamicImport, DiagnosticSeverity.Debug, moduleKey, data);
        }

        /// <summary>
        /// 快捷：发出 Error 级别事件。
        /// </summary>
        public void EmitError(string eventName, DiagnosticCategory category, string moduleKey, object? data = null)
        {
            Emit(eventName, category, DiagnosticSeverity.Error, moduleKey, data);
        }

        /// <summary>
        /// 快捷：发出 Warning 级别事件。
        /// </summary>
        public void EmitWarning(string eventName, DiagnosticCategory category, string moduleKey, object? data = null)
        {
            Emit(eventName, category, DiagnosticSeverity.Warning, moduleKey, data);
        }

        /// <summary>
        /// 输出全部事件为 JSONL 格式字符串（每行一个 JSON 对象）。
        /// </summary>
        public string ToJsonLines()
        {
            var events = Events;
            if (events.Count == 0) return "";

            var sb = new System.Text.StringBuilder(events.Count * 200);
            foreach (var evt in events)
            {
                sb.AppendLine(evt.ToJsonLine());
            }
            return sb.ToString();
        }

        /// <summary>
        /// 输出全部事件为可读文本格式。
        /// </summary>
        public string ToReadableText()
        {
            var events = Events;
            if (events.Count == 0) return "";

            var sb = new System.Text.StringBuilder(events.Count * 120);
            foreach (var evt in events)
            {
                sb.AppendLine(evt.ToReadableString());
            }
            return sb.ToString();
        }

        /// <summary>
        /// 生成诊断摘要统计。
        /// </summary>
        public DiagnosticSummary GetSummary()
        {
            var events = Events;
            var categoryCount = new Dictionary<DiagnosticCategory, int>();
            var severityCount = new Dictionary<DiagnosticSeverity, int>();

            foreach (var evt in events)
            {
                categoryCount[evt.Category] = categoryCount.GetValueOrDefault(evt.Category) + 1;
                severityCount[evt.Severity] = severityCount.GetValueOrDefault(evt.Severity) + 1;
            }

            return new DiagnosticSummary(events.Count, categoryCount, severityCount);
        }

        /// <summary>
        /// 清空所有已收集事件。
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _events.Clear();
            }
        }
    }

    /// <summary>
    /// 诊断摘要统计。
    /// </summary>
    public sealed record DiagnosticSummary(
        int TotalEvents,
        IReadOnlyDictionary<DiagnosticCategory, int> ByCategory,
        IReadOnlyDictionary<DiagnosticSeverity, int> BySeverity)
    {
        /// <summary>
        /// 转为结构化对象（可 JSON 序列化）。
        /// </summary>
        public object ToStructured()
        {
            var catDict = new Dictionary<string, int>();
            foreach (var kvp in ByCategory) catDict[kvp.Key.ToString()] = kvp.Value;

            var sevDict = new Dictionary<string, int>();
            foreach (var kvp in BySeverity) sevDict[kvp.Key.ToString()] = kvp.Value;

            return new
            {
                totalEvents = TotalEvents,
                byCategory = catDict,
                bySeverity = sevDict
            };
        }
    }

    /// <summary>
    /// 全链路模块错误码常量。
    /// 所有 Module System 组件（resolver/loader/security/interop/dynamic）的错误码集中管理。
    /// </summary>
    public static class ModuleErrorCodes
    {
        // ──── Resolver 错误 ────
        /// <summary>空 specifier。</summary>
        public const string ResEmptySpecifier = "PC_RES_000";
        /// <summary>路径解析失败（文件不存在或候选路径穷尽）。</summary>
        public const string ResPathNotFound = "PC_RES_001";
        /// <summary>相对路径缺少上下文。</summary>
        public const string ResMissingContext = "PC_RES_002";
        /// <summary>Builtin 未注册。</summary>
        public const string ResBuiltinNotFound = "PC_RES_003";
        /// <summary>裸包名解析失败。</summary>
        public const string ResBareNotFound = "PC_RES_004";

        // ──── Loader 错误 ────
        /// <summary>入口文件解析失败。</summary>
        public const string LoadEntryFailed = "PC_LOAD_001";
        /// <summary>模块文件不存在。</summary>
        public const string LoadFileNotFound = "PC_LOAD_002";
        /// <summary>依赖解析失败。</summary>
        public const string LoadDependencyFailed = "PC_LOAD_003";
        /// <summary>模块加载管道异常。</summary>
        public const string LoadPipelineError = "PC_LOAD_004";
        /// <summary>循环依赖状态异常。</summary>
        public const string LoadCircularError = "PC_LOAD_005";
        /// <summary>互操作依赖未找到。</summary>
        public const string LoadInteropMissing = "PC_LOAD_006";

        // ──── Dynamic Import 错误 ────
        /// <summary>动态导入解析失败。</summary>
        public const string DynResolveFailed = "PC_DYN_001";
        /// <summary>动态导入 specifier 无法解析。</summary>
        public const string DynUnresolved = "PC_DYN_002";
        /// <summary>动态导入目标状态 Failed。</summary>
        public const string DynTargetFailed = "PC_DYN_003";
        /// <summary>动态导入意外异常。</summary>
        public const string DynUnexpected = "PC_DYN_004";

        // ──── Security 错误 ────
        /// <summary>路径越界（含符号链接越界）。</summary>
        public const string SecOutOfBoundary = "PC_SEC_001";
        /// <summary>未授权白名单。</summary>
        public const string SecUnauthorized = "PC_SEC_002";
        /// <summary>非法 specifier / 空路径。</summary>
        public const string SecInvalidPath = "PC_SEC_003";

        // ──── Interop 错误 ────
        /// <summary>ESM namespace 为 null。</summary>
        public const string InteropNullNamespace = "PC_INTEROP_001";
        /// <summary>目标模块未加载。</summary>
        public const string InteropNotLoaded = "PC_INTEROP_002";
        /// <summary>未知互操作方向。</summary>
        public const string InteropUnknownDirection = "PC_INTEROP_003";
    }

    /// <summary>
    /// 统一结构化错误接口——所有模块系统异常类型（resolution/load/security/interop）均实现此接口，
    /// 确保错误上下文完整且可序列化。
    /// </summary>
    public interface IModuleStructuredError
    {
        /// <summary>业务错误码。</summary>
        string Code { get; }

        /// <summary>修复建议。</summary>
        string? Hint { get; }

        /// <summary>转为可序列化的结构化错误对象。</summary>
        object ToStructuredError();
    }
}
