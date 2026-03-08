using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Drx.Sdk.Shared.JavaScript.Attributes;
using Drx.Sdk.Shared.JavaScript.Metadata;

namespace Drx.Sdk.Shared.JavaScript.Registration
{
    /// <summary>
    /// 脚本类型元数据注册表，实现 <see cref="IScriptRegistry"/>。
    /// 非单例：每个 <see cref="Engine.JavaScriptEngine"/> 实例持有独立注册表，
    /// 通过依赖注入（Scoped/Transient）注入，不再依赖静态 Instance 字段。
    /// 依赖：ScriptTypeMetadata、ScriptExportType、IScriptRegistry。
    /// </summary>
    public sealed class ScriptRegistry : IScriptRegistry
    {
        private const string ExportRegistrationDebugEnvVar = "DRX_JS_EXPORT_REG_DEBUG";

        // 按导出名称索引所有已注册元数据
        private readonly ConcurrentDictionary<string, ScriptTypeMetadata> _exportedTypes =
            new(StringComparer.Ordinal);

        // 按导出类型分组，方便按分类批量查询
        private readonly ConcurrentDictionary<ScriptExportType, ConcurrentBag<ScriptTypeMetadata>> _typeGroups =
            new();

        /// <summary>
        /// 初始化 <see cref="ScriptRegistry"/>，为每个 <see cref="ScriptExportType"/> 值预建分组桶。
        /// </summary>
        public ScriptRegistry()
        {
            foreach (ScriptExportType exportType in Enum.GetValues(typeof(ScriptExportType)))
                _typeGroups[exportType] = new ConcurrentBag<ScriptTypeMetadata>();
        }

        /// <inheritdoc />
        public void RegisterType(ScriptTypeMetadata metadata)
        {
            if (metadata is null)
                throw new ArgumentNullException(nameof(metadata));

            if (_exportedTypes.TryAdd(metadata.ExportName, metadata))
            {
                if (IsExportRegistrationDebugEnabled())
                {
                    Logger.Debug($"注册 JavaScript 导出类型: {metadata.ExportName} ({metadata.Type.FullName})");
                }

                _typeGroups[metadata.ExportType].Add(metadata);
            }
        }

        private static bool IsExportRegistrationDebugEnabled()
        {
            var value = Environment.GetEnvironmentVariable(ExportRegistrationDebugEnvVar);
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public ScriptTypeMetadata? GetExportedType(string name)
        {
            _exportedTypes.TryGetValue(name, out var meta);
            return meta;
        }

        /// <inheritdoc />
        public IEnumerable<ScriptTypeMetadata> GetAllExportedTypes()
            => _exportedTypes.Values;

        /// <inheritdoc />
        public IEnumerable<ScriptTypeMetadata> GetExportedByType(ScriptExportType type)
            => _typeGroups.TryGetValue(type, out var bag) ? bag : Enumerable.Empty<ScriptTypeMetadata>();

        /// <summary>
        /// 尝试按名称取得元数据，不抛异常。
        /// </summary>
        /// <param name="name">JavaScript 侧的导出名称。</param>
        /// <param name="meta">找到时输出对应元数据。</param>
        /// <returns>是否找到。</returns>
        public bool TryGetExportedType(string name, out ScriptTypeMetadata? meta)
            => _exportedTypes.TryGetValue(name, out meta);
    }
}
