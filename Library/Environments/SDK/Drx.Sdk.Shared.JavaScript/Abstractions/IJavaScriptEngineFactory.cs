using System;

namespace Drx.Sdk.Shared.JavaScript.Abstractions
{
    /// <summary>
    /// JavaScript 引擎工厂抽象，支持创建默认或自定义配置的多个引擎实例。
    /// 结合 DI 使用时，注册为 scoped 或 transient 以支持多引擎场景。
    /// 依赖：IJavaScriptEngine、JavaScriptEngineOptions（将在后续任务中创建）。
    /// </summary>
    public interface IJavaScriptEngineFactory
    {
        /// <summary>使用默认配置创建一个新的 JavaScript 引擎实例。</summary>
        /// <returns>新创建的引擎实例。调用方负责 Dispose。</returns>
        IJavaScriptEngine Create();

        /// <summary>使用自定义配置委托创建一个新的 JavaScript 引擎实例。</summary>
        /// <param name="configure">用于覆盖默认选项的配置委托。</param>
        /// <returns>新创建的引擎实例。调用方负责 Dispose。</returns>
        IJavaScriptEngine Create(Action<JavaScriptEngineOptions> configure);
    }
}
