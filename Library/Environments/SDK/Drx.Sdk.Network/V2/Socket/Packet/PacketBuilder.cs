using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Drx.Sdk.Network.V2.Socket.Packet;

/// <summary>
/// 数据包构建器，将数据包构建为 JSON 格式。
/// </summary>
public class PacketBuilder
{
    /// <summary>
    /// 初始化 <see cref="PacketBuilder"/> 类的新实例。
    /// </summary>
    public PacketBuilder()
    {
    }

    /// <summary>
    /// 内部字段集合，使用字典保存任意类型的数据。
    /// </summary>
    private readonly Dictionary<string, object?> _fields = new();

    /// <summary>
    /// 构建数据包为 JSON 字节数组（UTF8）。字节数组字段将以 Base64 编码输出。
    /// </summary>
    /// <returns>JSON 格式的 UTF8 字节数组。</returns>
    public byte[] Build()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        var normalized = NormalizeForSerialization(_fields);

        return JsonSerializer.SerializeToUtf8Bytes(normalized, options);
    }

    /// <summary>
    /// 添加任意对象字段。
    /// </summary>
    /// <param name="key">字段的键。</param>
    /// <param name="value">字段的值。</param>
    /// <returns>当前 <see cref="PacketBuilder"/> 实例。</returns>
    private PacketBuilder AddObject(string key, object? value)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
        _fields[key] = value;
        return this;
    }

    /// <summary>
    /// 添加字符串字段。
    /// </summary>
    public PacketBuilder Add(string key, string? value) => AddObject(key, value);

    /// <summary>
    /// 添加整数字段。
    /// </summary>
    public PacketBuilder Add(string key, int value) => AddObject(key, value);

    /// <summary>
    /// 添加布尔字段。
    /// </summary>
    public PacketBuilder Add(string key, bool value) => AddObject(key, value);

    /// <summary>
    /// 添加浮点数字段。
    /// </summary>
    public PacketBuilder Add(string key, float value) => AddObject(key, value);

    /// <summary>
    /// 添加双精度浮点数字段。
    /// </summary>
    public PacketBuilder Add(string key, double value) => AddObject(key, value);

    /// <summary>
    /// 添加长整数字段。
    /// </summary>
    public PacketBuilder Add(string key, long value) => AddObject(key, value);

    /// <summary>
    /// 添加短整数字段。
    /// </summary>
    public PacketBuilder Add(string key, short value) => AddObject(key, value);

    /// <summary>
    /// 添加字节字段。
    /// </summary>
    public PacketBuilder Add(string key, byte value) => AddObject(key, value);

    /// <summary>
    /// 添加无符号整数字段。
    /// </summary>
    public PacketBuilder Add(string key, uint value) => AddObject(key, value);

    /// <summary>
    /// 添加无符号长整数字段。
    /// </summary>
    public PacketBuilder Add(string key, ulong value) => AddObject(key, value);

    /// <summary>
    /// 添加无符号短整数字段。
    /// </summary>
    public PacketBuilder Add(string key, ushort value) => AddObject(key, value);

    /// <summary>
    /// 添加有符号字节字段。
    /// </summary>
    public PacketBuilder Add(string key, sbyte value) => AddObject(key, value);

    /// <summary>
    /// 添加数组字段。
    /// </summary>
    /// <typeparam name="T">数组元素的类型。</typeparam>
    /// <param name="key">字段的键。</param>
    /// <param name="value">字段的值。</param>
    /// <returns>当前 <see cref="PacketBuilder"/> 实例。</returns>
    public PacketBuilder Add<T>(string key, T[]? value)
    {
        if (value == null)
        {
            _fields[key] = null;
            return this;
        }

        _fields[key] = value;
        return this;
    }

    /// <summary>
    /// 添加嵌套的 <see cref="PacketBuilder"/> 字段。
    /// </summary>
    /// <param name="key">字段的键。</param>
    /// <param name="value">嵌套的 <see cref="PacketBuilder"/> 实例。</param>
    /// <returns>当前 <see cref="PacketBuilder"/> 实例。</returns>
    public PacketBuilder Add(string key, PacketBuilder? value)
    {
        if (value == null)
        {
            _fields[key] = null;
            return this;
        }

        _fields[key] = JsonDocument.Parse(value.Build()).RootElement.Clone();
        return this;
    }

    /// <summary>
    /// 添加字节数组字段，编码为 Base64。
    /// </summary>
    /// <param name="key">字段的键。</param>
    /// <param name="value">字节数组。</param>
    /// <returns>当前 <see cref="PacketBuilder"/> 实例。</returns>
    public PacketBuilder Add(string key, byte[]? value)
    {
        if (value == null)
        {
            _fields[key] = null;
            return this;
        }

        _fields[key] = value;
        return this;
    }

    /// <summary>
    /// 清空当前构建器的所有字段。
    /// </summary>
    private void Reset()
    {
        _fields.Clear();
    }

    /// <summary>
    /// 调试用：将当前构建的数据包以 JSON 格式打印到控制台。
    /// </summary>
    public void Dump()
    {
        var normalized = NormalizeForSerialization(_fields);
        var json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }

    /// <summary>
    /// 将内部对象转换为可序列化结构：把 byte[] 转为 Base64 字符串，保留数组和原始数字/字符串等。
    /// </summary>
    /// <param name="obj">要转换的对象。</param>
    /// <returns>可序列化的对象。</returns>
    private static object? NormalizeForSerialization(object? obj)
    {
        if (obj == null) return null;

        switch (obj)
        {
            case JsonElement je:
                return je;
            case byte[] bytes:
                return Convert.ToBase64String(bytes);
            case IDictionary<string, object?> dict:
                var res = new Dictionary<string, object?>();
                foreach (var kv in dict)
                {
                    res[kv.Key] = NormalizeForSerialization(kv.Value);
                }
                return res;
            case IDictionary genericDict:
                var res2 = new Dictionary<string, object?>();
                foreach (DictionaryEntry de in genericDict)
                {
                    res2[de.Key?.ToString() ?? string.Empty] = NormalizeForSerialization(de.Value);
                }
                return res2;
            case IEnumerable<object> enumObj:
                var list = new List<object?>();
                foreach (var item in enumObj)
                {
                    list.Add(NormalizeForSerialization(item));
                }
                return list;
            case IEnumerable enumerable when !(obj is string):
                var list2 = new List<object?>();
                foreach (var item in enumerable)
                {
                    list2.Add(NormalizeForSerialization(item));
                }
                return list2;
            default:
                return obj;
        }
    }
}
