using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Drx.Sdk.Shared.JavaScript.Attributes;

namespace Drx.Sdk.Shared.JavaScript.Metadata
{
    /// <summary>
    /// 程序集扫描器，实现 IScriptTypeScanner。
    /// 扫描带 ScriptExportAttribute 标注的类型，生成 ScriptTypeMetadata 集合。
    /// 实例类，移除静态状态，支持 DI 注入（Transient/Scoped）。
    /// 依赖：ITypeFilter（可选）、ScriptReflectionUtil、ScriptTypeMetadata。
    /// </summary>
    public sealed class ScriptTypeScanner : IScriptTypeScanner
    {
        private readonly ITypeFilter? _filter;

        /// <summary>
        /// 构造器。
        /// </summary>
        /// <param name="filter">可选的类型过滤器；为 null 时接受所有带 ScriptExportAttribute 的类型。</param>
        public ScriptTypeScanner(ITypeFilter? filter = null)
        {
            _filter = filter;
        }

        /// <inheritdoc />
        public IEnumerable<ScriptTypeMetadata> Scan()
        {
            return Scan(AppDomain.CurrentDomain.GetAssemblies());
        }

        /// <inheritdoc />
        public IEnumerable<ScriptTypeMetadata> Scan(IEnumerable<Assembly> assemblies)
        {
            var results = new List<ScriptTypeMetadata>();

            foreach (var asm in assemblies)
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // 部分类型加载失败时仍处理其余类型
                    types = ex.Types?.OfType<Type>().ToArray() ?? Array.Empty<Type>();
                }

                foreach (var type in types)
                {
                    if (type == null) continue;

                    var attr = type.GetCustomAttribute<ScriptExportAttribute>();
                    if (attr == null) continue;

                    // 如果注入了过滤器，委托给过滤器决策
                    if (_filter != null && !_filter.ShouldExport(type, attr))
                        continue;

                    var exportType = (ScriptExportType)(int)attr.ExportType;
                    if (!ScriptReflectionUtil.IsValidExport(type, exportType))
                        continue;

                    var exportName = string.IsNullOrEmpty(attr.Name)
                        ? ScriptReflectionUtil.GetJavaScriptFriendlyName(type)
                        : attr.Name;

                    results.Add(ScriptTypeMetadata.FromType(type, exportName, exportType));
                }
            }

            return results;
        }
    }
}
