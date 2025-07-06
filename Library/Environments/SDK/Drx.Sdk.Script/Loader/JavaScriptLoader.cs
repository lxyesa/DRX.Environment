using System.Linq.Expressions;
using System.Reflection;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Functions;
using Drx.Sdk.Script.Functions.Arrays;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace Drx.Sdk.Script.Loader
{
    public class JavaScriptLoader : IDisposable
    {
        #region Fields

        private readonly V8ScriptEngine _engine;
        private readonly ScriptDelegate _delegate;

        #endregion

        #region Constructor

        public JavaScriptLoader()
        {
            _engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDynamicModuleImports);
            _delegate = new ScriptDelegate(_engine);
            _engine.EnableAutoHostVariables = true;

            // 添加模块加载器
            _engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableFileLoading;

            Initialize();
        }

        private void Initialize()
        {
            _engine.AddHostType("int", typeof(int));
            _engine.AddHostType("uint", typeof(uint));
            _engine.AddHostType("long", typeof(long));
            _engine.AddHostType("ulong", typeof(ulong));
            _engine.AddHostType("float", typeof(float));
            _engine.AddHostType("double", typeof(double));
            _engine.AddHostType("string", typeof(string));
            _engine.AddHostType("bool", typeof(bool));
            _engine.AddHostType("object", typeof(object));
            _engine.AddHostType("intArray", typeof(int[]));
            _engine.AddHostType("uintArray", typeof(uint[]));
            _engine.AddHostType("longArray", typeof(long[]));
            _engine.AddHostType("ulongArray", typeof(ulong[]));
            _engine.AddHostType("floatArray", typeof(float[]));
            _engine.AddHostType("doubleArray", typeof(double[]));
            _engine.AddHostType("stringArray", typeof(string[]));
            _engine.AddHostType("boolArray", typeof(bool[]));
            _engine.AddHostType("objectArray", typeof(object[]));
            _engine.AddHostType("byteArray", typeof(byte[]));
            _engine.AddHostType("shortArray", typeof(short[]));

            _engine.AddHostType("values", typeof(Values));
            _engine.AddHostType("console", typeof(Drx.Sdk.Script.Functions.Console));
            _engine.AddHostType("JSArray", typeof(JSArray));
            _engine.AddHostType("JSArrayInt", typeof(JSArray<int>));
            _engine.AddHostType("JSArrayUInt", typeof(JSArray<uint>));
            _engine.AddHostType("JSArrayLong", typeof(JSArray<long>));
            _engine.AddHostType("JSArrayULong", typeof(JSArray<ulong>));
            _engine.AddHostType("JSArrayFloat", typeof(JSArray<float>));
            _engine.AddHostType("JSArrayDouble", typeof(JSArray<double>));
            _engine.AddHostType("JSArrayString", typeof(JSArray<string>));
            _engine.AddHostType("JSArrayBool", typeof(JSArray<bool>));
            _engine.AddHostType("JSArrayObject", typeof(JSArray<object>));
            _engine.AddHostType("JSArrayByte", typeof(JSArray<byte>));
            _engine.AddHostType("JSArrayShort", typeof(JSArray<short>));
            _engine.AddHostType("JSArrayUShort", typeof(JSArray<ushort>));
            _engine.AddHostType("JSArrayChar", typeof(JSArray<char>));
            _engine.AddHostType("JSArrayDecimal", typeof(JSArray<decimal>));
            _engine.AddHostType("JSArrayIntPtr", typeof(JSArray<IntPtr>));
            _engine.AddHostType("JSArrayUIntPtr", typeof(JSArray<UIntPtr>));
            _engine.AddHostType("JSArrayJSArray", typeof(JSArray<JSArray>));
            _engine.AddHostObject("delegate", _delegate);

            // 添加全局函数
            ImportMethod<Functions.Console>("log");
            ImportMethod<Functions.Console>("read");
            ImportMethod<Values>("IntptrToHex", "toHex");
        }
        #endregion

        #region Properties

        public V8ScriptEngine GlobalEngine => _engine;

        public ScriptDelegate ScriptDelegate => _delegate;

        #endregion

        #region Script Execution Methods

        public void ExecuteScript(string script)
        {
            _engine.Execute(script);
        }

        public void ExecuteScriptFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"脚本文件 {filePath} 不存在。");

            var script = File.ReadAllText(filePath);

            ExecuteScript(script);
        }

        public void ExecuteProject(string projectPath)
        {
            GlobalEngine.DocumentSettings.SearchPath = Path.Combine(projectPath, "modules");
            GlobalEngine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableFileLoading;


            string mainJsPath = Path.Combine(projectPath, "main.js");

            if (!File.Exists(mainJsPath))
                throw new FileNotFoundException($"Main 脚本文件在 {projectPath} 中不存在！");

            var script = File.ReadAllText(mainJsPath);
            var wrappedScript = $@"
                {script}

                // 检查是否定义了 main 函数
                if (typeof main === 'function') {{
                    // 执行 main 函数，处理同步和异步情况
                    (async () => {{
                        try {{
                            await main();
                        }} catch (error) {{
                            console.log('执行 main 函数时发生错误: ' + error);
                        }}
                    }})();
                }}
            ";

            ExecuteJavaScript(wrappedScript);
        }

        public async Task<object?> ExecuteFunction(string functionName, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(functionName))
            {
                throw new ArgumentException("请输入有效的方法名称", nameof(functionName));
            }

            
            if (_engine.Script.hasOwnProperty(functionName) && _engine.Evaluate($"typeof {functionName} === 'function'"))
            {
                try
                {
                    // 调用函数并获取返回值
                    dynamic result = _engine.Invoke(functionName, args);

                    // 处理异步函数返回的 Promise
                    if (result is Task task)
                    {
                        await task;
                        var resultProperty = task.GetType().GetProperty("Result");
                        return resultProperty?.GetValue(task);
                    }

                    // 处理 ScriptObject（可能是 Promise）
                    if (result is ScriptObject scriptObj)
                    {
                        // 检查是否是 Promise
                        bool isPromise = (bool)_engine.Evaluate($"({result}) instanceof Promise");
                        if (isPromise)
                        {
                            // 等待 Promise 完成并获取结果
                            var awaited = await _engine.Script.Promise.resolve(result);
                            return awaited;
                        }
                        return scriptObj;
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"执行函数 '{functionName}' 时发生错误: {ex.Message}");
                    throw;
                }
            }
            else
            {
                throw new MissingMethodException($"函数 '{functionName}' 未定义或不是一个函数。");
            }
        }

        #endregion

        #region Helper Methods

        private string ToCamelCase(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
                
            // 如果只有一个字符，直接转小写返回
            if (str.Length == 1)
                return str.ToLower();
                
            // 将第一个字符转换为小写，其余保持不变
            return char.ToLowerInvariant(str[0]) + str.Substring(1);
        }

        private void ExecuteJavaScript(string script)
        {
            _engine.Execute(script);
        }

        private string CreateWrapperClass(string jsName)
        {
            return $@"
                class {jsName} {{
                    constructor(...args) {{
                        this.base = new _{jsName}(...args);
                        
                        return new Proxy(this, {{
                            get: (target, prop) => {{
                                // 优先返回目标对象自己的方法
                                if (target[prop] && prop !== 'constructor') {{
                                    return target[prop];
                                }}
                                // 如果目标对象没有该方法，则返回代理到base的方法
                                if (target.base && typeof target.base[prop] === 'function') {{
                                    return (...args) => target.base[prop](...args);
                                }}
                                // 返回base的属性
                                if (target.base && prop in target.base) {{
                                    return target.base[prop];
                                }}
                                return target[prop];
                            }},
                            set: (target, prop, value) => {{
                                // 如果base对象有这个属性，就设置到base对象上
                                if (target.base && prop in target.base) {{
                                    target.base[prop] = value;
                                    return true;
                                }}
                                // 否则设置到目标对象上
                                target[prop] = value;
                                return true;
                            }}
                        }});
                    }}
                }}

                Object.defineProperty(globalThis, '{jsName}', {{
                    value: {jsName},
                    writable: true,
                    configurable: true,
                    enumerable: true
                }});
            ";
        }

        private string CreateStructWrapperClass(string jsName)
        {
            return $@"
                class {jsName} {{
                    constructor(...args) {{
                        this.base = new _{jsName}(...args);
                        return new Proxy(this, {{
                            get: (target, prop) => {{
                                if (target[prop] && prop !== 'constructor') {{
                                    return target[prop];
                                }}
                                if (target.base && typeof target.base[prop] === 'function') {{
                                    return (...args) => target.base[prop](...args);
                                }}
                                if (target.base && prop in target.base) {{
                                    return target.base[prop];
                                }}
                                return target[prop];
                            }},
                            set: (target, prop, value) => {{
                                if (target.base && prop in target.base) {{
                                    target.base[prop] = value;
                                    return true;
                                }}
                                target[prop] = value;
                                return true;
                            }}
                        }});
                    }}
                }}

                Object.defineProperty(globalThis, '{jsName}', {{
                    value: {jsName},
                    writable: true,
                    configurable: true,
                    enumerable: true
                }});
            ";
        }

        private string CreateArrayWrapperClass(string jsName)
        {
            return $@"
                class {jsName} {{
                    constructor(length) {{
                        if (typeof length === 'number') {{
                            // 创建指定长度的数组
                            this.base = new Array(length);
                            for(let i = 0; i < length; i++) {{
                                this.base[i] = new _{jsName}();
                            }}
                        }} else if (Array.isArray(length)) {{
                            // 从现有数组创建
                            this.base = length.map(item => new _{jsName}(item));
                        }} else {{
                            // 单个结构体
                            this.base = new _{jsName}(length);
                        }}

                        return new Proxy(this, {{
                            get: (target, prop) => {{
                                if (prop === 'length') {{
                                    return target.base.length;
                                }}
                                if (typeof prop === 'string' && !isNaN(prop)) {{
                                    // 数字索引访问
                                    return target.base[prop];
                                }}
                                if (target[prop] && prop !== 'constructor') {{
                                    return target[prop];
                                }}
                                if (target.base && typeof target.base[prop] === 'function') {{
                                    return (...args) => target.base[prop](...args);
                                }}
                                if (target.base && prop in target.base) {{
                                    return target.base[prop];
                                }}
                                return target[prop];
                            }},
                            set: (target, prop, value) => {{
                                if (typeof prop === 'string' && !isNaN(prop)) {{
                                    // 数字索引赋值
                                    target.base[prop] = new _{jsName}(value);
                                    return true;
                                }}
                                if (target.base && prop in target.base) {{
                                    target.base[prop] = value;
                                    return true;
                                }}
                                target[prop] = value;
                                return true;
                            }}
                        }});
                    }}

                    // 添加数组方法
                    forEach(callback) {{
                        return this.base.forEach(callback);
                    }}

                    map(callback) {{
                        return this.base.map(callback);
                    }}

                    filter(callback) {{
                        return this.base.filter(callback);
                    }}

                    slice(start, end) {{
                        return new {jsName}(this.base.slice(start, end));
                    }}

                    indexOf(searchElement, fromIndex) {{
                        return this.base.indexOf(searchElement, fromIndex);
                    }}

                    push(...items) {{
                        items = items.map(item => new _{jsName}(item));
                        return this.base.push(...items);
                    }}
                }}

                Object.defineProperty(globalThis, '{jsName}', {{
                    value: {jsName},
                    writable: true,
                    configurable: true,
                    enumerable: true
                }});
            ";
        }

        #endregion

        #region Import Methods
        
        /// <summary>
        /// 将变量导入为JavaScript全局变量
        /// </summary>
        /// <typeparam name="T">变量的类型</typeparam>
        /// <param name="variable">要导入的变量实例</param>
        /// <param name="jsName">JavaScript中使用的名称(可选)，默认使用类型名转为驼峰命名</param>
        public void ImportGlobalVariable<T>(T variable, string? jsName = null)
        {
            if (variable == null)
            {
                throw new ArgumentNullException(nameof(variable), "不能导入空变量");
            }
            
            Type type = typeof(T);
            
            // 如果没有指定JS名称，使用类型名转为首字母小写的驼峰命名
            if (string.IsNullOrEmpty(jsName))
            {
                // 对于泛型类型，仅使用基本类型名称
                string typeName = type.Name;
                if (typeName.Contains('`')) // 泛型类型名称包含"`"
                {
                    typeName = typeName.Substring(0, typeName.IndexOf('`'));
                }
                jsName = ToCamelCase(typeName);
            }
            
            // 添加到JavaScript引擎
            _engine.AddHostObject(jsName, variable);
            
            System.Console.WriteLine($"已导入变量(类型:{type.Name})为全局变量 {jsName}");
        }

        /// <summary>
        /// 将变量导入为JavaScript全局变量，指定自定义名称
        /// </summary>
        /// <param name="variable">要导入的变量实例</param>
        /// <param name="jsName">JavaScript中使用的名称</param>
        public void ImportGlobalVariable(object variable, string jsName)
        {
            if (variable == null)
            {
                throw new ArgumentNullException(nameof(variable), "不能导入空变量");
            }
            
            if (string.IsNullOrEmpty(jsName))
            {
                throw new ArgumentException("JavaScript变量名不能为空", nameof(jsName));
            }
            
            // 添加到JavaScript引擎
            _engine.AddHostObject(jsName, variable);
            
            System.Console.WriteLine($"已导入变量(类型:{variable.GetType().Name})为全局变量 {jsName}");
        }

        /// <summary>
        /// 将类型的静态字段导入为JavaScript全局变量
        /// </summary>
        /// <typeparam name="T">包含静态字段的类型</typeparam>
        /// <param name="fieldName">要导入的字段名</param>
        /// <param name="jsName">JavaScript中使用的名称(可选)，默认使用原字段名转为驼峰命名</param>
        /// <exception cref="ArgumentException">如果字段不存在或不是静态字段</exception>
        public void ImportGlobalField<T>(string fieldName, string? jsName = null)
        {
            var type = typeof(T);
            
            // 获取指定名称的字段
            var fieldInfo = type.GetField(fieldName, 
                BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
            
            if (fieldInfo == null)
            {
                throw new ArgumentException($"类型 {type.Name} 中找不到名为 {fieldName} 的公共静态字段");
            }
            
            if (!fieldInfo.IsStatic)
            {
                throw new ArgumentException($"字段 {fieldName} 不是静态字段");
            }
            
            // 如果没有指定JS名称，使用原字段名转为首字母小写的驼峰命名
            if (string.IsNullOrEmpty(jsName))
            {
                jsName = ToCamelCase(fieldName);
            }
            
            // 获取字段值并添加到JavaScript引擎
            object? value = fieldInfo.GetValue(null);
            _engine.AddHostObject(jsName, value);
            
            System.Console.WriteLine($"已导入静态字段 {type.Name}.{fieldName} 为全局变量 {jsName}");
        }


        /// <summary>
        /// 将类型的静态方法导入为JavaScript全局函数
        /// </summary>
        /// <typeparam name="T">包含静态方法的类型</typeparam>
        /// <param name="methodName">要导入的方法名</param>
        /// <param name="jsName">JavaScript中使用的名称(可选)，默认使用原方法名</param>
        /// <exception cref="ArgumentException">如果方法不存在或不是静态方法</exception>
        public void ImportMethod<T>(string methodName, string? jsName = null)
        {
            var type = typeof(T);
            
            // 获取指定名称的方法
            var methodInfo = type.GetMethod(methodName, 
                BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
            
            if (methodInfo == null)
            {
                throw new ArgumentException($"类型 {type.Name} 中找不到名为 {methodName} 的公共静态方法");
            }
            
            if (!methodInfo.IsStatic)
            {
                throw new ArgumentException($"方法 {methodName} 不是静态方法");
            }
            
            // 如果没有指定JS名称，使用原方法名转为首字母小写的驼峰命名
            if (string.IsNullOrEmpty(jsName))
            {
                jsName = ToCamelCase(methodName);
            }
            
            // 创建委托并添加到JavaScript引擎
            var delegateType = Expression.GetDelegateType(
                methodInfo.GetParameters().Select(p => p.ParameterType).Append(methodInfo.ReturnType).ToArray());
                
            var delegateInstance = Delegate.CreateDelegate(delegateType, null, methodInfo);
            _engine.AddHostObject(jsName, delegateInstance);
            
            System.Console.WriteLine($"已导入静态方法 {type.Name}.{methodName} 为全局函数 {jsName}");
        }

        /// <summary>
        /// 从实例对象导入方法为JavaScript全局函数
        /// </summary>
        /// <typeparam name="T">实例对象的类型</typeparam>
        /// <param name="instance">实例对象</param>
        /// <param name="methodName">要导入的方法名</param>
        /// <param name="jsName">JavaScript中使用的名称(可选)，默认使用原方法名</param>
        public void ImportInstanceMethod<T>(T instance, string methodName, string? jsName = null) where T : class
        {
            var type = typeof(T);
            
            // 获取指定名称的方法
            var methodInfo = type.GetMethod(methodName, 
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            
            if (methodInfo == null)
            {
                throw new ArgumentException($"类型 {type.Name} 中找不到名为 {methodName} 的公共实例方法");
            }
            
            // 如果没有指定JS名称，使用原方法名转为首字母小写的驼峰命名
            if (string.IsNullOrEmpty(jsName))
            {
                jsName = ToCamelCase(methodName);
            }
            
            // 创建委托并添加到JavaScript引擎
            var delegateType = Expression.GetDelegateType(
                methodInfo.GetParameters().Select(p => p.ParameterType).Append(methodInfo.ReturnType).ToArray());
                
            var delegateInstance = Delegate.CreateDelegate(delegateType, instance, methodInfo);
            _engine.AddHostObject(jsName, delegateInstance);
            
            System.Console.WriteLine($"已导入实例方法 {type.Name}.{methodName} 为全局函数 {jsName}");
        }

        public void ImportNetClass<T>() where T : class
        {
            var type = typeof(T);
            string jsName = ToCamelCase(type.Name);
            
            // 添加原始的 Host 类型，使用下划线前缀避免命名冲突
            _engine.AddHostType("_" + jsName, type);
            
            // 创建包装类，提供与原生类相似的接口
            string wrapperScript = CreateWrapperClass(jsName);
                            
            ExecuteJavaScript(wrapperScript);
        }

        public void ImportClass<T>() where T : class
        {
            var type = typeof(T);
            var attribute = type.GetCustomAttribute<ScriptClassAttribute>();
            if (attribute == null)
            {
                throw new ArgumentException($"类型 {type.Name} 没有标记 ScriptClassAttribute");
            }

            string jsName = attribute.Name ?? ToCamelCase(type.Name);
            
            if (attribute.HostType)
            {
                // 直接作为 Host 类型导出
                _engine.AddHostType(jsName, type);
                return;
            }
            
            // 添加原始的 Host 类型，使用下划线前缀避免命名冲突
            _engine.AddHostType("_" + jsName, type);
            
            string wrapperScript = CreateWrapperClass(jsName);
                        
            ExecuteJavaScript(wrapperScript);
        }

        /// <summary>
        /// 导入C#结构体到JavaScript运行时
        /// </summary>
        /// <typeparam name="T">要导入的结构体类型</typeparam>
        /// <param name="isArray">是否作为数组类型处理</param>
        public void ImportStruct<T>(bool isArray = false) where T : struct
        {
            var type = typeof(T);
            string jsName = ToCamelCase(type.Name);
            
            // 添加原始的Host类型
            _engine.AddHostType("_" + jsName, type);
            
            // 根据isArray参数创建不同的包装类
            string wrapperScript = isArray ? 
                CreateArrayWrapperClass(jsName) : 
                CreateStructWrapperClass(jsName);
            
            ExecuteJavaScript(wrapperScript);
        }

        #endregion

        #region Project Creation

        public string CreateProject(string projectName){
            string projectPath = Path.Combine(Environment.CurrentDirectory, "scripts", projectName);
            if (!Directory.Exists(projectPath))
            {
                Directory.CreateDirectory(projectPath);
                Directory.CreateDirectory(Path.Combine(projectPath, "modules"));
                Directory.CreateDirectory(Path.Combine(projectPath, "assets"));
            }

            if (!File.Exists(Path.Combine(projectPath, "main.js")))
            {
                File.WriteAllText(Path.Combine(projectPath, "main.js"), @"import ModuleClass from 'module_class.js';

function main() {
    try {
        while (true) {
            // 脚本主循环代码，可以在这里编写脚本逻辑
            // 如果您不需要主循环，可以删除这个while循环，但是 main 函数必须保留
            const module = new ModuleClass('module');
            module.greet();
            console.read();
        }
    } catch (error) {
        console.log(error.toString());
}");
                File.WriteAllText(Path.Combine(projectPath, "modules", "module_class.js"), @"export default class ModuleClass {
    constructor(name) {
        this.name = name;
    }

    greet() {
        console.log(`Hello, my name is ${this.name}`);
    }
}");
            }
            return projectPath;
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            _engine?.Dispose();
        }

        #endregion
    }
}