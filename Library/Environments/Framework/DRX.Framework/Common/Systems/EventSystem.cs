using DRX.Framework.Common.Args;
using DRX.Framework.Common.Interface;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace DRX.Framework.Common.Systems
{
    public class EventSystem : IEventSystem, IDisposable
    {
        // Win32 API 常量和导入
        private const uint WM_USER = 0x0400;
        private const uint WM_EVENT = WM_USER + 1;

        [DllImport("user32.dll")]
        private static extern bool PostThreadMessage(uint threadId, uint msg, nint wParam, nint lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        // 高性能并发集合
        private readonly ConcurrentDictionary<string, ConcurrentBag<RegisteredHandler>> _eventHandlers = new();
        private readonly ConcurrentDictionary<uint, string> _eventIdToNameMap = new();
        private readonly ConcurrentDictionary<Guid, (string eventName, EventHandler<NetworkEventArgs> handler)> _handlerIdMap = new();
        private readonly Channel<(string eventName, NetworkEventArgs args)> _eventChannel;
        private readonly uint _processingThreadId;
        private volatile bool _isProcessing;
        private readonly CancellationTokenSource _cts;

        public EventSystem()
        {
            var options = new BoundedChannelOptions(10000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            };

            _eventChannel = Channel.CreateBounded<(string, NetworkEventArgs)>(options);
            _cts = new CancellationTokenSource();
            _processingThreadId = GetCurrentThreadId();
            StartEventProcessing();
        }

        public Guid AddListener(string eventName, EventHandler<NetworkEventArgs> handler)
        {
            return AddListener(eventName, handler, null);
        }

        /// <summary>
        /// 添加事件监听器，支持唯一标识符以确保监听器的唯一性。
        /// </summary>
        /// <param name="eventName">事件名称。</param>
        /// <param name="handler">事件处理方法。</param>
        /// <param name="uniqueId">监听器的唯一标识符。如果提供，将确保该监听器唯一。</param>
        public Guid AddListener(string eventName, EventHandler<NetworkEventArgs> handler, string? uniqueId)
        {
            var handlerId = Guid.NewGuid();

            if (uniqueId != null)
            {
                var handlers = _eventHandlers.GetOrAdd(eventName, _ => new ConcurrentBag<RegisteredHandler>());

                // 检查是否已存在相同 uniqueId 的监听器
                if (handlers.Any(h => h.UniqueId == uniqueId))
                {
                    return handlerId;
                }

                handlers.Add(new RegisteredHandler { UniqueId = uniqueId, Handler = handler });
            }
            else
            {
                _eventHandlers.AddOrUpdate(
                    eventName,
                    new ConcurrentBag<RegisteredHandler>(new[] { new RegisteredHandler { Handler = handler } }),
                    (_, bag) =>
                    {
                        bag.Add(new RegisteredHandler { Handler = handler });
                        return bag;
                    });
            }

            _handlerIdMap.TryAdd(handlerId, (eventName, handler));
            return handlerId;
        }

        public void AddListener(uint eventId, EventHandler<NetworkEventArgs> handler)
        {
            if (_eventIdToNameMap.ContainsKey(eventId))
            {
                throw new ArgumentException($"Event ID {eventId} already exists.");
            }

            var eventName = eventId.ToString();
            _ = _eventIdToNameMap.TryAdd(eventId, eventName);
            AddListener(eventName, handler, null);
        }

        public void RemoveListener(Guid handlerId)
        {
            // 获取与 handlerId 关联的事件处理器
            if (_handlerIdMap.TryGetValue(handlerId, out var handlerInfo))
            {
                RemoveListener(handlerInfo.eventName, handlerId);
            }
        }

        public void RemoveListener(string eventName, Guid handlerId)
        {
            if (_eventHandlers.TryGetValue(eventName, out var handlers))
            {
                var updatedHandlers = new ConcurrentBag<RegisteredHandler>(
                    handlers.Where(h => !_handlerIdMap.ContainsKey(handlerId) || _handlerIdMap[handlerId].handler != h.Handler));

                _eventHandlers.TryUpdate(eventName, updatedHandlers, handlers);
                if (updatedHandlers.IsEmpty)
                {
                    _eventHandlers.TryRemove(eventName, out _);
                }
            }
        }

        public async Task PushEventAsync(string eventName, NetworkEventArgs args)
        {
            await _eventChannel.Writer.WriteAsync((eventName, args), _cts.Token);
            PostThreadMessage(_processingThreadId, WM_EVENT, nint.Zero, nint.Zero);
        }

        public async Task PushEventAsync(uint eventId, NetworkEventArgs args)
        {
            if (_eventIdToNameMap.TryGetValue(eventId, out var eventName))
            {
                await PushEventAsync(eventName, args);
            }
            else
            {
                throw new ArgumentException($"Event ID {eventId} not found.");
            }
        }

        public void StartEventProcessing()
        {
            if (_isProcessing) return;

            _isProcessing = true;
            Task.Factory.StartNew(
                ProcessEventsAsync,
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void StopEventProcessing()
        {
            _isProcessing = false;
            _cts.Cancel();
        }

        private async Task ProcessEventsAsync()
        {
            try
            {
                while (_isProcessing && !_cts.Token.IsCancellationRequested)
                {
                    var (eventName, args) = await _eventChannel.Reader.ReadAsync(_cts.Token);
                    await ProcessSingleEventAsync(eventName, args);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略异常
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "EventSystem", $"事件处理循环发生错误: {ex}");
            }
        }

        private async Task ProcessSingleEventAsync(string eventName, NetworkEventArgs args)
        {
            if (!_eventHandlers.TryGetValue(eventName, out var handlers))
                return;

            var tasks = handlers.Select(handler => Task.Run(() =>
            {
                try
                {
                    handler.Handler.Invoke(this, args);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "EventSystem",
                        $"处理事件 {eventName} 时发生异常: {ex.Message}");
                }
            }, _cts.Token));

            await Task.WhenAll(tasks);
        }

        public void Dispose()
        {
            StopEventProcessing();
            _cts.Dispose();
            _eventChannel.Writer.Complete();
        }

        /// <summary>
        /// 注册的事件处理器，包含可选的唯一标识符。
        /// </summary>
        private class RegisteredHandler
        {
            public string? UniqueId { get; set; }
            public EventHandler<NetworkEventArgs> Handler { get; set; } = default!;
        }
    }
}
