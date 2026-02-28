using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Http.Sse
{
    /// <summary>
    /// ISseWriter 的默认实现，封装 HttpListenerContext 的 OutputStream 进行 SSE 推送。
    /// 框架内部创建，在 [HttpSse] 方法调用前初始化。
    /// </summary>
    public class SseWriter : ISseWriter
    {
        private readonly HttpListenerContext _ctx;
        private readonly Stream _stream;
        private long _eventId;
        private bool _rejected;
        private bool _headersSent;
        private readonly object _writeLock = new();

        public string ClientId { get; }
        public string? LastEventId { get; }
        public bool IsConnected { get; private set; }
        public long CurrentEventId => Interlocked.Read(ref _eventId);

        internal SseWriter(HttpListenerContext ctx)
        {
            _ctx = ctx;
            ClientId = Guid.NewGuid().ToString("N");
            LastEventId = ctx.Request.Headers["Last-Event-ID"];
            _stream = ctx.Response.OutputStream;
            IsConnected = true;
        }

        internal void InitializeHeaders()
        {
            if (_headersSent) return;
            _ctx.Response.ContentType = "text/event-stream";
            _ctx.Response.Headers.Set("Cache-Control", "no-cache");
            _ctx.Response.Headers.Set("Connection", "keep-alive");
            _ctx.Response.Headers.Set("Access-Control-Allow-Origin", "*");
            _ctx.Response.Headers.Set("X-Accel-Buffering", "no");
            _ctx.Response.StatusCode = 200;
            _ctx.Response.SendChunked = true;
            _headersSent = true;
        }

        public async Task SendAsync(string? eventName, string data)
        {
            EnsureConnected();
            InitializeHeaders();

            var id = Interlocked.Increment(ref _eventId);
            var sb = new StringBuilder();
            sb.Append("id: ").Append(id).Append('\n');
            if (!string.IsNullOrEmpty(eventName))
                sb.Append("event: ").Append(eventName).Append('\n');
            foreach (var line in data.Split('\n'))
                sb.Append("data: ").Append(line).Append('\n');
            sb.Append('\n');

            await WriteRawAsync(sb.ToString()).ConfigureAwait(false);
        }

        public async Task SendJsonAsync<T>(string? eventName, T data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
            await SendAsync(eventName, json).ConfigureAwait(false);
        }

        public async Task SendBatchAsync(IEnumerable<(string? EventName, string Data)> events)
        {
            EnsureConnected();
            InitializeHeaders();

            var sb = new StringBuilder();
            foreach (var (eventName, data) in events)
            {
                var id = Interlocked.Increment(ref _eventId);
                sb.Append("id: ").Append(id).Append('\n');
                if (!string.IsNullOrEmpty(eventName))
                    sb.Append("event: ").Append(eventName).Append('\n');
                foreach (var line in data.Split('\n'))
                    sb.Append("data: ").Append(line).Append('\n');
                sb.Append('\n');
            }

            await WriteRawAsync(sb.ToString()).ConfigureAwait(false);
        }

        public async Task PingAsync()
        {
            EnsureConnected();
            InitializeHeaders();
            await WriteRawAsync(": heartbeat\n\n").ConfigureAwait(false);
        }

        public async Task SetRetryAsync(int milliseconds)
        {
            EnsureConnected();
            InitializeHeaders();
            await WriteRawAsync($"retry: {milliseconds}\n\n").ConfigureAwait(false);
        }

        public Task RejectAsync(int statusCode, string? reason = null)
        {
            if (_headersSent)
                throw new InvalidOperationException("SSE 响应头已发送，无法拒绝连接");

            _rejected = true;
            IsConnected = false;
            _ctx.Response.StatusCode = statusCode;

            if (!string.IsNullOrEmpty(reason))
            {
                var bytes = Encoding.UTF8.GetBytes(reason);
                _ctx.Response.ContentType = "text/plain; charset=utf-8";
                _ctx.Response.ContentLength64 = bytes.Length;
                _ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }

            try { _ctx.Response.Close(); } catch { }
            return Task.CompletedTask;
        }

        internal void MarkDisconnected() => IsConnected = false;

        internal bool WasRejected => _rejected;

        private async Task WriteRawAsync(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await _stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            await _stream.FlushAsync().ConfigureAwait(false);
        }

        private void EnsureConnected()
        {
            if (_rejected)
                throw new InvalidOperationException("SSE 连接已被拒绝");
            if (!IsConnected)
                throw new InvalidOperationException("SSE 连接已断开");
        }
    }
}
