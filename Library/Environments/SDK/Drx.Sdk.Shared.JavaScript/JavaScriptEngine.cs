using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DRX.Framework;

namespace Drx.Sdk.Shared.JavaScript
{
    /// <summary>
    /// JavaScript执行引擎，ClearScript托管封装，支持类型自动注册、全局变量、异常处理
    /// </summary>
    public class JavaScriptEngine : IDisposable
    {
        private static bool _initialized = false;
        private static readonly object _initLock = new();

        private readonly ClearScriptEngineWrapper _engine;

        static JavaScriptEngine()
        {
            EnsureInitialized();
        }

        public JavaScriptEngine()
        {
            EnsureInitialized();
            try
            {
                _engine = new ClearScriptEngineWrapper();
            }
            catch (Exception ex)
            {
                Logger.Error($"JavaScript引擎初始化失败: {ex.Message}");
                _engine = new ClearScriptEngineWrapper.MockClearScriptEngine();
            }
            RegisterAllExportedTypes();
        }

        /// <summary>
        /// 静态初始化，自动扫描并注册所有导出类型
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (_initLock)
            {
                if (_initialized) return;
                ScriptTypeScanner.EnsureScanned();
                _initialized = true;
            }
        }

        /// <summary>
        /// 注册所有导出类型到JS运行时
        /// </summary>
        private void RegisterAllExportedTypes()
        {
            foreach (var meta in ScriptRegistry.Instance.GetAllExportedTypes())
            {
                Logger.Debug($"注册导出类型: {meta.Type.FullName}");
                ScriptBinder.BindType(_engine, meta);
            }
        }

        /// <summary>
        /// 注册全局变量
        /// </summary>
        public void RegisterGlobal(string name, object value)
        {
            _engine.RegisterGlobal(name, value);
        }

        /// <summary>
        /// 直接调用指定 JS 文件中的函数
        /// </summary>
        /// <param name="functionName">函数名（全局或模块顶层可见）</param>
        /// <param name="filePath">脚本文件路径</param>
        /// <param name="args">传入的参数（可选）</param>
        /// <returns>函数返回值</returns>
        public object CallFunction(string functionName, string filePath, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(functionName))
                throw new ArgumentException("函数名不能为空", nameof(functionName));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("脚本文件路径不能为空", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("找不到指定的脚本文件", filePath);

            // 先执行文件，使函数进入全局
            ExecuteFile(filePath);

            // 构造调用表达式并单次执行
            var callExpr = BuildDirectInvokeScript(functionName, args);
            return _engine.Execute(callExpr);
        }

        /// <summary>
        /// 直接调用指定 JS 文件中的函数（泛型版本）
        /// </summary>
        public T CallFunction<T>(string functionName, string filePath, params object[] args)
        {
            var result = CallFunction(functionName, filePath, args);
            return (T)TypeConverter.FromJsValue(result, typeof(T));
        }

        private static string BuildDirectInvokeScript(string functionName, object[] args)
        {
            var funcRef = $"globalThis[\"{EscapeJsString(functionName)}\"]";
            var argLiterals = (args is { Length: > 0 })
                ? string.Join(", ", args.Select(ToJsLiteral))
                : string.Empty;
            return $"{funcRef}({argLiterals})";
        }

        private static string EscapeJsString(string s)
        {
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "");
        }

        private static string ToJsLiteral(object arg)
        {
            var jsVal = TypeConverter.ToJsValue(arg);
            if (jsVal is null) return "null";
            if (jsVal is bool b) return b ? "true" : "false";
            if (jsVal is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal)
                return Convert.ToString(jsVal, System.Globalization.CultureInfo.InvariantCulture);
            if (jsVal is string str) return $"\"{EscapeJsString(str)}\"";

            var json = System.Text.Json.JsonSerializer.Serialize(jsVal);
            return $"JSON.parse(\"{EscapeJsString(json)}\")";
        }

        /// <summary>
        /// 执行JS脚本（字符串）
        /// </summary>
        public object Execute(string script)
        {
            return _engine.Execute(script);
        }

        /// <summary>
        /// 执行JS脚本（文件）
        /// </summary>
        public object ExecuteFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("脚本文件路径不能为空", nameof(filePath));
            if (!File.Exists(filePath))
            {
                Logger.Error($"执行JavaScript脚本文件失败，文件不存在: {filePath}");
                throw new FileNotFoundException("找不到指定的脚本文件", filePath);
            }

            var script = File.ReadAllText(filePath);
            Logger.Info($"执行JavaScript脚本文件: {filePath}");
            return Execute(script);
        }

        /// <summary>
        /// 异步执行JS脚本
        /// </summary>
        public async Task<object> ExecuteAsync(string script)
        {
            return await _engine.ExecuteAsync(script);
        }

        /// <summary>
        /// 泛型异步执行
        /// </summary>
        public async Task<T> ExecuteAsync<T>(string script)
        {
            var result = await ExecuteAsync(script);
            return (T)TypeConverter.FromJsValue(result, typeof(T));
        }

        /// <summary>
        /// 泛型执行文件
        /// </summary>
        public T ExecuteFile<T>(string filePath)
        {
            var result = ExecuteFile(filePath);
            return (T)TypeConverter.FromJsValue(result, typeof(T));
        }

        public void Dispose()
        {
            _engine.Dispose();
        }

    }
}