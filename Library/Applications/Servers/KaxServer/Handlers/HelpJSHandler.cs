using System;
using System.Linq;
using System.Reflection;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.JavaScript;

namespace KaxServer.Handlers;

[ScriptExport("help", ScriptExportType.StaticClass)]
public static class HelpJSHandler
{
    // 样式常量与反射常量（集中管理）
    private const string TitleListPrefix = "================= JavaScript 可用类/静态类";
    private const string TitleSingle = "================= JavaScript 帮助信息 =================";
    private const string TitleTail = "======================================================";
    private const string TreeCtor = "├─ 构造函数:";
    private const string TreeMeth = "├─ 方法:";
    private const string TreeProp = "├─ 属性:";
    private const string TreeField = "└─ 字段:";
    private const string BulletMid = "│   • ";
    private const string BulletEnd = "    • ";
    private const BindingFlags CtorFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [ScriptExport]
    public static void GetHelp()
    {
        var allNames = GetAllExportNames();
        if (allNames.Count == 0)
        {
            Logger.Warn("未找到已注册的类或静态类。");
            return;
        }

        // 追加纯 Global（非 Type）的清单
        var globals = JavaScript.GetRegisteredGlobals() ?? new System.Collections.Generic.Dictionary<string, Type>();

        var total = allNames.Count + globals.Count;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{TitleListPrefix}（共 {total} 个）=================\n");
        int idx = 1;

        foreach (var className in allNames)
        {
            sb.AppendLine($"{idx}. {className}");
            if (ScriptRegistry.Instance.TryGetExportedType(className, out var meta) && meta != null)
            {
                // 保持原有清单模式：仅在列表中输出“类型行 + 四块细节（如有）”，不打印总头尾
                sb.AppendLine($"    类型: {meta.Type.FullName}");
                RenderConstructors(meta, sb, indent: "    ");
                RenderMethods(meta, sb, indent: "    ");
                RenderProperties(meta, sb, indent: "    ");
                RenderFields(meta, sb, indent: "    ");
            }
            sb.AppendLine();
            idx++;
        }

        // 渲染纯 Global（委托/实例/变量 => 指向类或值）
        foreach (var kv in globals)
        {
            var name = kv.Key;
            var type = kv.Value;

            // 若同名“类型导出”存在：按类展示（列出可用函数）
            if (ScriptRegistry.Instance.TryGetExportedType(name, out var metaForName) && metaForName != null)
            {
                sb.AppendLine($"{idx}. {name}");
                sb.AppendLine($"    类型: {metaForName.Type.FullName}");
                RenderMethods(metaForName, sb, indent: "    ");
                sb.AppendLine();
                idx++;
                continue;
            }

            sb.AppendLine($"{idx}. {name}");

            // 1) 委托：显示签名
            if (typeof(Delegate).IsAssignableFrom(type))
            {
                sb.AppendLine($"    类型: {type.FullName}");
                var invoke = type.GetMethod("Invoke");
                if (invoke != null)
                {
                    var ps = invoke.GetParameters();
                    var args = string.Join(", ", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    var ret = invoke.ReturnType == typeof(void) ? "void" : invoke.ReturnType.Name;
                    sb.AppendLine($"    {TreeMeth}");
                    sb.AppendLine($"    {BulletMid}{name}({args}) : {ret}");
                }
                sb.AppendLine();
                idx++;
                continue;
            }

            // 2) 变量指向“函数类/构建器类”等：用其类型在注册中心反查并展示方法清单
            var metaByType = GetAllExportMetas().FirstOrDefault(m => m.Type == type || m.Type.FullName == type.FullName);
            if (metaByType != null)
            {
                sb.AppendLine($"    类型: {metaByType.Type.FullName}");
                RenderMethods(metaByType, sb, indent: "    ");
                sb.AppendLine();
                idx++;
                continue;
            }

            // 3) 其他变量：显示类型与值（值暂不可获取）
            sb.AppendLine($"    类型: {type.FullName}");
            sb.AppendLine($"    值: <不可获取>");
            sb.AppendLine();
            idx++;
        }

        sb.AppendLine("================================================================================\n");
        WriteOutput(sb.ToString());
    }

    /// <summary>
    /// 获取指定对象、类型或导出名的 JavaScript 导出帮助信息。
    /// </summary>
    /// <param name="obj">支持 Type、实例对象或导出名（string），详见说明。</param>
    [ScriptExport]
    public static void GetHelp(object obj)
    {
        if (obj == null)
        {
            Logger.Warn("GetHelp(object): 参数 obj 不能为空。");
            return;
        }

        // 识别参数类型（使用可空局部并在调用前做显式分支，避免CS8601/CS8604）
        string? exportNameN = obj as string;
        Type? typeN = exportNameN == null
            ? (obj as Type) ?? obj.GetType()
            : null;

        ScriptTypeMetadata? meta;

        if (exportNameN != null)
        {
            if (!TryResolveMetadata(exportNameN, null, out meta) || meta == null)
            {
                Logger.Warn($"未找到对象 [{obj}] 的 ScriptRegistry 注册信息。");
                return;
            }
        }
        else
        {
            // 此处 typeN 一定非空（obj 为 Type 或实例对象）
            if (!TryResolveMetadata(null, typeN!, out meta) || meta == null)
            {
                Logger.Warn($"未找到对象 [{obj}] 的 ScriptRegistry 注册信息。");
                return;
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{TitleSingle}\n");
        sb.AppendLine($"导出名: {meta.ExportName}");
        sb.AppendLine($"类型: {meta.Type.FullName}");

        RenderConstructors(meta, sb);
        RenderMethods(meta, sb);
        RenderProperties(meta, sb);
        RenderFields(meta, sb);

        sb.AppendLine($"\n{TitleTail}");
        WriteOutput(sb.ToString());
    }

    // ===================== 私有工具方法 =====================

    private static System.Collections.Generic.IReadOnlyList<string> GetAllExportNames()
    {
        var classNames = JavaScript.GetRegisteredClasses() ?? new System.Collections.Generic.List<string>();
        var staticClassNames = ScriptRegistry.Instance.GetExportedStaticClasses()
            ?.Select(m => m.ExportName)
            .ToList() ?? new System.Collections.Generic.List<string>();

        // 仅返回“类型导出”的清单，纯 Global 单独渲染，避免名称冲突时的二义性
        return classNames.Concat(staticClassNames).Distinct().ToList();
    }

    private static System.Collections.Generic.IEnumerable<ScriptTypeMetadata> GetAllExportMetas()
    {
        var statics = ScriptRegistry.Instance.GetExportedStaticClasses() ?? System.Linq.Enumerable.Empty<ScriptTypeMetadata>();
        var classes = ScriptRegistry.Instance.GetExportedClasses() ?? System.Linq.Enumerable.Empty<ScriptTypeMetadata>();
        return statics.Concat(classes);
    }

    private static bool TryResolveMetadata(string? exportName, Type? type, out ScriptTypeMetadata? meta)
    {
        meta = null;

        if (!string.IsNullOrEmpty(exportName))
        {
            ScriptRegistry.Instance.TryGetExportedType(exportName, out var foundByName);
            if (foundByName != null)
            {
                meta = foundByName;
                return true;
            }

            // 纯 Global（非 Type）兜底：如果名字在全局目录里，返回 false 但不记录 warn，由调用方决定如何渲染
            var globals = JavaScript.GetRegisteredGlobals();
            if (globals != null && globals.ContainsKey(exportName))
            {
                // 没有 ScriptTypeMetadata 可返回
                return false;
            }
        }

        if (type != null)
        {
            var foundByType = GetAllExportMetas().FirstOrDefault(m => m.Type == type);
            if (foundByType != null)
            {
                meta = foundByType;
                return true;
            }
        }

        return false;
    }

    private static string FormatParams(System.Reflection.ParameterInfo[] ps)
        => string.Join(", ", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"));

    private static void RenderConstructors(ScriptTypeMetadata meta, System.Text.StringBuilder sb, string indent = "")
    {
        var ctors = meta.Type.GetConstructors(CtorFlags);
        if (ctors != null && ctors.Length > 0)
        {
            sb.AppendLine($"{indent}{TreeCtor}");
            foreach (var ctor in ctors)
            {
                sb.AppendLine($"{indent}{BulletMid}{meta.Type.Name}({FormatParams(ctor.GetParameters())})");
            }
        }
    }

    private static void RenderMethods(ScriptTypeMetadata meta, System.Text.StringBuilder sb, string indent = "")
    {
        if (meta.ExportedMethods != null && meta.ExportedMethods.Count > 0)
        {
            sb.AppendLine($"{indent}{TreeMeth}");
            foreach (var method in meta.ExportedMethods)
            {
                sb.AppendLine($"{indent}{BulletMid}{method.Name}({FormatParams(method.GetParameters())})");
            }
        }
    }

    private static void RenderProperties(ScriptTypeMetadata meta, System.Text.StringBuilder sb, string indent = "")
    {
        if (meta.ExportedProperties != null && meta.ExportedProperties.Count > 0)
        {
            sb.AppendLine($"{indent}{TreeProp}");
            foreach (var prop in meta.ExportedProperties)
            {
                sb.AppendLine($"{indent}{BulletMid}{prop.PropertyType.Name} {prop.Name}");
            }
        }
    }

    private static void RenderFields(ScriptTypeMetadata meta, System.Text.StringBuilder sb, string indent = "")
    {
        if (meta.ExportedFields != null && meta.ExportedFields.Count > 0)
        {
            sb.AppendLine($"{indent}{TreeField}");
            foreach (var field in meta.ExportedFields)
            {
                sb.AppendLine($"{indent}{BulletEnd}{field.FieldType.Name} {field.Name}");
            }
        }
    }

    private static void WriteOutput(string text)
    {
        Console.WriteLine(text);
    }
}
