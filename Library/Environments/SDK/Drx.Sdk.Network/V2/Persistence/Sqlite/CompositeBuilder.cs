using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;

namespace Drx.Sdk.Network.V2.Persistence.Sqlite;

/// <summary>
/// 复合数据构建器，用于构建和解析复合数据结构。
/// </summary>
public class CompositeBuilder
{
    // 私有字段：是否已构建完成
    private bool _isBuilt = false;

    // 私有字段：存储键值对的字典
    private readonly Dictionary<string, object?> _data = new();

    // 复合数据的表名和键
    public string TableName { get; }
    public string Key { get; }

    // 构造函数
    public CompositeBuilder(string tableName, string key)
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        Key = key ?? throw new ArgumentNullException(nameof(key));
    }

    // 内部构造函数，用于从字节数组解析
    private CompositeBuilder(string tableName, string key, Dictionary<string, object?> data)
    {
        TableName = tableName;
        Key = key;
        _data = data;
    }

    /// <summary>
    /// 添加键值对
    /// </summary>
    public CompositeBuilder Add<T>(string key, T value)
    {
        if (_isBuilt)
            throw new InvalidOperationException("CompositeBuilder 已构建完成，无法再修改");

        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("键不能为空", nameof(key));

        _data[key] = value;
        return this;
    }

    /// <summary>
    /// 移除键值对
    /// </summary>
    public bool Remove(string key)
    {
        if (_isBuilt)
            throw new InvalidOperationException("CompositeBuilder 已构建完成，无法再修改");

        return _data.Remove(key);
    }

    /// <summary>
    /// 清除所有键值对
    /// </summary>
    public void Clear()
    {
        if (_isBuilt)
            throw new InvalidOperationException("CompositeBuilder 已构建完成，无法再修改");

        _data.Clear();
    }

    /// <summary>
    /// 检查是否包含指定键
    /// </summary>
    public bool ContainsKey(string key)
    {
        return _data.ContainsKey(key);
    }

    /// <summary>
    /// 获取指定键的值
    /// </summary>
    public T? Get<T>(string key)
    {
        if (_data.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
                return typedValue;

            try
            {
                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default(T);
            }
        }
        return default(T);
    }

    /// <summary>
    /// 构建复合数据的字节数组表示
    /// 格式：@[entryCount:4][keyLength:2][keyBytes][valueType:1][valueLength:4][valueBytes]@...
    /// </summary>
    public byte[] Build()
    {
        _isBuilt = true;

        var buffer = new List<byte>();

        // 添加起始分隔符
        buffer.Add((byte)'@');

        // 写入条目数量 (4 bytes)
        var entryCountBytes = BitConverter.GetBytes(_data.Count);
        buffer.AddRange(entryCountBytes);

        foreach (var kvp in _data)
        {
            // 写入键
            var keyBytes = Encoding.UTF8.GetBytes(kvp.Key);
            var keyLengthBytes = BitConverter.GetBytes((ushort)keyBytes.Length);
            buffer.AddRange(keyLengthBytes);
            buffer.AddRange(keyBytes);

            // 写入值
            var (valueType, valueBytes) = SerializeValue(kvp.Value);
            buffer.Add(valueType);

            var valueLengthBytes = BitConverter.GetBytes(valueBytes.Length);
            buffer.AddRange(valueLengthBytes);
            buffer.AddRange(valueBytes);
        }

        // 添加结束分隔符
        buffer.Add((byte)'@');

        return buffer.ToArray();
    }

    /// <summary>
    /// 从字节数组解析复合数据
    /// </summary>
    public static CompositeBuilder? Parse(byte[] data, string tableName = "", string key = "")
    {
        if (data == null || data.Length < 2) return null;

        try
        {
            var index = 0;

            // 检查起始分隔符
            if (data[index] != '@') return null;
            index++;

            // 读取条目数量
            if (index + 4 > data.Length) return null;
            var entryCount = BitConverter.ToInt32(data, index);
            index += 4;

            var parsedData = new Dictionary<string, object?>();

            for (int i = 0; i < entryCount; i++)
            {
                // 读取键长度
                if (index + 2 > data.Length) return null;
                var keyLength = BitConverter.ToUInt16(data, index);
                index += 2;

                // 读取键
                if (index + keyLength > data.Length) return null;
                var keyBytes = new byte[keyLength];
                Array.Copy(data, index, keyBytes, 0, keyLength);
                var keyString = Encoding.UTF8.GetString(keyBytes);
                index += keyLength;

                // 读取值类型
                if (index >= data.Length) return null;
                var valueType = data[index];
                index++;

                // 读取值长度
                if (index + 4 > data.Length) return null;
                var valueLength = BitConverter.ToInt32(data, index);
                index += 4;

                // 读取值
                if (index + valueLength > data.Length) return null;
                var valueBytes = new byte[valueLength];
                Array.Copy(data, index, valueBytes, 0, valueLength);
                index += valueLength;

                var value = DeserializeValue(valueType, valueBytes);
                parsedData[keyString] = value;
            }

            // 检查结束分隔符
            if (index >= data.Length || data[index] != '@') return null;

            return new CompositeBuilder(tableName, key, parsedData);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 序列化值
    /// </summary>
    private static (byte type, byte[] bytes) SerializeValue(object? value)
    {
        if (value == null)
            return (0, Array.Empty<byte>());

        return value switch
        {
            string str => (1, Encoding.UTF8.GetBytes(str)),
            int i => (2, BitConverter.GetBytes(i)),
            long l => (3, BitConverter.GetBytes(l)),
            float f => (4, BitConverter.GetBytes(f)),
            double d => (5, BitConverter.GetBytes(d)),
            bool b => (6, BitConverter.GetBytes(b)),
            byte[] bytes => (7, bytes),
            _ => (1, Encoding.UTF8.GetBytes(value.ToString() ?? ""))
        };
    }

    /// <summary>
    /// 反序列化值
    /// </summary>
    private static object? DeserializeValue(byte type, byte[] bytes)
    {
        if (bytes.Length == 0) return null;

        return type switch
        {
            0 => null,
            1 => Encoding.UTF8.GetString(bytes),
            2 => BitConverter.ToInt32(bytes, 0),
            3 => BitConverter.ToInt64(bytes, 0),
            4 => BitConverter.ToSingle(bytes, 0),
            5 => BitConverter.ToDouble(bytes, 0),
            6 => BitConverter.ToBoolean(bytes, 0),
            7 => bytes,
            _ => Encoding.UTF8.GetString(bytes)
        };
    }

    /// <summary>
    /// 调试输出当前数据内容
    /// </summary>
    public void Dump()
    {
        Console.WriteLine($"CompositeBuilder - Table: {TableName}, Key: {Key}");
        Console.WriteLine("Data:");
        foreach (var kvp in _data)
        {
            var valueStr = kvp.Value switch
            {
                byte[] bytes => $"byte[{bytes.Length}]",
                null => "null",
                _ => kvp.Value.ToString()
            };
            Console.WriteLine($"  {kvp.Key}: {valueStr} ({kvp.Value?.GetType().Name ?? "null"})");
        }
    }

    /// <summary>
    /// 以只读字典形式导出当前数据（用于外部枚举和调试）
    /// </summary>
    public IReadOnlyDictionary<string, object?> AsDictionary()
    {
        return new ReadOnlyDictionary<string, object?>(_data);
    }
}
