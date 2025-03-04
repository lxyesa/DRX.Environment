using System;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Interfaces;
using Microsoft.ClearScript.V8;

namespace Drx.Sdk.Script.Functions;

[ScriptClass("values")]
public class Values : IScript
{
    /// <summary>
    /// 将值转换为字符串
    /// </summary>
    public static string ToString(object value)
    {
        return value?.ToString() ?? "null";
    }

    /// <summary>
    /// 将值转换为整数
    /// </summary>
    public static int ToInt(object value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        if (int.TryParse(value.ToString(), out int result))
            return result;

        throw new ArgumentException($"无法将值 '{value}' 转换为整数");
    }

    /// <summary>
    /// 将值转换为无符号整数
    /// </summary>
    public static uint ToUInt(object value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        if (uint.TryParse(value.ToString(), out uint result))
            return result;

        throw new ArgumentException($"无法将值 '{value}' 转换为无符号整数");
    }

    /// <summary>
    /// 将值转换为长整数
    /// </summary>
    public static long ToLong(object value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        if (long.TryParse(value.ToString(), out long result))
            return result;

        throw new ArgumentException($"无法将值 '{value}' 转换为长整数");
    }

    /// <summary>
    /// 将值转换为双精度浮点数
    /// </summary>
    public static double ToDouble(object value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        if (double.TryParse(value.ToString(), out double result))
            return result;

        throw new ArgumentException($"无法将值 '{value}' 转换为双精度浮点数");
    }

    /// <summary>
    /// 将值转换为布尔值
    /// </summary>
    public static bool ToBoolean(object value)
    {
        if (value == null)
            return false;

        if (bool.TryParse(value.ToString(), out bool result))
            return result;

        // 对于数字，0为false，非0为true
        if (double.TryParse(value.ToString(), out double numResult))
            return numResult != 0;

        // 对于字符串，空字符串为false，非空字符串为true
        if (value is string strValue)
            return !string.IsNullOrEmpty(strValue);

        return false;
    }

    /// <summary>
    /// 检查值是否为数字
    /// </summary>
    public static bool IsNumber(object value)
    {
        if (value == null)
            return false;

        return double.TryParse(value.ToString(), out _);
    }

    /// <summary>
    /// 检查值是否为整数
    /// </summary>
    public static bool IsInteger(object value)
    {
        if (value == null)
            return false;

        return int.TryParse(value.ToString(), out _);
    }

    /// <summary>
    /// 将十六进制字符串转换为整数
    /// </summary>
    public static int HexToInt(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            throw new ArgumentNullException(nameof(hex));

        // 移除0x前缀（如果有）
        hex = hex.Replace("0x", "").Replace("0X", "");

        if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int result))
            return result;

        throw new ArgumentException($"无法将十六进制字符串 '{hex}' 转换为整数");
    }

    /// <summary>
    /// 将整数转换为十六进制字符串
    /// </summary>
    public static string IntToHex(int value, bool prefix = true)
    {
        return prefix ? $"0x{value:X}" : value.ToString("X");
    }

    public static string IntptrToHex(IntPtr value)
    {
        return value.ToString("X");
    }
}