using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Drx.Sdk.Shared.JavaScript
{
    /// <summary>
    /// JavaScript运行时异常，封装详细错误信息
    /// </summary>
    [Serializable]
    public class JavaScriptException : Exception
    {
        public string? ScriptStack { get; }
        public string? ErrorType { get; }
        public string? ErrorLocation { get; }
        public ScriptExecutionContext? Context { get; }

        public JavaScriptException(string message, string? errorType, string? errorLocation, string? scriptStack, ScriptExecutionContext? context, Exception? inner = null)
            : base(message, inner)
        {
            ErrorType = errorType;
            ErrorLocation = errorLocation;
            ScriptStack = scriptStack;
            Context = context;
        }

        protected JavaScriptException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ErrorType = info.GetString(nameof(ErrorType));
            ErrorLocation = info.GetString(nameof(ErrorLocation));
            ScriptStack = info.GetString(nameof(ScriptStack));
            Context = info.GetValue(nameof(Context), typeof(ScriptExecutionContext)) as ScriptExecutionContext;
        }

        [Obsolete("Formatter-based serialization is obsolete and not recommended for use.")]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(ErrorType), ErrorType);
            info.AddValue(nameof(ErrorLocation), ErrorLocation);
            info.AddValue(nameof(ScriptStack), ScriptStack);
            info.AddValue(nameof(Context), Context);
        }

        public override string ToString()
        {
            return $"[JavaScriptException] {Message}\nType: {ErrorType}\nLocation: {ErrorLocation}\nStack: {ScriptStack}\nContext: {Context}\n{base.ToString()}";
        }
    }

    /// <summary>
    /// 记录脚本执行上下文信息
    /// </summary>
    [Serializable]
    public class ScriptExecutionContext
    {
        public string Script { get; set; }
        public string FilePath { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public int RetryCount { get; set; }
        public string Caller { get; set; }

        public override string ToString()
        {
            return $"Script: {Script?.Substring(0, Math.Min(60, Script.Length))}..., File: {FilePath}, Start: {StartTime}, Duration: {Duration}, Retry: {RetryCount}, Caller: {Caller}";
        }
    }
}