using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NetworkCoreStandard.Attributes;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;
using NLua;
using System.Collections.Concurrent;

namespace NetworkCoreStandard.Script;

public class LuaScriptEngine : IDisposable
{
    private readonly Lua _lua;
    private bool _disposed;
    private static readonly ConcurrentDictionary<Type, LuaExportAttribute> _exportCache = new();

    public LuaScriptEngine()
    {
        _lua = new Lua();
        InitializeEngine();
    }

    private void InitializeEngine()
    {
        // 注册基础函数
        _lua["Log"] = new Action<string, string>(Logger.Log);

        ExportMarkedTypes();
    }

    /// <summary>
    /// 导出标记了LuaExport的类型
    /// </summary>
    private void ExportMarkedTypes()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        _ = Parallel.ForEach(assemblies, assembly =>
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => !_exportCache.ContainsKey(t))
                    .ToList();

                foreach (var type in types)
                {
                    var attr = type.GetCustomAttribute<LuaExportAttribute>();
                    if (attr != null)
                    {
                        _ = _exportCache.TryAdd(type, attr);
                        ExportType(type, attr);
                    }
                }
            }
            catch
            {
                // 忽略加载失败的程序集
            }
        });
    }

    private void ExportType(Type type, LuaExportAttribute attr)
    {
        string luaName = attr.Name ?? type.Name;
        lock (_lua)
        {
            _lua.NewTable(luaName);
            var typeTable = _lua[luaName];

            if (attr.ExportMembers)
            {
                ExportStaticMethods(type, (LuaTable)typeTable);
                ExportInstanceMethods(type);
            }
        }
    }

    private void ExportStaticMethods(Type type, LuaTable typeTable)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<LuaExportAttribute>() != null);

        foreach (var method in methods)
        {
            var methodAttr = method.GetCustomAttribute<LuaExportAttribute>();
            string methodName = methodAttr.Name ?? method.Name;
            var paramTypes = method.GetParameters().Select(p => p.ParameterType)
                .Concat(new[] { method.ReturnType })
                .ToArray();
            var del = method.CreateDelegate(Expression.GetDelegateType(paramTypes));
            typeTable[methodName] = del;
        }
    }

    private void ExportInstanceMethods(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<LuaExportAttribute>() != null);

        foreach (var method in methods)
        {
            _lua[type.FullName + "." + method.Name] = method;
        }
    }

    public void LoadFile(string filePath, NetworkServer server)
    {
        try
        {
            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException($"Lua脚本文件不存在: {filePath}");
            }

            // 注入服务器实例到Lua环境
            _lua["server"] = server;

            // 使用UTF8编码读取文件
            using (var stream = new StreamReader(filePath, Encoding.UTF8))
            {
                string script = stream.ReadToEnd();
                _ = _lua.DoString(script);
            }

            // 调用脚本入口函数
            if (_lua["ScriptMain"] != null)
            {
                var scriptMain = _lua["ScriptMain"] as LuaFunction;
                _ = (scriptMain?.Call(server));
            }
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "LuaEngine", $"加载脚本失败: {ex.Message}");
        }
    }

    public void LoadScript(string script)
    {
        try
        {
            _ = _lua.DoString(script);
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "LuaEngine", $"执行脚本失败: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _lua.Dispose();
            _disposed = true;
        }
    }

    // 用于给Lua调用的辅助方法
    public T? CreateInstance<T>(params object[] args) where T : class
    {
        try
        {
            return Activator.CreateInstance(typeof(T), args) as T;
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "LuaEngine", $"创建实例失败: {ex.Message}");
            return null;
        }
    }
}
