using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// 进度流包装：用于在流读取时上报进度（不会在 Dispose 时关闭底层流）。
    /// </summary>
    internal class ProgressableStreamContent : Stream
    {
        private readonly Stream _inner;
        private readonly int _bufferSize;
        private readonly IProgress<long>? _progress;
        private readonly CancellationToken _cancellation;
        private long _totalRead;

        public ProgressableStreamContent(Stream inner, int bufferSize, IProgress<long>? progress, CancellationToken cancellation)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _bufferSize = bufferSize;
            _progress = progress;
            _cancellation = cancellation;
            _totalRead = 0;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override int Read(byte[] buffer, int offset, int count)
        {
            _cancellation.ThrowIfCancellationRequested();
            var read = _inner.Read(buffer, offset, Math.Min(count, _bufferSize));
            if (read > 0)
            {
                _totalRead += read;
                _progress?.Report(_totalRead);
            }
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _cancellation.ThrowIfCancellationRequested();
            var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read > 0)
            {
                _totalRead += read;
                _progress?.Report(_totalRead);
            }
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => _inner.BeginRead(buffer, offset, count, callback, state);
        public override int EndRead(IAsyncResult asyncResult) => _inner.EndRead(asyncResult);
        protected override void Dispose(bool disposing)
        {
            // 不在此处关闭底层流，调用方负责管理流的生命周期
            base.Dispose(disposing);
        }
    }
}
