using Drx.Sdk.Script.AST;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Drx.Sdk.Script.Interpreter
{
    public class Instance
    {
        public ClassNode ClassDefinition { get; }
        public Dictionary<string, object> Fields { get; }

        public Instance(ClassNode classDefinition)
        {
            ClassDefinition = classDefinition;
            Fields = new Dictionary<string, object>();
        }
    }

    public class Environment
    {
        private readonly Dictionary<string, (MethodInfo Method, object Instance)> functions = new();
        private readonly Dictionary<string, object> variables = new();
        // 新增存储类定义的字典
        private readonly Dictionary<string, ClassNode> classes = new();

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

        // 注册类定义
        public void RegisterClass(string className, ClassNode classNode)
        {
            classes[className] = classNode;
        }

        // 获取类定义
        public ClassNode GetClass(string className)
        {
            if (classes.TryGetValue(className, out var classNode))
                return classNode;
            throw new Exception($"Class '{className}' not found.");
        }

        // 创建对象实例
        public Instance CreateInstance(string className)
        {
            var classDef = GetClass(className);
            return new Instance(classDef);
        }
    }
}
