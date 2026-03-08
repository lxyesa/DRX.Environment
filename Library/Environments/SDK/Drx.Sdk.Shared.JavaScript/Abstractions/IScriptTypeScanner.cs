using System.Collections.Generic;
using System.Reflection;
using Drx.Sdk.Shared.JavaScript.Metadata;

namespace Drx.Sdk.Shared.JavaScript.Abstractions
{
    /// <summary>
    /// 程序集扫描器抽象，发现带 ScriptExport 标注的类型并生成元数据。
    /// 替代原有静态扫描逻辑，支持实例化和依赖注入。
    /// 依赖：ScriptTypeMetadata、ITypeFilter（可选）。
    /// </summary>
    public interface IScriptTypeScanner
    {
        /// <summary>扫描当前应用域中所有已加载程序集，返回可导出类型的元数据集合。</summary>
        /// <returns>发现的脚本类型元数据枚举。</returns>
        IEnumerable<Drx.Sdk.Shared.JavaScript.Metadata.ScriptTypeMetadata> Scan();

        /// <summary>扫描指定程序集集合，返回可导出类型的元数据集合。</summary>
        /// <param name="assemblies">要扫描的程序集列表。</param>
        /// <returns>发现的脚本类型元数据枚举。</returns>
        IEnumerable<Drx.Sdk.Shared.JavaScript.Metadata.ScriptTypeMetadata> Scan(IEnumerable<Assembly> assemblies);
    }
}
