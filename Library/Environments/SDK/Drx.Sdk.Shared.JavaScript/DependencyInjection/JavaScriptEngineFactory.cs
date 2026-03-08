using System;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Drx.Sdk.Shared.JavaScript.DependencyInjection
{
    /// <summary>
    /// JavaScript 引擎工厂实现，实现 <see cref="IJavaScriptEngineFactory"/>。
    /// 通过 <see cref="IServiceProvider"/> 创建引擎实例，支持默认配置或自定义配置覆盖。
    /// 应注册为 Singleton；引擎实例本身是 Transient。
    /// </summary>
    public sealed class JavaScriptEngineFactory : IJavaScriptEngineFactory
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 初始化 <see cref="JavaScriptEngineFactory"/>。
        /// </summary>
        /// <param name="serviceProvider">DI 服务提供者，用于解析引擎及其依赖。</param>
        public JavaScriptEngineFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc/>
        public IJavaScriptEngine Create()
            => _serviceProvider.GetRequiredService<IJavaScriptEngine>();

        /// <inheritdoc/>
        public IJavaScriptEngine Create(Action<JavaScriptEngineOptions> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            // 创建子作用域并在其中覆盖选项
            var scope = _serviceProvider.CreateScope();
            var options = scope.ServiceProvider.GetRequiredService<IOptions<JavaScriptEngineOptions>>();
            configure(options.Value);
            return scope.ServiceProvider.GetRequiredService<IJavaScriptEngine>();
        }
    }
}
