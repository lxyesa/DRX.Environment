using System;
using System.Linq;
using System.Reflection;

namespace Drx.Sdk.Shared.JavaScript
{
    public static class ScriptTypeScanner
    {
        private static bool _scanned = false;
        private static readonly object _lock = new();

        static ScriptTypeScanner()
        {
            ScanAndRegister();
        }

        public static void EnsureScanned()
        {
            if (_scanned) return;
            lock (_lock)
            {
                if (_scanned) return;
                ScanAndRegister();
            }
        }

        private static void ScanAndRegister()
        {
            if (_scanned) return;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                foreach (var type in asm.GetTypes())
                {
                    var attr = type.GetCustomAttribute<ScriptExportAttribute>();
                    if (attr != null)
                    {
                        Logger.Debug($"扫描到类型 {type.FullName}，导出名: {attr.Name}, 导出类型: {attr.ExportType}");
                        // 将ScriptExportAttribute.ExportType转换为ScriptExportType
                        var exportType = (ScriptExportType)(int)attr.ExportType;
                        if (!ScriptReflectionUtil.IsValidExport(type, exportType))
                            continue;
                        var exportName = string.IsNullOrEmpty(attr.Name)
                            ? ScriptReflectionUtil.GetJavaScriptFriendlyName(type)
                            : attr.Name;
                        var meta = ScriptTypeMetadata.FromType(type, exportName, exportType);
                        ScriptRegistry.Instance.RegisterType(meta);
                    }
                }
            }
            _scanned = true;
        }
    }
}