// Copyright (c) DRX SDK — Paperclip 正则表达式脚本桥接层
// 职责：将 System.Text.RegularExpressions 能力导出到 JS/TS 脚本
// 关键依赖：System.Text.RegularExpressions

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 正则表达式脚本桥接层。提供 .NET 侧正则匹配、替换、分割等静态 API。
/// 与 JS 原生正则不同，这里使用 .NET 引擎，支持更丰富的特性（命名组、Lookbehind 等）。
/// </summary>
public static class RegexBridge
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>判断字符串是否匹配正则。</summary>
    public static bool isMatch(string input, string pattern, bool ignoreCase = false)
    {
        var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        return Regex.IsMatch(input, pattern, options, DefaultTimeout);
    }

    /// <summary>返回第一个匹配结果 { success, value, index, groups }。</summary>
    public static object? match(string input, string pattern, bool ignoreCase = false)
    {
        var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        var m = Regex.Match(input, pattern, options, DefaultTimeout);
        if (!m.Success) return null;
        return MatchToObject(m);
    }

    /// <summary>返回所有匹配结果数组。</summary>
    public static object[] matches(string input, string pattern, bool ignoreCase = false)
    {
        var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        var results = new List<object>();
        foreach (Match m in Regex.Matches(input, pattern, options, DefaultTimeout))
        {
            results.Add(MatchToObject(m));
        }
        return results.ToArray();
    }

    /// <summary>正则替换。</summary>
    public static string replace(string input, string pattern, string replacement, bool ignoreCase = false)
    {
        var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        return Regex.Replace(input, pattern, replacement, options, DefaultTimeout);
    }

    /// <summary>正则分割。</summary>
    public static string[] split(string input, string pattern, bool ignoreCase = false)
    {
        var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        return Regex.Split(input, pattern, options, DefaultTimeout);
    }

    /// <summary>转义正则特殊字符。</summary>
    public static string escape(string input)
        => Regex.Escape(input);

    /// <summary>反转义正则特殊字符。</summary>
    public static string unescape(string input)
        => Regex.Unescape(input);

    private static object MatchToObject(Match m)
    {
        var groups = new Dictionary<string, object?>();
        for (var i = 0; i < m.Groups.Count; i++)
        {
            var g = m.Groups[i];
            var name = m.Groups[i].Name ?? i.ToString();
            groups[name] = g.Success ? g.Value : null;
        }
        return new { success = m.Success, value = m.Value, index = m.Index, length = m.Length, groups };
    }
}
