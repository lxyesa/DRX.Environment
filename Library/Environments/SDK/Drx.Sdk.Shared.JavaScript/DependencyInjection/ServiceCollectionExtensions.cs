using System;
using System.Linq;
using System.Reflection;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Drx.Sdk.Shared.JavaScript.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// 使用全限定别名避免与根命名空间同名类冲突
using ConcreteTypeConverter = Drx.Sdk.Shared.JavaScript.Conversion.TypeConverter;
using ConcreteScriptRegistry = Drx.Sdk.Shared.JavaScript.Registration.ScriptRegistry;
using ConcreteScriptBinder = Drx.Sdk.Shared.JavaScript.Registration.ExpressionTreeBinder;
using ConcreteScriptTypeScanner = Drx.Sdk.Shared.JavaScript.Metadata.ScriptTypeScanner;

namespace Drx.Sdk.Shared.JavaScript.DependencyInjection
{
    /// <summary>
    /// 为 <see cref="IServiceCollection"/> 提供 JavaScript 引擎 DI 注册扩展方法。
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 向 DI 容器注册 Drx JavaScript 引擎及其所有依赖服务。
        /// </summary>
        /// <param name="services">目标服务集合。</param>
        /// <param name="configure">可选的选项配置委托；为 null 时使用默认选项。</param>
        /// <returns>同一 <paramref name="services"/> 实例，支持链式调用。</returns>
        public static IServiceCollection AddDrxJavaScript(
            this IServiceCollection services,
            Action<JavaScriptEngineOptions>? configure = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // 注册选项
            if (configure != null)
                services.Configure<JavaScriptEngineOptions>(configure);
            else
                services.AddOptions<JavaScriptEngineOptions>();

            // 核心服务（Singleton：全局共享一份元数据/类型转换）
            services.AddSingleton<IScriptRegistry, ConcreteScriptRegistry>();
            services.AddSingleton<ITypeConverter, ConcreteTypeConverter>();
            services.AddSingleton<IScriptBinder, ConcreteScriptBinder>();
            services.AddSingleton<IScriptTypeScanner, ConcreteScriptTypeScanner>();

            // 运行时（Transient：每个引擎实例独立持有 V8 引擎上下文）
            services.AddTransient<IScriptEngineRuntime>(sp =>
            {
                try
                {
                    return new ClearScriptRuntime();
                }
                catch
                {
                    return new NullScriptRuntime();
                }
            });

            // 引擎实现
            services.AddTransient<IJavaScriptEngine, Drx.Sdk.Shared.JavaScript.Engine.JavaScriptEngine>();

            // 工厂
            services.AddSingleton<IJavaScriptEngineFactory, JavaScriptEngineFactory>();

            // 自动发现并注册实现了 IScriptBridge 的类型
            var bridges = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .Where(t => typeof(IScriptBridge).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

            foreach (var bridge in bridges)
                services.AddSingleton(typeof(IScriptBridge), bridge);

            // 注册启动初始化器（程序集扫描在首次解析 IScriptRegistryInitializer 时触发）
            // 注意：扫描和注册使用各自具体类型，规避旧版/新版 ScriptTypeMetadata 命名空间歧义
            services.AddSingleton<IScriptRegistryInitializer>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<JavaScriptEngineOptions>>().Value;

                // scanner 和 registry 实现内部各自使用一致的 ScriptTypeMetadata 版本
                // 此初始化器仅作标记，实际扫描注册由 JavaScriptEngine 构造时完成（任务 10）
                _ = options;
                return new ScriptRegistryInitializer();
            });

            return services;
        }

        private static System.Collections.Generic.IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null)!;
            }
            catch
            {
                return System.Linq.Enumerable.Empty<Type>();
            }
        }
    }

    /// <summary>用于标记注册表初始化完成的占位接口，供 DI 解析时触发扫描。</summary>
    internal interface IScriptRegistryInitializer { }

    /// <summary><see cref="IScriptRegistryInitializer"/> 的占位实现。</summary>
    internal sealed class ScriptRegistryInitializer : IScriptRegistryInitializer { }
}
