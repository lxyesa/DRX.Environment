namespace Drx.Sdk.Shared.JavaScript.Abstractions
{
    /// <summary>
    /// JavaScript 引擎插件接口。
    /// 插件在引擎初始化时自动加载，可向引擎注册宿主对象或执行初始化脚本。
    /// 依赖：IJavaScriptEngine。
    /// </summary>
    public interface IJavaScriptPlugin
    {
        /// <summary>插件唯一标识名称，用于日志和调试。</summary>
        string Name { get; }

        /// <summary>在引擎创建后调用，执行插件初始化逻辑。</summary>
        /// <param name="engine">正在初始化的 JavaScript 引擎实例。</param>
        void Initialize(IJavaScriptEngine engine);

        /// <summary>释放插件持有的资源。引擎销毁时调用。</summary>
        void Dispose();
    }
}
