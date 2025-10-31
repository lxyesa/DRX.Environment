using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Buffers.Text;
using System.Threading;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Drx.Sdk.Shared.Serialization
{
    /// <summary>
    /// Drx 序列化数据类（基础实现）。
    /// 说明：实现了一个轻量级键值存储，支持基本类型与嵌套对象的序列化/反序列化。
    /// 当前为基础骨架，后续可以按设计文档扩展优化（字符串表、变长编码、数组等）。
    /// 性能优化：使用 ArrayPool、方法内联、缓存编码器等技术提升性能。
    /// </summary>
    public class DrxSerializationData : System.Collections.IEnumerable
    {
        // 性能优化：缓存 UTF8 编码器以避免重复获取
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        
        // 性能优化：共享的 ArrayPool 用于临时缓冲区
        private static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Shared;
        
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
            Short = 8,
            Int = 9,
            UInt = 10,
            ULong = 11,
            Float = 12,
            Decimal = 13,
            Char = 14,
            Byte = 15,
            SByte = 16,
            IntPtr = 17,
            UIntPtr = 18,
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
            private readonly short _short;
            private readonly int _int;
            private readonly uint _uint;
            private readonly ulong _ulong;
            private readonly float _float;
            private readonly decimal _decimal;
            private readonly char _char;
            private readonly byte _byte;
            private readonly sbyte _sbyte;
            private readonly IntPtr _intPtr;
            private readonly UIntPtr _uintPtr;

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
                _arr = null;
                _short = default;
                _int = default;
                _uint = default;
                _ulong = default;
                _float = default;
                _decimal = default;
                _char = default;
                _byte = default;
                _sbyte = default;
                _intPtr = default;
                _uintPtr = default;
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
                _arr = null;
                _short = default;
                _int = default;
                _uint = default;
                _ulong = default;
                _float = default;
                _decimal = default;
                _char = default;
                _byte = default;
                _sbyte = default;
                _intPtr = default;
                _uintPtr = default;
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
                _arr = null;
                _short = default;
                _int = default;
                _uint = default;
                _ulong = default;
                _float = default;
                _decimal = default;
                _char = default;
                _byte = default;
                _sbyte = default;
                _intPtr = default;
                _uintPtr = default;
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
                    _arr = null;
                    _short = default;
                    _int = default;
                    _uint = default;
                    _ulong = default;
                    _float = default;
                    _decimal = default;
                    _char = default;
                    _byte = default;
                    _sbyte = default;
                    _intPtr = default;
                    _uintPtr = default;
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
                    _arr = null;
                    _short = default;
                    _int = default;
                    _uint = default;
                    _ulong = default;
                    _float = default;
                    _decimal = default;
                    _char = default;
                    _byte = default;
                    _sbyte = default;
                    _intPtr = default;
                    _uintPtr = default;
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
                _short = default;
                _int = default;
                _uint = default;
                _ulong = default;
                _float = default;
                _decimal = default;
                _char = default;
                _byte = default;
                _sbyte = default;
                _intPtr = default;
                _uintPtr = default;
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
                _short = default;
                _int = default;
                _uint = default;
                _ulong = default;
                _float = default;
                _decimal = default;
                _char = default;
                _byte = default;
                _sbyte = default;
                _intPtr = default;
                _uintPtr = default;
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
                _arr = null;
                _short = default;
                _int = default;
                _uint = default;
                _ulong = default;
                _float = default;
                _decimal = default;
                _char = default;
                _byte = default;
                _sbyte = default;
                _intPtr = default;
                _uintPtr = default;
            }

            /// <summary>
            /// 使用 short 初始化值容器。
            /// </summary>
            /// <param name="v">要保存的 short 值。</param>
            public DrxValue(short v)
            {
                Type = ValueType.Short;
                _short = v;
                _i64 = default;
                _dbl = default;
                _b = default;
                _s = null;
                _bytes = null;
                _obj = null;
                _arr = null;
                _int = default;
                _uint = default;
                _ulong = default;
                _float = default;
                _decimal = default;
                _char = default;
                _byte = default;
                _sbyte = default;
                _intPtr = default;
                _uintPtr = default;
            }

            /// <summary>
            /// 使用 int 初始化值容器。
            /// </summary>
            /// <param name="v">要保存的 int 值。</param>
            public DrxValue(int v)
            {
                Type = ValueType.Int;
                _int = v;
                _i64 = default;
                _dbl = default;
                _b = default;
                _s = null;
                _bytes = null;
                _obj = null;
                _arr = null;
                _short = default;
                _uint = default;
                _ulong = default;
                _float = default;
                _decimal = default;
                _char = default;
                _byte = default;
                _sbyte = default;
                _intPtr = default;
                _uintPtr = default;
            }

            /// <summary>
            /// 使用 uint 初始化值容器。
            /// </summary>
            /// <param name="v">要保存的 uint 值。</param>
            public DrxValue(uint v)
            {
                Type = ValueType.UInt;
                _uint = v;
                _i64 = default;
                _dbl = default;
                _b = default;
                _s = null;
                _bytes = null;
                _obj = null;
                _arr = null;
                _short = default;
                _int = default;
                _ulong = default;
                _float = default;
                _decimal = default;
                _char = default;
                _byte = default;
                _sbyte = default;
                _intPtr = default;
                _uintPtr = default;
            }

            /// <summary>
            /// 使用 ulong 初始化值容器。
            /// </summary>
            /// <param name="v">要保存的 ulong 值。</param>
            public DrxValue(ulong v)
            {
                Type = ValueType.ULong;
                _ulong = v;
                _i64 = default;
                _dbl = default;
                _b = default;
                _s = null;
                _bytes = null;
                _obj = null;
                _arr = null;
                _short = default;
                _int = default;
                _uint = default;
                _float = default;
                _decimal = default;
                _char = default;
                _byte = default;
                _sbyte = default;
                _intPtr = default;
                _uintPtr = default;
            }

            /// <summary>
            /// 使用 float 初始化值容器。
            /// </summary>
            /// <param name="v">要保存的 float 值。</param>
            public DrxValue(float v)
            {
                Type = ValueType.Float;
                _float = v;
                _i64 = default;
                _dbl = default;
                _b = default;
                _s = null;
                _bytes = null;
                _obj = null;
                _arr = null;
                _short = default;
                _int = default;
                _uint = default;
                _ulong = default;
                _decimal = default;
                _char = default;
                _byte = default;
                _sbyte = default;
                _intPtr = default;
                _uintPtr = default;
            }

            /// <summary>
            /// 使用 decimal 初始化值容器。
            /// </summary>
            /// <param name="v">要保存的 decimal 值。</param>
            public DrxValue(decimal v)
            {
                Type = ValueType.Decimal;
                _decimal = v;
                _i64 = default;
                _dbl = default;
                _b = default;
                _s = null;
                _bytes = null;
                _obj = null;
                _arr = null;
                _short = default;
                _int = default;
                _uint = default;
                _ulong = default;
                _float = default;
                _char = default;
                _byte = default;
                _sbyte = default;
                _intPtr = default;
                _uintPtr = default;
            }

            /// <summary>
            /// 使用 char 初始化值容器。
            /// </summary>
            /// <param name="v">要保存的 char 值。</param>
            public DrxValue(char v)
            {
                Type = ValueType.Char;
                _char = v;
                _i64 = default;
                _dbl = default;
                _b = default;
                _s = null;
                _bytes = null;
                _obj = null;
                _arr = null;
                _short = default;
                _int = default;
                _uint = default;
                _ulong = default;
                _float = default;
                _decimal = default;
                _byte = default;
                _sbyte = default;
                _intPtr = default;
                _uintPtr = default;
            }

            /// <summary>
            /// 使用 byte 初始化值容器。
            /// </summary>
            /// <param name="v">要保存的 byte 值。</param>
            public DrxValue(byte v)
            {
                Type = ValueType.Byte;
                _byte = v;
                _i64 = default;
                _dbl = default;
                _b = default;
                _s = null;
                _bytes = null;
                _obj = null;
                _arr = null;
                _short = default;
                _int = default;
                _uint = default;
                _ulong = default;
                _float = default;
                _decimal = default;
                _char = default;
                _sbyte = default;
                _intPtr = default;
                _uintPtr = default;
            }

            /// <summary>
            /// 使用 sbyte 初始化值容器。
            /// </summary>
            /// <param name="v">要保存的 sbyte 值。</param>
            public DrxValue(sbyte v)
            {
                Type = ValueType.SByte;
                _sbyte = v;
                _i64 = default;
                _dbl = default;
                _b = default;
                _s = null;
                _bytes = null;
                _obj = null;
                _arr = null;
                _short = default;
                _int = default;
                _uint = default;
                _ulong = default;
                _float = default;
                _decimal = default;
                _char = default;
                _byte = default;
                _intPtr = default;
                _uintPtr = default;
            }

            /// <summary>
            /// 使用 IntPtr 初始化值容器。
            /// </summary>
            /// <param name="v">要保存的 IntPtr 值。</param>
            public DrxValue(IntPtr v)
            {
                Type = ValueType.IntPtr;
                _intPtr = v;
                _i64 = default;
                _dbl = default;
                _b = default;
                _s = null;
                _bytes = null;
                _obj = null;
                _arr = null;
                _short = default;
                _int = default;
                _uint = default;
                _ulong = default;
                _float = default;
                _decimal = default;
                _char = default;
                _byte = default;
                _sbyte = default;
                _uintPtr = default;
            }

            /// <summary>
            /// 使用 UIntPtr 初始化值容器。
            /// </summary>
            /// <param name="v">要保存的 UIntPtr 值。</param>
            public DrxValue(UIntPtr v)
            {
                Type = ValueType.UIntPtr;
                _uintPtr = v;
                _i64 = default;
                _dbl = default;
                _b = default;
                _s = null;
                _bytes = null;
                _obj = null;
                _arr = null;
                _short = default;
                _int = default;
                _uint = default;
                _ulong = default;
                _float = default;
                _decimal = default;
                _char = default;
                _byte = default;
                _sbyte = default;
                _intPtr = default;
            }

            /// <summary>
            /// 将当前值作为 Int64 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 Int64 值。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public long AsInt64() => _i64;
            
            /// <summary>
            /// 将当前值作为 Double 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 Double 值。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public double AsDouble() => _dbl;
            
            /// <summary>
            /// 将当前值作为 Bool 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 Bool 值。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AsBool() => _b;
            
            /// <summary>
            /// 将当前值作为字符串返回（无类型检查）。
            /// </summary>
            /// <returns>内部字符串，可能为 null。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public string? AsString() => _s;
            
            /// <summary>
            /// 将当前值作为字节数组返回（无类型检查）。
            /// </summary>
            /// <returns>内部字节数组，可能为 null。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[]? AsBytes() => _bytes;
            
            /// <summary>
            /// 将当前值作为嵌套对象返回（无类型检查）。
            /// </summary>
            /// <returns>内部嵌套对象，可能为 null。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public DrxSerializationData? AsObject() => _obj;
            
            /// <summary>
            /// 将当前值作为值数组返回（无类型检查）。
            /// </summary>
            /// <returns>内部值数组，可能为 null。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public DrxValue[]? AsArray() => _arr;

            /// <summary>
            /// 将当前值作为 Short 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 Short 值。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public short AsShort() => _short;

            /// <summary>
            /// 将当前值作为 Int 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 Int 值。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int AsInt() => _int;

            /// <summary>
            /// 将当前值作为 UInt 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 UInt 值。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint AsUInt() => _uint;

            /// <summary>
            /// 将当前值作为 ULong 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 ULong 值。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ulong AsULong() => _ulong;

            /// <summary>
            /// 将当前值作为 Float 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 Float 值。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float AsFloat() => _float;

            /// <summary>
            /// 将当前值作为 Decimal 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 Decimal 值。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public decimal AsDecimal() => _decimal;

            /// <summary>
            /// 将当前值作为 Char 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 Char 值。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public char AsChar() => _char;

            /// <summary>
            /// 将当前值作为 Byte 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 Byte 值。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte AsByte() => _byte;

            /// <summary>
            /// 将当前值作为 SByte 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 SByte 值。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public sbyte AsSByte() => _sbyte;

            /// <summary>
            /// 将当前值作为 IntPtr 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 IntPtr 值。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public IntPtr AsIntPtr() => _intPtr;

            /// <summary>
            /// 将当前值作为 UIntPtr 返回（无类型检查）。
            /// </summary>
            /// <returns>内部的 UIntPtr 值。</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public UIntPtr AsUIntPtr() => _uintPtr;

            /// <summary>
            /// 泛型访问器：根据 T 的类型自动分派到相应的 AsXxx 方法。
            /// 支持常见标量类型以及数组类型。
            /// </summary>
            /// <typeparam name="T">目标类型</typeparam>
            /// <returns>转换后的值</returns>
            /// <exception cref="InvalidOperationException">当类型不匹配时抛出。</exception>
            public T As<T>()
            {
                var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

                if (targetType == typeof(long)) return (T)(object)AsInt64();
                if (targetType == typeof(double)) return (T)(object)AsDouble();
                if (targetType == typeof(bool)) return (T)(object)AsBool();
                if (targetType == typeof(string)) return (T)(object)AsString();
                if (targetType == typeof(byte[])) return (T)(object)AsBytes();
                if (targetType == typeof(DrxSerializationData)) return (T)(object)AsObject();
                if (targetType == typeof(DrxValue[])) return (T)(object)AsArray();
                if (targetType == typeof(short)) return (T)(object)AsShort();
                if (targetType == typeof(int)) return (T)(object)AsInt();
                if (targetType == typeof(uint)) return (T)(object)AsUInt();
                if (targetType == typeof(ulong)) return (T)(object)AsULong();
                if (targetType == typeof(float)) return (T)(object)AsFloat();
                if (targetType == typeof(decimal)) return (T)(object)AsDecimal();
                if (targetType == typeof(char)) return (T)(object)AsChar();
                if (targetType == typeof(byte)) return (T)(object)AsByte();
                if (targetType == typeof(sbyte)) return (T)(object)AsSByte();
                if (targetType == typeof(IntPtr)) return (T)(object)AsIntPtr();
                if (targetType == typeof(UIntPtr)) return (T)(object)AsUIntPtr();

                // 数组类型
                if (targetType.IsArray)
                {
                    var elem = targetType.GetElementType();
                    var arr = AsArray();
                    if (arr == null) return default!;
                    if (elem == typeof(long)) return (T)(object)arr.Select(v => v.As<long>()).ToArray();
                    if (elem == typeof(int)) return (T)(object)arr.Select(v => v.As<int>()).ToArray();
                    if (elem == typeof(short)) return (T)(object)arr.Select(v => v.As<short>()).ToArray();
                    if (elem == typeof(ushort)) return (T)(object)arr.Select(v => v.As<ushort>()).ToArray();
                    if (elem == typeof(byte)) return (T)(object)arr.Select(v => v.As<byte>()).ToArray();
                    if (elem == typeof(sbyte)) return (T)(object)arr.Select(v => v.As<sbyte>()).ToArray();
                    if (elem == typeof(uint)) return (T)(object)arr.Select(v => v.As<uint>()).ToArray();
                    if (elem == typeof(ulong)) return (T)(object)arr.Select(v => v.As<ulong>()).ToArray();
                    if (elem == typeof(float)) return (T)(object)arr.Select(v => v.As<float>()).ToArray();
                    if (elem == typeof(double)) return (T)(object)arr.Select(v => v.As<double>()).ToArray();
                    if (elem == typeof(bool)) return (T)(object)arr.Select(v => v.As<bool>()).ToArray();
                    if (elem == typeof(string)) return (T)(object)arr.Select(v => v.As<string>()).ToArray();
                    if (elem == typeof(byte[])) return (T)(object)arr.Select(v => v.As<byte[]>()).ToArray();
                    if (elem == typeof(DrxSerializationData)) return (T)(object)arr.Select(v => v.As<DrxSerializationData>()).ToArray();
                }

                throw new InvalidOperationException($"Unsupported type {typeof(T)} for As<T>");
            }
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

        // Internal helper that sets a DrxValue with proper locking (keeps existing SetX semantics untouched).
        private void Put(string key, DrxValue v)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 支持集合初始化器：Add(string key, object? value)
        /// 将常见 CLR 类型映射到内部的 DrxValue 表示（优先使用现有 SetX 方法以保持语义一致性，例如字节数组会被复制）。
        /// 不支持的类型将抛出 ArgumentException 以便尽早发现错误。
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值（可为 null）</param>
        public void Add(string key, object? value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));

            // 常见类型映射（覆盖多数场景）
            switch (value)
            {
                case null:
                    // 使用 SetString(null) 代表 Null 类型，保持与 SetString 行为一致
                    SetString(key, null);
                    break;
                case string s:
                    SetString(key, s);
                    break;
                case bool b:
                    SetBool(key, b);
                    break;
                case byte[] bytes:
                    // 保持 SetBytes 的复制语义
                    SetBytes(key, bytes);
                    break;
                case DrxValue dv:
                    // 直接写入 DrxValue（不可变语义由 DrxValue 本身保证）
                    Put(key, dv);
                    break;
                case DrxValue[] dva:
                    SetArray(key, dva);
                    break;
                case DrxSerializationData obj:
                    SetObject(key, obj);
                    break;
                case double d:
                    SetDouble(key, d);
                    break;
                case float f:
                    SetFloat(key, f);
                    break;
                case int i:
                    SetInt32(key, i);
                    break;
                case long l:
                    SetInt(key, l);
                    break;
                case short sh:
                    SetShort(key, sh);
                    break;
                case byte by:
                    SetByte(key, by);
                    break;
                case sbyte sb:
                    SetSByte(key, sb);
                    break;
                case uint ui:
                    SetUInt32(key, ui);
                    break;
                case ulong ul:
                    SetUInt64(key, ul);
                    break;
                case decimal d:
                    SetDecimal(key, d);
                    break;
                case char c:
                    SetChar(key, c);
                    break;
                case IntPtr ip:
                    SetIntPtr(key, ip);
                    break;
                case UIntPtr up:
                    SetUIntPtr(key, up);
                    break;
                case IEnumerable<DrxValue> seqDv:
                    SetArray(key, System.Linq.Enumerable.ToArray(seqDv));
                    break;
                case IEnumerable<string> seqS:
                    {
                        var arr = System.Linq.Enumerable.ToArray(seqS);
                        var va = new DrxValue[arr.Length];
                        for (int i = 0; i < arr.Length; i++) va[i] = new DrxValue(arr[i]);
                        SetArray(key, va);
                        break;
                    }
                case IEnumerable<int> seqI:
                    {
                        var arr = System.Linq.Enumerable.ToArray(seqI);
                        var va = new DrxValue[arr.Length];
                        for (int i = 0; i < arr.Length; i++) va[i] = new DrxValue(arr[i]);
                        SetArray(key, va);
                        break;
                    }
                case IEnumerable<long> seqL:
                    {
                        var arr = System.Linq.Enumerable.ToArray(seqL);
                        var va = new DrxValue[arr.Length];
                        for (int i = 0; i < arr.Length; i++) va[i] = new DrxValue(arr[i]);
                        SetArray(key, va);
                        break;
                    }
                default:
                    // 尝试将任意 IConvertible 数值转为 Int64 / Double（类型安全由 Convert 抛出的异常负责）
                    if (value is IConvertible conv)
                    {
                        var typeCode = conv.GetTypeCode();
                        switch (typeCode)
                        {
                            case TypeCode.Byte:
                            case TypeCode.SByte:
                            case TypeCode.Int16:
                            case TypeCode.UInt16:
                            case TypeCode.Int32:
                            case TypeCode.UInt32:
                            case TypeCode.Int64:
                            case TypeCode.UInt64:
                                try
                                {
                                    var asI64 = Convert.ToInt64(conv);
                                    SetInt(key, asI64);
                                    return;
                                }
                                catch (Exception ex) when (ex is OverflowException || ex is InvalidCastException || ex is FormatException)
                                {
                                    // fallthrough to error
                                }
                                break;
                            case TypeCode.Single:
                            case TypeCode.Double:
                            case TypeCode.Decimal:
                                try
                                {
                                    var asD = Convert.ToDouble(conv);
                                    SetDouble(key, asD);
                                    return;
                                }
                                catch (Exception) { /* fallthrough */ }
                                break;
                            case TypeCode.Boolean:
                                SetBool(key, Convert.ToBoolean(conv));
                                return;
                            case TypeCode.String:
                                SetString(key, Convert.ToString(conv));
                                return;
                        }
                    }

                    // 不可识别的类型
                    throw new ArgumentException($"Unsupported value type for Add: {(value?.GetType().FullName ?? "null")}", nameof(value));
            }
        }

        /// <summary>
        /// 允许以 KeyValuePair 的形式初始化（例如 from LINQ 等场景）。
        /// </summary>
        public void Add(KeyValuePair<string, DrxValue> kv)
        {
            if (kv.Key is null) throw new ArgumentNullException(nameof(kv.Key));
            Put(kv.Key, kv.Value);
        }

        /// <summary>
        /// 返回线程安全拷贝的枚举器，枚举期间不会持有写锁，避免死锁与长时间锁持有。
        /// </summary>
        public IEnumerator<KeyValuePair<string, DrxValue>> GetEnumerator()
        {
            _lock.EnterReadLock();
            try
            {
                // 复制到列表，返回其枚举器；拷贝保证枚举稳定性与避免长时间持锁
                var copy = new List<KeyValuePair<string, DrxValue>>(_map);
                return copy.GetEnumerator();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 返回当前对象中所有键的线程安全拷贝。
        /// 枚举期间不会持有读锁，避免死锁与长时间锁持有。
        /// </summary>
        public IEnumerable<string> Keys
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    // 创建键集合的快照并返回，保证在外部枚举时不会持有内部锁
                    return new List<string>(_map.Keys);
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
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
        /// 设置指定键的字节数组（无拷贝版本，性能优化）。
        /// 警告：调用者需要确保后续不修改传入的数组，否则会破坏数据一致性。
        /// 用于性能敏感场景，避免不必要的内存拷贝。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="bytes">字节数组，允许为 null 表示 Null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBytesNoCopy(string key, byte[]? bytes)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(bytes);
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
        /// 设置指定键的 short 值。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">要保存的 short 值。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetShort(string key, short value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的 int 值。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">要保存的 int 值。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetInt32(string key, int value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的 uint 值。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">要保存的 uint 值。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetUInt32(string key, uint value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的 ulong 值。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">要保存的 ulong 值。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetUInt64(string key, ulong value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的 decimal 值。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">要保存的 decimal 值。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetDecimal(string key, decimal value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的 char 值。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">要保存的 char 值。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetChar(string key, char value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的 byte 值。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">要保存的 byte 值。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetByte(string key, byte value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的 sbyte 值。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">要保存的 sbyte 值。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetSByte(string key, sbyte value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的 IntPtr 值。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">要保存的 IntPtr 值。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetIntPtr(string key, IntPtr value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
            _lock.EnterWriteLock();
            try { _map[key] = v; }
            finally { _lock.ExitWriteLock(); }
        }

        /// <summary>
        /// 设置指定键的 UIntPtr 值。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="value">要保存的 UIntPtr 值。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetUIntPtr(string key, UIntPtr value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var v = new DrxValue(value);
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
        /// 设置指定键的 32 位整数数组（便捷重载）。若传入 null 则表示 Null。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="arr">整型数组，允许为 null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetArray(string key, int[]? arr)
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
        /// 设置指定键的 16 位有符号整数数组（便捷重载）。若传入 null 则表示 Null。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="arr">短整型数组，允许为 null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetArray(string key, short[]? arr)
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
        /// 设置指定键的 32 位无符号整数数组（便捷重载）。若传入 null 则表示 Null。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="arr">无符号整型数组，允许为 null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetArray(string key, uint[]? arr)
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
        /// 设置指定键的单精度浮点数组（便捷重载）。若传入 null 则表示 Null。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="arr">float 数组，允许为 null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetArray(string key, float[]? arr)
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
        /// 设置指定键的字符数组（便捷重载）。若传入 null 则表示 Null。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="arr">字符数组，允许为 null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetArray(string key, char[]? arr)
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
        /// 设置指定键的字节数组（便捷重载）。若传入 null 则表示 Null。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="arr">字节数组，允许为 null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetArray(string key, byte[]? arr)
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
        /// 设置指定键的 8 位有符号整数数组（便捷重载）。若传入 null 则表示 Null。
        /// </summary>
        /// <param name="key">键，不能为空。</param>
        /// <param name="arr">有符号字节数组，允许为 null。</param>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        public void SetArray(string key, sbyte[]? arr)
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
        /// 设置指定键的字符串数组（便捷重载）。 若传入 null 则表示 Null，数组元素允许为 null。
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
        /// <returns>成功时返回对应的 DrxValue，否则返回 null。</returns>
        /// <exception cref="ArgumentNullException">当 key 为 null 时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DrxValue? TryGet(string key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            _lock.EnterReadLock();
            try
            {
                if (_map.TryGetValue(key, out var value))
                {
                    return value;
                }
                return null;
            }
            finally { _lock.ExitReadLock(); }
        }

        /// <summary>
        /// 尝试按键获取字符串值（仅在值类型为 String 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应字符串，否则返回 null。</returns>
        public string? TryGetString(string key)
        {
            var v = TryGet(key);
            if (v?.Type == ValueType.String)
            {
                return v.Value.AsString();
            }
            return null;
        }

        /// <summary>
        /// 尝试按键获取 32 位整数值（仅在值类型为 Int 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应整数，否则返回 null。</returns>
        public int? TryGetInt32(string key)
        {
            var v = TryGet(key);
            if (v?.Type == ValueType.Int)
            {
                return v.Value.AsInt();
            }
            return null;
        }

        /// <summary>
        /// 尝试按键获取 64 位整数值（仅在值类型为 Int64 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应整数，否则返回 null。</returns>
        public long? TryGetInt(string key)
        {
            var v = TryGet(key);
            if (v?.Type == ValueType.Int64)
            {
                return v.Value.AsInt64();
            }
            return null;
        }

        /// <summary>
        /// 尝试按键获取单精度浮点值（仅在值类型为 Double 时成功并转换为 float）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应 float 值，否则返回 null。</returns>
        public float? TryGetFloat(string key)
        {
            var v = TryGet(key);
            if (v?.Type == ValueType.Double)
            {
                return (float)v.Value.AsDouble();
            }
            return null;
        }

        /// <summary>
        /// 尝试按键获取双精度浮点值（仅在值类型为 Double 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应 double 值，否则返回 null。</returns>
        public double? TryGetDouble(string key)
        {
            var v = TryGet(key);
            if (v?.Type == ValueType.Double)
            {
                return v.Value.AsDouble();
            }
            return null;
        }

        /// <summary>
        /// 尝试按键获取布尔值（仅在值类型为 Bool 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应布尔值，否则返回 null。</returns>
        public bool? TryGetBool(string key)
        {
            var v = TryGet(key);
            if (v?.Type == ValueType.Bool)
            {
                return v.Value.AsBool();
            }
            return null;
        }

        /// <summary>
        /// 尝试按键获取字节数组（仅在值类型为 Bytes 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应字节数组，否则返回 null。</returns>
        public byte[]? TryGetBytes(string key)
        {
            var v = TryGet(key);
            if (v?.Type == ValueType.Bytes)
            {
                return v.Value.AsBytes();
            }
            return null;
        }

        /// <summary>
        /// 尝试按键获取嵌套对象（仅在值类型为 Object 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应对象，否则返回 null。</returns>
        public DrxSerializationData? TryGetObject(string key)
        {
            var v = TryGet(key);
            if (v?.Type == ValueType.Object)
            {
                return v.Value.AsObject();
            }
            return null;
        }

        /// <summary>
        /// 尝试按键获取值数组（仅在值类型为 Array 时成功）。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应 DrxValue 数组，否则返回 null。</returns>
        public DrxValue[]? TryGetArray(string key)
        {
            var v = TryGet(key);
            if (v?.Type == ValueType.Array)
            {
                return v.Value.AsArray();
            }
            return null;
        }

        /// <summary>
        /// 泛型读取器：根据 T 的类型自动分派到相应的 TryGetX 方法。
        /// 支持常见标量（long/int/double/float/bool/string/byte[]/DrxSerializationData/DrxValue[]）
        /// 以及数组（long[], int[], double[], bool[], string[]/string?[], byte[][], DrxSerializationData[]）。
        /// 当 T 为 Nullable<TUnderlying> 时也会自动处理。
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="key">键</param>
        /// <returns>成功时返回对应值，否则返回 null。</returns>
        public T? TryGetValue<T>(string key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));

            // 支持 Nullable<T> 的情况
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            // 标量类型分支
            if (targetType == typeof(long))
            {
                var v = TryGetInt(key);
                return v is null ? default : (T)(object)v.Value;
            }
            if (targetType == typeof(int))
            {
                var v = TryGetInt32(key);
                return v is null ? default : (T)(object)v.Value;
            }
            if (targetType == typeof(double))
            {
                var d = TryGetDouble(key);
                return d is null ? default : (T)(object)d.Value;
            }
            if (targetType == typeof(float))
            {
                var f = TryGetFloat(key);
                if (f is not null) return (T)(object)f.Value;
                // 允许从 double 转换为 float
                var dd = TryGetDouble(key);
                return dd is null ? default : (T)(object)(float)dd.Value;
            }
            if (targetType == typeof(bool))
            {
                var b = TryGetBool(key);
                return b is null ? default : (T)(object)b.Value;
            }
            if (targetType == typeof(string))
            {
                var s = TryGetString(key);
                return s is null ? default : (T)(object)s;
            }
            if (targetType == typeof(byte[]))
            {
                var bytes = TryGetBytes(key);
                return bytes is null ? default : (T)(object)bytes;
            }
            if (targetType == typeof(DrxSerializationData))
            {
                var obj = TryGetObject(key);
                return obj is null ? default : (T)(object)obj;
            }
            if (targetType == typeof(DrxValue[]))
            {
                var va = TryGetArray(key);
                return va is null ? default : (T)(object)va;
            }

            // 额外的数值类型支持（short/ushort/byte/sbyte/uint/ulong/IntPtr/UIntPtr）
            if (targetType == typeof(short))
            {
                var v = TryGetInt(key);
                if (v is not null && v.Value >= short.MinValue && v.Value <= short.MaxValue)
                {
                    return (T)(object)(short)v.Value;
                }
                return default;
            }
            if (targetType == typeof(ushort))
            {
                var v = TryGetInt(key);
                if (v is not null && v.Value >= 0 && v.Value <= ushort.MaxValue)
                {
                    return (T)(object)(ushort)v.Value;
                }
                return default;
            }
            if (targetType == typeof(byte))
            {
                var v = TryGetInt(key);
                if (v is not null && v.Value >= byte.MinValue && v.Value <= byte.MaxValue)
                {
                    return (T)(object)(byte)v.Value;
                }
                return default;
            }
            if (targetType == typeof(sbyte))
            {
                var v = TryGetInt(key);
                if (v is not null && v.Value >= sbyte.MinValue && v.Value <= sbyte.MaxValue)
                {
                    return (T)(object)(sbyte)v.Value;
                }
                return default;
            }
            if (targetType == typeof(uint))
            {
                var v = TryGetInt(key);
                if (v is not null && v.Value >= 0 && v.Value <= uint.MaxValue)
                {
                    return (T)(object)(uint)v.Value;
                }
                return default;
            }
            if (targetType == typeof(ulong))
            {
                var v = TryGetInt(key);
                if (v is not null && v.Value >= 0)
                {
                    // 注意：如果原始 ulong 大于 long.MaxValue，则 Add 在写入时会抛出，因此这里不会看到那些值
                    return (T)(object)(ulong)v.Value;
                }
                return default;
            }
            if (targetType == typeof(IntPtr))
            {
                var v = TryGetInt(key);
                if (v is not null)
                {
                    if (IntPtr.Size == 4)
                    {
                        if (v.Value >= int.MinValue && v.Value <= int.MaxValue)
                        {
                            return (T)(object)new IntPtr((int)v.Value);
                        }
                    }
                    else
                    {
                        return (T)(object)new IntPtr(v.Value);
                    }
                }
                return default;
            }
            if (targetType == typeof(UIntPtr))
            {
                var v = TryGetInt(key);
                if (v is not null && v.Value >= 0)
                {
                    if (UIntPtr.Size == 4)
                    {
                        if (v.Value <= uint.MaxValue)
                        {
                            return (T)(object)new UIntPtr((uint)v.Value);
                        }
                    }
                    else
                    {
                        // UIntPtr(long) ctor is not available; use unchecked cast via ulong
                        return (T)(object)new UIntPtr((ulong)v.Value);
                    }
                }
                return default;
            }

            // 数组类型分派（例如 int[], long[], double[] 等）
            if (targetType.IsArray)
            {
                var elem = targetType.GetElementType();
                if (elem == typeof(long))
                {
                    var la = TryGetLongArray(key);
                    return la is null ? default : (T)(object)la;
                }
                if (elem == typeof(int))
                {
                    // 原生没有 int[] 存储，尝试从 long[] 转换
                    var la = TryGetLongArray(key);
                    if (la is null) return default;
                    var ia = new int[la.Length];
                    for (int i = 0; i < la.Length; i++)
                    {
                        if (la[i] < int.MinValue || la[i] > int.MaxValue) return default;
                        ia[i] = (int)la[i];
                    }
                    return (T)(object)ia;
                }
                if (elem == typeof(short))
                {
                    var la = TryGetLongArray(key);
                    if (la is null) return default;
                    var ra = new short[la.Length];
                    for (int i = 0; i < la.Length; i++)
                    {
                        if (la[i] < short.MinValue || la[i] > short.MaxValue) return default;
                        ra[i] = (short)la[i];
                    }
                    return (T)(object)ra;
                }
                if (elem == typeof(ushort))
                {
                    var la = TryGetLongArray(key);
                    if (la is null) return default;
                    var ra = new ushort[la.Length];
                    for (int i = 0; i < la.Length; i++)
                    {
                        if (la[i] < 0 || la[i] > ushort.MaxValue) return default;
                        ra[i] = (ushort)la[i];
                    }
                    return (T)(object)ra;
                }
                if (elem == typeof(byte))
                {
                    var la = TryGetLongArray(key);
                    if (la is null) return default;
                    var ra = new byte[la.Length];
                    for (int i = 0; i < la.Length; i++)
                    {
                        if (la[i] < byte.MinValue || la[i] > byte.MaxValue) return default;
                        ra[i] = (byte)la[i];
                    }
                    return (T)(object)ra;
                }
                if (elem == typeof(sbyte))
                {
                    var la = TryGetLongArray(key);
                    if (la is null) return default;
                    var ra = new sbyte[la.Length];
                    for (int i = 0; i < la.Length; i++)
                    {
                        if (la[i] < sbyte.MinValue || la[i] > sbyte.MaxValue) return default;
                        ra[i] = (sbyte)la[i];
                    }
                    return (T)(object)ra;
                }
                if (elem == typeof(uint))
                {
                    var la = TryGetLongArray(key);
                    if (la is null) return default;
                    var ra = new uint[la.Length];
                    for (int i = 0; i < la.Length; i++)
                    {
                        if (la[i] < 0 || la[i] > uint.MaxValue) return default;
                        ra[i] = (uint)la[i];
                    }
                    return (T)(object)ra;
                }
                if (elem == typeof(ulong))
                {
                    var la = TryGetLongArray(key);
                    if (la is null) return default;
                    var ra = new ulong[la.Length];
                    for (int i = 0; i < la.Length; i++)
                    {
                        if (la[i] < 0) return default;
                        ra[i] = (ulong)la[i];
                    }
                    return (T)(object)ra;
                }
                if (elem == typeof(IntPtr))
                {
                    var la = TryGetLongArray(key);
                    if (la is null) return default;
                    var ra = new IntPtr[la.Length];
                    for (int i = 0; i < la.Length; i++)
                    {
                        var vv = la[i];
                        if (IntPtr.Size == 4)
                        {
                            if (vv < int.MinValue || vv > int.MaxValue) return default;
                            ra[i] = new IntPtr((int)vv);
                        }
                        else
                        {
                            ra[i] = new IntPtr(vv);
                        }
                    }
                    return (T)(object)ra;
                }
                if (elem == typeof(UIntPtr))
                {
                    var la = TryGetLongArray(key);
                    if (la is null) return default;
                    var ra = new UIntPtr[la.Length];
                    for (int i = 0; i < la.Length; i++)
                    {
                        var vv = la[i];
                        if (vv < 0) return default;
                        if (UIntPtr.Size == 4)
                        {
                            if (vv > uint.MaxValue) return default;
                            ra[i] = new UIntPtr((uint)vv);
                        }
                        else
                        {
                            ra[i] = new UIntPtr((ulong)vv);
                        }
                    }
                    return (T)(object)ra;
                }
                if (elem == typeof(double))
                {
                    var da = TryGetDoubleArray(key);
                    return da is null ? default : (T)(object)da;
                }
                if (elem == typeof(bool))
                {
                    var ba = TryGetBoolArray(key);
                    return ba is null ? default : (T)(object)ba;
                }
                if (elem == typeof(string))
                {
                    var sa = TryGetStringArray(key);
                    return sa is null ? default : (T)(object)sa;
                }
                if (elem == typeof(byte[]))
                {
                    var bba = TryGetBytesArray(key);
                    return bba is null ? default : (T)(object)bba;
                }
                if (elem == typeof(DrxSerializationData))
                {
                    var oa = TryGetObjectArray(key);
                    return oa is null ? default : (T)(object)oa;
                }
            }

            // 未知类型，不支持
            return default;
        }

        // 原生类型数组读取方法（若类型不一致则返回 false）
        /// <summary>
        /// 尝试按键获取 64 位整数数组；当数组元素类型均为 Int64 时成功。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应 long 数组，否则返回 null。</returns>
        public long[]? TryGetLongArray(string key)
        {
            var arr = TryGetArray(key);
            if (arr is null) return null;
            var outArr = new long[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].Type != ValueType.Int64) return null;
                outArr[i] = arr[i].AsInt64();
            }
            return outArr;
        }

        /// <summary>
        /// 尝试按键获取 int 数组；当数组元素类型均为 Int64 且值可安全转换为 int 时成功。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应 int 数组，否则返回 null。</returns>
        public int[]? TryGetIntArray(string key)
        {
            var arr = TryGetArray(key);
            if (arr is null) return null;
            var outArr = new int[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].Type != ValueType.Int64)
                {
                    return null;
                }
                var v = arr[i].AsInt64();
                if (v < int.MinValue || v > int.MaxValue) return null;
                outArr[i] = (int)v;
            }
            return outArr;
        }

        /// <summary>
        /// 尝试按键获取 double 数组；当数组元素类型均为 Double 时成功。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应 double 数组，否则返回 null。</returns>
        public double[]? TryGetDoubleArray(string key)
        {
            var arr = TryGetArray(key);
            if (arr is null) return null;
            var outArr = new double[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].Type != ValueType.Double) return null;
                outArr[i] = arr[i].AsDouble();
            }
            return outArr;
        }

        /// <summary>
        /// 尝试按键获取 bool 数组；当数组元素类型均为 Bool 时成功。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应 bool 数组，否则返回 null。</returns>
        public bool[]? TryGetBoolArray(string key)
        {
            var arr = TryGetArray(key);
            if (arr is null) return null;
            var outArr = new bool[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].Type != ValueType.Bool) return null;
                outArr[i] = arr[i].AsBool();
            }
            return outArr;
        }

        /// <summary>
        /// 尝试按键获取字符串数组；支持数组元素为 null。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应字符串数组，否则返回 null。</returns>
        public string?[]? TryGetStringArray(string key)
        {
            var arr = TryGetArray(key);
            if (arr is null) return null;
            var outArr = new string?[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].Type == ValueType.Null)
                {
                    outArr[i] = null;
                    continue;
                }
                if (arr[i].Type != ValueType.String) return null;
                outArr[i] = arr[i].AsString();
            }
            return outArr;
        }

        /// <summary>
        /// 尝试按键获取字节数组数组；支持元素为 null。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应字节数组集合，否则返回 null。</returns>
        public byte[][]? TryGetBytesArray(string key)
        {
            var arr = TryGetArray(key);
            if (arr is null) return null;
            var outArr = new byte[arr.Length][];
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].Type == ValueType.Null)
                {
                    outArr[i] = null!;
                    continue;
                }
                if (arr[i].Type != ValueType.Bytes) return null;
                outArr[i] = arr[i].AsBytes()!;
            }
            return outArr;
        }

        /// <summary>
        /// 尝试按键获取嵌套对象数组；支持元素为 null。
        /// </summary>
        /// <param name="key">要获取的键。</param>
        /// <returns>成功时返回对应嵌套对象数组，否则返回 null。</returns>
        public DrxSerializationData?[]? TryGetObjectArray(string key)
        {
            var arr = TryGetArray(key);
            if (arr is null) return null;
            var outArr = new DrxSerializationData?[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].Type == ValueType.Null)
                {
                    outArr[i] = null;
                    continue;
                }
                if (arr[i].Type != ValueType.Object) return null;
                outArr[i] = arr[i].AsObject();
            }
            return outArr;
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

                // 将字符串编码到租用缓缓冲区
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
                case ValueType.Short:
                    WriteVarInt64(s, v.AsShort());
                    break;
                case ValueType.Int:
                    WriteVarInt64(s, v.AsInt());
                    break;
                case ValueType.UInt:
                    WriteVarInt64(s, v.AsUInt());
                    break;
                case ValueType.ULong:
                    WriteVarInt64(s, (long)v.AsULong());
                    break;
                case ValueType.Float:
                    WriteDouble(s, v.AsFloat());
                    break;
                case ValueType.Decimal:
                    WriteDouble(s, (double)v.AsDecimal());
                    break;
                case ValueType.Char:
                    WriteVarInt64(s, v.AsChar());
                    break;
                case ValueType.Byte:
                    WriteVarInt64(s, v.AsByte());
                    break;
                case ValueType.SByte:
                    WriteVarInt64(s, v.AsSByte());
                    break;
                case ValueType.IntPtr:
                    WriteVarInt64(s, v.AsIntPtr().ToInt64());
                    break;
                case ValueType.UIntPtr:
                    WriteVarInt64(s, (long)v.AsUIntPtr().ToUInt64());
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteVarUInt(Stream s, uint value)
        {
            while (value >= 0x80)
            {
                s.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            s.WriteByte((byte)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        // 性能优化：字符串写入方法，使用缓存的编码器和 ArrayPool
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteStringUtf8(Stream s, string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                WriteVarUInt(s, 0);
                return;
            }

            // 获取所需字节数
            int byteCount = Utf8NoBom.GetByteCount(str);
            WriteVarUInt(s, (uint)byteCount);

            if (byteCount == 0) return;

            // 对于小字符串使用 stackalloc，大字符串使用 ArrayPool
            if (byteCount <= 256)
            {
                Span<byte> buffer = stackalloc byte[byteCount];
                Utf8NoBom.GetBytes(str, buffer);
                s.Write(buffer);
            }
            else
            {
                byte[] buffer = BytePool.Rent(byteCount);
                try
                {
                    int written = Utf8NoBom.GetBytes(str, 0, str.Length, buffer, 0);
                    s.Write(buffer, 0, written);
                }
                finally
                {
                    BytePool.Return(buffer);
                }
            }
        }

        // 性能优化：字符串读取方法，使用 ArrayPool
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string ReadStringUtf8(Stream s)
        {
            uint len = ReadVarUInt(s);
            if (len == 0) return string.Empty;

            int length = (int)len;
            
            // 对于小字符串使用 stackalloc
            if (length <= 256)
            {
                Span<byte> buffer = stackalloc byte[length];
                int read = s.Read(buffer);
                if (read != length) throw new InvalidDataException("Unexpected end of stream reading string");
                return Utf8NoBom.GetString(buffer);
            }
            else
            {
                byte[] buffer = BytePool.Rent(length);
                try
                {
                    int read = s.Read(buffer, 0, length);
                    if (read != length) throw new InvalidDataException("Unexpected end of stream reading string");
                    return Utf8NoBom.GetString(buffer, 0, length);
                }
                finally
                {
                    BytePool.Return(buffer);
                }
            }
        }

        // ZigZag encode for signed integers, then store as varuint
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                                    var written = Encoding.UTF8.GetBytes(str, 0, str.Length, rentStr, 0);
                                    s.Write(rentStr, 0, written);
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
                        case ValueType.Short:
                            var sh = (short)ReadVarInt64(s);
                            ds.SetShort(key, sh);
                            break;
                        case ValueType.Int:
                            var i32 = (int)ReadVarInt64(s);
                            ds.SetInt32(key, i32);
                            break;
                        case ValueType.UInt:
                            var ui32 = (uint)ReadVarInt64(s);
                            ds.SetUInt32(key, ui32);
                            break;
                        case ValueType.ULong:
                            var ui64 = (ulong)ReadVarInt64(s);
                            ds.SetUInt64(key, ui64);
                            break;
                        case ValueType.Float:
                            var f = (float)ReadDouble(s);
                            ds.SetFloat(key, f);
                            break;
                        case ValueType.Decimal:
                            var dec = (decimal)ReadDouble(s);
                            ds.SetDecimal(key, dec);
                            break;
                        case ValueType.Char:
                            var ch = (char)ReadVarInt64(s);
                            ds.SetChar(key, ch);
                            break;
                        case ValueType.Byte:
                            var b = (byte)ReadVarInt64(s);
                            ds.SetByte(key, b);
                            break;
                        case ValueType.SByte:
                            var sb = (sbyte)ReadVarInt64(s);
                            ds.SetSByte(key, sb);
                            break;
                        case ValueType.IntPtr:
                            var ip = new IntPtr(ReadVarInt64(s));
                            ds.SetIntPtr(key, ip);
                            break;
                        case ValueType.UIntPtr:
                            var up = new UIntPtr((ulong)ReadVarInt64(s));
                            ds.SetUIntPtr(key, up);
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
                                var ss = ReadInt32(s);
                                if (ss == 0) { newObj.SetString(key, string.Empty); break; }
                                var sBuf = new byte[ss];
                                var sr = s.Read(sBuf, 0, ss);
                                if (sr != ss) throw new InvalidDataException("Unexpected end of stream reading string");
                                newObj.SetString(key, Encoding.UTF8.GetString(sBuf));
                                break;
                            case ValueType.Bytes:
                                var bl = ReadInt32(s);
                                var bBuf = new byte[bl];
                                var br = s.Read(bBuf, 0, bl);
                                if (br != bl) throw new InvalidDataException("Unexpected end of stream reading bytes");
                                newObj.SetBytes(key, bBuf);
                                break;
                            case ValueType.Object:
                                var child = ReadObjectPayload(s, idToObj);
                                newObj.SetObject(key, child);
                                break;
                            case ValueType.Array:
                                var arr = ReadArrayPayload(s, idToObj);
                                newObj.SetArray(key, arr);
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
                    var arr = ReadArrayPayload(s, idToObj);
                    return new DrxValue(arr);
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
                            var child = ReadObjectPayload(s, idToObj);
                            newObj.SetObject(key, child);
                            break;
                        case ValueType.Short:
                            var sh = (short)ReadVarInt64(s);
                            newObj.SetShort(key, sh);
                            break;
                        case ValueType.Int:
                            var i32 = (int)ReadVarInt64(s);
                            newObj.SetInt32(key, i32);
                            break;
                        case ValueType.UInt:
                            var ui32 = (uint)ReadVarInt64(s);
                            newObj.SetUInt32(key, ui32);
                            break;
                        case ValueType.ULong:
                            var ui64 = (ulong)ReadVarInt64(s);
                            newObj.SetUInt64(key, ui64);
                            break;
                        case ValueType.Float:
                            var f = (float)ReadDouble(s);
                            newObj.SetFloat(key, f);
                            break;
                        case ValueType.Decimal:
                            var dec = (decimal)ReadDouble(s);
                            newObj.SetDecimal(key, dec);
                            break;
                        case ValueType.Char:
                            var ch = (char)ReadVarInt64(s);
                            newObj.SetChar(key, ch);
                            break;
                        case ValueType.Byte:
                            var b = (byte)ReadVarInt64(s);
                            newObj.SetByte(key, b);
                            break;
                        case ValueType.SByte:
                            var sb = (sbyte)ReadVarInt64(s);
                            newObj.SetSByte(key, sb);
                            break;
                        case ValueType.IntPtr:
                            var ip = new IntPtr(ReadVarInt64(s));
                            newObj.SetIntPtr(key, ip);
                            break;
                        case ValueType.UIntPtr:
                            var up = new UIntPtr((ulong)ReadVarInt64(s));
                            newObj.SetUIntPtr(key, up);
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
                case ValueType.Short:
                    return new DrxValue((short)ReadVarInt64(s));
                case ValueType.Int:
                    return new DrxValue((int)ReadVarInt64(s));
                case ValueType.UInt:
                    return new DrxValue((uint)ReadVarInt64(s));
                case ValueType.ULong:
                    return new DrxValue((ulong)ReadVarInt64(s));
                case ValueType.Float:
                    return new DrxValue((float)ReadDouble(s));
                case ValueType.Decimal:
                    return new DrxValue((decimal)ReadDouble(s));
                case ValueType.Char:
                    return new DrxValue((char)ReadVarInt64(s));
                case ValueType.Byte:
                    return new DrxValue((byte)ReadVarInt64(s));
                case ValueType.SByte:
                    return new DrxValue((sbyte)ReadVarInt64(s));
                case ValueType.IntPtr:
                    return new DrxValue(new IntPtr(ReadVarInt64(s)));
                case ValueType.UIntPtr:
                    return new DrxValue(new UIntPtr((ulong)ReadVarInt64(s)));
                default:
                    throw new InvalidDataException("Unknown value type during deserialize (compact)");
            }
        }
        #endregion
    }
}
