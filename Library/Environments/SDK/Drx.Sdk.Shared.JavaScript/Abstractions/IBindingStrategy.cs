using Drx.Sdk.Shared.JavaScript.Metadata;

namespace Drx.Sdk.Shared.JavaScript.Abstractions
{
    /// <summary>
    /// 自定义类型绑定策略接口。
    /// 判断并执行特定 ScriptTypeMetadata 到 IScriptEngineRuntime 的绑定逻辑。
    /// 依赖：IScriptEngineRuntime、ScriptTypeMetadata。
    /// </summary>
    public interface IBindingStrategy
    {
        /// <summary>判断此策略是否能处理指定元数据的绑定。</summary>
        /// <param name="metadata">待绑定的类型元数据。</param>
        /// <returns>可处理时返回 true。</returns>
        bool CanBind(ScriptTypeMetadata metadata);

        /// <summary>将指定类型元数据绑定到脚本运行时。仅在 CanBind 返回 true 时调用。</summary>
        /// <param name="runtime">目标脚本运行时。</param>
        /// <param name="metadata">描述要绑定类型的元数据。</param>
        void Bind(IScriptEngineRuntime runtime, ScriptTypeMetadata metadata);
    }
}
