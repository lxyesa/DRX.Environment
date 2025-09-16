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

        #region 发包收包方法 - 新版握手协议

        /// <summary>
        /// 新版异步发送数据包：先发送握手包头 {"packetSize":int}（可加密）-> 等待服务端 {"message_request":bool,...} -> 再发送JSON包体。
        /// </summary>
        /// <param name="data">要发送的数据（对象将序列化为 JSON，string 原样按 UTF8）</param>
        /// <param name="onResponse">收到服务端后续业务层响应时的回调（可空）</param>
        /// <param name="timeout">握手/消息队列等待超时时间，默认30s</param>
        /// <param name="token">取消令牌</param>
        public async Task SendPacketAsync(
            object data,
            Action<DrxTcpClient, byte[]>? onResponse,
            TimeSpan timeout,
            CancellationToken token = default)
        {
            // 统一转换为字节后转调字节重载，避免在 AOT 环境使用反射序列化
            if (data is null)
                throw new ArgumentNullException(nameof(data), "SendPacketAsync data cannot be null");

            byte[] payload;
            if (data is byte[] b) payload = b;
            else if (data is string s) payload = Encoding.UTF8.GetBytes(s);
            else
            {
                // 尽量避免 System.Text.Json 在 AOT 的反射路径，这里只在必要时序列化匿名/POCO。
                // 如果你的环境禁用了反射，请优先在调用侧传入 string/byte[]。
                var json = System.Text.Json.JsonSerializer.Serialize(data);
                payload = Encoding.UTF8.GetBytes(json);
            }

            await SendPacketAsync(payload, onResponse, timeout, token);
        }

        /// <summary>
        /// 新增重载：纯字节直发（遵循握手头 + 包体协议），不做任何对象序列化。
        /// 调用侧若已构造好 UTF-8 JSON 或任意二进制，直接传入 payload。
        /// </summary>
        public async Task SendPacketAsync(
            byte[] payload,
            Action<DrxTcpClient, byte[]>? onResponse,
            TimeSpan timeout,
            CancellationToken token = default)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (!Connected)
            {
                _logger?.LogWarning("Cannot send packet to a disconnected client.");
                return;
            }

            var stream = GetStream();

            // 1) 握手头：{"packetSize":int}
            string headerJson;
            try
            {
                headerJson = $"{{\"packetSize\":{payload.Length}}}";
            }
            catch
            {
                headerJson = "{\"packetSize\":0}";
            }
            var headerBytes = Encoding.UTF8.GetBytes(headerJson);

            OnDataSent?.Invoke(this, headerBytes);

            byte[] toSendHeader = headerBytes;
            if (_packetEncryptor != null)
                toSendHeader = _packetEncryptor.Encrypt(toSendHeader);
            else if (_packetIntegrityProvider != null)
                toSendHeader = _packetIntegrityProvider.Protect(toSendHeader);

            await stream.WriteAsync(toSendHeader, 0, toSendHeader.Length, token);

            // 2) 等待 { "message_request": true } 回包
            var handshakeResp = await ReceiveSingleJsonAsync(timeout, token);
            if (handshakeResp == null)
            {
                _logger?.LogWarning("Handshake response timeout.");
                return;
            }

            bool allow = false;
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(handshakeResp);
                if (doc.RootElement.TryGetProperty("message_request", out var mr))
                    allow = mr.GetBoolean();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Invalid handshake response format.");
            }
            if (!allow)
            {
                _logger?.LogWarning("Server rejected message during handshake.");
                return;
            }

            // 3) 发送包体
            var bodyBytes = payload;
            OnDataSent?.Invoke(this, bodyBytes);

            if (_packetEncryptor != null)
                bodyBytes = _packetEncryptor.Encrypt(bodyBytes);
            else if (_packetIntegrityProvider != null)
                bodyBytes = _packetIntegrityProvider.Protect(bodyBytes);

            await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length, token);

            // 4) 等待业务响应（可选）
            if (onResponse != null)
            {
                var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                Action<DrxTcpClient, byte[]> handler = (client, resp) => tcs.TrySetResult(resp);
                OnDataReceived += handler;

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(timeout);
                try
                {
                    var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout, cts.Token));
                    if (completedTask == tcs.Task && !cts.IsCancellationRequested)
                    {
                        var resp = await tcs.Task;
                        onResponse?.Invoke(this, resp);
                    }
                }
                finally
                {
                    OnDataReceived -= handler;
                }
            }
        }

        /// <summary>
        /// 发送字符串消息（使用新版握手协议）
        /// </summary>
        public async Task SendMessageAsync(string message, CancellationToken token = default)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            await SendPacketAsync(messageBytes, null, TimeSpan.FromSeconds(30), token);
        }

        /// <summary>
        /// 发送响应（沿用原有格式拼接，但走新版握手协议）
        /// </summary>
        public async Task SendResponseAsync(SocketStatusCode code, CancellationToken token = default, params object[] args)
        {
            string rawMessage;
            if (args.Length == 1 && args[0] != null && args[0].GetType().IsClass && !(args[0] is string))
            {
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
                var messageParts = new List<string> { ((int)code).ToString() };
                messageParts.AddRange(args.Select(a => a?.ToString() ?? string.Empty));
                rawMessage = string.Join("|", messageParts);
            }

            await SendMessageAsync(rawMessage, token);
        }

        /// <summary>
        /// 开始接收：不再按4字节长度拆包；改为“整个读取到的块 -> 解密/验签 -> 直接作为一条消息触发”
        /// 服务端会控制握手与业务的分帧；客户端这里只负责把收到的明文字节上抛。
        /// </summary>
        public async Task StartReceivingAsync(CancellationToken token = default)
        {
            try
            {
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
                        var chunk = buffer.AsSpan(0, bytesRead).ToArray();

                        if (_packetEncryptor != null)
                        {
                            chunk = _packetEncryptor.Decrypt(chunk);
                            if (chunk == null)
                            {
                                _logger?.LogWarning("Failed to decrypt received data.");
                                continue;
                            }
                        }
                        else if (_packetIntegrityProvider != null)
                        {
                            chunk = _packetIntegrityProvider.Unprotect(chunk);
                            if (chunk == null)
                            {
                                _logger?.LogWarning("Packet integrity check failed. Tampered packet suspected.");
                                continue;
                            }
                        }

                        // 直接上抛一条消息块
                        OnDataReceived?.Invoke(this, chunk);
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
        /// 等待一条 JSON 文本回复（用于握手回包）。在超时内从 OnDataReceived 管道上消费一帧并尝试解析为 JSON 字符串。
        /// </summary>
        private async Task<string?> ReceiveSingleJsonAsync(TimeSpan timeout, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<DrxTcpClient, byte[]> handler = (client, data) =>
            {
                tcs.TrySetResult(data);
            };
            OnDataReceived += handler;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeout);

            try
            {
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout, cts.Token));
                if (completedTask != tcs.Task || cts.IsCancellationRequested)
                {
                    return null;
                }
                var bytes = await tcs.Task;
                return Encoding.UTF8.GetString(bytes);
            }
            finally
            {
                OnDataReceived -= handler;
            }
        }

        /// <summary>
        /// 同步方式等待一条上抛消息（保持 API 兼容，默认30s）
        /// </summary>
        public Task<byte[]?> ReceivePacketAsync(TimeSpan maxWaitTime = default)
        {
            if (maxWaitTime == default)
                maxWaitTime = TimeSpan.FromSeconds(30);

            byte[] result = null;
            var completed = false;
            var resetEvent = new ManualResetEventSlim(false);

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
                var receiveTask = StartReceivingAsync();
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