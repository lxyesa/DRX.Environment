using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NetworkCoreStandard.Attributes;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;
using NLua;

namespace NetworkCoreStandard.Script;

public class LuaScriptEngine : IDisposable
{
    private readonly Lua _lua;
    private bool _disposed;

    public LuaScriptEngine()
    {
        _lua = new Lua();
        InitializeEngine();
    }

    private void InitializeEngine()
    {
        // 注册基础函数
        _lua["Log"] = new Action<string, string>(Logger.Log);

        // 自动导出标记了LuaExport的类型
        ExportMarkedTypes();
    }

    private void ExportMarkedTypes()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    var attr = type.GetCustomAttribute<LuaExportAttribute>();
                    if (attr != null)
                    {
                        string luaName = attr.Name ?? type.Name;

                        // 创建类型表
                        _lua.NewTable(luaName);
                        var typeTable = _lua[luaName];

                        if (attr.ExportMembers)
                        {
                            // 导出静态成员
                            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                            {
                                var methodAttr = method.GetCustomAttribute<LuaExportAttribute>();
                                if (methodAttr != null)
                                {
                                    string methodName = methodAttr.Name ?? method.Name;
                                    var del = method.CreateDelegate(Expression.GetDelegateType(
                                        (from p in method.GetParameters() select p.ParameterType)
                                        .Concat(new[] { method.ReturnType })
                                        .ToArray()));

                                    ((LuaTable)typeTable)[methodName] = del;
                                }
                            }

                            // 导出实例方法
                            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                            {
                                var methodAttr = method.GetCustomAttribute<LuaExportAttribute>();
                                if (methodAttr != null)
                                {
                                    _lua[type.FullName + "." + method.Name] = method;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                continue;
            }
        }
    }

    public void LoadFile(string filePath, NetworkServer server)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Lua脚本文件不存在: {filePath}");
            }

            // 注入服务器实例到Lua环境
            _lua["server"] = server;

            // 使用UTF8编码读取文件
            using (var stream = new StreamReader(filePath, Encoding.UTF8))
            {
                string script = stream.ReadToEnd();
                _lua.DoString(script);
            }

            // 调用脚本入口函数
            if (_lua["ScriptMain"] != null)
            {
                var scriptMain = _lua["ScriptMain"] as LuaFunction;
                scriptMain?.Call(server);
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
            _lua.DoString(script);
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
