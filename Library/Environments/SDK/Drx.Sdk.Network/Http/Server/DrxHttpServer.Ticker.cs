using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Entry;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpServer 高性能 Ticker API 部分
    /// </summary>
    public partial class DrxHttpServer
    {
        /// <summary>
        /// 每隔指定毫秒执行一次回调（返回 IDisposable 用于取消）。
        /// </summary>
        public IDisposable DoTicker(int intervalMs, Action<DrxHttpServer> callback)
        {
            if (intervalMs <= 0) throw new ArgumentException("intervalMs must be > 0", nameof(intervalMs));
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            var id = Interlocked.Increment(ref _tickerIdCounter);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var entry = new TickerEntry
            {
                Id = id,
                IntervalMs = intervalMs,
                NextDueMs = now + intervalMs,
                SyncCallback = callback,
                Cancelled = false
            };
            _tickers[id] = entry;
            EnsureTickerThreadRunning();
            _tickerWake?.Set();
            return new TickerRegistration(this, id);
        }

        /// <summary>
        /// 异步回调版本
        /// </summary>
        public IDisposable DoTicker(int intervalMs, Func<DrxHttpServer, Task> asyncCallback)
        {
            if (intervalMs <= 0) throw new ArgumentException("intervalMs must be > 0", nameof(intervalMs));
            if (asyncCallback == null) throw new ArgumentNullException(nameof(asyncCallback));

            var id = Interlocked.Increment(ref _tickerIdCounter);
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var entry = new TickerEntry
            {
                Id = id,
                IntervalMs = intervalMs,
                NextDueMs = now + intervalMs,
                AsyncCallback = asyncCallback,
                Cancelled = false
            };
            _tickers[id] = entry;
            EnsureTickerThreadRunning();
            _tickerWake?.Set();
            return new TickerRegistration(this, id);
        }

        private void EnsureTickerThreadRunning()
        {
            lock (_tickerLock)
            {
                if (_tickerThread != null && _tickerThread.IsAlive) return;
                _tickerThread = new Thread(() => TickerLoop())
                {
                    IsBackground = true,
                    Name = "DrxHttpServer-Ticker",
                    Priority = ThreadPriority.AboveNormal
                };
                _tickerThread.Start();
            }
        }

        internal void UnregisterTicker(int id)
        {
            if (_tickers.TryRemove(id, out _)) { _tickerWake?.Set(); }
        }

        private void TickerLoop()
        {
            try
            {
                while (!(_cts?.IsCancellationRequested ?? true))
                {
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long nextDueMs = long.MaxValue;
                    var due = new List<TickerEntry>();

                    foreach (var kv in _tickers)
                    {
                        var e = kv.Value;
                        if (e.Cancelled) continue;
                        if (now >= e.NextDueMs)
                        {
                            due.Add(e);
                            var missed = (int)Math.Max(0, (now - e.NextDueMs) / (long)e.IntervalMs);
                            e.NextDueMs += (missed + 1) * (long)e.IntervalMs;
                        }
                        if (e.NextDueMs < nextDueMs) nextDueMs = e.NextDueMs;
                    }

                    if (due.Count > 0)
                    {
                        foreach (var e in due)
                        {
                            try
                            {
                                if (e.SyncCallback != null)
                                {
                                    Task.Run(() =>
                                    {
                                        try { e.SyncCallback(this); }
                                        catch (Exception ex) { try { Logger.Error($"Ticker callback error (id={e.Id}): {ex}"); } catch { } }
                                    });
                                }
                                else if (e.AsyncCallback != null)
                                {
                                    _ = e.AsyncCallback(this).ContinueWith(t =>
                                    {
                                        if (t.Exception != null)
                                        {
                                            try { Logger.Error($"Ticker async callback error (id={e.Id}): {t.Exception}"); } catch { }
                                        }
                                    }, TaskContinuationOptions.ExecuteSynchronously);
                                }
                            }
                            catch (Exception ex)
                            {
                                try { Logger.Error($"Dispatch ticker (id={e.Id}) failed: {ex}"); } catch { }
                            }
                        }
                        continue;
                    }

                    if (nextDueMs == long.MaxValue)
                    {
                        _tickerWake?.WaitOne(Timeout.Infinite);
                        continue;
                    }

                    var waitMs = (int)Math.Max(1, Math.Min((int)(nextDueMs - now), 1000));
                    _tickerWake?.WaitOne(waitMs);
                }
            }
            catch (ThreadAbortException) { }
            catch (Exception ex)
            {
                try { Logger.Error($"TickerLoop 异常: {ex}"); } catch { }
            }
        }
    }
}
