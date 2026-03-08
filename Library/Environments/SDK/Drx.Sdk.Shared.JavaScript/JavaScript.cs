using System;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Drx.Sdk.Shared.JavaScript.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Drx.Sdk.Shared.JavaScript
{
    /// <summary>
    /// JavaScript 静态门面核心（Lazy 引擎入口）。
    /// 依赖：DependencyInjection.AddDrxJavaScript、IJavaScriptEngine。
    /// </summary>
    public static partial class JavaScript
    {
        private static readonly Lazy<IJavaScriptEngine> _defaultEngine = new(CreateDefaultEngine);

        private static IJavaScriptEngine Engine => _defaultEngine.Value;

        private static IJavaScriptEngine CreateDefaultEngine()
        {
            var services = new ServiceCollection();
            services.AddDrxJavaScript();

            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IJavaScriptEngine>();
        }
    }
}