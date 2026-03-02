using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// HTTP 请求/响应处理的对象池和缓冲区复用管理器。
    /// 通过池化频繁创建的对象和复用字节缓冲区，显著减少 GC 压力和内存分配。
    /// 
    /// 多核优化策略：
    ///   - 使用 ThreadLocal + 全局 ConcurrentQueue 的两层池化，减少跨核争用
    ///   - ThreadLocal 层（L1 缓存）：每线程独享，零竞争
    ///   - ConcurrentQueue 层（L2 缓存）：线程间共享，低争用回退
    ///   - 使用 ArrayPool&lt;byte&gt; 复用字节缓冲区（避免每次请求分配新数组）
    ///   - 使用 PooledMemoryStream 替代 MemoryStream（减少 LOH 分配）
    /// </summary>
    internal static class HttpObjectPool
    {
        /// <summary>
        /// 共享的字节数组池，用于请求体读取和响应体写入。
        /// 使用 .NET 内置的 ArrayPool 实现高效的缓冲区租借和归还。
        /// </summary>
        public static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Shared;

        /// <summary>
        /// 默认缓冲区大小（用于请求体读取），4KB 适合大多数 API 请求
        /// </summary>
        public const int DefaultBufferSize = 4096;

        /// <summary>
        /// 大缓冲区大小（用于文件流等场景），64KB
        /// </summary>
        public const int LargeBufferSize = 65536;

        #region Dictionary 对象池（两层池化）

        // L1: ThreadLocal 层（每线程独享栈，零竞争）
        [ThreadStatic]
        private static Stack<Dictionary<string, string>>? _dictLocalPool;
        private const int MaxLocalDictPoolSize = 8;

        // L2: 全局 ConcurrentQueue 回退层
        private static readonly ConcurrentQueue<Dictionary<string, string>> _dictGlobalPool = new();
        private static int _dictGlobalPoolCount = 0;
        private const int MaxGlobalDictPoolSize = 64;

        /// <summary>
        /// 从池中租借一个 Dictionary&lt;string, string&gt;（用于路由参数等场景）
        /// 优先从 ThreadLocal L1 缓存获取，回退到全局 L2 缓存。
        /// </summary>
        public static Dictionary<string, string> RentDictionary()
        {
            // L1: ThreadLocal 栈
            _dictLocalPool ??= new Stack<Dictionary<string, string>>();
            if (_dictLocalPool.Count > 0)
            {
                var dict = _dictLocalPool.Pop();
                dict.Clear();
                return dict;
            }

            // L2: 全局 ConcurrentQueue
            if (_dictGlobalPool.TryDequeue(out var globalDict))
            {
                Interlocked.Decrement(ref _dictGlobalPoolCount);
                globalDict.Clear();
                return globalDict;
            }

            return new Dictionary<string, string>();
        }

        /// <summary>
        /// 将 Dictionary 归还到池中供下次复用
        /// 优先归还到 ThreadLocal L1 缓存，溢出部分归还到全局 L2 缓存。
        /// </summary>
        public static void ReturnDictionary(Dictionary<string, string> dict)
        {
            if (dict == null) return;
            dict.Clear();

            // L1: ThreadLocal 栈
            _dictLocalPool ??= new Stack<Dictionary<string, string>>();
            if (_dictLocalPool.Count < MaxLocalDictPoolSize)
            {
                _dictLocalPool.Push(dict);
                return;
            }

            // L2: 全局 ConcurrentQueue（溢出）
            if (Interlocked.Increment(ref _dictGlobalPoolCount) <= MaxGlobalDictPoolSize)
            {
                _dictGlobalPool.Enqueue(dict);
            }
            else
            {
                Interlocked.Decrement(ref _dictGlobalPoolCount);
            }
        }

        #endregion

        #region StringBuilder 对象池（两层池化）

        // L1: ThreadLocal 层
        [ThreadStatic]
        private static Stack<StringBuilder>? _sbLocalPool;
        private const int MaxLocalSbPoolSize = 4;

        // L2: 全局 ConcurrentQueue 回退层
        private static readonly ConcurrentQueue<StringBuilder> _sbGlobalPool = new();
        private static int _sbGlobalPoolCount = 0;
        private const int MaxGlobalSbPoolSize = 32;

        /// <summary>
        /// 从池中租借一个 StringBuilder
        /// </summary>
        public static StringBuilder RentStringBuilder()
        {
            // L1: ThreadLocal 栈
            _sbLocalPool ??= new Stack<StringBuilder>();
            if (_sbLocalPool.Count > 0)
            {
                var sb = _sbLocalPool.Pop();
                sb.Clear();
                return sb;
            }

            // L2: 全局 ConcurrentQueue
            if (_sbGlobalPool.TryDequeue(out var globalSb))
            {
                Interlocked.Decrement(ref _sbGlobalPoolCount);
                globalSb.Clear();
                return globalSb;
            }

            return new StringBuilder(256);
        }

        /// <summary>
        /// 将 StringBuilder 归还到池中
        /// </summary>
        public static void ReturnStringBuilder(StringBuilder sb)
        {
            if (sb == null) return;
            // 过大的 StringBuilder 不归还（避免内存浪费）
            if (sb.Capacity > 8192) return;

            sb.Clear();

            // L1: ThreadLocal 栈
            _sbLocalPool ??= new Stack<StringBuilder>();
            if (_sbLocalPool.Count < MaxLocalSbPoolSize)
            {
                _sbLocalPool.Push(sb);
                return;
            }

            // L2: 全局 ConcurrentQueue（溢出）
            if (Interlocked.Increment(ref _sbGlobalPoolCount) <= MaxGlobalSbPoolSize)
            {
                _sbGlobalPool.Enqueue(sb);
            }
            else
            {
                Interlocked.Decrement(ref _sbGlobalPoolCount);
            }
        }

        #endregion

        #region 可回收 MemoryStream

        /// <summary>
        /// 创建一个基于池化缓冲区的 MemoryStream。
        /// 此实现使用 ArrayPool 租借的缓冲区作为底层存储，在 Dispose 时归还缓冲区。
        /// 适用于请求体读取等需要临时缓冲的场景。
        /// </summary>
        /// <param name="initialCapacity">初始容量（会向上对齐到 ArrayPool 的桶大小）</param>
        public static PooledMemoryStream CreatePooledMemoryStream(int initialCapacity = DefaultBufferSize)
        {
            return new PooledMemoryStream(initialCapacity);
        }

        #endregion
    }

    /// <summary>
    /// 基于 ArrayPool 的可回收 MemoryStream 实现。
    /// 与标准 MemoryStream 的关键区别：
    ///   - 底层缓冲区从 ArrayPool 租借而非直接分配
    ///   - Dispose 时自动归还缓冲区（不会进入 GC）
    ///   - 支持动态扩容（扩容时归还旧缓冲区，租借新缓冲区）
    /// 减少大对象堆（LOH）分配和 Gen2 GC。
    /// </summary>
    internal sealed class PooledMemoryStream : Stream
    {
        private byte[] _buffer;
        private int _length;
        private int _position;
        private bool _disposed;

        public PooledMemoryStream(int initialCapacity = 4096)
        {
            _buffer = HttpObjectPool.BytePool.Rent(Math.Max(initialCapacity, 256));
            _length = 0;
            _position = 0;
        }

        public override bool CanRead => !_disposed;
        public override bool CanSeek => !_disposed;
        public override bool CanWrite => !_disposed;
        public override long Length => _length;
        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _position = (int)value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            int available = _length - _position;
            if (available <= 0) return 0;
            int toRead = Math.Min(count, available);
            Buffer.BlockCopy(_buffer, _position, buffer, offset, toRead);
            _position += toRead;
            return toRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            EnsureCapacity(_position + count);
            Buffer.BlockCopy(buffer, offset, _buffer, _position, count);
            _position += count;
            if (_position > _length) _length = _position;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            int newPos = origin switch
            {
                SeekOrigin.Begin => (int)offset,
                SeekOrigin.Current => _position + (int)offset,
                SeekOrigin.End => _length + (int)offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            if (newPos < 0) throw new IOException("Seek before begin");
            _position = newPos;
            return _position;
        }

        public override void SetLength(long value)
        {
            ThrowIfDisposed();
            EnsureCapacity((int)value);
            _length = (int)value;
            if (_position > _length) _position = _length;
        }

        public override void Flush() { }

        /// <summary>
        /// 将已写入的数据复制到新的字节数组（类似 MemoryStream.ToArray）
        /// </summary>
        public byte[] ToArray()
        {
            ThrowIfDisposed();
            var result = new byte[_length];
            Buffer.BlockCopy(_buffer, 0, result, 0, _length);
            return result;
        }

        /// <summary>
        /// 获取已写入数据的 ReadOnlySpan 视图（零拷贝访问，避免 ToArray 分配）
        /// </summary>
        public ReadOnlySpan<byte> GetWrittenSpan()
        {
            ThrowIfDisposed();
            return new ReadOnlySpan<byte>(_buffer, 0, _length);
        }

        /// <summary>
        /// 获取已写入数据的 ReadOnlyMemory 视图
        /// </summary>
        public ReadOnlyMemory<byte> GetWrittenMemory()
        {
            ThrowIfDisposed();
            return new ReadOnlyMemory<byte>(_buffer, 0, _length);
        }

        private void EnsureCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= _buffer.Length) return;

            int newCapacity = Math.Max(_buffer.Length * 2, requiredCapacity);
            var newBuffer = HttpObjectPool.BytePool.Rent(newCapacity);
            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _length);
            HttpObjectPool.BytePool.Return(_buffer);
            _buffer = newBuffer;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PooledMemoryStream));
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_buffer != null)
                {
                    HttpObjectPool.BytePool.Return(_buffer);
                    _buffer = null!;
                }
            }
            base.Dispose(disposing);
        }
    }
}
