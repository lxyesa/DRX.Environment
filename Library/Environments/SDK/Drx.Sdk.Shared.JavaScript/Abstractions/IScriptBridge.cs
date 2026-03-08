namespace Drx.Sdk.Shared.JavaScript.Abstractions
{
    /// <summary>
    /// 脚本桥接抽象，用于自动发现并向 JavaScript 引擎注册宿主对象或服务。
    /// 实现类可通过 DI 容器扫描并批量注册，取代手动逐一调用 RegisterGlobal。
    /// 依赖：IJavaScriptEngine。
    /// </summary>
    public interface IScriptBridge
    {
        /// <summary>将桥接对象注册到指定的 JavaScript 引擎实例。</summary>
        /// <param name="engine">目标 JavaScript 引擎。</param>
        void Register(IJavaScriptEngine engine);
    }
}
