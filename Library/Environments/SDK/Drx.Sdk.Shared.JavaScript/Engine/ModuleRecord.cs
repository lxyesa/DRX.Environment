using System;
using System.Collections.Generic;

namespace Drx.Sdk.Shared.JavaScript.Engine
{
    /// <summary>
    /// 模块记录——模块系统中已加载模块实例的完整状态快照。
    /// 状态机：Loading → Loaded | Failed。
    /// </summary>
    public sealed class ModuleRecord
    {
        /// <summary>
        /// 模块缓存键（规范化绝对路径，大小写不敏感场景直接比较小写）。
        /// </summary>
        public string CacheKey { get; }

        /// <summary>
        /// 模块规范化 URL（绝对文件路径）。
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// 模块类型。
        /// </summary>
        public ModuleKind Kind { get; }

        /// <summary>
        /// 模块当前状态。
        /// </summary>
        public ModuleRecordState State { get; private set; }

        /// <summary>
        /// 加载完成后的模块命名空间/导出对象（仅 <see cref="ModuleRecordState.Loaded"/> 时有值）。
        /// </summary>
        public object? Namespace { get; private set; }

        /// <summary>
        /// 模块导出字典（仅 <see cref="ModuleRecordState.Loaded"/> 时有值）。
        /// </summary>
        public IReadOnlyDictionary<string, object?>? Exports { get; private set; }

        /// <summary>
        /// 模块的直接依赖列表（已解析的 cache key）。
        /// </summary>
        public IReadOnlyList<string> Dependencies { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// 加载失败时的异常信息（仅 <see cref="ModuleRecordState.Failed"/> 时有值）。
        /// </summary>
        public ModuleLoadException? Error { get; private set; }

        /// <summary>
        /// 加载耗时（从 Loading 转到 Loaded/Failed 的总时长）。
        /// </summary>
        public TimeSpan? LoadDuration { get; private set; }

        /// <summary>
        /// 创建一个处于 <see cref="ModuleRecordState.Loading"/> 状态的新模块记录。
        /// </summary>
        public ModuleRecord(string cacheKey, string url, ModuleKind kind)
        {
            CacheKey = cacheKey ?? throw new ArgumentNullException(nameof(cacheKey));
            Url = url ?? throw new ArgumentNullException(nameof(url));
            Kind = kind;
            State = ModuleRecordState.Loading;
        }

        /// <summary>
        /// 将模块标记为加载成功。仅允许从 <see cref="ModuleRecordState.Loading"/> 转换。
        /// </summary>
        /// <exception cref="InvalidOperationException">非法状态转换。</exception>
        public void MarkLoaded(
            object? moduleNamespace,
            IReadOnlyDictionary<string, object?>? exports,
            IReadOnlyList<string> dependencies,
            TimeSpan loadDuration)
        {
            if (State != ModuleRecordState.Loading)
            {
                throw new InvalidOperationException(
                    $"[ModuleRecord] 非法状态转换：无法从 {State} 转为 Loaded。(module: {Url})");
            }

            Namespace = moduleNamespace;
            Exports = exports;
            Dependencies = dependencies ?? Array.Empty<string>();
            LoadDuration = loadDuration;
            State = ModuleRecordState.Loaded;
        }

        /// <summary>
        /// 将模块标记为加载失败。仅允许从 <see cref="ModuleRecordState.Loading"/> 转换。
        /// </summary>
        /// <exception cref="InvalidOperationException">非法状态转换。</exception>
        public void MarkFailed(ModuleLoadException error, TimeSpan loadDuration)
        {
            if (State != ModuleRecordState.Loading)
            {
                throw new InvalidOperationException(
                    $"[ModuleRecord] 非法状态转换：无法从 {State} 转为 Failed。(module: {Url})");
            }

            Error = error ?? throw new ArgumentNullException(nameof(error));
            LoadDuration = loadDuration;
            State = ModuleRecordState.Failed;
        }

        /// <summary>
        /// 输出模块记录的结构化诊断信息。
        /// </summary>
        public object ToDiagnostic()
        {
            return new
            {
                cacheKey = CacheKey,
                url = Url,
                kind = Kind.ToString(),
                state = State.ToString(),
                dependencyCount = Dependencies.Count,
                loadDurationMs = LoadDuration?.TotalMilliseconds,
                errorCode = Error?.Code
            };
        }
    }

    /// <summary>
    /// 模块记录状态机。
    /// </summary>
    public enum ModuleRecordState
    {
        /// <summary>加载中（占位，用于循环依赖检测）。</summary>
        Loading = 0,
        /// <summary>加载完成，可供使用。</summary>
        Loaded = 1,
        /// <summary>加载失败，携带错误信息。</summary>
        Failed = 2
    }

    /// <summary>
    /// 模块类型枚举。
    /// </summary>
    public enum ModuleKind
    {
        /// <summary>ECMAScript Module。</summary>
        Esm = 0,
        /// <summary>CommonJS 模块。</summary>
        Cjs = 1,
        /// <summary>JSON 数据模块。</summary>
        Json = 2,
        /// <summary>内建模块（SDK bridge）。</summary>
        Builtin = 3
    }

    /// <summary>
    /// 模块加载异常，标准化错误结构。
    /// </summary>
    public sealed class ModuleLoadException : Exception
    {
        /// <summary>业务错误码（PC_LOAD_*）。</summary>
        public string Code { get; }

        /// <summary>模块 URL。</summary>
        public string ModuleUrl { get; }

        /// <summary>加载阶段。</summary>
        public string Phase { get; }

        /// <summary>修复建议。</summary>
        public string? Hint { get; }

        /// <summary>
        /// 创建模块加载异常。
        /// </summary>
        public ModuleLoadException(
            string code,
            string moduleUrl,
            string phase,
            string reason,
            string? hint = null,
            Exception? innerException = null)
            : base($"[{code}] {reason} (module: {moduleUrl}, phase: {phase})", innerException)
        {
            Code = code;
            ModuleUrl = moduleUrl;
            Phase = phase;
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
                phase = Phase,
                message = Message,
                hint = Hint,
                innerError = InnerException?.Message
            };
        }
    }
}
