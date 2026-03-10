// Copyright (c) DRX SDK — Paperclip JSON 序列化脚本桥接层
// 职责：将 .NET 侧的 JSON 序列化/反序列化能力导出到 JS/TS 脚本
// 关键依赖：System.Text.Json

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json;

namespace DrxPaperclip.Hosting;

/// <summary>
/// JSON 序列化脚本桥接层。虽然 JS 本身有 JSON.parse/stringify，
/// 但此 Bridge 允许脚本直接使用 .NET 侧的 JSON 能力，尤其适用于处理 .NET 对象序列化和美化输出。
/// </summary>
public static class JsonBridge
{
    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 将任意 .NET 对象序列化为 JSON 字符串。
    /// </summary>
    public static string stringify(object? value, bool pretty = false)
    {
        if (value is null) return "null";
        var options = pretty ? PrettyOptions : CompactOptions;
        return JsonSerializer.Serialize(value, options);
    }

    /// <summary>
    /// 将 JSON 字符串解析为动态对象。
    /// </summary>
    public static object? parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var doc = JsonDocument.Parse(json);
        return ConvertElement(doc.RootElement);
    }

    /// <summary>
    /// 从文件读取 JSON 并解析。
    /// </summary>
    public static object? readFile(string filePath)
    {
        var json = System.IO.File.ReadAllText(filePath);
        return parse(json);
    }

    /// <summary>
    /// 将对象序列化为 JSON 并写入文件。
    /// </summary>
    public static void writeFile(string filePath, object? value, bool pretty = true)
    {
        var dir = System.IO.Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(filePath, stringify(value, pretty));
    }

    #region JSON Element → 动态对象转换

    private static object? ConvertElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new ExpandoObject() as IDictionary<string, object?>;
                foreach (var prop in element.EnumerateObject())
                {
                    obj[prop.Name] = ConvertElement(prop.Value);
                }
                return obj;

            case JsonValueKind.Array:
                var arr = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    arr.Add(ConvertElement(item));
                }
                return arr.ToArray();

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l)) return l;
                return element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return null;
        }
    }

    #endregion
}
