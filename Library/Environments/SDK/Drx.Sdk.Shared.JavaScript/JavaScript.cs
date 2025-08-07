using System;
using System.Threading.Tasks;
using System.Linq;
using DRX.Framework;

namespace Drx.Sdk.Shared.JavaScript
{
    public static class JavaScript
    {
        private static readonly JavaScriptEngine _engine = new JavaScriptEngine();

        // 纯 Global（非 Type）注册目录：仅保存名称与其 .NET 类型（避免持有可变实例）
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Type> _globalsCatalog
            = new System.Collections.Concurrent.ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

        // 静态构造函数，注册 logger 桥接对象
        static JavaScript()
        {
            // 依赖自动注册系统注册 logger，无需手动注册
        }

        /// <summary>
        /// 执行JS脚本（字符串）
        /// </summary>
        public static object Execute(string scriptString)
        {
            return _engine.Execute(scriptString);
        }

        /// <summary>
        /// 异步执行JS脚本（字符串）
        /// </summary>
        public static async Task<bool> ExecuteAsync(string scriptString)
        {
            await _engine.ExecuteAsync(scriptString);
            return true;
        }

        /// <summary>
        /// 泛型异步执行
        /// </summary>
        public static async Task<T> ExecuteAsync<T>(string scriptString)
        {
            return await _engine.ExecuteAsync<T>(scriptString);
        }

        /// <summary>
        /// 执行JS脚本（文件）
        /// </summary>
        public static object ExecuteFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("脚本文件路径不能为空", nameof(filePath));
            if (!System.IO.File.Exists(filePath))
                throw new System.IO.FileNotFoundException("找不到指定的脚本文件", filePath);

            return _engine.ExecuteFile(filePath);
        }

        /// <summary>
        /// 泛型执行文件
        /// </summary>
        public static T ExecuteFile<T>(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("脚本文件路径不能为空", nameof(filePath));
            if (!System.IO.File.Exists(filePath))
                throw new System.IO.FileNotFoundException("找不到指定的脚本文件", filePath);

            return _engine.ExecuteFile<T>(filePath);
        }
        /// <summary>
        /// 为 JS 全局注册一个方法（支持任何 Delegate）
        /// </summary>
        public static void RegisterGlobal(string name, Delegate method)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("全局名称不能为空", nameof(name));
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            // 直接把委托当作 value 交给引擎（由引擎侧负责调用）
            _engine.RegisterGlobal(name, method);

            // 记录到纯 Global 目录（委托类型）
            _globalsCatalog[name] = method.GetType();
        }

        /// <summary>
        /// 为 JS 全局注册一个无参方法
        /// </summary>
        public static void RegisterGlobal(string name, Action action)
            => RegisterGlobal(name, (Delegate)action);

        /// <summary>
        /// 为 JS 全局注册一个有返回值且无参的方法
        /// </summary>
        public static void RegisterGlobal<TResult>(string name, Func<TResult> func)
            => RegisterGlobal(name, (Delegate)func);

        /// <summary>
        /// 为 JS 全局注册一个属性 Getter（延迟读取，允许返回 null）
        /// </summary>
        public static void RegisterGlobal(string name, Func<object?> getter)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("全局名称不能为空", nameof(name));
            if (getter == null)
                throw new ArgumentNullException(nameof(getter));

            _engine.RegisterGlobal(name, getter);

            // 记录到纯 Global 目录（延迟 getter 的委托类型）
            _globalsCatalog[name] = getter.GetType();
        }

        /// <summary>
        /// 为 JS 全局注册一个字段 Getter（延迟读取，允许返回 null）
        /// </summary>
        public static void RegisterField(string name, Func<object?> getter)
            => RegisterGlobal(name, getter);

        /// <summary>
        /// 为 JS 全局注册一个字段 Getter（泛型，避免与 RegisterGlobal<TResult>(..., Func<...>) 二义性）
        /// </summary>
        public static void RegisterField<T>(string name, Func<T> getter)
            => RegisterGlobal<T>(name, getter);

        /// <summary>
        /// 安全异步执行JS脚本（字符串），捕获异常
        /// </summary>
        public static async Task<(bool success, object result)> TryExecuteAsync(string scriptString)
        {
            try
            {
                var result = await _engine.ExecuteAsync<object>(scriptString);
                return (true, result);
            }
            catch (Exception ex)
            {
                return (false, $"[JavaScript.TryExecuteAsync] {ex.Message}");
            }
        }

        /// <summary>
        /// 安全执行JS脚本（文件），捕获异常
        /// </summary>
        public static (bool success, object result) TryExecuteFile(string filePath)
        {
            try
            {
                var result = ExecuteFile(filePath);
                return (true, result);
            }
            catch (Exception ex)
            {
                Logger.Error($"无法执行脚本文件 {filePath}：{ex}");
                return (false, $"[JavaScript.TryExecuteFile] {ex.Message}");
            }
        }

        /// <summary>
        /// 泛型安全执行JS脚本（字符串），捕获异常
        /// </summary>
        public static (bool success, T result) TryExecute<T>(string scriptString)
            where T : notnull
        {
            try
            {
                object result = Execute(scriptString);
                if (result is T t)
                    return (true, t);
                return (false, default!);
            }
            catch (Exception)
            {
                return (false, default!);
            }
        }

        /// <summary>
        /// 泛型安全异步执行JS脚本（字符串），捕获异常
        /// </summary>
        public static async Task<(bool success, T result)> TryExecuteAsync<T>(string scriptString)
            where T : notnull
        {
            try
            {
                object result = await _engine.ExecuteAsync<object>(scriptString);
                if (result is T t)
                    return (true, t);
                return (false, default!);
            }
            catch (Exception)
            {
                return (false, default!);
            }
        }

        /// <summary>
        /// 泛型安全执行JS脚本（文件），捕获异常
        /// </summary>
        public static (bool success, T result) TryExecuteFile<T>(string filePath)
            where T : notnull
        {
            try
            {
                object result = ExecuteFile(filePath);
                if (result is T t)
                    return (true, t);
                return (false, default!);
            }
            catch (Exception)
            {
                return (false, default!);
            }
        }

        /// <summary>
        /// 手动注册：将 C# 函数、类、字段、属性暴露为 JS 全局。
        /// 支持传入委托/实例/类型（含静态类）。当传入 Type 时以 name 作为导出名。
        /// </summary>
        /// <param name="name">JS 全局名称（或类型导出别名）</param>
        /// <param name="value">委托/实例/Type</param>
        public static void RegisterGlobal(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("全局名称不能为空", nameof(name));
            // 运行时防御：避免将 null 传入下层非可空 API
            if (value == null)
                throw new ArgumentNullException(nameof(value), "注册的值不能为 null");

            // 传入类型：按类/静态类导出，并绑定其带有 ScriptExportAttribute 的成员
            if (value is Type type)
            {
                var exportType = ScriptReflectionUtil.IsStaticClass(type)
                    ? ScriptExportType.StaticClass
                    : ScriptExportType.Class;

                var meta = ScriptTypeMetadata.FromType(type, name, exportType);
                ScriptRegistry.Instance.RegisterType(meta);
                // 直接通过底层包装器注册类型，并按特性绑定其导出成员
                // 注意：ScriptBinder.BindType 需要 ClearScriptEngineWrapper，而此处仅有 JavaScriptEngine
                // 所以改为由引擎包装器完成类型注册，成员绑定仍由 ScriptBinder 完成，但需拿到包装器实例。
                // 这里通过 JavaScriptEngine 暴露的 RegisterGlobal 与内部 RegisterType 达到相同效果：
                //   1) 先注册类型本身到 ClearScript 运行时（可在 JS 侧用于 new 或静态调用）
                //   2) 再将成员（方法/属性/字段）以全局形式暴露（与自动扫描一致）
                // 由于当前上下文拿不到内部包装器实例，直接调用包装器的功能不合适，因此复用现有绑定流程的公共入口：
                //   - 注册类型本身：使用 JavaScriptEngine 的私有包装器不可达，这里通过 Binder 的流程改为逐成员导出到全局。
                //   - 同时保持注册中心与元数据缓存一致。
                // 处理方式：手动导出成员到全局（等价于 ScriptBinder.BindType 内部成员注册的那部分）
                foreach (var method in meta.ExportedMethods)
                {
                    _engine.RegisterGlobal(method.Name, (Func<object[], object>)(args =>
                    {
                        var parameters = method.GetParameters();
                        var realArgs = new object?[parameters.Length];
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            var val = TypeConverter.FromJsValue(args[i], parameters[i].ParameterType);
                            if (val == null && parameters[i].ParameterType.IsValueType && Nullable.GetUnderlyingType(parameters[i].ParameterType) == null)
                                realArgs[i] = (object)Activator.CreateInstance(parameters[i].ParameterType)!;
                            else
                                realArgs[i] = (object?)val;
                        }
                        var result = method.Invoke(null, realArgs);
                        return TypeConverter.ToJsValue(result);
                    }));
                }
                foreach (var prop in meta.ExportedProperties)
                {
                    _engine.RegisterGlobal(prop.Name, new Func<object?>(() => prop.GetValue(null)));
                }
                foreach (var field in meta.ExportedFields)
                {
                    // 使用延迟读取包装，避免将 null 直接传入非可空 RegisterGlobal
                    _engine.RegisterGlobal(field.Name, new Func<object?>(() => field.GetValue(null)));
                }
                return;
            }

            // 其他情况（委托/实例/常量等）直接暴露为全局对象
            _engine.RegisterGlobal(name, value);

            // 记录到纯 Global 目录（实例/常量取其运行时类型；若是委托也能覆盖上面的登记）
            _globalsCatalog[name] = value.GetType();
        }

        /// <summary>
        /// 获取所有已注册的 JavaScript 类名。
        /// </summary>
        /// <returns>包含所有已注册类名称的字符串列表。</returns>
        public static System.Collections.Generic.List<string> GetRegisteredClasses()
        {
            try
            {
                var classList = ScriptRegistry.Instance.GetExportedClasses()
                    .Select(meta => meta.ExportName)
                    .ToList();
                System.Diagnostics.Debug.WriteLine("[GetRegisteredClasses] 已注册类名: " + string.Join(", ", classList));
                return classList;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[GetRegisteredClasses][异常] " + ex);
                throw;
            }
        }

        /// <summary>
        /// 获取所有通过 RegisterGlobal(...) 注册的“纯 Global”（非 Type 导出）。
        /// 返回字典：名称 -> .NET 类型（用于 Help 渲染委托签名或对象类型）。
        /// </summary>
        public static System.Collections.Generic.Dictionary<string, Type> GetRegisteredGlobals()
        {
            // 拷贝一份只读视图，避免外部修改内部并发字典
            return new System.Collections.Generic.Dictionary<string, Type>(_globalsCatalog, _globalsCatalog.Comparer);
        }

        /// <summary>
        /// 调用 JS 文件中的函数：CallFunction("function", filePath, params object[] args)
        /// </summary>
        public static object CallFunction(string functionName, string filePath, params object[] args)
            => _engine.CallFunction(functionName, filePath, args);

        /// <summary>
        /// 调用 JS 文件中的函数（泛型返回）
        /// </summary>
        public static T CallFunction<T>(string functionName, string filePath, params object[] args)
            => _engine.CallFunction<T>(functionName, filePath, args);
    }
}