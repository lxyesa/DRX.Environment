using System.Reflection;
using Drx.Sdk.Shared.JavaScript.Metadata;

namespace Drx.Sdk.Shared.JavaScript.Abstractions
{
    /// <summary>
    /// 将 .NET 类型和方法绑定到脚本运行时的抽象。
    /// 负责把 ScriptTypeMetadata 描述的成员注入到 IScriptEngineRuntime。
    /// 依赖：IScriptEngineRuntime、ScriptTypeMetadata。
    /// </summary>
    public interface IScriptBinder
    {
        /// <summary>按元数据描述将指定类型绑定到脚本运行时。</summary>
        /// <param name="runtime">目标脚本运行时。</param>
        /// <param name="metadata">描述要绑定的类型、导出名称及成员的元数据。</param>
        void BindType(IScriptEngineRuntime runtime, ScriptTypeMetadata metadata);

        /// <summary>将单个方法以指定名称绑定到脚本运行时。</summary>
        /// <param name="runtime">目标脚本运行时。</param>
        /// <param name="name">脚本中可见的方法名。</param>
        /// <param name="method">要绑定的反射方法信息。</param>
        void BindMethod(IScriptEngineRuntime runtime, string name, MethodInfo method);
    }
}
