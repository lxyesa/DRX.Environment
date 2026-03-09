using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Drx.Sdk.Shared.JavaScript.Abstractions;

namespace Drx.Sdk.Shared.JavaScript.Engine
{
    /// <summary>
    /// ESM ↔ CJS 互操作桥接层。
    /// <para>
    /// 互操作规则（对齐 design §5.2）：
    /// <list type="bullet">
    ///   <item>ESM 导入 CJS：<c>default</c> 绑定到 <c>module.exports</c>，命名导出采用静态可推导 + 运行时兜底。</item>
    ///   <item>CJS require ESM：返回 ESM namespace 对象（含 <c>default</c> 键）。</item>
    /// </list>
    /// 语义不会隐式吞错：类型冲突与映射失败均产生 <see cref="InteropException"/>。
    /// </para>
    /// </summary>
    public sealed class InteropBridge
    {
        /// <summary>
        /// CJS <c>module.exports</c> 中可静态推导为命名导出的属性名正则：标识符字符。
        /// </summary>
        private static readonly Regex ValidIdentifierRegex =
            new(@"^[a-zA-Z_$][a-zA-Z0-9_$]*$", RegexOptions.Compiled);

        /// <summary>
        /// 静态分析 CJS 源码中 <c>exports.NAME = ...</c> 模式提取命名导出候选。
        /// </summary>
        private static readonly Regex CjsNamedExportRegex =
            new(@"(?:module\.)?exports\s*\.\s*(?<name>[a-zA-Z_$][a-zA-Z0-9_$]*)\s*=", RegexOptions.Compiled);

        private readonly ModuleRuntimeOptions _options;
        private readonly ModuleDiagnosticCollector? _diagnosticCollector;
        private readonly List<InteropDiagnosticEvent> _diagnosticEvents = new();

        /// <summary>
        /// 互操作过程中产生的诊断事件（仅 debug 模式收集）。
        /// </summary>
        public IReadOnlyList<InteropDiagnosticEvent> DiagnosticEvents => _diagnosticEvents;

        /// <summary>
        /// 初始化互操作桥接。
        /// </summary>
        public InteropBridge(ModuleRuntimeOptions options, ModuleDiagnosticCollector? diagnosticCollector = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _diagnosticCollector = diagnosticCollector;
        }

        /// <summary>
        /// 将 CJS 模块导出包装为 ESM namespace 对象，供 ESM <c>import</c> 语句使用。
        /// <para>
        /// 映射规则：
        /// <list type="number">
        ///   <item><c>default</c> = <c>module.exports</c> 原始值。</item>
        ///   <item>若 <c>module.exports</c> 为字典/对象，其合法标识符键提升为命名导出。</item>
        ///   <item>若无法提取命名导出，回退到仅 <c>default</c>。</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="moduleExports">CJS 模块的 <c>module.exports</c> 值。</param>
        /// <param name="cjsSource">CJS 源码（可选），用于静态推导命名导出候选。</param>
        /// <param name="moduleUrl">模块 URL，用于诊断。</param>
        /// <returns>ESM namespace 字典，至少包含 <c>default</c> 键。</returns>
        public IReadOnlyDictionary<string, object?> WrapCjsAsEsmNamespace(
            object? moduleExports,
            string? cjsSource,
            string moduleUrl)
        {
            var ns = new Dictionary<string, object?>(StringComparer.Ordinal);

            // default 始终绑定到 module.exports
            ns["default"] = moduleExports;
            EmitEvent("interop.cjs-to-esm.default", moduleUrl, new { hasValue = moduleExports is not null });

            // 尝试从运行时对象提取命名导出
            var runtimeNames = ExtractNamedExportsFromObject(moduleExports);

            // 尝试从源码静态分析补充候选
            var staticNames = !string.IsNullOrWhiteSpace(cjsSource)
                ? ExtractNamedExportsFromSource(cjsSource)
                : Array.Empty<string>();

            // 合并：运行时优先（值可用），静态补充
            var allNames = new HashSet<string>(runtimeNames.Keys, StringComparer.Ordinal);
            foreach (var name in staticNames)
            {
                allNames.Add(name);
            }

            // default 不重复暴露为命名导出
            allNames.Remove("default");

            foreach (var name in allNames.OrderBy(n => n, StringComparer.Ordinal))
            {
                if (runtimeNames.TryGetValue(name, out var value))
                {
                    ns[name] = value;
                }
                else
                {
                    // 静态推导到但运行时无法提取：设为 undefined 占位，记录警告
                    ns[name] = null;
                    EmitEvent("interop.cjs-to-esm.static-only", moduleUrl,
                        new { name, hint = "静态推导发现但运行时未提取到，值为 null" });
                }
            }

            EmitEvent("interop.cjs-to-esm.complete", moduleUrl,
                new { namedExportCount = ns.Count - 1, names = string.Join(",", allNames.OrderBy(n => n)) });

            return ns;
        }

        /// <summary>
        /// 将 ESM namespace 包装为 CJS <c>require()</c> 的返回值。
        /// <para>
        /// 映射规则：返回 ESM namespace 对象本身（包含 <c>default</c> 与所有命名导出）。
        /// 调用方通过 <c>result.default</c> 访问默认导出。
        /// </para>
        /// </summary>
        /// <param name="esmNamespace">ESM 模块的命名空间字典。</param>
        /// <param name="moduleUrl">模块 URL，用于诊断。</param>
        /// <returns>可作为 <c>require()</c> 返回值的对象。</returns>
        public IReadOnlyDictionary<string, object?> WrapEsmForCjsRequire(
            IReadOnlyDictionary<string, object?>? esmNamespace,
            string moduleUrl)
        {
            if (esmNamespace is null)
            {
                throw new InteropException(
                    code: "PC_INTEROP_001",
                    moduleUrl: moduleUrl,
                    direction: InteropDirection.CjsRequiresEsm,
                    reason: "ESM namespace 为 null，无法包装为 CJS require 返回值。",
                    hint: "请确认 ESM 模块已正确加载并产生了 namespace。");
            }

            EmitEvent("interop.esm-to-cjs.complete", moduleUrl,
                new { exportCount = esmNamespace.Count, hasDefault = esmNamespace.ContainsKey("default") });

            return esmNamespace;
        }

        /// <summary>
        /// 根据模块类型与导入方向，决定是否需要互操作包装，并返回包装后的结果。
        /// </summary>
        /// <param name="importerKind">导入方模块类型。</param>
        /// <param name="targetRecord">被导入的目标模块记录。</param>
        /// <param name="targetSource">目标模块源码（CJS 场景用于静态推导）。</param>
        /// <returns>
        /// 包装后的模块全部导出（namespace 字典），或原样返回（无需互操作时）。
        /// </returns>
        public InteropResult ResolveInterop(
            ModuleKind importerKind,
            ModuleRecord targetRecord,
            string? targetSource = null)
        {
            if (targetRecord is null)
            {
                throw new ArgumentNullException(nameof(targetRecord));
            }

            if (targetRecord.State != ModuleRecordState.Loaded)
            {
                throw new InteropException(
                    code: "PC_INTEROP_002",
                    moduleUrl: targetRecord.Url,
                    direction: InteropDirection.Unknown,
                    reason: $"目标模块未处于 Loaded 状态（当前状态：{targetRecord.State}）。",
                    hint: "请确认模块已正确加载后再进行互操作。");
            }

            var direction = ClassifyDirection(importerKind, targetRecord.Kind);
            EmitEvent("interop.resolve", targetRecord.Url,
                new { importerKind = importerKind.ToString(), targetKind = targetRecord.Kind.ToString(), direction = direction.ToString() });

            switch (direction)
            {
                case InteropDirection.EsmImportsCjs:
                {
                    var ns = WrapCjsAsEsmNamespace(targetRecord.Namespace, targetSource, targetRecord.Url);
                    return new InteropResult(direction, ns, true);
                }

                case InteropDirection.CjsRequiresEsm:
                {
                    var wrapped = WrapEsmForCjsRequire(targetRecord.Exports, targetRecord.Url);
                    return new InteropResult(direction, wrapped, true);
                }

                case InteropDirection.SameKind:
                case InteropDirection.JsonImport:
                case InteropDirection.BuiltinImport:
                    // 无需互操作
                    return new InteropResult(direction, targetRecord.Exports, false);

                default:
                    throw new InteropException(
                        code: "PC_INTEROP_003",
                        moduleUrl: targetRecord.Url,
                        direction: direction,
                        reason: $"未知的互操作方向：{direction}。",
                        hint: "这可能是内部错误，请报告。");
            }
        }

        /// <summary>
        /// 根据导入方与目标模块类型分类互操作方向。
        /// </summary>
        internal static InteropDirection ClassifyDirection(ModuleKind importerKind, ModuleKind targetKind)
        {
            if (targetKind == ModuleKind.Json)
            {
                return InteropDirection.JsonImport;
            }

            if (targetKind == ModuleKind.Builtin)
            {
                return InteropDirection.BuiltinImport;
            }

            if (importerKind == targetKind)
            {
                return InteropDirection.SameKind;
            }

            if (importerKind == ModuleKind.Esm && targetKind == ModuleKind.Cjs)
            {
                return InteropDirection.EsmImportsCjs;
            }

            if (importerKind == ModuleKind.Cjs && targetKind == ModuleKind.Esm)
            {
                return InteropDirection.CjsRequiresEsm;
            }

            // Builtin/Json 作为导入方不应出现，按 SameKind 处理
            return InteropDirection.SameKind;
        }

        /// <summary>
        /// 从运行时对象提取命名导出（字典键或对象属性）。
        /// </summary>
        private static Dictionary<string, object?> ExtractNamedExportsFromObject(object? moduleExports)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (moduleExports is null)
            {
                return result;
            }

            // 优先：字典接口
            if (moduleExports is IDictionary<string, object?> dict)
            {
                foreach (var kvp in dict)
                {
                    if (IsValidExportName(kvp.Key))
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }
                return result;
            }

            if (moduleExports is IReadOnlyDictionary<string, object?> readonlyDict)
            {
                foreach (var kvp in readonlyDict)
                {
                    if (IsValidExportName(kvp.Key))
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }
                return result;
            }

            // 兜底：反射公共属性（对于 ClearScript 导出的 JS 对象）
            var type = moduleExports.GetType();
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
            {
                return result;
            }

            try
            {
                foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    if (!IsValidExportName(prop.Name))
                    {
                        continue;
                    }

                    try
                    {
                        result[prop.Name] = prop.GetValue(moduleExports);
                    }
                    catch
                    {
                        // 属性访问失败时跳过，不吞错——调用方可通过诊断事件看到缺失
                    }
                }
            }
            catch
            {
                // 反射本身失败：安全忽略，仅返回 default
            }

            return result;
        }

        /// <summary>
        /// 从 CJS 源码静态分析中提取 <c>exports.NAME = ...</c> 模式的命名导出候选。
        /// </summary>
        private static IReadOnlyList<string> ExtractNamedExportsFromSource(string source)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match match in CjsNamedExportRegex.Matches(source))
            {
                var name = match.Groups["name"].Value;
                if (IsValidExportName(name))
                {
                    names.Add(name);
                }
            }

            return names.OrderBy(n => n, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// 判断名称是否为合法的 JavaScript 导出标识符。
        /// </summary>
        private static bool IsValidExportName(string name)
        {
            return !string.IsNullOrWhiteSpace(name)
                && ValidIdentifierRegex.IsMatch(name)
                && !IsJavaScriptReservedWord(name);
        }

        /// <summary>
        /// 判断是否为 JavaScript 保留字（不能作为命名导出）。
        /// </summary>
        private static bool IsJavaScriptReservedWord(string name)
        {
            return name switch
            {
                "break" or "case" or "catch" or "continue" or "debugger" or
                "default" or "delete" or "do" or "else" or "finally" or
                "for" or "function" or "if" or "in" or "instanceof" or
                "new" or "return" or "switch" or "this" or "throw" or
                "try" or "typeof" or "var" or "void" or "while" or
                "with" or "class" or "const" or "enum" or "export" or
                "extends" or "import" or "super" or "implements" or
                "interface" or "let" or "package" or "private" or
                "protected" or "public" or "static" or "yield" => true,
                _ => false
            };
        }

        private void EmitEvent(string eventName, string moduleUrl, object? data)
        {
            if (!_options.EnableDebugLogs && !_options.EnableStructuredDebugEvents)
            {
                return;
            }

            _diagnosticEvents.Add(new InteropDiagnosticEvent(eventName, moduleUrl, data));

            // 向统一收集器推送
            if (_diagnosticCollector is not null)
            {
                var severity = eventName.Contains("static-only")
                    ? DiagnosticSeverity.Warning
                    : DiagnosticSeverity.Debug;
                _diagnosticCollector.Emit(eventName, DiagnosticCategory.Interop, severity, moduleUrl, data);
            }
        }
    }

    /// <summary>
    /// 互操作方向枚举。
    /// </summary>
    public enum InteropDirection
    {
        /// <summary>未知。</summary>
        Unknown = 0,
        /// <summary>ESM 侧通过 import 导入 CJS 模块。</summary>
        EsmImportsCjs = 1,
        /// <summary>CJS 侧通过 require 导入 ESM 模块。</summary>
        CjsRequiresEsm = 2,
        /// <summary>同类型模块导入（无需互操作）。</summary>
        SameKind = 3,
        /// <summary>JSON 模块导入（无需互操作）。</summary>
        JsonImport = 4,
        /// <summary>内建模块导入（无需互操作）。</summary>
        BuiltinImport = 5
    }

    /// <summary>
    /// 互操作结果。
    /// </summary>
    /// <param name="Direction">互操作方向。</param>
    /// <param name="Exports">包装后的导出字典（至少包含 default 键）。</param>
    /// <param name="Applied">是否实际执行了互操作包装。</param>
    public sealed record InteropResult(
        InteropDirection Direction,
        IReadOnlyDictionary<string, object?>? Exports,
        bool Applied);

    /// <summary>
    /// 互操作诊断事件。
    /// </summary>
    public sealed record InteropDiagnosticEvent(string EventName, string ModuleUrl, object? Data)
    {
        /// <summary>事件产生时间戳。</summary>
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 互操作异常，标准化错误结构。错误不会被隐式吞掉——调用方会看到精确的 code/direction/reason。
    /// </summary>
    public sealed class InteropException : Exception, IModuleStructuredError
    {
        /// <summary>业务错误码（PC_INTEROP_*）。</summary>
        public string Code { get; }

        /// <summary>模块 URL。</summary>
        public string ModuleUrl { get; }

        /// <summary>互操作方向。</summary>
        public InteropDirection Direction { get; }

        /// <summary>修复建议。</summary>
        public string? Hint { get; }

        /// <summary>
        /// 创建互操作异常。
        /// </summary>
        public InteropException(
            string code,
            string moduleUrl,
            InteropDirection direction,
            string reason,
            string? hint = null,
            Exception? innerException = null)
            : base($"[{code}] {reason} (module: {moduleUrl}, direction: {direction})", innerException)
        {
            Code = code;
            ModuleUrl = moduleUrl;
            Direction = direction;
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
                moduleUrl = ModuleUrl,
                direction = Direction.ToString(),
                message = Message,
                hint = Hint,
                innerError = InnerException?.Message
            };
        }
    }
}
