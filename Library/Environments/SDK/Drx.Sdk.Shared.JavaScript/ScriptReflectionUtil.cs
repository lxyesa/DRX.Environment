using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Drx.Sdk.Shared.JavaScript
{
    public static class ScriptReflectionUtil
    {
        public static bool IsStaticClass(Type type)
        {
            return type.IsAbstract && type.IsSealed;
        }

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

        public static Type SafeGetType(string typeName)
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

        public static bool IsExportedMember(MemberInfo member)
        {
            return member.GetCustomAttribute<ScriptExportAttribute>() != null;
        }
    }
}