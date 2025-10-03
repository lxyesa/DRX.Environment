using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Drx.Sdk.Network.V2.Socket
{
    /// <summary>
    /// 网络客户端，支持 TCP 和 UDP 协议
    /// </summary>
    public class NetworkClient : IDisposable
    {
        // 远程端点
        private readonly IPEndPoint _remoteEndPoint;
        private readonly ProtocolType _protocolType;
        private readonly SocketType _socketType;
        private readonly System.Net.Sockets.Socket _realSocket;

        // 表示是否已释放
        public bool Disposed { get; private set; } = false;

        // 表示是否已连接（对于 UDP，如果调用 Connect 或成功 SendTo 后可能为 true）
        public bool Connected => _realSocket?.Connected ?? false;

        // 连接超时（秒）
        private float _timeout = 5.0f;
        public float Timeout
        {
            get => _timeout;
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "Timeout must be greater than zero.");
                _timeout = value;
            }
        }

        // 超时事件
        public delegate void TimeOutEventHandler(object sender, EventArgs e);
        public event TimeOutEventHandler? OnTimeout;

        // 构造器，根据协议类型创建对应 Socket
        public NetworkClient(IPEndPoint remoteEndPoint, ProtocolType protocolType = ProtocolType.Tcp)
        {
            _remoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            _protocolType = protocolType;
            _socketType = (_protocolType == ProtocolType.Udp) ? SocketType.Dgram : SocketType.Stream;
            _realSocket = new System.Net.Sockets.Socket(_remoteEndPoint.AddressFamily, _socketType, _protocolType);
        }

        // 事件：连接、断开、接收数据、错误
        public delegate void ConnectedHandler(NetworkClient sender);
        public delegate void DisconnectedHandler(NetworkClient sender);
        public delegate void DataReceivedHandler(NetworkClient sender, byte[] data, EndPoint remote);
        public delegate void ErrorHandler(NetworkClient sender, Exception ex);

        public event ConnectedHandler? OnConnected;
        public event DisconnectedHandler? OnDisconnected;
        public event DataReceivedHandler? OnDataReceived;
        public event ErrorHandler? OnError;

        // 接收循环控制
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;

        // 异步连接（主要用于 TCP），UDP 可选择 Connect 设置默认目标
        public async Task<bool> ConnectAsync()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(NetworkClient));

            if (_realSocket.Connected) return true;

            // 对于 UDP，我们可以直接 Connect 同步（设置默认远端）或直接返回 true
            if (_protocolType == ProtocolType.Udp)
            {
                try
                {
                    _realSocket.Connect(_remoteEndPoint);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    return false;
                }
            }

            // TCP: 使用 SocketAsyncEventArgs 封装
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var args = new SocketAsyncEventArgs { RemoteEndPoint = _remoteEndPoint };

            void CompletedHandler(object? s, SocketAsyncEventArgs e)
            {
                e.Completed -= CompletedHandler;
                if (e.SocketError == SocketError.Success) tcs.TrySetResult(true);
                else tcs.TrySetException(new SocketException((int)e.SocketError));
            }

            args.Completed += CompletedHandler;

            try
            {
                bool willRaiseEvent;
                try
                {
                    willRaiseEvent = _realSocket.ConnectAsync(args);
                }
                catch (NotSupportedException)
                {
                    // 降级到同步
                    _realSocket.Connect(_remoteEndPoint);
                    args.Completed -= CompletedHandler;
                    args.Dispose();
                    return _realSocket.Connected;
                }

                if (!willRaiseEvent)
                {
                    args.Completed -= CompletedHandler;
                    var ok = args.SocketError == SocketError.Success;
                    args.Dispose();
                    return ok;
                }

                var timeout = TimeSpan.FromSeconds(_timeout);
                using var cts = new CancellationTokenSource();
                var delayTask = Task.Delay(timeout, cts.Token);

                var completed = await Task.WhenAny(tcs.Task, delayTask).ConfigureAwait(false);
                if (completed == delayTask)
                {
                    try { _realSocket.Close(); } catch { }
                    OnTimeout?.Invoke(this, EventArgs.Empty);
                    args.Completed -= CompletedHandler;
                    args.Dispose();
                    return false;
                }

                cts.Cancel();
                await tcs.Task.ConfigureAwait(false);
                args.Completed -= CompletedHandler;
                args.Dispose();
                // 连接成功，启动接收循环
                if (_receiveCts == null)
                    _receiveCts = new CancellationTokenSource();
                _receiveTask = Task.Run(() => ReceiveLoop(_receiveCts.Token), _receiveCts.Token);

                OnConnected?.Invoke(this);

                return _realSocket.Connected;
            }
            catch (Exception ex)
            {
                try
                {
                    args.Completed -= CompletedHandler;
                    args.Dispose();
                }
                catch { }
                Debug.WriteLine(ex);
                return false;
            }
        }

        // 断开连接
        public void Disconnect()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(NetworkClient));
            if (_realSocket.Connected)
            {
                try { _realSocket.Shutdown(SocketShutdown.Both); } catch { }

                // 取消接收
                try { _receiveCts?.Cancel(); } catch { }

                try { _realSocket.Close(); } catch { }

                // 等待接收任务结束（非阻塞等待短时）
                try { _receiveTask?.Wait(100); } catch { }

                OnDisconnected?.Invoke(this);
            }
        }

        // 发送数据（针对 TCP 使用 Send，同步；针对 UDP 使用 SendTo）
        public void Send(byte[] data)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(NetworkClient));
            if (_protocolType == ProtocolType.Udp)
            {
                // UDP 不要求已Connect，也可以直接 SendTo
                try
                {
                    _realSocket.SendTo(data, _remoteEndPoint);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    throw;
                }
            }
            else
            {
                if (!_realSocket.Connected) throw new InvalidOperationException("Socket is not connected.");
                _realSocket.Send(data);
            }
        }

        public async Task<bool> SendAsync(byte[] data, IPEndPoint? target = null)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(NetworkClient));
            if (_protocolType == ProtocolType.Udp)
            {
                // UDP 可选择 Connect 或直接 SendTo
                try
                {
                    if (target != null)
                    {
                        await _realSocket.SendToAsync(new ArraySegment<byte>(data), SocketFlags.None, target).ConfigureAwait(false);
                    }
                    else
                    {
                        if (!_realSocket.Connected) throw new InvalidOperationException("Socket is not connected.");
                        await _realSocket.SendAsync(new ArraySegment<byte>(data), SocketFlags.None).ConfigureAwait(false);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    OnError?.Invoke(this, ex);
                    return false;
                }
            }
            else
            {
                if (!_realSocket.Connected) throw new InvalidOperationException("Socket is not connected.");
                try
                {
                    await _realSocket.SendAsync(new ArraySegment<byte>(data), SocketFlags.None).ConfigureAwait(false);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    OnError?.Invoke(this, ex);
                    return false;
                }
            }
        }

        // 接收循环（在单独线程上运行，使用阻塞 Receive）
        private void ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[8192];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int read;
                    try
                    {
                        read = _realSocket.Receive(buffer);
                    }
                    catch (System.Net.Sockets.SocketException se)
                    {
                        OnError?.Invoke(this, se);
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    if (read == 0) break; // 对端关闭

                    var data = new byte[read];
                    Array.Copy(buffer, 0, data, 0, read);
                    try
                    {
                        OnDataReceived?.Invoke(this, data, _realSocket.RemoteEndPoint!);
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(this, ex);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
            }
            finally
            {
                try { _receiveCts?.Cancel(); } catch { }
                try { _realSocket.Close(); } catch { }
                OnDisconnected?.Invoke(this);
            }
        }

        // 获取底层 Socket
        public System.Net.Sockets.Socket GetRawSocket()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(NetworkClient));
            return _realSocket;
        }

        public IPEndPoint GetRemoteEndPoint() => _remoteEndPoint;
        public ProtocolType GetProtocolType() => _protocolType;

        // 释放资源
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            try { _receiveCts?.Cancel(); } catch { }
            try { _receiveTask?.Wait(100); } catch { }
            try { _realSocket.Dispose(); } catch { }
            OnDisconnected?.Invoke(this);
        }
    }
}