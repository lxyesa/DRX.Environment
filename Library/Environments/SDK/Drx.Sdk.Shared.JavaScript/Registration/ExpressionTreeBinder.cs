using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Drx.Sdk.Shared.JavaScript.Metadata;

namespace Drx.Sdk.Shared.JavaScript.Registration
{
    /// <summary>
    /// 使用 Expression Tree 编译的高性能方法绑定器，实现 <see cref="IScriptBinder"/>。
    /// 替代反射 <c>MethodInfo.Invoke</c>，通过 <see cref="ConcurrentDictionary{TKey,TValue}"/>
    /// 缓存已编译委托，避免重复编译。
    /// 依赖：<see cref="ITypeConverter"/>（Abstractions 命名空间）、<see cref="IScriptEngineRuntime"/>。
    /// </summary>
    public sealed class ExpressionTreeBinder : IScriptBinder
    {
        private readonly ITypeConverter _converter;

        /// <summary>已编译方法委托缓存，Key 为 <see cref="MethodInfo"/>，线程安全。</summary>
        private readonly ConcurrentDictionary<MethodInfo, Func<object?[], object?>> _compiledDelegates = new();

        /// <summary>
        /// 初始化 <see cref="ExpressionTreeBinder"/> 实例。
        /// </summary>
        /// <param name="converter">用于参数类型转换的类型转换器。</param>
        public ExpressionTreeBinder(ITypeConverter converter)
        {
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 绑定顺序：先注册宿主类型，再绑定各导出方法，
        /// 最后以延迟 <see cref="Func{TResult}"/> 包装注册属性和字段（延迟读取，支持运行时变化）。
        /// </remarks>
        public void BindType(IScriptEngineRuntime runtime, ScriptTypeMetadata metadata)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));

            // 注册宿主类型（使脚本可通过 new 调用）
            runtime.AddHostType(metadata.ExportName, metadata.Type);

            // 绑定各导出方法
            foreach (var method in metadata.ExportedMethods)
                BindMethod(runtime, method.Name, method);

            // 注册属性：延迟 Func<object?> 字段注册，读取时才获取值
            foreach (var prop in metadata.ExportedProperties)
            {
                var capturedProp = prop;
                runtime.AddHostObject(capturedProp.Name, new Func<object?>(() => capturedProp.GetValue(null)));
            }

            // 注册字段：延迟 Func<object?> 字段注册，读取时才获取值
            foreach (var field in metadata.ExportedFields)
            {
                var capturedField = field;
                runtime.AddHostObject(capturedField.Name, new Func<object?>(() => capturedField.GetValue(null)));
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 从 <see cref="_compiledDelegates"/> 缓存中获取或编译委托后，
        /// 以 <see cref="Func{T, TResult}"/> 形式注册到运行时。
        /// </remarks>
        public void BindMethod(IScriptEngineRuntime runtime, string name, MethodInfo method)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (method == null) throw new ArgumentNullException(nameof(method));

            var compiled = GetOrCompileDelegate(method);
            runtime.AddHostObject(name, compiled);
        }

        /// <summary>
        /// 从缓存获取已编译委托；未命中时调用 <see cref="CompileDelegate"/> 编译并存入缓存。
        /// </summary>
        /// <param name="method">要获取或编译委托的方法。</param>
        /// <returns>接受 <c>object?[]</c> 参数并返回 <c>object?</c> 的已编译委托。</returns>
        private Func<object?[], object?> GetOrCompileDelegate(MethodInfo method)
            => _compiledDelegates.GetOrAdd(method, CompileDelegate);

        /// <summary>
        /// 使用 Expression Tree 将 <paramref name="method"/> 编译为强类型委托，
        /// 内部通过 <see cref="ITypeConverter.FromJsValue(object?, Type)"/> 完成参数的 JS→.NET 类型转换。
        /// <para>
        /// 对于返回 <see cref="void"/> 的方法，插入 <c>null</c> 常量使委托统一返回 <c>object?</c>。
        /// </para>
        /// </summary>
        /// <param name="method">要编译的反射方法信息。</param>
        /// <returns>编译好的 <c>Func&lt;object?[], object?&gt;</c> 委托。</returns>
        private Func<object?[], object?> CompileDelegate(MethodInfo method)
        {
            var parameters = method.GetParameters();
            // 入参：object?[] args
            var argsParam = Expression.Parameter(typeof(object?[]), "args");

            // 为每个参数生成类型转换表达式：ITypeConverter.FromJsValue(args[i], paramType)
            var convertMethod = typeof(ITypeConverter).GetMethod(
                nameof(ITypeConverter.FromJsValue),
                new[] { typeof(object), typeof(Type) })!;

            var convertedArgs = new Expression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                // args[i]
                var argAccess = Expression.ArrayIndex(argsParam, Expression.Constant(i));
                // (ParamType)_converter.FromJsValue(args[i], typeof(ParamType))
                var converted = Expression.Call(
                    Expression.Constant(_converter),
                    convertMethod,
                    argAccess,
                    Expression.Constant(paramType));
                convertedArgs[i] = Expression.Convert(converted, paramType);
            }

            // 静态方法调用：MethodInfo.DeclaringType.Method(args...)
            Expression call = Expression.Call(null, method, convertedArgs);

            if (method.ReturnType == typeof(void))
            {
                // void 方法：执行后返回 null（统一返回类型为 object?）
                var block = Expression.Block(
                    call,
                    Expression.Constant(null, typeof(object)));
                return Expression.Lambda<Func<object?[], object?>>(block, argsParam).Compile();
            }

            // 有返回值：装箱为 object?
            var boxed = Expression.Convert(call, typeof(object));
            return Expression.Lambda<Func<object?[], object?>>(boxed, argsParam).Compile();
        }
    }
}
