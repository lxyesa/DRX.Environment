using System;
using Drx.Sdk.Shared.JavaScript.Attributes;

namespace Drx.Sdk.Shared.JavaScript.Abstractions
{
    /// <summary>
    /// 类型导出过滤器接口。
    /// 在扫描阶段决定某个标注了 ScriptExportAttribute 的类型是否真正导出到脚本环境。
    /// 依赖：ScriptExportAttribute。
    /// </summary>
    public interface ITypeFilter
    {
        /// <summary>判断指定类型是否应导出到脚本引擎。</summary>
        /// <param name="type">候选的 .NET 类型。</param>
        /// <param name="attribute">附着在该类型上的 ScriptExportAttribute 实例。</param>
        /// <returns>应导出时返回 true，否则返回 false 以跳过。</returns>
        bool ShouldExport(Type type, ScriptExportAttribute attribute);
    }
}
