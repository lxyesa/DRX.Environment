using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Interfaces;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace Drx.Sdk.Script.Functions;

[ScriptClass("delegate")]
public class ScriptDelegate : IScript
{
    private readonly Dictionary<string, dynamic> _delegates = new();
    private readonly V8ScriptEngine _engine;

    public ScriptDelegate(V8ScriptEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>
    /// 创建并注册一个委托
    /// </summary>
    public string CreateDelegate(dynamic scriptFunction, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(scriptFunction);

        if (string.IsNullOrEmpty(name))
        {
            name = GetFunctionName(scriptFunction);
        }

        _delegates[name] = scriptFunction;
        return name;
    }

    /// <summary>
    /// 调用指定的委托
    /// </summary>
    [ScriptUsage(ScriptAccess.None)]
    public async Task<object?> Invoke(string name, params object[] args)
    {
        if (!_delegates.TryGetValue(name, out var scriptFunction))
        {
            throw new KeyNotFoundException($"委托 '{name}' 未找到");
        }

        try
        {
            if (scriptFunction is Task task)
            {
                await task;
                return default;
            }
            
            if (scriptFunction is ScriptObject scriptObj)
            {
                object? result = scriptObj.Invoke(false, args);
                if (result is Task taskResult)
                {
                    await taskResult;
                    var resultProperty = taskResult.GetType().GetProperty("Result");
                    return resultProperty?.GetValue(taskResult) ?? default;
                }
                return result;
            }
            
            object? invokeResult = scriptFunction.DynamicInvoke(args);
            if (invokeResult is Task asyncResult)
            {
                await asyncResult;
                var resultProperty = asyncResult.GetType().GetProperty("Result");
                return resultProperty?.GetValue(asyncResult) ?? default;
            }
            
            return invokeResult;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"执行委托 '{name}' 时发生错误: {ex.Message}");
            throw;
        }
    }

    public bool RemoveDelegate(string name) => _delegates.Remove(name);

    public void ClearDelegates() => _delegates.Clear();

    private string GetFunctionName(dynamic scriptFunction)
    {
        try
        {
            var functionName = ((ScriptObject)scriptFunction).GetProperty("name")?.ToString();
            return !string.IsNullOrEmpty(functionName) 
                ? functionName 
                : $"delegate_{Guid.NewGuid():N}";
        }
        catch
        {
            return $"delegate_{Guid.NewGuid():N}";
        }
    }
}