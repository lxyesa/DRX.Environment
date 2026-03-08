using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Drx.Sdk.Shared.JavaScript.Engine
{
    /// <summary>
    /// JavaScript 引擎执行能力（脚本执行、文件执行、函数调用）。
    /// 所有求值入口统一通过 <c>IScriptEngineRuntime.Evaluate</c> / <c>EvaluateAsync</c>。
    /// </summary>
    public sealed partial class JavaScriptEngine
    {
        /// <inheritdoc/>
        public object? Execute(string script)
        {
            ThrowIfDisposed();
            if (script == null) throw new ArgumentNullException(nameof(script));

            return _runtime.Evaluate(script);
        }

        /// <inheritdoc/>
        public T Execute<T>(string script)
        {
            var result = Execute(script);
            return (T)_converter.FromJsValue(result, typeof(T))!;
        }

        /// <inheritdoc/>
        public async ValueTask<object?> ExecuteAsync(string script)
        {
            ThrowIfDisposed();
            if (script == null) throw new ArgumentNullException(nameof(script));

            return await _runtime.EvaluateAsync(script).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask<T> ExecuteAsync<T>(string script)
        {
            var result = await ExecuteAsync(script).ConfigureAwait(false);
            return (T)_converter.FromJsValue(result, typeof(T))!;
        }

        /// <inheritdoc/>
        public object? ExecuteFile(string filePath)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("脚本文件路径不能为空。", nameof(filePath));

            var script = ReadScriptFromFile(filePath);
            return Execute(script);
        }

        /// <inheritdoc/>
        public T ExecuteFile<T>(string filePath)
        {
            var result = ExecuteFile(filePath);
            return (T)_converter.FromJsValue(result, typeof(T))!;
        }

        /// <inheritdoc/>
        public object? CallFunction(string functionName, string filePath, params object[] args)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(functionName))
                throw new ArgumentException("函数名不能为空。", nameof(functionName));

            ExecuteFile(filePath);
            var callScript = BuildDirectInvokeScript(functionName, args);
            return Execute(callScript);
        }

        /// <inheritdoc/>
        public T CallFunction<T>(string functionName, string filePath, params object[] args)
        {
            var result = CallFunction(functionName, filePath, args);
            return (T)_converter.FromJsValue(result, typeof(T))!;
        }

        private string ReadScriptFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("找不到指定的脚本文件。", filePath);

            if (!_options.EnableScriptCaching)
                return File.ReadAllText(filePath);

            return _scriptFileCache.GetOrAdd(filePath, static path => File.ReadAllText(path));
        }

        private static string BuildDirectInvokeScript(string functionName, object[] args)
        {
            var funcRef = $"globalThis[\"{EscapeJsString(functionName)}\"]";
            var argLiterals = (args is { Length: > 0 })
                ? string.Join(", ", args.Select(ToJsLiteral))
                : string.Empty;

            return $"{funcRef}({argLiterals})";
        }

        private static string EscapeJsString(string value)
        {
            return value.Replace("\\", "\\\\", StringComparison.Ordinal)
                        .Replace("\"", "\\\"", StringComparison.Ordinal)
                        .Replace("\n", "\\n", StringComparison.Ordinal)
                        .Replace("\r", string.Empty, StringComparison.Ordinal);
        }

        private static string ToJsLiteral(object arg)
        {
            var jsValue = arg;
            if (jsValue is null) return "null";
            if (jsValue is bool b) return b ? "true" : "false";
            if (jsValue is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal)
                return Convert.ToString(jsValue, CultureInfo.InvariantCulture)!;
            if (jsValue is string str) return $"\"{EscapeJsString(str)}\"";

            var json = JsonSerializer.Serialize(jsValue);
            return $"JSON.parse(\"{EscapeJsString(json)}\")";
        }
    }
}
