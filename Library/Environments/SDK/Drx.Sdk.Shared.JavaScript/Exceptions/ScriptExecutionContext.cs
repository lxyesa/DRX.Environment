using System;
using System.Runtime.Serialization;

namespace Drx.Sdk.Shared.JavaScript.Exceptions
{
    /// <summary>
    /// 记录脚本执行上下文信息，用于错误诊断和性能追踪。
    /// 存储执行脚本片段、文件路径、时间戳、耗时及调用者信息。
    /// </summary>
    [Serializable]
    public class ScriptExecutionContext
    {
        /// <summary>正在执行的脚本内容（可能被截断）。</summary>
        public string Script { get; set; }

        /// <summary>脚本文件路径；内联脚本时为 null。</summary>
        public string FilePath { get; set; }

        /// <summary>脚本执行开始时间（UTC）。</summary>
        public DateTime StartTime { get; set; }

        /// <summary>脚本执行总耗时；执行中时为 null。</summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>已重试次数。</summary>
        public int RetryCount { get; set; }

        /// <summary>触发执行的调用者标识（方法名或模块名）。</summary>
        public string Caller { get; set; }

        /// <inheritdoc/>
        public override string ToString() =>
            $"Script: {Script?.Substring(0, Math.Min(60, Script.Length))}..., File: {FilePath}, Start: {StartTime}, Duration: {Duration}, Retry: {RetryCount}, Caller: {Caller}";
    }
}
