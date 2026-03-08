using System.Collections.Generic;
using Drx.Sdk.Shared.JavaScript.Attributes;
using Drx.Sdk.Shared.JavaScript.Metadata;

namespace Drx.Sdk.Shared.JavaScript.Abstractions
{
    /// <summary>
    /// 脚本类型元数据注册表抽象。
    /// 提供类型元数据的注册和多维查询能力，替代原有单例静态注册表。
    /// 依赖：ScriptTypeMetadata、ScriptExportType。
    /// </summary>
    public interface IScriptRegistry
    {
        /// <summary>注册一个脚本类型元数据项。</summary>
        /// <param name="metadata">要注册的类型元数据。</param>
        void RegisterType(ScriptTypeMetadata metadata);

        /// <summary>按导出名称查找已注册的类型元数据。</summary>
        /// <param name="name">脚本中使用的导出名称。</param>
        /// <returns>匹配的元数据；未找到时返回 null。</returns>
        ScriptTypeMetadata? GetExportedType(string name);

        /// <summary>获取所有已注册的类型元数据集合。</summary>
        IEnumerable<ScriptTypeMetadata> GetAllExportedTypes();

        /// <summary>按导出类型过滤并获取已注册的类型元数据。</summary>
        /// <param name="type">要筛选的导出类型枚举值。</param>
        IEnumerable<ScriptTypeMetadata> GetExportedByType(ScriptExportType type);
    }
}
