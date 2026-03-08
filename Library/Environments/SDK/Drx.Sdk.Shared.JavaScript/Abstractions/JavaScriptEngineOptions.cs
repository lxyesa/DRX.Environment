using System;
using System.Collections.Generic;
using System.Reflection;

namespace Drx.Sdk.Shared.JavaScript.Abstractions
{
    /// <summary>
    /// JavaScript 引擎配置选项，控制缓存、重试、程序集扫描范围及插件加载。
    /// 通过 <see cref="DependencyInjection.ServiceCollectionExtensions.AddDrxJavaScript"/> 注册并传入配置委托。
    /// </summary>
    public sealed class JavaScriptEngineOptions
    {
        /// <summary>是否启用脚本文件执行结果缓存，默认 true。</summary>
        public bool EnableScriptCaching { get; set; } = true;

        /// <summary>引擎执行失败时的最大重试次数，默认 1。</summary>
        public int MaxRetry { get; set; } = 1;

        /// <summary>指定要扫描 <see cref="Attributes.ScriptExportAttribute"/> 类型的程序集范围；为空则扫描当前域全部程序集。</summary>
        public Assembly[] ScanAssemblies { get; set; } = Array.Empty<Assembly>();

        /// <summary>引擎启动时自动初始化的插件列表。</summary>
        public IList<IJavaScriptPlugin> Plugins { get; } = new List<IJavaScriptPlugin>();

        /// <summary>可选的类型转换器配置回调，在 DI 容器初始化后被调用。</summary>
        public Action<ITypeConverter>? ConfigureConverter { get; set; }
    }
}
