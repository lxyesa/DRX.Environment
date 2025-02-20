using Drx.Sdk.Script.AST;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Interpreter;
using Drx.Sdk.Script.Parser;
using System;
using System.Linq;
using System.Reflection;

namespace Drx.Sdk.Script
{
    public class ScriptEngine
    {
        private readonly ScriptInterpreter interpreter;
        private readonly ScriptParser parser;

        public ScriptEngine()
        {
            interpreter = new ScriptInterpreter();
            parser = new ScriptParser();
        }

        public void RegisterScriptFunctions(object functions)
        {
            var methods = functions.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<ScriptFuncAttribute>() != null);

            foreach (var method in methods)
            {
                var attribute = method.GetCustomAttribute<ScriptFuncAttribute>();
                if (attribute != null)
                {
                    ValidateMethodParameters(method);
                    interpreter.RegisterFunction(attribute.Name, method, functions);
                }
            }
        }

        private void ValidateMethodParameters(MethodInfo method)
        {
            var validTypes = new Type[]
            {
                typeof(IntPtr), typeof(bool), typeof(long), typeof(string), typeof(byte),
                typeof(int), typeof(double), typeof(float), typeof(char), typeof(short),
                typeof(uint), typeof(ulong), typeof(ushort), typeof(sbyte)
            };

            foreach (var parameter in method.GetParameters())
            {
                if (!validTypes.Contains(parameter.ParameterType))
                {
                    throw new InvalidOperationException($"Parameter type '{parameter.ParameterType}' is not supported in method '{method.Name}'.");
                }
            }
        }

        public AstNode Parse(string scriptText)
        {
            return parser.Parse(scriptText);
        }

        public void Execute(AstNode ast)
        {
            interpreter.Execute(ast);
        }
    }
}
