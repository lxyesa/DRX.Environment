using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Drx.Sdk.Shared.JavaScript.Attributes;

namespace Drx.Sdk.Shared.JavaScript.Metadata
{
    /// <summary>
    /// 脚本类型元数据，描述一个导出到 JavaScript 的 .NET 类型的所有可用成员。
    /// 提供线程安全的静态缓存工厂方法，自动识别 ScriptIgnore 和 ScriptName 标注。
    /// 依赖：ScriptExportAttribute、ScriptIgnoreAttribute、ScriptNameAttribute、ScriptExportType。
    /// </summary>
    public sealed class ScriptTypeMetadata
    {
        /// <summary>导出的 .NET 类型。</summary>
        public Type Type { get; }

        /// <summary>在 JavaScript 中使用的导出名称。</summary>
        public string ExportName { get; }

        /// <summary>导出类型分类（Class / StaticClass / Function）。</summary>
        public ScriptExportType ExportType { get; }

        /// <summary>应绑定到 JavaScript 的方法列表（已排除 ScriptIgnore 成员）。</summary>
        public IReadOnlyList<MethodInfo> ExportedMethods { get; }

        /// <summary>应绑定到 JavaScript 的属性列表（已排除 ScriptIgnore 成员）。</summary>
        public IReadOnlyList<PropertyInfo> ExportedProperties { get; }

        /// <summary>应绑定到 JavaScript 的字段列表（已排除 ScriptIgnore 成员）。</summary>
        public IReadOnlyList<FieldInfo> ExportedFields { get; }

        /// <summary>
        /// 每个成员在 JavaScript 中对应的名称映射
        /// （若有 ScriptName 标注则使用其指定名称，否则使用成员原名）。
        /// Key 为 MemberInfo，Value 为 JS 侧名称。
        /// </summary>
        public IReadOnlyDictionary<MemberInfo, string> MemberJsNames { get; }

        // 线程安全缓存，避免对同一 Type 重复反射
        private static readonly ConcurrentDictionary<Type, ScriptTypeMetadata> _cache = new();

        private ScriptTypeMetadata(
            Type type,
            string exportName,
            ScriptExportType exportType,
            List<MethodInfo> methods,
            List<PropertyInfo> properties,
            List<FieldInfo> fields,
            Dictionary<MemberInfo, string> memberJsNames)
        {
            Type = type;
            ExportName = exportName;
            ExportType = exportType;
            ExportedMethods = methods.AsReadOnly();
            ExportedProperties = properties.AsReadOnly();
            ExportedFields = fields.AsReadOnly();
            MemberJsNames = memberJsNames;
        }

        /// <summary>
        /// 从 <paramref name="type"/> 构建或从缓存取得 <see cref="ScriptTypeMetadata"/>。
        /// 成员过滤：跳过带 <see cref="ScriptIgnoreAttribute"/> 的成员。
        /// 名称解析：优先使用 <see cref="ScriptNameAttribute"/>，其次使用成员原名。
        /// </summary>
        /// <param name="type">要分析的 .NET 类型。</param>
        /// <param name="exportName">在 JavaScript 中注册的顶层名称。</param>
        /// <param name="exportType">导出类型（Class/StaticClass/Function）。</param>
        public static ScriptTypeMetadata FromType(Type type, string exportName, ScriptExportType exportType)
        {
            // 同一 Type 的首次请求才走完整反射流程
            return _cache.GetOrAdd(type, t => BuildMetadata(t, exportName, exportType));
        }

        private static ScriptTypeMetadata BuildMetadata(Type type, string exportName, ScriptExportType exportType)
        {
            const BindingFlags flags =
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            var methods = new List<MethodInfo>();
            var properties = new List<PropertyInfo>();
            var fields = new List<FieldInfo>();
            var memberJsNames = new Dictionary<MemberInfo, string>();

            foreach (var m in type.GetMethods(flags))
            {
                if (m.GetCustomAttribute<ScriptIgnoreAttribute>() != null) continue;
                if (m.GetCustomAttribute<ScriptExportAttribute>() == null) continue;

                methods.Add(m);
                memberJsNames[m] = ResolveJsName(m, m.Name);
            }

            foreach (var p in type.GetProperties(flags))
            {
                if (p.GetCustomAttribute<ScriptIgnoreAttribute>() != null) continue;
                if (p.GetCustomAttribute<ScriptExportAttribute>() == null) continue;

                properties.Add(p);
                memberJsNames[p] = ResolveJsName(p, p.Name);
            }

            foreach (var f in type.GetFields(flags))
            {
                if (f.GetCustomAttribute<ScriptIgnoreAttribute>() != null) continue;
                if (f.GetCustomAttribute<ScriptExportAttribute>() == null) continue;

                fields.Add(f);
                memberJsNames[f] = ResolveJsName(f, f.Name);
            }

            return new ScriptTypeMetadata(type, exportName, exportType, methods, properties, fields, memberJsNames);
        }

        /// <summary>
        /// 解析成员在 JavaScript 侧的名称：
        /// 先查 <see cref="ScriptNameAttribute"/>，未找到则回退为 <paramref name="defaultName"/>。
        /// </summary>
        private static string ResolveJsName(MemberInfo member, string defaultName)
        {
            var nameAttr = member.GetCustomAttribute<ScriptNameAttribute>();
            return nameAttr != null ? nameAttr.Name : defaultName;
        }

        /// <summary>清除类型反射缓存（仅用于测试场景）。</summary>
        internal static void ClearCache() => _cache.Clear();
    }
}
