using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Drx.Sdk.Network.Security;
using DRX.Framework;

namespace Drx.Sdk.Network.Socket
{
    /// <summary>
    /// 扩展的 TcpClient 类，提供额外的映射存储功能和完全兼容 SocketServerService 的发包收包功能
    /// </summary>
    public class DrxTcpClient : TcpClient
    {
        // 存储映射数据的字典，结构：mapId -> (mapKey -> mapValue)
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object>> _maps = new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>();

        // 数据接收缓冲区，用于处理粘包
        private readonly List<byte> _receiveBuffer = new List<byte>();
        private readonly object _receiveBufferLock = new object();

        // 加密和完整性保护
        private IPacketEncryptor _packetEncryptor;
        private IPacketIntegrityProvider _packetIntegrityProvider;
        private ILogger<DrxTcpClient> _logger;

        // 自动接收标志，确保只启动一次接收任务
        private int _receivingStarted = 0;
        private CancellationTokenSource _autoReceiveCts;

        // 事件和委托
        public event Action<DrxTcpClient, byte[]> OnDataReceived;
        public event Action<DrxTcpClient, byte[]> OnDataSent;
        public event Action<DrxTcpClient> OnDisconnected;

        /// <summary>
        /// 设置数据包加密器
        /// </summary>
        /// <param name="encryptor">加密器实例</param>
        public void SetEncryptor(IPacketEncryptor encryptor)
        {
            if (_packetIntegrityProvider != null)
                throw new InvalidOperationException("Cannot enable both encryption and integrity check simultaneously. Please choose one.");
            _packetEncryptor = encryptor;
        }

        /// <summary>
        /// 设置数据包完整性保护器
        /// </summary>
        /// <param name="integrityProvider">完整性保护器实例</param>
        public void SetIntegrityProvider(IPacketIntegrityProvider integrityProvider)
        {
            if (_packetEncryptor != null)
                throw new InvalidOperationException("Cannot enable both encryption and integrity check simultaneously. Please choose one.");
            _packetIntegrityProvider = integrityProvider;
        }

        /// <summary>
        /// 设置日志记录器
        /// </summary>
        /// <param name="logger">日志记录器实例</param>
        public void SetLogger(ILogger<DrxTcpClient> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 初始化 DrxTcpClient 的新实例
        /// </summary>
        public DrxTcpClient() : base()
        {
        }

        /// <summary>
        /// 重写 ConnectAsync，连接建立后自动启动接收
        /// </summary>
        public new async Task ConnectAsync(string host, int port)
        {
            await base.ConnectAsync(host, port);
            TryStartReceiving();
        }

        /// <summary>
        /// 重写 Connect，连接建立后自动启动接收
        /// </summary>
        public new void Connect(string host, int port)
        {
            base.Connect(host, port);
            TryStartReceiving();
        }

        /// <summary>
        /// 尝试自动启动接收任务（线程安全，确保只启动一次）
        /// </summary>
        private void TryStartReceiving()
        {
            if (Interlocked.CompareExchange(ref _receivingStarted, 1, 0) == 0)
            {
                _autoReceiveCts = new CancellationTokenSource();
                _ = StartReceivingAsync(_autoReceiveCts.Token);
            }
        }

        /// <summary>
        /// 使用指定的主机名和端口号初始化 DrxTcpClient 的新实例
        /// </summary>
        /// <param name="hostname">要连接到的主机名</param>
        /// <param name="port">要连接到的端口号</param>
        public DrxTcpClient(string hostname, int port) : base(hostname, port)
        {
        }

        /// <summary>
        /// 使用指定的地址族初始化 DrxTcpClient 的新实例
        /// </summary>
        /// <param name="addressFamily">要使用的地址族</param>
        public DrxTcpClient(AddressFamily addressFamily) : base(addressFamily)
        {
        }

        /// <summary>
        /// 将值存储到指定的映射中
        /// </summary>
        /// <typeparam name="T">值的类型</typeparam>
        /// <param name="mapId">映射ID，如果不存在则创建</param>
        /// <param name="mapKey">映射键</param>
        /// <param name="mapValue">要存储的值</param>
        /// <returns>如果成功存储则返回 true，否则返回 false</returns>
        public Task<bool> PushMap<T>(string mapId, string mapKey, T mapValue)
        {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(mapKey))
                return Task.FromResult(false);

            var map = _maps.GetOrAdd(mapId, _ => new ConcurrentDictionary<string, object>());
            return Task.FromResult(map.AddOrUpdate(mapKey, mapValue!, (_, _) => mapValue!) != null);
        }

        /// <summary>
        /// 从指定的映射中获取值
        /// </summary>
        /// <typeparam name="T">值的类型</typeparam>
        /// <param name="mapId">映射ID</param>
        /// <param name="mapKey">映射键</param>
        /// <returns>如果找到则返回值，否则返回默认值</returns>
        public Task<T> GetMap<T>(string mapId, string mapKey)
        {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(mapKey))
                return Task.FromResult<T>(default!);

            if (_maps.TryGetValue(mapId, out var map) && map.TryGetValue(mapKey, out var value))
            {
                if (value is T typedValue)
                    return Task.FromResult(typedValue);
            }

            return Task.FromResult<T>(default!);
        }

        /// <summary>
        /// 检查指定的映射是否存在
        /// </summary>
        /// <param name="mapId">映射ID</param>
        /// <returns>如果映射存在则返回 true，否则返回 false</returns>
        public Task<bool> HasMap(string mapId)
        {
            return Task.FromResult(!string.IsNullOrEmpty(mapId) && _maps.ContainsKey(mapId));
        }

        /// <summary>
        /// 检查指定的映射键是否存在
        /// </summary>
        /// <param name="mapId">映射ID</param>
        /// <param name="mapKey">映射键</param>
        /// <returns>如果映射键存在则返回 true，否则返回 false</returns>
        public Task<bool> HasMapKey(string mapId, string mapKey)
        {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(mapKey))
                return Task.FromResult(false);

            return Task.FromResult(_maps.TryGetValue(mapId, out var map) && map.ContainsKey(mapKey));
        }

        /// <summary>
        /// 从指定的映射中移除键
        /// </summary>
        /// <param name="mapId">映射ID</param>
        /// <param name="mapKey">要移除的映射键</param>
        /// <returns>如果成功移除则返回 true，否则返回 false</returns>
        public Task<bool> RemoveMapKey(string mapId, string mapKey)
        {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(mapKey))
                return Task.FromResult(false);

            if (_maps.TryGetValue(mapId, out var map))
                return Task.FromResult(map.TryRemove(mapKey, out _));

            return Task.FromResult(false);
        }

        /// <summary>
        /// 移除整个映射
        /// </summary>
        /// <param name="mapId">要移除的映射ID</param>
        /// <returns>如果成功移除则返回 true，否则返回 false</returns>
        public Task<bool> RemoveMap(string mapId)
        {
            if (string.IsNullOrEmpty(mapId))
                return Task.FromResult(false);

            return Task.FromResult(_maps.TryRemove(mapId, out _));
        }

        /// <summary>
        /// 获取指定映射中的所有键
        /// </summary>
        /// <param name="mapId">映射ID</param>
        /// <returns>映射中的所有键，如果映射不存在则返回空集合</returns>
        public Task<IEnumerable<string>> GetMapKeys(string mapId)
        {
            if (string.IsNullOrEmpty(mapId) || !_maps.TryGetValue(mapId, out var map))
                return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

            return Task.FromResult<IEnumerable<string>>(map.Keys);
        }

        /// <summary>
        /// 清除所有映射数据
        /// </summary>
        public Task ClearAllMaps()
        {
            _maps.Clear();
            return Task.CompletedTask;
        }

        #region 发包收包方法 - 完全兼容 SocketServerService.cs

        /// <summary>
        /// 新版异步发送数据包，支持临时响应回调与超时注销，无需全局事件。
        /// </summary>
        /// <param name="data">要发送的数据</param>
        /// <param name="onResponse">本次请求的响应回调</param>
        /// <param name="timeout">超时时间</param>
        /// <param name="token">取消令牌</param>
        /// <returns>发送任务</returns>
        public async Task SendPacketAsync(
            object data,
            Action<DrxTcpClient, byte[]>? onResponse,
            TimeSpan timeout,
            CancellationToken token = default)
        {
            if (!Connected)
            {
                _logger?.LogWarning("Cannot send packet to a disconnected client.");
                return;
            }

            // 数据序列化
            byte[] messageBytes = null;
            if (data is byte[] bytes)
            {
                messageBytes = bytes;
            }
            else if (data is string str)
            {
                messageBytes = Encoding.UTF8.GetBytes(str);
            }
            else if (data != null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(data);
                messageBytes = Encoding.UTF8.GetBytes(json);
            }
            else
            {
                throw new ArgumentNullException(nameof(data), "SendPacketAsync data cannot be null");
            }

            // 组包：4字节长度+内容
            byte[] lengthHeader = BitConverter.GetBytes(messageBytes.Length);
            byte[] sendBuffer = new byte[lengthHeader.Length + messageBytes.Length];
            Array.Copy(lengthHeader, 0, sendBuffer, 0, lengthHeader.Length);
            Array.Copy(messageBytes, 0, sendBuffer, lengthHeader.Length, messageBytes.Length);

            // 触发发送前事件（原始包格式）
            OnDataSent?.Invoke(this, sendBuffer);

            // 加密/签名（对整个包进行）
            if (_packetEncryptor != null)
            {
                sendBuffer = _packetEncryptor.Encrypt(sendBuffer);
            }
            else if (_packetIntegrityProvider != null)
            {
                sendBuffer = _packetIntegrityProvider.Protect(sendBuffer);
            }

            var stream = GetStream();
            await stream.WriteAsync(sendBuffer, 0, sendBuffer.Length, token);

            // 临时响应回调注册与超时处理
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<DrxTcpClient, byte[]> handler = (client, resp) =>
            {
                tcs.TrySetResult(resp);
            };
            OnDataReceived += handler;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeout);

            try
            {
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout, cts.Token));
                if (completedTask == tcs.Task && !cts.IsCancellationRequested)
                {
                    // 收到响应，触发回调
                    var resp = await tcs.Task;
                    onResponse?.Invoke(this, resp);
                }
                // 超时则自动注销，不触发回调
            }
            finally
            {
                OnDataReceived -= handler;
            }
        }

        /// <summary>
        /// 发送字符串消息
        /// </summary>
        /// <param name="message">要发送的字符串消息</param>
        /// <param name="token">取消令牌</param>
        /// <returns>发送任务</returns>
        public async Task SendMessageAsync(string message, CancellationToken token = default)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            await SendPacketAsync(messageBytes, null, TimeSpan.FromSeconds(30), token);
        }

        /// <summary>
        /// 发送响应，与 SocketServerService.SendResponseAsync 相同的格式
        /// </summary>
        /// <param name="code">状态码</param>
        /// <param name="token">取消令牌</param>
        /// <param name="args">附加参数</param>
        /// <returns>发送任务</returns>
        public async Task SendResponseAsync(SocketStatusCode code, CancellationToken token = default, params object[] args)
        {
            string rawMessage;
            if (args.Length == 1 && args[0] != null && args[0].GetType().IsClass && !(args[0] is string))
            {
                // 如果只传了一个对象且不是字符串，则序列化为JSON，并自动加入状态码
                var obj = args[0];
                var dict = new Dictionary<string, object>();
                foreach (var prop in obj.GetType().GetProperties())
                {
                    var value = prop.GetValue(obj);
                    dict[prop.Name] = value ?? string.Empty;
                }
                dict["stute_code"] = (int)code;
                rawMessage = System.Text.Json.JsonSerializer.Serialize(dict);
            }
            else
            {
                // 兼容旧模式，code+参数用|分隔
                var messageParts = new List<string> { ((int)code).ToString() };
                messageParts.AddRange(args.Select(a => a?.ToString() ?? string.Empty));
                rawMessage = string.Join("|", messageParts);
            }

            await SendMessageAsync(rawMessage, token);
        }

        /// <summary>
        /// 开始接收数据包，使用与 SocketServerService 相同的协议解析（支持粘包处理）
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns>接收任务</returns>
        public async Task StartReceivingAsync(CancellationToken token = default)
        {
            try
            {
                // 标志已启动接收（用于手动调用时也能防止重复）
                if (Interlocked.CompareExchange(ref _receivingStarted, 1, 0) == 0 && token == default)
                {
                    _autoReceiveCts = new CancellationTokenSource();
                    token = _autoReceiveCts.Token;
                }

                using (var stream = GetStream())
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;

                    while (!token.IsCancellationRequested && Connected &&
                           (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) != 0)
                    {
                        lock (_receiveBufferLock)
                        {
                            // 累积收到的数据
                            _receiveBuffer.AddRange(buffer.AsSpan(0, bytesRead).ToArray());
                        }

                        // 处理粘包 - 完全兼容C++的包头+包体粘包处理（小端字节序）
                        await ProcessReceiveBuffer(token);
                    }
                }
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                _logger?.LogWarning("Client disconnected abruptly.");
                OnDisconnected?.Invoke(this);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Packet receiving cancelled.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during packet receiving.");
                throw;
            }
        }

        /// <summary>
        /// 处理接收缓冲区中的数据包
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns>处理任务</returns>
        private Task ProcessReceiveBuffer(CancellationToken token)
        {
            while (true)
            {
                byte[] receivedData;
                lock (_receiveBufferLock)
                {
                    if (_receiveBuffer.Count == 0)
                        break;
                    receivedData = _receiveBuffer.ToArray();
                    _receiveBuffer.Clear();
                }

                // 解密/验签（对整个包流处理）
                if (_packetEncryptor != null)
                {
                    _logger?.LogTrace("Decrypting received data...");
                    receivedData = _packetEncryptor.Decrypt(receivedData);
                    if (receivedData == null)
                    {
                        _logger?.LogWarning("Failed to decrypt received data.");
                        continue;
                    }
                }
                else if (_packetIntegrityProvider != null)
                {
                    _logger?.LogTrace("Verifying packet integrity...");
                    receivedData = _packetIntegrityProvider.Unprotect(receivedData);
                    if (receivedData == null)
                    {
                        _logger?.LogWarning("Packet integrity check failed. Tampered packet suspected.");
                        continue;
                    }
                }

                int offset = 0;
                while (offset + 4 <= receivedData.Length)
                {
                    int packetLength = BitConverter.ToInt32(receivedData, offset);
                    if (packetLength < 0 || packetLength > 100 * 1024 * 1024)
                    {
                        _logger?.LogWarning("Invalid packet length {len}, skipping.", packetLength);
                        break;
                    }
                    if (offset + 4 + packetLength > receivedData.Length)
                    {
                        // 剩余数据不足一个包，放回缓冲区
                        lock (_receiveBufferLock)
                        {
                            _receiveBuffer.AddRange(receivedData.Skip(offset).ToArray());
                        }
                        break;
                    }
                    byte[] packBytes = new byte[packetLength];
                    Array.Copy(receivedData, offset + 4, packBytes, 0, packetLength);
                    offset += 4 + packetLength;

                    _logger?.LogInformation("[Debug] Received packet: {str}", Encoding.UTF8.GetString(packBytes));
                    OnDataReceived?.Invoke(this, packBytes);
                }
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 同步方式处理一个数据包
        /// </summary>
        /// <param name="maxWaitTime">最大等待时间</param>
        /// <returns>接收到的数据包，如果超时则返回null</returns>
        public Task<byte[]?> ReceivePacketAsync(TimeSpan maxWaitTime = default)
        {
            if (maxWaitTime == default)
                maxWaitTime = TimeSpan.FromSeconds(30); // 默认30秒超时

            byte[] result = null;
            var completed = false;
            var resetEvent = new ManualResetEventSlim(false);

            // 临时订阅事件
            Action<DrxTcpClient, byte[]> handler = (client, data) =>
            {
                if (!completed)
                {
                    result = data;
                    completed = true;
                    resetEvent.Set();
                }
            };

            OnDataReceived += handler;

            try
            {
                // 启动接收任务
                var receiveTask = StartReceivingAsync();

                // 等待数据到达或超时
                var waitCompleted = resetEvent.Wait(maxWaitTime);

                if (!waitCompleted)
                {
                    _logger?.LogWarning("ReceivePacketAsync timed out after {timeout}", maxWaitTime);
                }

                return Task.FromResult(result);
            }
            finally
            {
                OnDataReceived -= handler;
                resetEvent.Dispose();
            }
        }

        #endregion
    }

    /// <summary>
    /// DrxTcpClient 的扩展方法
    /// </summary>
    public static class DrxTcpClientExtensions
    {
        /// <summary>
        /// 将 TcpClient 转换为 DrxTcpClient
        /// </summary>
        /// <param name="tcpClient">要转换的 TcpClient</param>
        /// <returns>转换后的 DrxTcpClient</returns>
        public static DrxTcpClient ToDrxTcpClient(this TcpClient tcpClient)
        {
            if (tcpClient == null)
                return null;

            if (tcpClient is DrxTcpClient drxClient)
                return drxClient;

            var client = new DrxTcpClient();
            client.Client = tcpClient.Client;
            return client;
        }
    }
}