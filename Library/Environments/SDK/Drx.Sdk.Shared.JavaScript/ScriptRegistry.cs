using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DRX.Framework;

namespace Drx.Sdk.Shared.JavaScript
{
    public class ScriptRegistry
    {
        private readonly ConcurrentDictionary<string, ScriptTypeMetadata> _exportedTypes = new();
        private readonly ConcurrentDictionary<ScriptExportType, ConcurrentBag<ScriptTypeMetadata>> _typeGroups = new();

        private static readonly Lazy<ScriptRegistry> _instance = new(() => new ScriptRegistry());
        public static ScriptRegistry Instance => _instance.Value;

        private ScriptRegistry()
        {
            foreach (ScriptExportType type in Enum.GetValues(typeof(ScriptExportType)))
            {
                _typeGroups[type] = new ConcurrentBag<ScriptTypeMetadata>();
            }
        }

        public void RegisterType(ScriptTypeMetadata metadata)
        {
            if (_exportedTypes.TryAdd(metadata.ExportName, metadata))
            {
                Logger.Debug($"注册 JavaScript 导出类型: {metadata.ExportName} ({metadata.Type.FullName})");
                _typeGroups[metadata.ExportType].Add(metadata);
            }
        }

        public ScriptTypeMetadata? GetExportedType(string name)
        {
            _exportedTypes.TryGetValue(name, out var meta);
            return meta;
        }

        public IEnumerable<ScriptTypeMetadata> GetExportedClasses()
            => _typeGroups[ScriptExportType.Class];

        public IEnumerable<ScriptTypeMetadata> GetExportedFunctions()
            => _typeGroups[ScriptExportType.Function];

        public IEnumerable<ScriptTypeMetadata> GetExportedStaticClasses()
            => _typeGroups[ScriptExportType.StaticClass];

        public IEnumerable<ScriptTypeMetadata> GetAllExportedTypes()
            => _exportedTypes.Values;

        public bool TryGetExportedType(string name, out ScriptTypeMetadata? meta)
            => _exportedTypes.TryGetValue(name, out meta);
    }
}