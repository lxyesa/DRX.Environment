using System;
using System.IO;
using System.Reflection;
using Drx.Sdk.Script.Attributes;
using NLua;
using Drx.Sdk.Script.Interfaces;

namespace Drx.Sdk.Script
{
    [Obsolete("Lua脚本加载器已经陆续不再受到支持，它可能会因为各种问题而导致程序崩溃。请使用JavaScript脚本加载器。")]
    public class LuaScriptLoader : IDisposable
    {
        private static LuaScriptLoader? _instance;
        private static readonly object _lock = new object();
        private readonly Lua _engine;
        private static bool _isLuaContext;
        public static bool IsLuaContext 
        {
            get => _isLuaContext;
            private set => _isLuaContext = value;
        }

        private LuaScriptLoader()
        {
            _engine = new Lua();
        }

        [Obsolete("Lua脚本加载器已经陆续不再受到支持，它可能会因为各种问题而导致程序崩溃。请使用JavaScript脚本加载器。")]
        /// <summary>
        /// 获取 Lua 脚本加载器的实例。
        /// </summary>
        /// 该函数随时可能会被移除，因为 Lua 脚本加载器已经陆续不再受到支持。
        public static LuaScriptLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LuaScriptLoader();
                        }
                    }
                }
                return _instance;
            }
        }

        public Lua Engine => _engine;

        public void ExecuteScript(string script)
        {
            _engine.DoString(script);
        }

        public void ExecuteScriptFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"脚本文件 {filePath} 不存在。");

            // 读取原始脚本内容
            string originalScript = File.ReadAllText(filePath);
            
            // 构建包装后的脚本，添加异步main函数的执行
            string wrappedScript = @"
                local function async_main()
                    " + originalScript + @"
                end

                -- 创建协程执行异步main函数
                local co = coroutine.create(async_main)
                local success, error = coroutine.resume(co)
                if not success then
                    error('执行脚本发生错误: ' .. tostring(error))
            end";

            // 执行包装后的脚本
            _engine.DoString(wrappedScript);
        }

        public void ExecuteProject(string projectPath)
        {
            string mainLuaPath = Path.Combine(projectPath, "main.lua");

            if (!File.Exists(mainLuaPath))
                throw new FileNotFoundException($"Main 脚本文件在 {projectPath} 中不存在！");

            ExecuteScriptFromFile(mainLuaPath);
        }

        public Task ExecuteScriptAsync(string script)
        {
            return Task.Run(() => ExecuteScript(script));
        }

        public Task ExecuteScriptFromFileAsync(string filePath)
        {
            return Task.Run(() => ExecuteScriptFromFile(filePath));
        }

        public Task ExecuteProjectAsync(string projectPath)
        {
            return Task.Run(() => ExecuteProject(projectPath));
        }

        public void Dispose()
        {
            _engine?.Dispose();
        }

        private string ToCamelCase(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
                
            if (str.Length == 1)
                return str.ToLower();
                
            return char.ToLowerInvariant(str[0]) + str.Substring(1);
        }

        public void ImportClass<T>()
        {
            IsLuaContext = true;
            try
            {
                var type = typeof(T);
                var attribute = type.GetCustomAttribute<ScriptClassAttribute>();
                if (attribute == null)
                {
                    throw new ArgumentException($"类型 {type.Name} 没有标记 ScriptClassAttribute");
                }

                string luaName = attribute.Name ?? ToCamelCase(type.Name);

                // 先在 Lua 中创建表
                _engine.DoString($"{luaName} = {{}}");

                // 获取类型中所有公共方法
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

                // 创建类型实例
                var instance = Activator.CreateInstance<T>();

                // 注册每个方法
                foreach (var method in methods)
                {
                    if (method.DeclaringType == typeof(object))
                        continue; // 跳过 Object 类的方法

                    // 将方法名转换为小驼峰命名
                    string methodName = ToCamelCase(method.Name);
                    string fullMethodName = $"{luaName}.{methodName}";
                    _engine.RegisterFunction(fullMethodName, instance, method);
                }
            }
            finally
            {
                IsLuaContext = false;
            }
        }
    }
}