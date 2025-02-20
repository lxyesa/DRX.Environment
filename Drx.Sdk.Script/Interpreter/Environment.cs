using System;
using System.Collections.Generic;
using System.Reflection;

namespace Drx.Sdk.Script.Interpreter
{
    public class Environment
    {
        private readonly Dictionary<string, (MethodInfo Method, object Instance)> functions = new();
        private readonly Dictionary<string, object> variables = new();

        public void RegisterFunction(string name, MethodInfo method, object instance)
        {
            functions[name] = (method, instance);
        }

        public void SetVariable(string name, object value)
        {
            variables[name] = value;
        }

        public object GetVariable(string name)
        {
            if (variables.TryGetValue(name, out var value))
            {
                return value;
            }

            throw new Exception($"Variable '{name}' not found.");
        }

        // 修改后的 InvokeFunction 方法，返回一个 object
        public object? InvokeFunction(string name, object[] arguments)
        {
            if (functions.TryGetValue(name, out var function))
            {
                var result = function.Method.Invoke(function.Instance, arguments);
                return result; // 如果方法返回 void，则 result 为 null
            }
            else
            {
                throw new Exception($"Function '{name}' not found.");
            }
        }
    }
}
