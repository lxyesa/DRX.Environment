using DRX.Framework.Common.Args;
using DRX.Framework.Common.Base;
using DRX.Framework.Common.Enums.Packet;
using DRX.Framework.Common.Models;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace DRX.Framework.Common
{
    /// <summary>
    /// 网络客户端类，用于连接和通信与服务器。
    /// </summary>
    public abstract class DRXClient : DRXBehaviour
    {
        #region 字段
        /// <summary>
        /// 客户端Socket实例。
        /// </summary>
        protected DRXSocket _socket;

        /// <summary>
        /// 加密密钥。
        /// </summary>
        protected string _key;

        /// <summary>
        /// 服务器IP地址。
        /// </summary>
        protected string _serverIP;

        /// <summary>
        /// 服务器端口号。
        /// </summary>
        protected int _serverPort;

        /// <summary>
        /// 客户端连接状态。
        /// </summary>
        protected bool _isConnected;

        /// <summary>
        /// 服务器最后一次操作时间。
        /// </summary>
        protected DateTime _serverLastActionTime;

        /// <summary>
        /// 待处理的请求集合。
        /// </summary>
        protected readonly ConcurrentDictionary<string, TaskCompletionSource<byte[]>> _pendingRequests
            = new ConcurrentDictionary<string, TaskCompletionSource<byte[]>>();
        #endregion

        #region 事件
        /// <summary>
        /// 接收数据时触发的事件。
        /// </summary>
        public event EventHandler<NetworkEventArgs>? OnReceiveCallback;

        /// <summary>
        /// 发生错误时触发的事件。
        /// </summary>
        public event EventHandler<NetworkEventArgs>? OnErrorCallback;

        /// <summary>
        /// 连接成功时触发的事件。
        /// </summary>
        public event EventHandler<NetworkEventArgs>? OnConnectedCallback;

        /// <summary>
        /// 断开连接时触发的事件。
        /// </summary>
        public event EventHandler<NetworkEventArgs>? OnDisconnectedCallback;

        /// <summary>
        /// 数据发送完成时触发的事件。
        /// </summary>
        public event EventHandler<NetworkEventArgs>? OnDataSentCallback;
        #endregion

        #region 属性
        /// <summary>
        /// 获取客户端的连接状态。
        /// </summary>
        public bool IsConnected => _isConnected;
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化 <see cref="DRXClient"/> 的新实例。
        /// </summary>
        /// <param name="serverIP">服务器的IP地址。</param>
        /// <param name="serverPort">服务器的端口号。</param>
        public DRXClient(string serverIP, int serverPort, string key) : base()
        {
            _serverIP = serverIP;
            _serverPort = serverPort;
            _socket = new DRXSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _isConnected = false;
            _key = key;
        }
        #endregion

        #region 连接方法
        /// <summary>
        /// 连接到服务器。
        /// </summary>
        public virtual void Connect()
        {
            try
            {
                _ = _socket.BeginConnect(new IPEndPoint(IPAddress.Parse(_serverIP), _serverPort),
                    new AsyncCallback(ConnectCallback), null);
            }
            catch (Exception ex)
            {
                OnErrorCallback?.Invoke(this, new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"连接服务器时发生错误: {ex.Message}"
                ));
            }
        }

        /// <summary>
        /// 处理连接回调。
        /// </summary>
        /// <param name="ar">异步操作结果。</param>
        protected virtual void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                _socket.EndConnect(ar);
                _isConnected = true;

                OnConnectedCallback?.Invoke(this, new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent
                ));

                BeginReceive();
            }
            catch (Exception ex)
            {
                OnErrorCallback?.Invoke(this, new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"连接服务器时发生错误: {ex.Message}"
                ));
            }
        }

        /// <summary>
        /// 发送基于 <see cref="BasePacket{T}"/> 的数据包，并等待响应。
        /// </summary>
        /// <typeparam name="T">数据包类型，必须继承自 <see cref="BasePacket{T}"/> 并具有公共的无参数构造函数。</typeparam>
        /// <param name="packet">要发送的数据包。</param>
        /// <param name="timeout">等待响应的超时时间（毫秒）。</param>
        /// <returns>服务器响应的数据包字节数组。</returns>
        public virtual async Task<byte[]> SendAsync<T>(T packet, int timeout = 0) where T : BasePacket<T>, new()
        {
            if (!_isConnected)
            {
                OnErrorCallback?.Invoke(this, new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: "未连接到服务器"
                ));
                throw new InvalidOperationException("未连接到服务器");
            }

            try
            {
                // 添加请求 ID
                string requestID = Guid.NewGuid().ToString();
                packet.Headers.Add(PacketHeaderKey.RequestID, requestID);

                // 准备 TaskCompletionSource 用于等待响应
                var tcs = new TaskCompletionSource<byte[]>();
                if (!_pendingRequests.TryAdd(requestID, tcs))
                {
                    throw new InvalidOperationException("无法添加待处理请求");
                }

                // 发送数据包
                byte[] data = packet.Pack(_key);
                _ = _socket.BeginSend(data, 0, data.Length, SocketFlags.None,
                   new AsyncCallback(SendCallback), null);

                // 触发数据发送事件
                OnDataSentCallback?.Invoke(this, new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    packet: data
                ));

                // 等待响应或超时
                if (await Task.WhenAny(tcs.Task, Task.Delay(timeout)) == tcs.Task)
                {
                    BeginReceive();
                    return await tcs.Task;
                }
                else
                {
                    _pendingRequests.TryRemove(requestID, out _); // 移除请求
                    BeginReceive();
                    throw new TimeoutException("发送数据包等待响应超时");
                }
            }
            catch (Exception ex)
            {
                OnErrorCallback?.Invoke(this, new NetworkEventArgs(
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"发送数据包时发生错误: {ex.Message}"
                ));
                BeginReceive();
                throw;
            }
        }

        /// <summary>
        /// 处理发送数据回调。
        /// </summary>
        /// <param name="ar">异步操作结果。</param>
        protected virtual void SendCallback(IAsyncResult ar)
        {
            try
            {
                _ = _socket.EndSend(ar);
            }
            catch (Exception ex)
            {
                OnErrorCallback?.Invoke(this, new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"发送数据包时发生错误: {ex.Message}"
                ));
                HandleDisconnect();
            }
        }
        #endregion

        #region 接收方法
        /// <summary>
        /// 开始接收数据。
        /// </summary>
        protected virtual void BeginReceive()
        {
            byte[] buffer = new byte[8192];
            try
            {
                _ = _socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None,
                    new AsyncCallback(ReceiveCallback), buffer);
            }
            catch
            {
                HandleDisconnect();
            }
        }

        /// <summary>
        /// 处理接收到的数据回调。
        /// </summary>
        /// <param name="ar">异步操作结果。</param>
        protected virtual void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                int bytesRead = _socket.EndReceive(ar);
                if (bytesRead > 0)
                {
                    byte[]? buffer = ar.AsyncState as byte[];

                    DRXPacket receivedPacket = DRXPacket.Unpack(buffer.Take(bytesRead).ToArray(), _key);

                    string? requestID = receivedPacket.Headers.ContainsKey(PacketHeaderKey.RequestID)
                        ? receivedPacket.Headers[PacketHeaderKey.RequestID]?.ToString()
                        : null;


                    if (!string.IsNullOrEmpty(requestID) && _pendingRequests.TryRemove(requestID, out var tcs))
                    {
                        tcs.SetResult(buffer.Take(bytesRead).ToArray());
                    }

                    OnReceiveCallback?.Invoke(this, new NetworkEventArgs(
                        socket: _socket,
                        eventType: NetworkEventType.HandlerEvent,
                        packet: buffer.Take(bytesRead).ToArray()
                    ));
                    // 继续接收数据
                    BeginReceive();
                }
                else
                {
                    HandleDisconnect();
                }
            }
            catch (Exception ex)
            {
                OnErrorCallback?.Invoke(this, new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"接收数据时发生错误: {ex.Message}"
                ));
                HandleDisconnect();
            }
        }
        #endregion

        #region HTTP 请求方法
        /// <summary>
        /// 异步发送 GET 请求并返回解包后的 DRXPacket。
        /// </summary>
        /// <param name="apiUrl">API 的 URL。</param>
        /// <param name="key">用于解包的密钥。</param>
        /// <returns>解包后的 DRXPacket 实例，如果请求失败则返回 null。</returns>
        public async Task<DRXPacket?> TrySendGetAsync(string apiUrl, string key)
        {
            using var httpClient = new HttpClient();
            try
            {
                var httpResponse = await httpClient.GetAsync(apiUrl);
                if (httpResponse.IsSuccessStatusCode)
                {
                    var responsePacketString = await httpResponse.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(responsePacketString))
                    {
                        // 使用 UnpackBase64 方法处理带引号的 Base64 字符串
                        var unpacket = DRXPacket.UnpackBase64(responsePacketString, key);
                        return unpacket;
                    }
                    else
                    {
                        Logger.Log("HttpResponse", "响应内容为空。");
                    }
                }
                else
                {
                    Logger.Log("HttpResponse", $"错误状态码: {httpResponse.StatusCode}");
                }
            }
            catch (FormatException ex)
            {
                Logger.Log("HttpResponse", $"Base64 格式错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Log("HttpResponse", $"请求失败: {ex.Message}");
            }

            return null;
        }
        #endregion

        #region 断开连接方法
        /// <summary>
        /// 处理断开连接。
        /// </summary>
        protected virtual void HandleDisconnect()
        {
            if (_isConnected)
            {
                _isConnected = false; // 更新连接状态

                OnDisconnectedCallback?.Invoke(this, new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent
                ));

                try
                {
                    _socket.Close();
                }
                catch (ObjectDisposedException)
                {
                    // 忽略已释放对象的异常
                }
            }
        }

        /// <summary>
        /// 主动断开与服务器的连接。
        /// </summary>
        public virtual void Disconnect()
        {
            HandleDisconnect();
        }
        #endregion
    }
}
