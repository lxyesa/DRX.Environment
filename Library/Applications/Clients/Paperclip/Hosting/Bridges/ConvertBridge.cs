// Copyright (c) DRX SDK — Paperclip 类型转换脚本桥接层
// 职责：将 System.Convert 及常用类型解析能力导出到 JS/TS 脚本
// 关键依赖：System

using System;
using System.Globalization;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 类型转换脚本桥接层。提供 .NET 侧数值/日期/进制转换等静态 API。
/// </summary>
public static class ConvertBridge
{
    #region 数值转换

    /// <summary>转换为 32 位整数。</summary>
    public static int toInt(object? value)
        => Convert.ToInt32(value, CultureInfo.InvariantCulture);

    /// <summary>转换为 64 位整数。</summary>
    public static long toLong(object? value)
        => Convert.ToInt64(value, CultureInfo.InvariantCulture);

    /// <summary>转换为双精度浮点数。</summary>
    public static double toDouble(object? value)
        => Convert.ToDouble(value, CultureInfo.InvariantCulture);

    /// <summary>转换为布尔值。</summary>
    public static bool toBool(object? value)
        => Convert.ToBoolean(value, CultureInfo.InvariantCulture);

    /// <summary>转换为字符串。</summary>
    public static string toString(object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    #endregion

    #region 进制转换

    /// <summary>整数转十六进制字符串。</summary>
    public static string toHex(long value)
        => value.ToString("X");

    /// <summary>十六进制字符串转整数。</summary>
    public static long fromHex(string hex)
        => Convert.ToInt64(hex, 16);

    /// <summary>整数转二进制字符串。</summary>
    public static string toBinary(long value)
        => Convert.ToString(value, 2);

    /// <summary>二进制字符串转整数。</summary>
    public static long fromBinary(string binary)
        => Convert.ToInt64(binary, 2);

    /// <summary>整数转八进制字符串。</summary>
    public static string toOctal(long value)
        => Convert.ToString(value, 8);

    /// <summary>八进制字符串转整数。</summary>
    public static long fromOctal(string octal)
        => Convert.ToInt64(octal, 8);

    #endregion

    #region 日期时间

    /// <summary>获取当前 UTC 时间的 ISO 8601 字符串。</summary>
    public static string nowUtc()
        => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>获取当前本地时间的 ISO 8601 字符串。</summary>
    public static string now()
        => DateTime.Now.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>获取 Unix 时间戳（秒）。</summary>
    public static long unixTimestamp()
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>获取 Unix 时间戳（毫秒）。</summary>
    public static long unixTimestampMs()
        => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>将 ISO 8601 字符串解析为 Unix 时间戳（秒）。</summary>
    public static long parseToUnix(string dateString)
    {
        var dto = DateTimeOffset.Parse(dateString, CultureInfo.InvariantCulture);
        return dto.ToUnixTimeSeconds();
    }

    /// <summary>将 Unix 时间戳（秒）转为 ISO 8601 字符串。</summary>
    public static string fromUnix(long unixSeconds)
        => DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToString("O", CultureInfo.InvariantCulture);

    /// <summary>格式化日期时间字符串。</summary>
    public static string formatDate(string dateString, string format)
    {
        var dt = DateTime.Parse(dateString, CultureInfo.InvariantCulture);
        return dt.ToString(format, CultureInfo.InvariantCulture);
    }

    #endregion

    #region 安全解析

    /// <summary>尝试解析整数，失败返回默认值。</summary>
    public static int tryParseInt(string value, int defaultValue = 0)
        => int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : defaultValue;

    /// <summary>尝试解析浮点数，失败返回默认值。</summary>
    public static double tryParseDouble(string value, double defaultValue = 0)
        => double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : defaultValue;

    /// <summary>尝试解析布尔值，失败返回默认值。</summary>
    public static bool tryParseBool(string value, bool defaultValue = false)
        => bool.TryParse(value, out var result) ? result : defaultValue;

    #endregion
}
