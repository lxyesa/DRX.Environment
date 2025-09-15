using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

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

            public long AsInt64() => _i64;
            public double AsDouble() => _dbl;
            public bool AsBool() => _b;
            public string? AsString() => _s;
            public byte[]? AsBytes() => _bytes;
            public DrxSerializationData? AsObject() => _obj;
            public DrxValue[]? AsArray() => _arr;
        }

        private readonly Dictionary<string, DrxValue> _map;
        private readonly ReaderWriterLockSlim _lock = new();

        public DrxSerializationData()
        {
            _map = new Dictionary<string, DrxValue>(StringComparer.Ordinal);
        }

        public DrxSerializationData(int capacity)
        {
            _map = new Dictionary<string, DrxValue>(capacity, StringComparer.Ordinal);
        }

        // 基础操作
        public void SetString(string key, string? value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        public void SetInt(string key, long value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        public void SetDouble(string key, double value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        public void SetBool(string key, bool value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

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

        public void SetFloat(string key, float value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue((double)value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        public void SetObject(string key, DrxSerializationData? obj)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(obj!);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        public void SetArray(string key, DrxValue[]? arr)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(arr!);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        // 原生数组重载：便捷方法，将原生数组转换为内部 DrxValue[] 存储
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

        public bool Remove(string key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            _lock.EnterWriteLock();
            try { return _map.Remove(key); }
            finally { _lock.ExitWriteLock(); }
        }

        public bool ContainsKey(string key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            _lock.EnterReadLock();
            try { return _map.ContainsKey(key); }
            finally { _lock.ExitReadLock(); }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try { _map.Clear(); }
            finally { _lock.ExitWriteLock(); }
        }

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
        public byte[] Serialize()
        {
            using var ms = new MemoryStream();
            SerializeTo(ms);
            return ms.ToArray();
        }

        public void SerializeTo(Stream stream)
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));
            _lock.EnterReadLock();
            try
            {
                // 对象引用表：对象引用 -> id（从 1 开始）
                var objToId = new Dictionary<DrxSerializationData, int>(ReferenceEqualityComparer<DrxSerializationData>.Default);
                int nextId = 1;

                WriteInt32(stream, _map.Count);
                foreach (var kv in _map)
                {
                    var keyBytes = Encoding.UTF8.GetBytes(kv.Key);
                    WriteInt32(stream, keyBytes.Length);
                    stream.Write(keyBytes, 0, keyBytes.Length);
                    stream.WriteByte((byte)kv.Value.Type);
                    // 写入具体 payload（对于数组/对象使用递归 helper）
                    WriteValuePayload(stream, kv.Value, objToId, ref nextId);
                }
            }
            finally { _lock.ExitReadLock(); }
        }

        // 写入单个值的 payload（不包含前导的 type 字节）
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
                    var sb = Encoding.UTF8.GetBytes(ss);
                    WriteInt32(s, sb.Length);
                    s.Write(sb, 0, sb.Length);
                    break;
                case ValueType.Bytes:
                    var b = v.AsBytes() ?? Array.Empty<byte>();
                    WriteInt32(s, b.Length);
                    s.Write(b, 0, b.Length);
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
                    var keyBytes = Encoding.UTF8.GetBytes(kv.Key);
                    WriteInt32(s, keyBytes.Length);
                    s.Write(keyBytes, 0, keyBytes.Length);
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
                            var sb = Encoding.UTF8.GetBytes(str);
                            WriteInt32(s, sb.Length);
                            s.Write(sb, 0, sb.Length);
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
        public static DrxSerializationData DeserializeFrom(Stream s)
        {
            if (s is null) throw new ArgumentNullException(nameof(s));

            var ds = new DrxSerializationData();

            // id -> object 映射，用于引用解析
            var idToObj = new Dictionary<int, DrxSerializationData>();

            var count = ReadInt32(s);
            for (int i = 0; i < count; i++)
            {
                var keyLen = ReadInt32(s);
                var keyBuf = new byte[keyLen];
                var read = s.Read(keyBuf, 0, keyLen);
                if (read != keyLen) throw new InvalidDataException("Unexpected end of stream reading key");
                var key = Encoding.UTF8.GetString(keyBuf);
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
                        var sBuf = new byte[sl];
                        var sr = s.Read(sBuf, 0, sl);
                        if (sr != sl) throw new InvalidDataException("Unexpected end of stream reading string");
                        ds.SetString(key, Encoding.UTF8.GetString(sBuf));
                        break;
                    case ValueType.Bytes:
                        var bl = ReadInt32(s);
                        var bBuf = new byte[bl];
                        var br = s.Read(bBuf, 0, bl);
                        if (br != bl) throw new InvalidDataException("Unexpected end of stream reading bytes");
                        ds.SetBytes(key, bBuf);
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

            return ds;
        }

        // 读取对象载荷并返回对象引用（支持新建/引用/null）
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
                    var keyBuf = new byte[keyLen];
                    var read = s.Read(keyBuf, 0, keyLen);
                    if (read != keyLen) throw new InvalidDataException("Unexpected end of stream reading key");
                    var key = Encoding.UTF8.GetString(keyBuf);
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
                            var sBuf = new byte[sl];
                            var sr = s.Read(sBuf, 0, sl);
                            if (sr != sl) throw new InvalidDataException("Unexpected end of stream reading string");
                            newObj.SetString(key, Encoding.UTF8.GetString(sBuf));
                            break;
                        case ValueType.Bytes:
                            var bl = ReadInt32(s);
                            var bBuf = new byte[bl];
                            var br = s.Read(bBuf, 0, bl);
                            if (br != bl) throw new InvalidDataException("Unexpected end of stream reading bytes");
                            newObj.SetBytes(key, bBuf);
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

                return newObj;
            }

            throw new InvalidDataException("Unknown object flag");
        }

        // 读取数组载荷并返回 DrxValue[]（支持嵌套数组/对象）
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

        // 给定类型读取一个值（不包含前导 type 字节），返回 DrxValue
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
        #endregion
    }
}
