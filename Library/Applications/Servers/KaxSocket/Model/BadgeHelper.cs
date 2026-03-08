using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KaxSocket;

public class BadgeEntry
{
    public string Text { get; set; } = string.Empty;
    public int[] Color { get; set; } = new[] { 120, 120, 120 };
}

public static class BadgeHelper
{
    private static readonly Regex LegacyBadgePattern = new(@"^(.+?)\[(\d{1,3}),(\d{1,3}),(\d{1,3})\]$", RegexOptions.Compiled);

    /// <summary>
    /// 将 Badges 字段（JSON 数组或旧分号格式）解析为 List&lt;BadgeEntry&gt;。
    /// </summary>
    public static List<BadgeEntry> ParseBadges(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<BadgeEntry>();

        var trimmed = raw.Trim();

        // JSON 数组路径
        if (trimmed.StartsWith('['))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<BadgeEntry>>(trimmed, JsonOptions);
                return list ?? new List<BadgeEntry>();
            }
            catch { /* 降级到旧格式解析 */ }
        }

        // 旧格式：badge1[r,g,b];badge2[r,g,b]
        var result = new List<BadgeEntry>();
        foreach (var segment in trimmed.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var m = LegacyBadgePattern.Match(segment);
            if (m.Success)
            {
                result.Add(new BadgeEntry
                {
                    Text = m.Groups[1].Value.Trim(),
                    Color = new[]
                    {
                        Clamp(int.Parse(m.Groups[2].Value)),
                        Clamp(int.Parse(m.Groups[3].Value)),
                        Clamp(int.Parse(m.Groups[4].Value))
                    }
                });
            }
            else if (!string.IsNullOrWhiteSpace(segment))
            {
                result.Add(new BadgeEntry { Text = segment.Trim() });
            }
        }

        return result;
    }

    /// <summary>
    /// 将徽章列表序列化为 JSON 数组字符串。
    /// </summary>
    public static string Serialize(List<BadgeEntry> badges)
    {
        if (badges == null || badges.Count == 0)
            return "[]";

        return JsonSerializer.Serialize(badges, JsonOptions);
    }

    private static int Clamp(int v) => Math.Max(0, Math.Min(255, v));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
}
