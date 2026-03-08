using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Drx.Sdk.Shared.JavaScript.Attributes;

namespace Drx.Sdk.Shared.JavaScript.Metadata
{
    /// <summary>
    /// 脚本反射工具类，提供判断和命名转换的静态辅助方法。
    /// 供 ScriptTypeScanner 和元数据构建流程调用。
    /// 依赖：ScriptExportAttribute、ScriptExportType。
    /// </summary>
    public static class ScriptReflectionUtil
    {
        /// <summary>判断指定类型是否为静态类。</summary>
        /// <param name="type">要检查的类型。</param>
        /// <returns>是静态类时返回 true。</returns>
        public static bool IsStaticClass(Type type)
        {
            return type.IsAbstract && type.IsSealed;
        }

        /// <summary>根据导出类型验证目标类型是否满足导出条件。</summary>
        /// <param name="type">候选类型。</param>
        /// <param name="exportType">期望的导出分类。</param>
        /// <returns>满足条件时返回 true。</returns>
        public static bool IsValidExport(Type type, ScriptExportType exportType)
        {
            return exportType switch
            {
                ScriptExportType.Class => !IsStaticClass(type),
                ScriptExportType.StaticClass => IsStaticClass(type),
                ScriptExportType.Function => type.IsSubclassOf(typeof(Delegate)),
                _ => false
            };
        }

        /// <summary>将 .NET 类型名称转换为 JavaScript 友好的名称（去除泛型反引号后缀）。</summary>
        /// <param name="type">目标类型。</param>
        /// <returns>适合在 JS 中使用的标识符字符串。</returns>
        public static string GetJavaScriptFriendlyName(Type type)
        {
            if (!type.IsGenericType)
                return type.Name;

            var sb = new StringBuilder();
            sb.Append(type.Name[..type.Name.IndexOf('`')]);
            sb.Append("_");
            sb.Append(string.Join("_", type.GetGenericArguments().Select(GetJavaScriptFriendlyName)));
            return sb.ToString();
        }

        /// <summary>安全获取指定名称的类型，不抛出异常。</summary>
        /// <param name="typeName">完全限定类型名称字符串。</param>
        /// <returns>找到则返回对应 Type，否则返回 null。</returns>
        public static Type? SafeGetType(string typeName)
        {
            try
            {
                return Type.GetType(typeName, false);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>判断成员是否带有 ScriptExportAttribute 标注。</summary>
        /// <param name="member">要检查的成员。</param>
        /// <returns>有标注时返回 true。</returns>
        public static bool IsExportedMember(MemberInfo member)
        {
            return member.GetCustomAttribute<ScriptExportAttribute>() != null;
        }
    }
}
