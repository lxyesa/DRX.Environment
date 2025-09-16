using System;
using System.Reflection;

namespace Drx.Sdk.Shared.JavaScript
{
    /// <summary>
    /// 负责将.NET类型、方法、属性绑定到JavaScript运行时，实现双向调用
    /// </summary>
    public static class ScriptBinder
    {
        /// <summary>
        /// 注册类型到ClearScript运行时
        /// </summary>
        public static void BindType(ClearScriptEngineWrapper engine, ScriptTypeMetadata meta)
        {
            Logger.Debug($"开始绑定类型: {meta.Type.FullName}");
            // 注册类型本身
            engine.RegisterType(meta);
        
            // 注册方法
            foreach (var method in meta.ExportedMethods)
            {
                engine.RegisterGlobal(method.Name, CreateMethodDelegate(engine, method));
            }
        
            // 注册属性
            foreach (var prop in meta.ExportedProperties)
            {
                engine.RegisterGlobal(prop.Name, new Func<object>(() => prop.GetValue(null)));
            }
        
            // 注册字段
            foreach (var field in meta.ExportedFields)
            {
                engine.RegisterGlobal(field.Name, field.GetValue(null));
            }
        }

        /// <summary>
        /// 创建.NET方法的JS可调用委托
        /// </summary>
        private static Func<object[], object> CreateMethodDelegate(ClearScriptEngineWrapper engine, MethodInfo method)
        {
            return (args) =>
            {
                var parameters = method.GetParameters();
                var realArgs = new object?[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    var val = TypeConverter.FromJsValue(args[i], parameters[i].ParameterType);
                    if (val == null && parameters[i].ParameterType.IsValueType && Nullable.GetUnderlyingType(parameters[i].ParameterType) == null)
                    {
                        realArgs[i] = (object)Activator.CreateInstance(parameters[i].ParameterType)!;
                    }
                    else
                    {
                        realArgs[i] = (object?)val;
                    }
                }
                var result = method.Invoke(null, realArgs);
                return TypeConverter.ToJsValue(result);
            };
        }
    }
}