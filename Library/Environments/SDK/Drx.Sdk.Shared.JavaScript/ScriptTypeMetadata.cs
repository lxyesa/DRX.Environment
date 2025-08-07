using System;
using System.Collections.Generic;
using System.Reflection;

namespace Drx.Sdk.Shared.JavaScript
{
    public class ScriptTypeMetadata
    {
        public Type Type { get; }
        public string ExportName { get; }
        public ScriptExportType ExportType { get; }
        public IReadOnlyList<MethodInfo> ExportedMethods { get; }
        public IReadOnlyList<PropertyInfo> ExportedProperties { get; }
        public IReadOnlyList<FieldInfo> ExportedFields { get; }

        private static readonly Dictionary<Type, ScriptTypeMetadata> _cache = new();

        private ScriptTypeMetadata(Type type, string exportName, ScriptExportType exportType,
            List<MethodInfo> methods, List<PropertyInfo> properties, List<FieldInfo> fields)
        {
            Type = type;
            ExportName = exportName;
            ExportType = exportType;
            ExportedMethods = methods.AsReadOnly();
            ExportedProperties = properties.AsReadOnly();
            ExportedFields = fields.AsReadOnly();
        }

        public static ScriptTypeMetadata FromType(Type type, string exportName, ScriptExportType exportType)
        {
            if (_cache.TryGetValue(type, out var meta))
                return meta;

            var methods = new List<MethodInfo>();
            var properties = new List<PropertyInfo>();
            var fields = new List<FieldInfo>();

            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.GetCustomAttribute<ScriptExportAttribute>() != null)
                    methods.Add(m);
            }
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (p.GetCustomAttribute<ScriptExportAttribute>() != null)
                    properties.Add(p);
            }
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (f.GetCustomAttribute<ScriptExportAttribute>() != null)
                    fields.Add(f);
            }

            var metadata = new ScriptTypeMetadata(type, exportName, exportType, methods, properties, fields);
            _cache[type] = metadata;
            return metadata;
        }
    }
}