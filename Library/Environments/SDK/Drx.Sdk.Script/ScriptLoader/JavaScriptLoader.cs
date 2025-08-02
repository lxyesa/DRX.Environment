using System;
using System.IO;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;

namespace Drx.Sdk.Script.ScriptLoader
{
    public class JavaScriptLoader : IDisposable
    {
        private readonly ScriptEngine engine;
        private readonly Dictionary<string, Microsoft.ClearScript.V8.V8Script> scriptCache = new();

        public JavaScriptLoader()
        {
            engine = new Microsoft.ClearScript.V8.V8ScriptEngine();
        }

        public string LoadScript(string scriptPath)
        {
            // 从程序运行目录加载一个JavaScript脚本
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException($"脚本文件未找到: {scriptPath}");
            }
            
            return File.ReadAllText(scriptPath);
        }

        public void ExecuteScript(string scriptContent)
        {
            // 自动编译并缓存脚本内容（以内容哈希为key），然后执行
            var v8Engine = engine as Microsoft.ClearScript.V8.V8ScriptEngine;
            if (v8Engine != null)
            {
                var key = scriptContent.GetHashCode().ToString();
                if (!scriptCache.TryGetValue(key, out var compiledScript))
                {
                    compiledScript = v8Engine.Compile(scriptContent);
                    scriptCache[key] = compiledScript;
                }
                v8Engine.Execute(compiledScript);
            }
            else
            {
                // 非V8引擎，直接执行
                engine.Execute(scriptContent);
            }
        }

        /// <summary>
        /// 编译并缓存脚本文件，返回已编译脚本对象
        /// </summary>
        [Obsolete("已自动编译，无需手动调用。")]
        public Microsoft.ClearScript.V8.V8Script CompileScript(string scriptPath)
        {
            if (scriptCache.TryGetValue(scriptPath, out var cachedScript))
            {
                return cachedScript;
            }
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException($"脚本文件未找到: {scriptPath}");
            }
            var scriptContent = File.ReadAllText(scriptPath);
            var v8Engine = engine as Microsoft.ClearScript.V8.V8ScriptEngine;
            if (v8Engine == null)
            {
                throw new InvalidOperationException("当前 ScriptEngine 不是 V8ScriptEngine，无法编译脚本。");
            }
            var compiled = v8Engine.Compile(scriptContent);
            scriptCache[scriptPath] = compiled;
            return compiled;
        }

        /// <summary>
        /// 执行已缓存或新编译的脚本
        /// </summary>
        [Obsolete("已自动编译，无需手动调用。")]
        public void ExecuteCompiledScript(string scriptPath)
        {
            var v8Engine = engine as Microsoft.ClearScript.V8.V8ScriptEngine;
            if (v8Engine == null)
            {
                throw new InvalidOperationException("当前 ScriptEngine 不是 V8ScriptEngine，无法执行已编译脚本。");
            }
            var script = CompileScript(scriptPath);
            v8Engine.Execute(script);
        }

        public void RegisterClass<T>(string className)
        {
            // 注册一个JavaScript类到脚本环境中
            // JS脚本可以通过 className 来访问这个类
            // 例如：var instance = new className();
            engine.AddHostType(className, typeof(T));
        }

        public void RegisterGlobal(string name, object value)
        {
            // 将一个全局变量注册到JavaScript环境中
            // JS脚本可以通过 name 来访问这个全局变量
            // 例如：console.log(name);
            // Global必须为一个已经实例化的对象
            engine.AddHostObject(name, value);
        }

        public void RegisterFunction(string functionName, Delegate function)
        {
            // 注册一个函数到JavaScript环境中
            // JS脚本可以通过 functionName 来调用这个函数
            // 例如：functionName();
            engine.AddHostObject(functionName, function);
        }

        public void RegisterEvent(string eventName, Action eventHandler)
        {
            // 注册一个事件到JavaScript环境中
            // JS脚本可以通过 eventName 来订阅这个事件
            // 例如：eventName.subscribe(() => { /* 处理事件 */ });
            engine.AddHostObject(eventName, eventHandler);
        }

        public void RegisterStaticClass<T>(string className)
        {
            // 注册一个静态类到JavaScript环境中
            // JS脚本可以通过 className 来访问这个静态类
            // 例如：className.staticMethod();
            // 目标类必须为静态类，并且不允许存在动态实例方法
            engine.AddHostType(className, typeof(T));
        }

        public void RegisterStaticFunction(string functionName, Delegate function)
        {
            // 注册一个静态函数到JavaScript环境中
            // JS脚本可以通过 functionName 来调用这个静态函数
            // 例如：functionName();
            // 目标函数必须为静态方法
            engine.AddHostObject(functionName, function);
        }

        public void RegisterBaseClass<T>(string className)
        {
            // 注册一个基类到JavaScript环境中
            // JS脚本可以通过继承 className 来使用这个基类
            // 例如：class DerivedClass extends className { /* 实现 */ }
            // 目标类必须为基类，并且允许被其他类继承
            engine.AddHostType(className, typeof(T));
        }

        /// <summary>
        /// 执行脚本并返回结果
        /// </summary>
        public object EvaluateScript(string scriptContent)
        {
            // 执行并返回结果
            return engine.Evaluate(scriptContent);
        }

        /// <summary>
        /// 尝试执行脚本，捕获异常
        /// </summary>
        public bool TryExecuteScript(string scriptContent, out Exception? error)
        {
            error = null;
            try
            {
                engine.Execute(scriptContent);
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }

        /// <summary>
        /// 重置脚本环境
        /// </summary>
        public void Reset()
        {
            engine?.Dispose();
            scriptCache.Clear();
            var v8Type = typeof(Microsoft.ClearScript.V8.V8ScriptEngine);
            var ctor = v8Type.GetConstructor(Type.EmptyTypes);
            if (ctor != null)
            {
                var newEngine = (ScriptEngine?)ctor.Invoke(null);
                var field = typeof(JavaScriptLoader)
                    .GetField("engine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null && newEngine != null)
                {
                    field.SetValue(this, newEngine);
                }
                else
                {
                    throw new InvalidOperationException("无法重置 V8ScriptEngine。");
                }
            }
            else
            {
                throw new InvalidOperationException("无法重置 V8ScriptEngine。");
            }
        }

        public void Dispose()
        {
            engine?.Dispose();
            scriptCache.Clear();
        }
    }
}