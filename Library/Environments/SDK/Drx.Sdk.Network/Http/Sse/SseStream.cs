using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.Sse
{
    /// <summary>
    /// 客户端 SSE 流读取器，解析 text/event-stream 协议并分发事件。
    /// 支持自动重连、类型化事件订阅和断线续传（Last-Event-ID）。
    /// </summary>
    public class SseStream : IAsyncDisposable
    {
        private readonly System.Net.Http.HttpClient _httpClient;
        private readonly string _url;
        private readonly SseConnectOptions _options;
        private readonly ConcurrentDictionary<string, List<Action<SseEvent>>> _typedHandlers = new();
        private CancellationTokenSource _cts;
        private Task? _readTask;
        private string? _lastEventId;
        private int _retryAttempt;
        private bool _disposed;

        /// <summary>
        /// 收到任意 SSE 事件时触发
        /// </summary>
        public event EventHandler<SseEvent>? OnMessage;

        /// <summary>
        /// 连接建立时触发
        /// </summary>
        public event EventHandler? OnOpen;

        /// <summary>
        /// 连接关闭时触发（包括正常关闭和异常断开）
        /// </summary>
        public event EventHandler? OnClose;

        /// <summary>
        /// 发生错误时触发
        /// </summary>
        public event EventHandler<Exception>? OnError;

        /// <summary>
        /// 开始重连时触发，参数为 (重试次数, 延迟毫秒)
        /// </summary>
        public event EventHandler<(int Attempt, int DelayMs)>? OnReconnecting;

        /// <summary>
        /// 连接是否活跃
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// 最后收到的事件 ID
        /// </summary>
        public string? LastEventId => _lastEventId;

        internal SseStream(System.Net.Http.HttpClient httpClient, string url, SseConnectOptions? options)
        {
            _httpClient = httpClient;
            _url = url;
            _options = options ?? new SseConnectOptions();
            _lastEventId = _options.LastEventId;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(_options.CancellationToken);
        }

        /// <summary>
        /// 订阅特定事件名的处理器，数据自动反序列化为指定类型
        /// </summary>
        public SseStream OnEvent<T>(string eventName, Action<T> handler)
        {
            var wrapper = new Action<SseEvent>(e =>
            {
                try
                {
                    var data = JsonSerializer.Deserialize<T>(e.Data, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (data != null) handler(data);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, new InvalidOperationException($"反序列化事件 '{eventName}' 数据失败: {ex.Message}", ex));
                }
            });

            _typedHandlers.AddOrUpdate(
                eventName,
                _ => new List<Action<SseEvent>> { wrapper },
                (_, list) => { list.Add(wrapper); return list; }
            );

            return this;
        }

        /// <summary>
        /// 订阅特定事件名的原始字符串处理器
        /// </summary>
        public SseStream OnEvent(string eventName, Action<string> handler)
        {
            var wrapper = new Action<SseEvent>(e => handler(e.Data));

            _typedHandlers.AddOrUpdate(
                eventName,
                _ => new List<Action<SseEvent>> { wrapper },
                (_, list) => { list.Add(wrapper); return list; }
            );

            return this;
        }

        internal Task StartAsync()
        {
            _readTask = RunAsync();
            return Task.CompletedTask;
        }

        private async Task RunAsync()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await ConnectAndReadAsync(_cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    IsConnected = false;
                    OnError?.Invoke(this, ex);

                    var policy = _options.RetryPolicy;
                    if (policy == null || (policy.MaxRetries >= 0 && _retryAttempt >= policy.MaxRetries))
                    {
                        break;
                    }

                    var delay = policy.CalculateDelay(_retryAttempt);
                    OnReconnecting?.Invoke(this, (_retryAttempt, delay));
                    _retryAttempt++;

                    try
                    {
                        await Task.Delay(delay, _cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            IsConnected = false;
            OnClose?.Invoke(this, EventArgs.Empty);
        }

        private async Task ConnectAndReadAsync(CancellationToken ct)
        {
            using var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, _url);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

            if (_options.Headers != null)
            {
                foreach (var kvp in _options.Headers)
                {
                    request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }

            if (!string.IsNullOrEmpty(_lastEventId))
            {
                request.Headers.TryAddWithoutValidation("Last-Event-ID", _lastEventId);
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            IsConnected = true;
            _retryAttempt = 0;
            OnOpen?.Invoke(this, EventArgs.Empty);

            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? currentId = null;
            string currentEvent = "message";
            var dataBuilder = new StringBuilder();
            int? retry = null;

            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);

                if (line == null) break;

                if (string.IsNullOrEmpty(line))
                {
                    if (dataBuilder.Length > 0)
                    {
                        var data = dataBuilder.ToString();
                        if (data.EndsWith("\n"))
                            data = data.Substring(0, data.Length - 1);

                        var sseEvent = new SseEvent
                        {
                            Id = currentId,
                            EventName = currentEvent,
                            Data = data,
                            Retry = retry
                        };

                        if (currentId != null)
                            _lastEventId = currentId;

                        DispatchEvent(sseEvent);
                    }

                    currentId = null;
                    currentEvent = "message";
                    dataBuilder.Clear();
                    retry = null;
                    continue;
                }

                if (line.StartsWith(":"))
                    continue;

                var colonIndex = line.IndexOf(':');
                string field, value;

                if (colonIndex >= 0)
                {
                    field = line.Substring(0, colonIndex);
                    value = colonIndex + 1 < line.Length && line[colonIndex + 1] == ' '
                        ? line.Substring(colonIndex + 2)
                        : line.Substring(colonIndex + 1);
                }
                else
                {
                    field = line;
                    value = "";
                }

                switch (field)
                {
                    case "id":
                        currentId = value;
                        break;
                    case "event":
                        currentEvent = value;
                        break;
                    case "data":
                        dataBuilder.Append(value).Append('\n');
                        break;
                    case "retry":
                        if (int.TryParse(value, out var r))
                            retry = r;
                        break;
                }
            }
        }

        private void DispatchEvent(SseEvent sseEvent)
        {
            try
            {
                OnMessage?.Invoke(this, sseEvent);
            }
            catch { }

            if (_typedHandlers.TryGetValue(sseEvent.EventName, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    try { handler(sseEvent); } catch { }
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            try { _cts.Cancel(); } catch { }

            if (_readTask != null)
            {
                try { await _readTask.ConfigureAwait(false); } catch { }
            }

            try { _cts.Dispose(); } catch { }
            IsConnected = false;

            GC.SuppressFinalize(this);
        }
    }
}
