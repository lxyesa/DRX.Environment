using System;
using System.Reflection;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Drx.Sdk.Shared.JavaScript.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Drx.Sdk.Shared.JavaScript.Engine
{
    /// <summary>
    /// JavaScript 引擎 Fluent API 构建器，无需外部 DI 容器即可链式配置并创建 <see cref="IJavaScriptEngine"/> 实例。
    /// 依赖：<see cref="JavaScriptEngineOptions"/>、<see cref="ServiceCollectionExtensions.AddDrxJavaScript"/>。
    /// </summary>
    public sealed class JavaScriptEngineBuilder
    {
        private readonly JavaScriptEngineOptions _options = new JavaScriptEngineOptions();

        /// <summary>
        /// 配置引擎选项。
        /// </summary>
        /// <param name="configure">选项配置委托，接收当前 <see cref="JavaScriptEngineOptions"/> 实例。</param>
        /// <returns>当前 <see cref="JavaScriptEngineBuilder"/>，支持链式调用。</returns>
        public JavaScriptEngineBuilder WithOption(Action<JavaScriptEngineOptions> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_options);
            return this;
        }

        /// <summary>
        /// 追加类型转换器配置回调，在 Build 时由 <see cref="JavaScriptEngineOptions.ConfigureConverter"/> 调用。
        /// 多次调用将叠加，不会覆盖前一个回调。
        /// </summary>
        /// <param name="configure">类型转换器配置委托。</param>
        /// <returns>当前 <see cref="JavaScriptEngineBuilder"/>，支持链式调用。</returns>
        public JavaScriptEngineBuilder WithConverter(Action<ITypeConverter> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var previous = _options.ConfigureConverter;
            _options.ConfigureConverter = previous == null
                ? configure
                : converter => { previous(converter); configure(converter); };

            return this;
        }

        /// <summary>
        /// 添加启动插件，引擎初始化时自动调用 <see cref="IJavaScriptPlugin.Initialize"/>。
        /// </summary>
        /// <param name="plugin">要注册的插件实例。</param>
        /// <returns>当前 <see cref="JavaScriptEngineBuilder"/>，支持链式调用。</returns>
        public JavaScriptEngineBuilder WithPlugin(IJavaScriptPlugin plugin)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));
            _options.Plugins.Add(plugin);
            return this;
        }

        /// <summary>
        /// 设置 <see cref="Attributes.ScriptExportAttribute"/> 扫描范围，限定到指定程序集。
        /// 未调用此方法时将扫描当前 AppDomain 下的全部程序集。
        /// </summary>
        /// <param name="assemblies">要扫描的程序集列表。</param>
        /// <returns>当前 <see cref="JavaScriptEngineBuilder"/>，支持链式调用。</returns>
        public JavaScriptEngineBuilder WithAssemblies(params Assembly[] assemblies)
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));
            _options.ScanAssemblies = assemblies;
            return this;
        }

        /// <summary>
        /// 完成链式配置，创建并返回已配置的 <see cref="IJavaScriptEngine"/> 实例。
        /// 内部创建独立的 <see cref="IServiceCollection"/>，通过 DI 组装所有依赖，之后建议由调用方持有并在使用结束后调用 Dispose。
        /// </summary>
        /// <returns>已初始化的 <see cref="IJavaScriptEngine"/> 实例。</returns>
        public IJavaScriptEngine Build()
        {
            // 捕获当前选项快照传入 DI 注册
            var capturedOptions = _options;

            var services = new ServiceCollection();
            services.AddDrxJavaScript(opt =>
            {
                opt.EnableScriptCaching = capturedOptions.EnableScriptCaching;
                opt.MaxRetry = capturedOptions.MaxRetry;
                opt.ScanAssemblies = capturedOptions.ScanAssemblies;
                opt.ConfigureConverter = capturedOptions.ConfigureConverter;

                foreach (var plugin in capturedOptions.Plugins)
                    opt.Plugins.Add(plugin);
            });

            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IJavaScriptEngine>();
        }
    }
}
