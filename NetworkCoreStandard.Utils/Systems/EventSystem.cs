using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using NetworkCoreStandard.Utils.Interface;
using NetworkCoreStandard.EventArgs;
using System.Threading.Channels;

namespace NetworkCoreStandard.Utils.Systems
{
    public class EventSystem : IEventSystem, IDisposable
    {
        // Win32 API 常量和导入
        private const uint WM_USER = 0x0400;
        private const uint WM_EVENT = WM_USER + 1;
        
        [DllImport("user32.dll")]
        private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        // 高性能并发集合
        private readonly ConcurrentDictionary<string, ConcurrentBag<EventHandler<NetworkEventArgs>>> _eventHandlers = new();
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

        public void AddListener(string eventName, EventHandler<NetworkEventArgs> handler)
        {
            _eventHandlers.AddOrUpdate(
                eventName,
                new ConcurrentBag<EventHandler<NetworkEventArgs>>(new[] { handler }),
                (_, bag) =>
                {
                    bag.Add(handler);
                    return bag;
                });
        }

        public void RemoveListener(string eventName, EventHandler<NetworkEventArgs> handler)
        {
            if (_eventHandlers.TryGetValue(eventName, out var handlers))
            {
                var newBag = new ConcurrentBag<EventHandler<NetworkEventArgs>>(
                    handlers.Where(h => h != handler));
                
                _eventHandlers.TryUpdate(eventName, newBag, handlers);
                if (newBag.IsEmpty)
                {
                    _eventHandlers.TryRemove(eventName, out _);
                }
            }
        }

        public async Task PushEventAsync(string eventName, NetworkEventArgs args)
        {
            await _eventChannel.Writer.WriteAsync((eventName, args), _cts.Token);
            PostThreadMessage(_processingThreadId, WM_EVENT, IntPtr.Zero, IntPtr.Zero);
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
                    handler.Invoke(this, args);
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
    }
}