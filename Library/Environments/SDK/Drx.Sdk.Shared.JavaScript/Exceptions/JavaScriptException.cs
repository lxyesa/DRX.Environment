using System;
using System.Runtime.Serialization;

namespace Drx.Sdk.Shared.JavaScript.Exceptions
{
    /// <summary>
    /// JavaScript 运行时异常，封装脚本执行错误的详细信息。
    /// 包含错误类型、源位置、脚本堆栈跟踪及执行上下文。
    /// </summary>
    [Serializable]
    public class JavaScriptException : Exception
    {
        /// <summary>JavaScript 错误堆栈跟踪字符串。</summary>
        public string? ScriptStack { get; }

        /// <summary>JavaScript 错误类型（如 "TypeError"、"ReferenceError"）。</summary>
        public string? ErrorType { get; }

        /// <summary>错误发生的脚本位置（文件名:行号）。</summary>
        public string? ErrorLocation { get; }

        /// <summary>触发异常时的脚本执行上下文快照。</summary>
        public ScriptExecutionContext? Context { get; }

        /// <summary>
        /// 创建包含完整错误信息的 JavaScriptException 实例。
        /// </summary>
        /// <param name="message">错误消息。</param>
        /// <param name="errorType">JavaScript 错误类型名称。</param>
        /// <param name="errorLocation">脚本错误位置。</param>
        /// <param name="scriptStack">JavaScript 调用堆栈。</param>
        /// <param name="context">执行上下文；可为 null。</param>
        /// <param name="inner">内部异常；可为 null。</param>
        public JavaScriptException(
            string message,
            string? errorType,
            string? errorLocation,
            string? scriptStack,
            ScriptExecutionContext? context,
            Exception? inner = null)
            : base(message, inner)
        {
            ErrorType = errorType;
            ErrorLocation = errorLocation;
            ScriptStack = scriptStack;
            Context = context;
        }

        /// <summary>序列化构造器（用于跨 AppDomain 传递异常）。</summary>
#pragma warning disable SYSLIB0051
        protected JavaScriptException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ErrorType = info.GetString(nameof(ErrorType));
            ErrorLocation = info.GetString(nameof(ErrorLocation));
            ScriptStack = info.GetString(nameof(ScriptStack));
            Context = info.GetValue(nameof(Context), typeof(ScriptExecutionContext)) as ScriptExecutionContext;
        }
#pragma warning restore SYSLIB0051

        /// <summary>将异常数据序列化到 <see cref="SerializationInfo"/>。</summary>
        [Obsolete("Formatter-based serialization is obsolete and not recommended for use.")]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(ErrorType), ErrorType);
            info.AddValue(nameof(ErrorLocation), ErrorLocation);
            info.AddValue(nameof(ScriptStack), ScriptStack);
            info.AddValue(nameof(Context), Context);
        }

        /// <inheritdoc/>
        public override string ToString() =>
            $"[JavaScriptException] {Message}\nType: {ErrorType}\nLocation: {ErrorLocation}\nStack: {ScriptStack}\nContext: {Context}\n{base.ToString()}";
    }
}
