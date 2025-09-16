using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Buffers.Text;
using System.Threading;
using System.IO.Compression;

namespace Drx.Sdk.Shared.Serialization
{
    /// <summary>
    /// Drx 序列化数据类（基础实现）。
    /// 说明：实现了一个轻量级键值存储，支持基本类型与嵌套对象的序列化/反序列化。
    /// 当前为基础骨架，后续可以按设计文档扩展优化（字符串表、变长编码、数组等）。
    /// </summary>
    public class DrxSerializationData
    {
        // 内部值类型标签
        /// <summary>
        /// 值类型枚举：表示 DrxValue 可以承载的数据类型。
        /// </summary>
        public enum ValueType : byte
        {
            Null = 0,
            Int64 = 1,
            Double = 2,
            Bool = 3,
            String = 4,
            Bytes = 5,
            Array = 6,
            Object = 7,
        }

        // 值的容器（不可变语义）
        /// <summary>
        /// 值容器（不可变语义），用于在 DrxSerializationData 中表示不同类型的值。
        /// </summary>
        public readonly struct DrxValue
        {
            public ValueType Type { get; }

            private readonly long _i64;
            private readonly double _dbl;
            private readonly bool _b;
            private readonly string? _s;
            private readonly byte[]? _bytes;
            private readonly DrxSerializationData? _obj;
            private readonly DrxValue[]? _arr;

            /// <summary>
            /// 使用 64 位有符号整数初始化值容器。
            /// </summary>
            /// <param name="v">要保存的整数值。</param>
            public DrxValue(long v)
            {
                Type = ValueType.Int64;
                _i64 = v;
                _dbl = default;
                _b = default;
                _s = null;
                _bytes = null;
                _obj = null;
            }

            /// <summary>
            /// 使用双精度浮点数初始化值容器。
            /// </summary>
            /// <param name="v">要保存的双精度浮点值。</param>
            public DrxValue(double v)
            {
                Type = ValueType.Double;
                _dbl = v;
                _i64 = default;
                _b = default;
                _s = null;
                _bytes = null;
                _obj = null;
            }

            /// <summary>
            /// 使用布尔值初始化值容器。
            /// </summary>
            /// <param name="v">要保存的布尔值。</param>
            public DrxValue(bool v)
            {
                Type = ValueType.Bool;
                _b = v;
                _i64 = default;
                _dbl = default;
                _s = null;
                _bytes = null;
                _obj = null;
            }

            /// <summary>
            /// 使用字符串初始化值容器。若传入 null，则表示 Null 类型。
            /// </summary>
            /// <param name="s">要保存的字符串，允许为 null。</param>
            public DrxValue(string? s)
            {
                if (s is null)
                {
                    Type = ValueType.Null;
                    _s = null;
                    _bytes = null;
                    _obj = null;
                    _i64 = default;
                    _dbl = default;
                    _b = default;
                }
                else
                {
                    Type = ValueType.String;
                    _s = s;
                    _bytes = null;
                    _obj = null;
                    _i64 = default;
                    _dbl = default;
                    _b = default;
                }
            }

            /// <summary>
            /// 使用字节数组初始化值容器。若传入 null，则表示 Null 类型。
            /// </summary>
            /// <param name="bytes">要保存的字节数组，允许为 null。</param>
            public DrxValue(byte[]? bytes)
            {
                Type = bytes is null ? ValueType.Null : ValueType.Bytes;
                _bytes = bytes;
                _s = null;
                _obj = null;
                _arr = null;
                _i64 = default;
                _dbl = default;
                _b = default;
            }

            /// <summary>
            /// 使用 DrxValue 数组初始化值容器。若传入 null，则表示 Null 类型。
            /// </summary>
            /// <param name="arr">要保存的值数组，允许为 null；方法会克隆数组以保持不可变语义。</param>
            public DrxValue(DrxValue[] arr)
            {
                if (arr is null)
                {
                    Type = ValueType.Null;
                    _arr = null;
                }
                else
                {
                    Type = ValueType.Array;
                    // 复制数组以保持不可变语义
                    _arr = (DrxValue[])arr.Clone();
                }

                _obj = null;
                _s = null;
                _bytes = null;
                _i64 = default;
                _dbl = default;
                _b = default;
            }

            /// <summary>
            /// 使用嵌套对象初始化值容器。若传入 null，则表示 Null 类型。
            /// </summary>
            /// <param name="obj">要保存的嵌套对象，允许为 null。</param>
            public DrxValue(DrxSerializationData obj)
            {
                Type = obj is null ? ValueType.Null : ValueType.Object;
                _obj = obj;
                _s = null;
                _bytes = null;
                _i64 = default;
                _dbl = default;
                _b = default;
            }

            /// <summary>
            /// 将当前值作为 Int64 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 Int64 值。</returns>
            public long AsInt64() => _i64;
            /// <summary>
            /// 将当前值作为 Double 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 Double 值。</returns>
            public double AsDouble() => _dbl;
            /// <summary>
            /// 将当前值作为 Bool 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 Bool 值。</returns>
            public bool AsBool() => _b;
            /// <summary>
            /// 将当前值作为字符串返回（无类型检查）。
            /// </summary>
            /// <returns>内部字符串，可能为 null。</returns>
            public string? AsString() => _s;
            /// <summary>
            /// 将当前值作为字节数组返回（无类型检查）。
            /// </summary>
            /// <returns>内部字节数组，可能为 null。</returns>
            public byte[]? AsBytes() => _bytes;
            /// <summary>
            /// 将当前值作为嵌套对象返回（无类型检查）。
            /// </summary>
            /// <returns>内部嵌套对象，可能为 null。</returns>
            public DrxSerializationData? AsObject() => _obj;
            /// <summary>
            /// 将当前值作为值数组返回（无类型检查）。
            /// </summary>
            /// <returns>内部值数组，可能为 null。</returns>
            public DrxValue[]? AsArray() => _arr;
        }

        private readonly Dictionary<string, DrxValue> _map;
        private readonly ReaderWriterLockSlim _lock = new();

        /// <summary>
        /// 创建一个空的 DrxSerializationData 实例，使用默认容量。
        /// </summary>
        public DrxSerializationData()
        {
            _map = new Dictionary<string, DrxValue>(StringComparer.Ordinal);
        }

        /// <summary>
        /// 创建一个具有指定初始容量的 DrxSerializationData 实例。
        /// </summary>
        /// <param name="capacity">内部字典的初始容量。</param>
        public DrxSerializationData(int capacity)
        {
            _map = new Dictionary<string, DrxValue>(capacity, StringComparer.Ordinal);
        }

        // 基础操作
        /// <summary>
        /// 设置指定键的字符串值。若 value 为 null，则存储为 Null 类型。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">字符串值，允许为 null 表示 Null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetString(string key, string? value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的 64 位整数值。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">要保存的整数值。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetInt(string key, long value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的双精度浮点值。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">要保存的 double 值。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetDouble(string key, double value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的布尔值。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">要保存的布尔值。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetBool(string key, bool value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的字节数组。方法会默认克隆传入数组以保证安全语义。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="bytes">字节数组，允许为 null 表示 Null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetBytes(string key, byte[]? bytes)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            // 默认复制以保持安全；若性能关键可增加不复制的重载
            var payload = bytes is null ? null : (byte[])bytes.Clone();
            var v = new DrxValue(payload!);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的单精度浮点值（内部以 double 存储）。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">要保存的 float 值。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetFloat(string key, float value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue((double)value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的嵌套对象值。传入 null 表示 Null。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="obj">嵌套对象，允许为 null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetObject(string key, DrxSerializationData? obj)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(obj!);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的值数组（DrxValue[]）。传入 null 表示 Null。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="arr">DrxValue 数组，允许为 null；方法会克隆数组以保持不可变语义。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetArray(string key, DrxValue[]? arr)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(arr!);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        // 原生数组重载：便捷方法，将原生数组转换为内部 DrxValue[] 存储
        /// <summary>
        /// 设置指定键的 64 位整数数组（便捷重载）。若传入 null 则表示 Null。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="arr">长整型数组，允许为 null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetArray(string key, long[]? arr)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (arr is null)
            {
                SetArray(key, (DrxValue[]?)null);
                return;
            }
            var va = new DrxValue[arr.Length];
            for (int i = 0; i < arr.Length; i++) va[i] = new DrxValue(arr[i]);
            SetArray(key, va);
        }

        /// <summary>
        /// 设置指定键的双精度浮点数组（便捷重载）。若传入 null 则表示 Null。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="arr">double 数组，允许为 null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetArray(string key, double[]? arr)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (arr is null)
            {
                SetArray(key, (DrxValue[]?)null);
                return;
            }
            var va = new DrxValue[arr.Length];
            for (int i = 0; i < arr.Length; i++) va[i] = new DrxValue(arr[i]);
            SetArray(key, va);
        }

        /// <summary>
        /// 设置指定键的布尔数组（便捷重载）。若传入 null 则表示 Null。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="arr">bool 数组，允许为 null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetArray(string key, bool[]? arr)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (arr is null)
            {
                SetArray(key, (DrxValue[]?)null);
                return;
            }
            var va = new DrxValue[arr.Length];
            for (int i = 0; i < arr.Length; i++) va[i] = new DrxValue(arr[i]);
            SetArray(key, va);
        }

        /// <summary>
        /// 设置指定键的字符串数组（便捷重载）。若传入 null 则表示 Null，数组元素允许为 null。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="arr">字符串数组，允许为 null 且元素可为 null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetArray(string key, string?[]? arr)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (arr is null)
            {
                SetArray(key, (DrxValue[]?)null);
                return;
            }
            var va = new DrxValue[arr.Length];
            for (int i = 0; i < arr.Length; i++) va[i] = new DrxValue(arr[i]);
            SetArray(key, va);
        }

        /// <summary>
        /// 设置指定键的字节数组数组（便捷重载）。若传入 null 则表示 Null，内部会克隆每个字节数组。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="arr">字节数组集合，允许为 null 且元素可为 null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetArray(string key, byte[][]? arr)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (arr is null)
            {
                SetArray(key, (DrxValue[]?)null);
                return;
            }
            var va = new DrxValue[arr.Length];
            for (int i = 0; i < arr.Length; i++) va[i] = new DrxValue(arr[i] is null ? null : (byte[])arr[i].Clone());
            SetArray(key, va);
        }

        /// <summary>
        /// 设置指定键的嵌套对象数组（便捷重载）。若传入 null 则表示 Null，数组元素允许为 null。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="arr">嵌套对象数组，允许为 null 且元素可为 null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetArray(string key, DrxSerializationData?[]? arr)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (arr is null)
            {
                SetArray(key, (DrxValue[]?)null);
                return;
            }
            var va = new DrxValue[arr.Length];
            for (int i = 0; i < arr.Length; i++) va[i] = new DrxValue(arr[i]!);
            SetArray(key, va);
        }

        /// <summary>
        /// 从当前对象中移除指定键及其值（如果存在）。
        /// </summary>
        /// <param name="key">要移除的键，不能为空。</param>
        /// <returns>如果键存在并被移除则返回 true，否则返回 false。</returns>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public bool Remove(string key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            _lock.EnterWriteLock();
            try { return _map.Remove(key); }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 检查当前对象是否包含指定键。
        /// </summary>
        /// <param name="key">要检查的键，不能为空。</param>
        /// <returns>如果包含该键则返回 true，否则返回 false。</returns>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public bool ContainsKey(string key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            _lock.EnterReadLock();
            try { return _map.ContainsKey(key); }
            finally { _lock.ExitReadLock(); }
        }

        /// <summary>
        /// 清空当前对象中的所有键值对。
        /// </summary>
        public void Clear()
        {
            _lock.EnterWriteLock();
            try { _map.Clear(); }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 尝试按键获取通用 DrxValue 值。
        /// </summary>
        /// <param name="key">要获取的键，不能为空。</param>
        /// <param name="value">若存在则输出对应的 DrxValue，否则输出默认值。</param>
        /// <returns>存在则返回 true，否则返回 false。</returns>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public bool TryGet(string key, out DrxValue value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            _lock.EnterReadLock();
            try
            {
                if (_map.TryGetValue(key, out value)) return true;
                value = default;
                return false;
            }
            finally { _lock.ExitReadLock(); }
        }

        /// <summary>
        /// 尝试按键获取字符串值（仅在值类型为 String 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">输出字符串，或 null。</param>
        /// <returns>成功返回 true，否则返回 false。</returns>
        public bool TryGetString(string key, out string? value)
        {
            if (TryGet(key, out var v) && v.Type == ValueType.String)
            {
                value = v.AsString();
                return true;
            }
            value = null;
            return false;
        }

        /// <summary>
        /// 尝试按键获取 64 位整数值（仅在值类型为 Int64 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">输出整数值。</param>
        /// <returns>成功返回 true，否则返回 false。</returns>
        public bool TryGetInt(string key, out long value)
        {
            if (TryGet(key, out var v) && v.Type == ValueType.Int64)
            {
                value = v.AsInt64();
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// 尝试按键获取单精度浮点值（仅在值类型为 Double 时成功并转换为 float）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">输出 float 值。</param>
        /// <returns>成功返回 true，否则返回 false。</returns>
        public bool TryGetFloat(string key, out float value)
        {
            if (TryGet(key, out var v) && v.Type == ValueType.Double)
            {
                value = (float)v.AsDouble();
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// 尝试按键获取双精度浮点值（仅在值类型为 Double 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">输出 double 值。</param>
        /// <returns>成功返回 true，否则返回 false。</returns>
        public bool TryGetDouble(string key, out double value)
        {
            if (TryGet(key, out var v) && v.Type == ValueType.Double)
            {
                value = v.AsDouble();
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// 尝试按键获取布尔值（仅在值类型为 Bool 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">输出布尔值。</param>
        /// <returns>成功返回 true，否则返回 false。</returns>
        public bool TryGetBool(string key, out bool value)
        {
            if (TryGet(key, out var v) && v.Type == ValueType.Bool)
            {
                value = v.AsBool();
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// 尝试按键获取字节数组（仅在值类型为 Bytes 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">输出字节数组，可能为 null。</param>
        /// <returns>成功返回 true，否则返回 false。</returns>
        public bool TryGetBytes(string key, out byte[]? value)
        {
            if (TryGet(key, out var v) && v.Type == ValueType.Bytes)
            {
                value = v.AsBytes();
                return true;
            }
            value = null;
            return false;
        }

        /// <summary>
        /// 尝试按键获取嵌套对象（仅在值类型为 Object 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">输出嵌套对象，可能为 null。</param>
        /// <returns>成功返回 true，否则返回 false。</returns>
        public bool TryGetObject(string key, out DrxSerializationData? value)
        {
            if (TryGet(key, out var v) && v.Type == ValueType.Object)
            {
                value = v.AsObject();
                return true;
            }
            value = null;
            return false;
        }

        /// <summary>
        /// 尝试按键获取值数组（仅在值类型为 Array 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">输出 DrxValue 数组，可能为 null。</param>
        /// <returns>成功返回 true，否则返回 false。</returns>
        public bool TryGetArray(string key, out DrxValue[]? value)
        {
            if (TryGet(key, out var v) && v.Type == ValueType.Array)
            {
                value = v.AsArray();
                return true;
            }
            value = null;
            return false;
        }

        // 原生类型数组读取方法（若类型不一致则返回 false）
        /// <summary>
        /// 尝试按键获取 64 位整数数组；当数组元素类型均为 Int64 时成功。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">输出 long 数组，或 null。</param>
        /// <returns>成功返回 true，否则返回 false（包括类型不匹配）。</returns>
        public bool TryGetLongArray(string key, out long[]? value)
        {
            value = null;
            if (!TryGetArray(key, out var arr) || arr is null) return false;
            var outArr = new long[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].Type != ValueType.Int64) return false;
                outArr[i] = arr[i].AsInt64();
            }
            value = outArr;
            return true;
        }

        /// <summary>
        /// 尝试按键获取 double 数组；当数组元素类型均为 Double 时成功。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">输出 double 数组，或 null。</param>
        /// <returns>成功返回 true，否则返回 false（包括类型不匹配）。</returns>
        public bool TryGetDoubleArray(string key, out double[]? value)
        {
            value = null;
            if (!TryGetArray(key, out var arr) || arr is null) return false;
            var outArr = new double[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].Type != ValueType.Double) return false;
                outArr[i] = arr[i].AsDouble();
            }
            value = outArr;
            return true;
        }

        /// <summary>
        /// 尝试按键获取 bool 数组；当数组元素类型均为 Bool 时成功。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">输出 bool 数组，或 null。</param>
        /// <returns>成功返回 true，否则返回 false（包括类型不匹配）。</returns>
        public bool TryGetBoolArray(string key, out bool[]? value)
        {
            value = null;
            if (!TryGetArray(key, out var arr) || arr is null) return false;
            var outArr = new bool[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].Type != ValueType.Bool) return false;
                outArr[i] = arr[i].AsBool();
            }
            value = outArr;
            return true;
        }

        /// <summary>
        /// 尝试按键获取字符串数组；支持数组元素为 null。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">输出字符串数组，或 null。</param>
        /// <returns>成功返回 true，否则返回 false（包括类型不匹配）。</returns>
        public bool TryGetStringArray(string key, out string?[]? value)
        {
            value = null;
            if (!TryGetArray(key, out var arr) || arr is null) return false;
            var outArr = new string?[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].Type == ValueType.Null)
                {
                    outArr[i] = null;
                    continue;
                }
                if (arr[i].Type != ValueType.String) return false;
                outArr[i] = arr[i].AsString();
            }
            value = outArr;
            return true;
        }

        /// <summary>
        /// 尝试按键获取字节数组数组；支持元素为 null。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">输出字节数组集合，或 null。</param>
        /// <returns>成功返回 true，否则返回 false（包括类型不匹配）。</returns>
        public bool TryGetBytesArray(string key, out byte[][]? value)
        {
            value = null;
            if (!TryGetArray(key, out var arr) || arr is null) return false;
            var outArr = new byte[arr.Length][];
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].Type == ValueType.Null)
                {
                    outArr[i] = null!;
                    continue;
                }
                if (arr[i].Type != ValueType.Bytes) return false;
                outArr[i] = arr[i].AsBytes()!;
            }
            value = outArr;
            return true;
        }

        /// <summary>
        /// 尝试按键获取嵌套对象数组；支持元素为 null。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <param name="value">输出嵌套对象数组，或 null。</param>
        /// <returns>成功返回 true，否则返回 false（包括类型不匹配）。</returns>
        public bool TryGetObjectArray(string key, out DrxSerializationData?[]? value)
        {
            value = null;
            if (!TryGetArray(key, out var arr) || arr is null) return false;
            var outArr = new DrxSerializationData?[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].Type == ValueType.Null)
                {
                    outArr[i] = null;
                    continue;
                }
                if (arr[i].Type != ValueType.Object) return false;
                outArr[i] = arr[i].AsObject();
            }
            value = outArr;
            return true;
        }

        // 序列化格式（简单实现，用于 PoC）：
        // 新的流式序列化格式（支持引用表与流式写入）：
        // Top-level: [int32 entryCount] then for each entry: [int32 keyLen][keyUtf8][1 byte type][payload]
        // payload per type:
        //   Int64: 8 bytes (little endian)
        //   Double: 8 bytes (IEEE 754)
        //   Bool: 1 byte
        //   String: [int32 len][utf8 bytes]
        //   Bytes: [int32 len][raw bytes]
        //   Object: [1 byte objFlag][..]
        //     objFlag: 0 = null
        //              1 = new object -> [int32 id][int32 entryCount][entries...]
        //              2 = reference -> [int32 id]
        /// <summary>
        /// 将当前对象序列化为字节数组（使用当前实现的 compact 格式）。
        /// </summary>
        /// <returns>序列化后的字节数组。</returns>
        public byte[] Serialize()
        {
            using var ms = new MemoryStream();
            SerializeTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// 将当前对象以序列化格式写入指定的流（不关闭流）。
        /// </summary>
        /// <param name="stream">目标流，不能为空。</param>
        /// <exception cref="ArgumentNullException">当 stream 为 null 时抛出。</exception>
        public void SerializeTo(Stream stream)
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));

            // 新格式魔数 + 版本（兼容新实现）
            stream.WriteByte(0xDA);
            stream.WriteByte(0x59);
            stream.WriteByte(0x01); // version 1 => varint + string table

            // 先遍历对象图，分配对象 id，并收集所有字符串（keys + string values）
            var objToId = new Dictionary<DrxSerializationData, int>(ReferenceEqualityComparer<DrxSerializationData>.Default);
            int nextObjId = 1;
            var stringToId = new Dictionary<string, int>(StringComparer.Ordinal);
            int nextStrId = 1;

            void AddString(string? s)
            {
                if (s is null) return;
                if (!stringToId.ContainsKey(s)) stringToId[s] = nextStrId++;
            }

            void VisitObject(DrxSerializationData o)
            {
                if (o is null) return;
                if (objToId.ContainsKey(o)) return;
                objToId[o] = nextObjId++;

                // 收集该对象的 keys 与直接 string 值，递归收集嵌套对象/数组中的字符串
                o._lock.EnterReadLock();
                try
                {
                    foreach (var kv in o._map)
                    {
                        AddString(kv.Key);
                        var v = kv.Value;
                        if (v.Type == ValueType.String)
                        {
                            AddString(v.AsString());
                        }
                        else if (v.Type == ValueType.Object)
                        {
                            var child = v.AsObject();
                            if (child != null) VisitObject(child);
                        }
                        else if (v.Type == ValueType.Array)
                        {
                            var arr = v.AsArray();
                            if (arr != null)
                            {
                                foreach (var e in arr)
                                {
                                    if (e.Type == ValueType.String) AddString(e.AsString());
                                    else if (e.Type == ValueType.Object)
                                    {
                                        var co = e.AsObject();
                                        if (co != null) VisitObject(co);
                                    }
                                }
                            }
                        }
                    }
                }
                finally { o._lock.ExitReadLock(); }
            }

            // 从根对象开始遍历（VisitObject 会为每个对象单独加读锁，避免在此处持有根锁以免递归申请）
            VisitObject(this);

            // 写入字符串表（id 从 1 开始）
            // 格式： per-entry [1 byte flag][varuint len][bytes]
            // flag: 0 = raw utf8, 1 = deflate-compressed
            WriteVarUInt(stream, (uint)stringToId.Count);
            // 保证写入顺序稳定：按插入顺序写入字典迭代即可
            const int CompressThreshold = 64; // 大于等于该阈值时尝试压缩
            foreach (var kv in stringToId)
            {
                var str = kv.Key;
                var bcount = Encoding.UTF8.GetByteCount(str);
                if (bcount == 0)
                {
                    // flag 0 + len 0
                    stream.WriteByte(0);
                    WriteVarUInt(stream, 0);
                    continue;
                }

                // 将字符串编码到租用缓冲区
                var rent = ArrayPool<byte>.Shared.Rent(bcount);
                int wrote = 0;
                try
                {
                    wrote = Encoding.UTF8.GetBytes(str, 0, str.Length, rent, 0);
                    // 尝试压缩（仅在达到阈值时）
                    if (bcount >= CompressThreshold)
                    {
                        using var msComp = new MemoryStream();
                        using (var ds = new DeflateStream(msComp, CompressionLevel.Fastest, true))
                        {
                            ds.Write(rent, 0, wrote);
                        }
                        var comp = msComp.ToArray();
                        if (comp.Length < wrote)
                        {
                            // 写入压缩标记与压缩后长度与内容
                            stream.WriteByte(1);
                            WriteVarUInt(stream, (uint)comp.Length);
                            if (comp.Length > 0) stream.Write(comp, 0, comp.Length);
                            continue;
                        }
                        // 否则回退为未压缩路径
                    }

                    // 未压缩写入（flag 0）
                    stream.WriteByte(0);
                    WriteVarUInt(stream, (uint)wrote);
                    if (wrote > 0)
                    {
                        stream.Write(rent, 0, wrote);
                    }
                }
                finally { ArrayPool<byte>.Shared.Return(rent); }
            }

            // 写入顶层条目数（varuint）
            WriteVarUInt(stream, (uint)_map.Count);

            // 在写入对象时需要记录已经序列化过的对象 id，以在遇到重复时写 reference
            var emitted = new HashSet<int>();

            // 仅在枚举顶层 _map 时取得读锁，避免对同一对象重复申请读锁导致 LockRecursionException
            _lock.EnterReadLock();
            try
            {
                foreach (var kv in _map)
                {
                    var keyId = stringToId[kv.Key];
                    WriteVarUInt(stream, (uint)keyId);
                    stream.WriteByte((byte)kv.Value.Type);
                    WriteValuePayloadCompact(stream, kv.Value, objToId, emitted, stringToId);
                }
            }
            finally { _lock.ExitReadLock(); }
        }

        // 写入单个值的 payload（不包含前导的 type 字节） - 旧版（保留用于向后兼容）
        private static void WriteValuePayload(Stream s, DrxValue v, Dictionary<DrxSerializationData, int> objToId, ref int nextId)
        {
            switch (v.Type)
            {
                case ValueType.Null:
                    break;
                case ValueType.Int64:
                    WriteInt64(s, v.AsInt64());
                    break;
                case ValueType.Double:
                    WriteDouble(s, v.AsDouble());
                    break;
                case ValueType.Bool:
                    s.WriteByte((byte)(v.AsBool() ? 1 : 0));
                    break;
                case ValueType.String:
                    var ss = v.AsString() ?? string.Empty;
                    var sl = Encoding.UTF8.GetByteCount(ss);
                    WriteInt32(s, sl);
                    if (sl > 0)
                    {
                        var rent = ArrayPool<byte>.Shared.Rent(sl);
                        try
                        {
                            var written = Encoding.UTF8.GetBytes(ss, 0, ss.Length, rent, 0);
                            s.Write(rent, 0, written);
                        }
                        finally { ArrayPool<byte>.Shared.Return(rent); }
                    }
                    break;
                case ValueType.Bytes:
                    var b = v.AsBytes() ?? Array.Empty<byte>();
                    WriteInt32(s, b.Length);
                    if (b.Length > 0) s.Write(b, 0, b.Length);
                    break;
                case ValueType.Object:
                    WriteObjectPayload(s, v.AsObject(), objToId, ref nextId);
                    break;
                case ValueType.Array:
                    var arr = v.AsArray() ?? Array.Empty<DrxValue>();
                    WriteInt32(s, arr.Length);
                    foreach (var elem in arr)
                    {
                        s.WriteByte((byte)elem.Type);
                        WriteValuePayload(s, elem, objToId, ref nextId);
                    }
                    break;
                default:
                    throw new InvalidDataException("Unknown value type");
            }
        }

        // Compact 写入实现：使用 varuint / string table / varint 编码
        private static void WriteValuePayloadCompact(Stream s, DrxValue v, Dictionary<DrxSerializationData, int> objToId, HashSet<int> emitted, Dictionary<string, int> stringToId)
        {
            switch (v.Type)
            {
                case ValueType.Null:
                    break;
                case ValueType.Int64:
                    WriteVarInt64(s, v.AsInt64());
                    break;
                case ValueType.Double:
                    WriteDouble(s, v.AsDouble());
                    break;
                case ValueType.Bool:
                    s.WriteByte((byte)(v.AsBool() ? 1 : 0));
                    break;
                case ValueType.String:
                    var ss = v.AsString() ?? string.Empty;
                    var sid = stringToId[ss];
                    WriteVarUInt(s, (uint)sid);
                    break;
                case ValueType.Bytes:
                    var b = v.AsBytes() ?? Array.Empty<byte>();
                    WriteVarUInt(s, (uint)b.Length);
                    if (b.Length > 0) s.Write(b, 0, b.Length);
                    break;
                case ValueType.Object:
                    WriteObjectPayloadCompact(s, v.AsObject(), objToId, emitted, stringToId);
                    break;
                case ValueType.Array:
                    var arr = v.AsArray() ?? Array.Empty<DrxValue>();
                    WriteVarUInt(s, (uint)arr.Length);
                    foreach (var elem in arr)
                    {
                        s.WriteByte((byte)elem.Type);
                        WriteValuePayloadCompact(s, elem, objToId, emitted, stringToId);
                    }
                    break;
                default:
                    throw new InvalidDataException("Unknown value type");
            }
        }

        // Compact 对象写入（使用事先分配好的 objToId 与 stringToId）
        private static void WriteObjectPayloadCompact(Stream s, DrxSerializationData? obj, Dictionary<DrxSerializationData, int> objToId, HashSet<int> emitted, Dictionary<string, int> stringToId)
        {
            if (obj is null)
            {
                s.WriteByte(0); // null
                return;
            }

            if (!objToId.TryGetValue(obj, out var id))
            {
                // 不应该发生：所有对象在序列化前应已分配 id
                throw new InvalidDataException("Object id not assigned before compact serialization");
            }

            if (emitted.Contains(id))
            {
                s.WriteByte(2); // reference
                WriteVarUInt(s, (uint)id);
                return;
            }

            // new object
            emitted.Add(id);
            s.WriteByte(1);
            WriteVarUInt(s, (uint)id);

            // 写入条目数与条目
            obj._lock.EnterReadLock();
            try
            {
                WriteVarUInt(s, (uint)obj._map.Count);
                foreach (var kv in obj._map)
                {
                    var keyId = stringToId[kv.Key];
                    WriteVarUInt(s, (uint)keyId);
                    s.WriteByte((byte)kv.Value.Type);
                    WriteValuePayloadCompact(s, kv.Value, objToId, emitted, stringToId);
                }
            }
            finally { obj._lock.ExitReadLock(); }
        }

        /// <summary>
        /// 从字节数组中反序列化并返回 DrxSerializationData 实例。
        /// </summary>
        /// <param name="buffer">包含序列化数据的字节数组，不能为空。</param>
        /// <returns>反序列化得到的 DrxSerializationData。</returns>
        /// <exception cref="ArgumentNullException">当 buffer 为 null 时抛出。</exception>
        public static DrxSerializationData Deserialize(byte[] buffer)
        {
            if (buffer is null) throw new ArgumentNullException(nameof(buffer));
            using var ms = new MemoryStream(buffer, false);
            return DeserializeFrom(ms);
        }

        #region Binary Helpers
        private static void WriteInt32(Stream s, int v)
        {
            Span<byte> buf = stackalloc byte[4];
            BitConverter.TryWriteBytes(buf, v);
            s.Write(buf);
        }

        private static int ReadInt32(Stream s)
        {
            Span<byte> buf = stackalloc byte[4];
            int read = s.Read(buf);
            if (read != 4) throw new InvalidDataException("Unexpected end of stream reading Int32");
            return BitConverter.ToInt32(buf);
        }

        private static void WriteInt64(Stream s, long v)
        {
            Span<byte> buf = stackalloc byte[8];
            BitConverter.TryWriteBytes(buf, v);
            s.Write(buf);
        }

        private static long ReadInt64(Stream s)
        {
            Span<byte> buf = stackalloc byte[8];
            int read = s.Read(buf);
            if (read != 8) throw new InvalidDataException("Unexpected end of stream reading Int64");
            return BitConverter.ToInt64(buf);
        }

        private static void WriteDouble(Stream s, double v)
        {
            Span<byte> buf = stackalloc byte[8];
            BitConverter.TryWriteBytes(buf, v);
            s.Write(buf);
        }

        private static double ReadDouble(Stream s)
        {
            Span<byte> buf = stackalloc byte[8];
            int read = s.Read(buf);
            if (read != 8) throw new InvalidDataException("Unexpected end of stream reading Double");
            return BitConverter.ToDouble(buf);
        }

        // --- Varint helpers（LEB128 风格） ---
        private static void WriteVarUInt(Stream s, uint value)
        {
            while (value >= 0x80)
            {
                s.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            s.WriteByte((byte)value);
        }

        private static uint ReadVarUInt(Stream s)
        {
            uint result = 0;
            int shift = 0;
            while (true)
            {
                int b = s.ReadByte();
                if (b < 0) throw new InvalidDataException("Unexpected end of stream reading varuint");
                result |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
                if (shift > 35) throw new InvalidDataException("VarUInt too long");
            }
            return result;
        }

        // ZigZag encode for signed integers, then store as varuint
        private static void WriteVarInt64(Stream s, long value)
        {
            ulong zig = (ulong)((value << 1) ^ (value >> 63));
            while (zig >= 0x80)
            {
                s.WriteByte((byte)(zig | 0x80));
                zig >>= 7;
            }
            s.WriteByte((byte)zig);
        }

        private static long ReadVarInt64(Stream s)
        {
            ulong result = 0;
            int shift = 0;
            while (true)
            {
                int b = s.ReadByte();
                if (b < 0) throw new InvalidDataException("Unexpected end of stream reading varint64");
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
                if (shift > 70) throw new InvalidDataException("VarInt64 too long");
            }
            // ZigZag decode
            long value = (long)((result >> 1) ^ (~(result & 1) + 1));
            return value;
        }

        // 引用相等比较器（用于字典键为对象引用）
        private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public static readonly ReferenceEqualityComparer<T> Default = new ReferenceEqualityComparer<T>();
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        // 写入对象载荷（支持引用表）
        private static void WriteObjectPayload(Stream s, DrxSerializationData? obj, Dictionary<DrxSerializationData, int> objToId, ref int nextId)
        {
            if (obj is null)
            {
                s.WriteByte(0); // null
                return;
            }

            if (objToId.TryGetValue(obj, out var existingId))
            {
                s.WriteByte(2); // reference
                WriteInt32(s, existingId);
                return;
            }

            // new object
            var id = nextId++;
            objToId[obj] = id;
            s.WriteByte(1);
            WriteInt32(s, id);

            // 写入对象的条目数和条目
            // 直接访问内部_map 以避免为每个条目重复加锁；但是我们将进入读锁以保证一致性
            obj._lock.EnterReadLock();
            try
            {
                WriteInt32(s, obj._map.Count);
                foreach (var kv in obj._map)
                {
                    var keyLen = Encoding.UTF8.GetByteCount(kv.Key);
                    WriteInt32(s, keyLen);
                    var keyBuf = ArrayPool<byte>.Shared.Rent(keyLen);
                    try
                    {
                        var wrote = Encoding.UTF8.GetBytes(kv.Key, 0, kv.Key.Length, keyBuf, 0);
                        s.Write(keyBuf, 0, wrote);
                    }
                    finally { ArrayPool<byte>.Shared.Return(keyBuf); }

                    s.WriteByte((byte)kv.Value.Type);
                    switch (kv.Value.Type)
                    {
                        case ValueType.Null:
                            break;
                        case ValueType.Int64:
                            WriteInt64(s, kv.Value.AsInt64());
                            break;
                        case ValueType.Double:
                            WriteDouble(s, kv.Value.AsDouble());
                            break;
                        case ValueType.Bool:
                            s.WriteByte((byte)(kv.Value.AsBool() ? 1 : 0));
                            break;
                        case ValueType.String:
                            var str = kv.Value.AsString() ?? string.Empty;
                            var strLen = Encoding.UTF8.GetByteCount(str);
                            WriteInt32(s, strLen);
                            if (strLen > 0)
                            {
                                var rentStr = ArrayPool<byte>.Shared.Rent(strLen);
                                try
                                {
                                    var wrote = Encoding.UTF8.GetBytes(str, 0, str.Length, rentStr, 0);
                                    s.Write(rentStr, 0, wrote);
                                }
                                finally { ArrayPool<byte>.Shared.Return(rentStr); }
                            }
                            break;
                        case ValueType.Bytes:
                            var b = kv.Value.AsBytes() ?? Array.Empty<byte>();
                            WriteInt32(s, b.Length);
                            s.Write(b, 0, b.Length);
                            break;
                        case ValueType.Object:
                            WriteObjectPayload(s, kv.Value.AsObject(), objToId, ref nextId);
                            break;
                        default:
                            throw new InvalidDataException("Unknown value type");
                    }
                }
            }
            finally { obj._lock.ExitReadLock(); }
        }

        // 流式反序列化：从流读取并返回 DrxSerializationData
        // 支持旧格式（no header，原实现）和新格式（magic+version, varint + string table）
        /// <summary>
        /// 从流中反序列化并返回 DrxSerializationData 实例。支持旧格式与新版 compact 格式。
        /// </summary>
        /// <param name="s">用于读取序列化数据的流，不能为空。</param>
        /// <returns>反序列化得到的 DrxSerializationData。</returns>
        /// <exception cref="ArgumentNullException">当 s 为 null 时抛出。</exception>
        public static DrxSerializationData DeserializeFrom(Stream s)
        {
            if (s is null) throw new ArgumentNullException(nameof(s));

            // 为了兼容非 seekable stream，我们先尝试读取前两字节进行魔数判断。
            int b1 = s.ReadByte();
            if (b1 < 0) throw new InvalidDataException("Unexpected end of stream");
            int b2 = s.ReadByte();
            if (b2 < 0) throw new InvalidDataException("Unexpected end of stream");

            // 如果是新格式魔数 0xDA 0x59，继续读取版本并走 compact 路径
            if (b1 == 0xDA && b2 == 0x59)
            {
                int ver = s.ReadByte();
                if (ver < 0) throw new InvalidDataException("Unexpected end of stream reading version");
                if (ver != 1) throw new InvalidDataException($"Unsupported serialization version {ver}");

                var ds = new DrxSerializationData();

                // id -> object 映射，用于引用解析（compact）
                var idToObj = new Dictionary<int, DrxSerializationData>();

                // 读取字符串表
                // 格式参考写入： per-entry [1 byte flag][varuint len][bytes]
                uint strCount = ReadVarUInt(s);
                var strings = new string[strCount + 1]; // 1-based
                for (uint si = 1; si <= strCount; si++)
                {
                    int flag = s.ReadByte();
                    if (flag < 0) throw new InvalidDataException("Unexpected end of stream reading string table flag");
                    uint bl = ReadVarUInt(s);
                    if (bl == 0) { strings[si] = string.Empty; continue; }

                    var buf = ArrayPool<byte>.Shared.Rent((int)bl);
                    try
                    {
                        var r = s.Read(buf, 0, (int)bl);
                        if (r != bl) throw new InvalidDataException("Unexpected end of stream reading string table entry");
                        if (flag == 0)
                        {
                            // raw utf8
                            strings[si] = Encoding.UTF8.GetString(buf, 0, (int)bl);
                        }
                        else if (flag == 1)
                        {
                            // deflate 压缩数据，解压后再做 utf8 解码
                            using var msComp = new MemoryStream(buf, 0, (int)bl, false);
                            using var decStream = new DeflateStream(msComp, CompressionMode.Decompress);
                            using var outMs = new MemoryStream();
                            decStream.CopyTo(outMs);
                            var dec = outMs.ToArray();
                            strings[si] = Encoding.UTF8.GetString(dec, 0, dec.Length);
                        }
                        else
                        {
                            throw new InvalidDataException($"Unknown string table entry flag {flag}");
                        }
                    }
                    finally { ArrayPool<byte>.Shared.Return(buf); }
                }

                // 读取顶层条目数
                uint entryCount = ReadVarUInt(s);
                for (uint ei = 0; ei < entryCount; ei++)
                {
                    uint keyId = ReadVarUInt(s);
                    if (keyId == 0 || keyId > (uint)strings.Length - 1) throw new InvalidDataException($"Invalid keyId {keyId}");
                    var key = strings[keyId];

                    var t = s.ReadByte();
                    if (t < 0) throw new InvalidDataException("Unexpected end of stream reading type");
                    var vt = (ValueType)t;

                    switch (vt)
                    {
                        case ValueType.Null:
                            ds.SetString(key, null);
                            break;
                        case ValueType.Int64:
                            var i64 = ReadVarInt64(s);
                            ds.SetInt(key, i64);
                            break;
                        case ValueType.Double:
                            var d = ReadDouble(s);
                            ds.SetDouble(key, d);
                            break;
                        case ValueType.Bool:
                            var vb = s.ReadByte();
                            if (vb < 0) throw new InvalidDataException("Unexpected end of stream reading bool");
                            ds.SetBool(key, vb != 0);
                            break;
                        case ValueType.String:
                            var sid = ReadVarUInt(s);
                            if (sid == 0) ds.SetString(key, string.Empty);
                            else
                            {
                                if (sid > (uint)strings.Length - 1) throw new InvalidDataException($"Invalid string id {sid}");
                                ds.SetString(key, strings[sid]);
                            }
                            break;
                        case ValueType.Bytes:
                            var bl = ReadVarUInt(s);
                            if (bl == 0) { ds.SetBytes(key, Array.Empty<byte>()); break; }
                            var bBuf = ArrayPool<byte>.Shared.Rent((int)bl);
                            try
                            {
                                var br = s.Read(bBuf, 0, (int)bl);
                                if (br != bl) throw new InvalidDataException("Unexpected end of stream reading bytes");
                                var exact = new byte[bl];
                                Buffer.BlockCopy(bBuf, 0, exact, 0, (int)bl);
                                ds.SetBytes(key, exact);
                            }
                            finally { ArrayPool<byte>.Shared.Return(bBuf); }
                            break;
                        case ValueType.Array:
                            var arr = ReadArrayPayloadCompact(s, idToObj, strings);
                            ds.SetArray(key, arr);
                            break;
                        case ValueType.Object:
                            var obj = ReadObjectPayloadCompact(s, idToObj, strings);
                            ds.SetObject(key, obj);
                            break;
                        default:
                            throw new InvalidDataException("Unknown value type during deserialize (compact)");
                    }
                }

                return ds;
            }
            else
            {
                // 不是魔数 => 视为旧格式。我们已读了两个字节，需要回退或重建流。
                if (s.CanSeek)
                {
                    s.Seek(-2, SeekOrigin.Current);
                    // 继续执行旧格式解析（下面直接复用原逻辑）
                    var ds = new DrxSerializationData();

                    // id -> object 映射，用于引用解析
                    var idToObj = new Dictionary<int, DrxSerializationData>();

                    var count = ReadInt32(s);
                    for (int i = 0; i < count; i++)
                    {
                        var keyLen = ReadInt32(s);
                        var keyBuf = ArrayPool<byte>.Shared.Rent(keyLen);
                        try
                        {
                            var read = s.Read(keyBuf, 0, keyLen);
                            if (read != keyLen) throw new InvalidDataException("Unexpected end of stream reading key");
                            var key = Encoding.UTF8.GetString(keyBuf, 0, keyLen);
                            var t = s.ReadByte();
                            if (t < 0) throw new InvalidDataException("Unexpected end of stream reading type");
                            var vt = (ValueType)t;
                            switch (vt)
                            {
                                case ValueType.Null:
                                    ds.SetString(key, null);
                                    break;
                                case ValueType.Int64:
                                    var i64 = ReadInt64(s);
                                    ds.SetInt(key, i64);
                                    break;
                                case ValueType.Double:
                                    var d = ReadDouble(s);
                                    ds.SetDouble(key, d);
                                    break;
                                case ValueType.Bool:
                                    var vb = s.ReadByte();
                                    if (vb < 0) throw new InvalidDataException("Unexpected end of stream reading bool");
                                    ds.SetBool(key, vb != 0);
                                    break;
                                case ValueType.String:
                                    var sl = ReadInt32(s);
                                    if (sl == 0) { ds.SetString(key, string.Empty); break; }
                                    var sBuf = ArrayPool<byte>.Shared.Rent(sl);
                                    try
                                    {
                                        var sr = s.Read(sBuf, 0, sl);
                                        if (sr != sl) throw new InvalidDataException("Unexpected end of stream reading string");
                                        ds.SetString(key, Encoding.UTF8.GetString(sBuf, 0, sl));
                                    }
                                    finally { ArrayPool<byte>.Shared.Return(sBuf); }
                                    break;
                                case ValueType.Bytes:
                                    var bl = ReadInt32(s);
                                    if (bl == 0) { ds.SetBytes(key, Array.Empty<byte>()); break; }
                                    var bBuf = ArrayPool<byte>.Shared.Rent(bl);
                                    try
                                    {
                                        var br = s.Read(bBuf, 0, bl);
                                        if (br != bl) throw new InvalidDataException("Unexpected end of stream reading bytes");
                                        // 复制到精确长度的新数组，保持 SetBytes 的语义（它会 Clone 传入数组）
                                        var exact = new byte[bl];
                                        Buffer.BlockCopy(bBuf, 0, exact, 0, bl);
                                        ds.SetBytes(key, exact);
                                    }
                                    finally { ArrayPool<byte>.Shared.Return(bBuf); }
                                    break;
                                case ValueType.Array:
                                    var arr = ReadArrayPayload(s, idToObj);
                                    ds.SetArray(key, arr);
                                    break;
                                case ValueType.Object:
                                    var obj = ReadObjectPayload(s, idToObj);
                                    ds.SetObject(key, obj);
                                    break;
                                default:
                                    throw new InvalidDataException("Unknown value type during deserialize");
                            }
                        }
                        finally { ArrayPool<byte>.Shared.Return(keyBuf); }
                    }

                    return ds;
                }
                else
                {
                    // 非 seekable：将已读的两字节和剩余流内容复制到内存流，然后用旧逻辑解析
                    var ms = new MemoryStream();
                    ms.WriteByte((byte)b1);
                    ms.WriteByte((byte)b2);
                    s.CopyTo(ms);
                    ms.Position = 0;
                    return DeserializeFrom(ms); // now ms is seekable and will go the seekable branch
                }
            }
        }

        // 读取对象载荷并返回对象引用（支持新建/引用/null） - 旧版实现保留
        private static DrxSerializationData? ReadObjectPayload(Stream s, Dictionary<int, DrxSerializationData> idToObj)
        {
            var flag = s.ReadByte();
            if (flag < 0) throw new InvalidDataException("Unexpected end of stream reading object flag");
            if (flag == 0) return null;
            if (flag == 2)
            {
                var refId = ReadInt32(s);
                if (!idToObj.TryGetValue(refId, out var refObj)) throw new InvalidDataException($"Unknown reference id {refId}");
                return refObj;
            }
            if (flag == 1)
            {
                var id = ReadInt32(s);
                var newObj = new DrxSerializationData();
                idToObj[id] = newObj;

                var entryCount = ReadInt32(s);
                for (int i = 0; i < entryCount; i++)
                {
                    var keyLen = ReadInt32(s);
                    var keyBuf = ArrayPool<byte>.Shared.Rent(keyLen);
                    try
                    {
                        var read = s.Read(keyBuf, 0, keyLen);
                        if (read != keyLen) throw new InvalidDataException("Unexpected end of stream reading key");
                        var key = Encoding.UTF8.GetString(keyBuf, 0, keyLen);
                        var t = s.ReadByte();
                        if (t < 0) throw new InvalidDataException("Unexpected end of stream reading type");
                        var vt = (ValueType)t;
                        switch (vt)
                        {
                            case ValueType.Null:
                                newObj.SetString(key, null);
                                break;
                            case ValueType.Int64:
                                var i64 = ReadInt64(s);
                                newObj.SetInt(key, i64);
                                break;
                            case ValueType.Double:
                                var d = ReadDouble(s);
                                newObj.SetDouble(key, d);
                                break;
                            case ValueType.Bool:
                                var vb = s.ReadByte();
                                if (vb < 0) throw new InvalidDataException("Unexpected end of stream reading bool");
                                newObj.SetBool(key, vb != 0);
                                break;
                            case ValueType.String:
                                var sl = ReadInt32(s);
                                if (sl == 0) { newObj.SetString(key, string.Empty); break; }
                                var sBuf = ArrayPool<byte>.Shared.Rent(sl);
                                try
                                {
                                    var sr = s.Read(sBuf, 0, sl);
                                    if (sr != sl) throw new InvalidDataException("Unexpected end of stream reading string");
                                    newObj.SetString(key, Encoding.UTF8.GetString(sBuf, 0, sl));
                                }
                                finally { ArrayPool<byte>.Shared.Return(sBuf); }
                                break;
                            case ValueType.Bytes:
                                var bl = ReadInt32(s);
                                if (bl == 0) { newObj.SetBytes(key, Array.Empty<byte>()); break; }
                                var bBuf = ArrayPool<byte>.Shared.Rent(bl);
                                try
                                {
                                    var br = s.Read(bBuf, 0, bl);
                                    if (br != bl) throw new InvalidDataException("Unexpected end of stream reading bytes");
                                    var exact = new byte[bl];
                                    Buffer.BlockCopy(bBuf, 0, exact, 0, bl);
                                    newObj.SetBytes(key, exact);
                                }
                                finally { ArrayPool<byte>.Shared.Return(bBuf); }
                                break;
                            case ValueType.Array:
                                var arr = ReadArrayPayload(s, idToObj);
                                newObj.SetArray(key, arr);
                                break;
                            case ValueType.Object:
                                var child = ReadObjectPayload(s, idToObj);
                                newObj.SetObject(key, child);
                                break;
                            default:
                                throw new InvalidDataException("Unknown value type during deserialize");
                        }
                    }
                    finally { ArrayPool<byte>.Shared.Return(keyBuf); }
                }

                return newObj;
            }

            throw new InvalidDataException("Unknown object flag");
        }

        // 读取数组载荷并返回 DrxValue[]（支持嵌套数组/对象） - 旧版实现保留
        private static DrxValue[] ReadArrayPayload(Stream s, Dictionary<int, DrxSerializationData> idToObj)
        {
            var len = ReadInt32(s);
            var arr = new DrxValue[len];
            for (int i = 0; i < len; i++)
            {
                var t = s.ReadByte();
                if (t < 0) throw new InvalidDataException("Unexpected end of stream reading array element type");
                var vt = (ValueType)t;
                arr[i] = ReadValueGivenType(s, vt, idToObj);
            }
            return arr;
        }

        // 给定类型读取一个值（不包含前导 type 字节），返回 DrxValue - 旧版实现保留
        private static DrxValue ReadValueGivenType(Stream s, ValueType vt, Dictionary<int, DrxSerializationData> idToObj)
        {
            switch (vt)
            {
                case ValueType.Null:
                    return new DrxValue((string?)null);
                case ValueType.Int64:
                    return new DrxValue(ReadInt64(s));
                case ValueType.Double:
                    return new DrxValue(ReadDouble(s));
                case ValueType.Bool:
                    var vb = s.ReadByte();
                    if (vb < 0) throw new InvalidDataException("Unexpected end of stream reading bool");
                    return new DrxValue(vb != 0);
                case ValueType.String:
                    var sl = ReadInt32(s);
                    var sBuf = new byte[sl];
                    var sr = s.Read(sBuf, 0, sl);
                    if (sr != sl) throw new InvalidDataException("Unexpected end of stream reading string");
                    return new DrxValue(Encoding.UTF8.GetString(sBuf));
                case ValueType.Bytes:
                    var bl = ReadInt32(s);
                    var bBuf = new byte[bl];
                    var br = s.Read(bBuf, 0, bl);
                    if (br != bl) throw new InvalidDataException("Unexpected end of stream reading bytes");
                    return new DrxValue(bBuf);
                case ValueType.Object:
                    var obj = ReadObjectPayload(s, idToObj);
                    return new DrxValue(obj!);
                case ValueType.Array:
                    var nested = ReadArrayPayload(s, idToObj);
                    return new DrxValue(nested);
                default:
                    throw new InvalidDataException("Unknown value type during deserialize");
            }
        }

        // Compact 读取：使用 varuint/string table 语义
        private static DrxSerializationData? ReadObjectPayloadCompact(Stream s, Dictionary<int, DrxSerializationData> idToObj, string[] strings)
        {
            var flag = s.ReadByte();
            if (flag < 0) throw new InvalidDataException("Unexpected end of stream reading object flag");
            if (flag == 0) return null;
            if (flag == 2)
            {
                var refId = (int)ReadVarUInt(s);
                if (!idToObj.TryGetValue(refId, out var refObj)) throw new InvalidDataException($"Unknown reference id {refId}");
                return refObj;
            }
            if (flag == 1)
            {
                var id = (int)ReadVarUInt(s);
                var newObj = new DrxSerializationData();
                idToObj[id] = newObj;

                var entryCount = (int)ReadVarUInt(s);
                for (int i = 0; i < entryCount; i++)
                {
                    var keyId = (int)ReadVarUInt(s);
                    if (keyId == 0 || keyId > strings.Length - 1) throw new InvalidDataException($"Invalid keyId {keyId}");
                    var key = strings[keyId];
                    var t = s.ReadByte();
                    if (t < 0) throw new InvalidDataException("Unexpected end of stream reading type");
                    var vt = (ValueType)t;
                    switch (vt)
                    {
                        case ValueType.Null:
                            newObj.SetString(key, null);
                            break;
                        case ValueType.Int64:
                            var i64 = ReadVarInt64(s);
                            newObj.SetInt(key, i64);
                            break;
                        case ValueType.Double:
                            var d = ReadDouble(s);
                            newObj.SetDouble(key, d);
                            break;
                        case ValueType.Bool:
                            var vb = s.ReadByte();
                            if (vb < 0) throw new InvalidDataException("Unexpected end of stream reading bool");
                            newObj.SetBool(key, vb != 0);
                            break;
                        case ValueType.String:
                            var sid = ReadVarUInt(s);
                            if (sid == 0) newObj.SetString(key, string.Empty);
                            else
                            {
                                if (sid > (uint)strings.Length - 1) throw new InvalidDataException($"Invalid string id {sid}");
                                newObj.SetString(key, strings[sid]);
                            }
                            break;
                        case ValueType.Bytes:
                            var bl = (int)ReadVarUInt(s);
                            if (bl == 0) { newObj.SetBytes(key, Array.Empty<byte>()); break; }
                            var bBuf = ArrayPool<byte>.Shared.Rent(bl);
                            try
                            {
                                var br = s.Read(bBuf, 0, bl);
                                if (br != bl) throw new InvalidDataException("Unexpected end of stream reading bytes");
                                var exact = new byte[bl];
                                Buffer.BlockCopy(bBuf, 0, exact, 0, bl);
                                newObj.SetBytes(key, exact);
                            }
                            finally { ArrayPool<byte>.Shared.Return(bBuf); }
                            break;
                        case ValueType.Array:
                            var arr = ReadArrayPayloadCompact(s, idToObj, strings);
                            newObj.SetArray(key, arr);
                            break;
                        case ValueType.Object:
                            var child = ReadObjectPayloadCompact(s, idToObj, strings);
                            newObj.SetObject(key, child);
                            break;
                        default:
                            throw new InvalidDataException("Unknown value type during deserialize (compact)");
                    }
                }

                return newObj;
            }

            throw new InvalidDataException("Unknown object flag");
        }

        private static DrxValue[] ReadArrayPayloadCompact(Stream s, Dictionary<int, DrxSerializationData> idToObj, string[] strings)
        {
            var len = (int)ReadVarUInt(s);
            var arr = new DrxValue[len];
            for (int i = 0; i < len; i++)
            {
                var t = s.ReadByte();
                if (t < 0) throw new InvalidDataException("Unexpected end of stream reading array element type");
                var vt = (ValueType)t;
                arr[i] = ReadValueGivenTypeCompact(s, vt, idToObj, strings);
            }
            return arr;
        }

        private static DrxValue ReadValueGivenTypeCompact(Stream s, ValueType vt, Dictionary<int, DrxSerializationData> idToObj, string[] strings)
        {
            switch (vt)
            {
                case ValueType.Null:
                    return new DrxValue((string?)null);
                case ValueType.Int64:
                    return new DrxValue(ReadVarInt64(s));
                case ValueType.Double:
                    return new DrxValue(ReadDouble(s));
                case ValueType.Bool:
                    var vb = s.ReadByte();
                    if (vb < 0) throw new InvalidDataException("Unexpected end of stream reading bool");
                    return new DrxValue(vb != 0);
                case ValueType.String:
                    var sid = ReadVarUInt(s);
                    if (sid == 0) return new DrxValue(string.Empty);
                    if (sid > (uint)strings.Length - 1) throw new InvalidDataException($"Invalid string id {sid}");
                    return new DrxValue(strings[sid]);
                case ValueType.Bytes:
                    var bl = (int)ReadVarUInt(s);
                    var bBuf = new byte[bl];
                    var br = s.Read(bBuf, 0, bl);
                    if (br != bl) throw new InvalidDataException("Unexpected end of stream reading bytes");
                    return new DrxValue(bBuf);
                case ValueType.Object:
                    var obj = ReadObjectPayloadCompact(s, idToObj, strings);
                    return new DrxValue(obj!);
                case ValueType.Array:
                    var nested = ReadArrayPayloadCompact(s, idToObj, strings);
                    return new DrxValue(nested);
                default:
                    throw new InvalidDataException("Unknown value type during deserialize (compact)");
            }
        }
        #endregion
    }
}
