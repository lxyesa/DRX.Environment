using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using Drx.Sdk.Network.Security;
using Drx.Sdk.Network.Legacy.Socket.Middleware;
using System.Collections.Concurrent;
using Drx.Sdk.Network.Legacy.Socket.Services;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Legacy.Socket
{
    /// <summary>
    /// 兼容模式：不再强制依赖 IHostedService/IServiceProvider。可在 ASP 模式由外层包装为 IHostedService。
    /// </summary>
    public class SocketServerService : IDisposable
    {
        private TcpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        // 可选的 UDP 监听（由 SocketHostOptions/UdpPort 决定是否创建）
        private UdpClient? _udpClient;
        private IPEndPoint? _udpLocalEndPoint;

        private readonly IServiceProvider? _serviceProviderOrNull;
        private readonly IPacketEncryptor? _packetEncryptor;
        private readonly IPacketIntegrityProvider? _packetIntegrityProvider;
        private readonly IReadOnlyList<ConnectionMiddleware> _connectionMiddlewares;
        private readonly IReadOnlyList<MessageMiddleware> _messageMiddlewares;
        private readonly ConcurrentDictionary<DrxTcpClient, bool> _connectedClients = new();
        private readonly IReadOnlyList<ISocketService> _socketServices;
        private Task _servicesTask;
        private readonly List<Task> _timerTasks = new();

        private readonly IEnumerable<SocketServerBuilder.TimerRegistration> _timersFromBuilder;
        private readonly int _port;
        private readonly int _udpPort;

        // 每连接的状态机与消息缓冲
        private class ConnectionState
        {
            public enum Phase
            {
                AwaitHeader,
                AwaitBody
            }

            public Phase Current = Phase.AwaitHeader;
            public int ExpectedBodySize = 0;
            public DateTime LastActivityUtc = DateTime.UtcNow;

            // 用于聚合分包的缓存
            public List<byte> Buffer = new List<byte>();
        }

        private readonly ConcurrentDictionary<DrxTcpClient, ConnectionState> _connStates = new();

        // 环境抽象（最小化）
        public string EnvironmentName { get; }

        public IServiceProvider? Services => _serviceProviderOrNull;

        public ICollection<DrxTcpClient> ConnectedClients => _connectedClients.Keys;

        public T? GetService<T>() where T : class
        {
            // 先从 DI
            var sp = _serviceProviderOrNull;
            if (sp != null)
            {
                var getService = typeof(System.Activator).Assembly; // just to avoid analyzer warning for null-conditional
            }
            if (sp is not null)
            {
                var svc = (sp as IServiceProvider)?.GetService(typeof(T)) as T;
                if (svc != null) return svc;
            }
            // 再从 socketServices
            if (typeof(ISocketService).IsAssignableFrom(typeof(T)))
            {
                return _socketServices.FirstOrDefault(s => s is T) as T;
            }
            return null;
        }

        public SocketServerService(
            IReadOnlyList<ConnectionMiddleware> connectionMiddlewares,
            IReadOnlyList<MessageMiddleware> messageMiddlewares,
            IReadOnlyList<ISocketService> socketServices,
            IPacketEncryptor? encryptorOrNull,
            IPacketIntegrityProvider? integrityOrNull,
            IEnumerable<SocketServerBuilder.TimerRegistration> timers,
            int port = 8463,
            int udpPort = 0,
            IServiceProvider? serviceProviderOrNull = null,
            string environmentName = "Production"
        )
        {
            _connectionMiddlewares = connectionMiddlewares ?? Array.Empty<ConnectionMiddleware>();
            _messageMiddlewares = messageMiddlewares ?? Array.Empty<MessageMiddleware>();
            _socketServices = socketServices ?? Array.Empty<ISocketService>();
            _packetEncryptor = encryptorOrNull;
            _packetIntegrityProvider = integrityOrNull;
            _timersFromBuilder = timers ?? Array.Empty<SocketServerBuilder.TimerRegistration>();
            _port = port;
            _udpPort = udpPort;
            _serviceProviderOrNull = serviceProviderOrNull;
            EnvironmentName = environmentName;

            if (_packetEncryptor != null && _packetIntegrityProvider != null)
            {
                throw new InvalidOperationException("Cannot enable both encryption and integrity check simultaneously. Please choose one.");
            }
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _cancellationTokenSource.Token;

            var serviceTasks = _socketServices.Select(s => s.ExecuteAsync(token)).ToList();
            foreach (var s in _socketServices)
            {
                // Run synchronous Execute methods in background threads so they don't block startup
                serviceTasks.Add(Task.Run(() => s.Execute(), token));
            }
            _servicesTask = Task.WhenAll(serviceTasks);

            // 启动定时器任务（来自构建器）
            foreach (var timer in _timersFromBuilder)
            {
                var t = Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            timer.Handler(this);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"[定时器] 定时器处理器发生错误。异常信息: {ex}");
                        }
                        await Task.Delay(TimeSpan.FromSeconds(timer.IntervalSeconds), token);
                    }
                }, token);
                _timerTasks.Add(t);
            }
            // 启动 TCP 监听
            Task.Run(() => ListenForClients(token), token);

            // 如果在构造时传入了 udpPort，则启动 UDP 服务（保持与构造时传入的加密/完整性设置一致）
            if (_udpPort > 0)
            {
                try
                {
                    _udpLocalEndPoint = new IPEndPoint(System.Net.IPAddress.Any, _udpPort);
                    _udpClient = new UdpClient(_udpLocalEndPoint);
                    // 启动后台接收循环
                    _ = Task.Run(() => UdpReceiveLoopAsync(_udpClient, _udpLocalEndPoint, token), token);
                    Logger.Info($"[udp] UDP 服务器已在端口 {_udpPort} 启动（由构造参数控制）。");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[udp] 启动 UDP 监听失败: {ex}");
                }
            }
            return Task.CompletedTask;
        }

        private async Task UdpReceiveLoopAsync(UdpClient udpClient, IPEndPoint local, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    UdpReceiveResult res;
                    try
                    {
                        res = await udpClient.ReceiveAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException ex)
                    {
                        Logger.Error($"[udp] 接收异常: {ex}");
                        await Task.Delay(500, token).ConfigureAwait(false);
                        continue;
                    }

                    var remote = res.RemoteEndPoint;
                    var raw = res.Buffer;

                    // 解密/验签
                    byte[]? payload = raw;
                    if (_packetEncryptor != null)
                    {
                        payload = _packetEncryptor.Decrypt(raw);
                        if (payload == null)
                        {
                            Logger.Warn($"[udp] 来自 {remote} 的包解密失败，忽略。");
                            continue;
                        }
                    }
                    else if (_packetIntegrityProvider != null)
                    {
                        payload = _packetIntegrityProvider.Unprotect(raw);
                        if (payload == null)
                        {
                            Logger.Warn($"[udp] 来自 {remote} 的包完整性校验失败，忽略。");
                            continue;
                        }
                    }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // 先同步回调
                            foreach (var svc in _socketServices)
                            {
                                try
                                {
                                    var resp = svc.OnUdpReceive(this, remote, payload);
                                    if (resp != null && resp.Length > 0)
                                    {
                                        var toSend = resp;
                                        if (_packetEncryptor != null) toSend = _packetEncryptor.Encrypt(toSend);
                                        else if (_packetIntegrityProvider != null) toSend = _packetIntegrityProvider.Protect(toSend);
                                        try { await udpClient.SendAsync(toSend, toSend.Length, remote).ConfigureAwait(false); } catch { }
                                        return;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error($"[udp] 服务 {svc.GetType().Name} 的同步 OnUdpReceive 异常: {ex}");
                                }
                            }

                            // 再异步回调
                            foreach (var svc in _socketServices)
                            {
                                try
                                {
                                    var asyncResp = await svc.OnUdpReceiveAsync(this, remote, payload, token).ConfigureAwait(false);
                                    if (asyncResp != null && asyncResp.Length > 0)
                                    {
                                        var toSend = asyncResp;
                                        if (_packetEncryptor != null) toSend = _packetEncryptor.Encrypt(toSend);
                                        else if (_packetIntegrityProvider != null) toSend = _packetIntegrityProvider.Protect(toSend);
                                        try { await udpClient.SendAsync(toSend, toSend.Length, remote).ConfigureAwait(false); } catch { }
                                        return;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error($"[udp] 服务 {svc.GetType().Name} 的异步 OnUdpReceiveAsync 异常: {ex}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"[udp] 处理 UDP 包时发生未捕获异常: {ex}");
                        }
                    }, token);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("[udp] 接收循环取消。");
            }
            catch (Exception ex)
            {
                Logger.Error($"[udp] 接收循环未处理异常: {ex}");
            }
            finally
            {
                try { udpClient.Close(); } catch { }
            }
        }

        private async Task ListenForClients(CancellationToken token)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                Logger.Info($"[socket] 正在监听: {_listener.LocalEndpoint}");

                while (!token.IsCancellationRequested)
                {
                    Logger.Info("[socket] 等待新客户端连接...");
                    TcpClient tcpClient = await _listener.AcceptTcpClientAsync(token);

                    DrxTcpClient client = tcpClient.ToDrxTcpClient();
                    Logger.Info($"[socket] 新客户端已连接: {client.Client.RemoteEndPoint}");

                    _ = HandleClientAsync(client, token);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("[socket] 套接字服务器已被取消停止。");
            }
            catch (Exception ex)
            {
                Logger.Error($"[socket] 套接字服务器发生错误。异常信息: {ex}");
            }
            finally
            {
                _listener?.Stop();
                Logger.Info("[socket] 套接字服务器已停止。");
            }
        }

        private async Task HandleClientAsync(DrxTcpClient client, CancellationToken token)
        {
            // --- Connection Middleware Pipeline ---
            var connectionContext = new ConnectionContext(this, client, token);
            foreach (var middleware in _connectionMiddlewares)
            {
                try
                {
                    await middleware(connectionContext);
                    if (connectionContext.IsRejected)
                    {
                        Logger.Warn($"来自 {client.Client.RemoteEndPoint} 的连接被连接中间件拒绝。");
                        client.Close();
                        return; // Stop processing this client
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"连接中间件处理客户端 {client.Client.RemoteEndPoint} 时发生异常，连接已关闭。异常信息: {ex}");
                    client.Close();
                    return;
                }
            }
            // --- End of Connection Middleware ---

            _connectedClients.TryAdd(client, true);
            Logger.Info($"客户端 {client.Client.RemoteEndPoint} 已加入连接列表。当前总连接数: {_connectedClients.Count}");

            // 初始化连接状态机
            _connStates[client] = new ConnectionState();

            // Fire and forget the connect hooks
            _ = Task.Run(() => TriggerConnectHooks(client, token), token);

            try
            {
                using (var stream = client.GetStream())
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;

                    while (!token.IsCancellationRequested && (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) != 0)
                    {
                        var receivedData = buffer.AsSpan(0, bytesRead).ToArray();

                        // 先尝试解密/验签
                        if (_packetEncryptor != null)
                        {
                            receivedData = _packetEncryptor.Decrypt(receivedData);
                            if (receivedData == null)
                            {
                                await SendJsonAsync(client, new { message_request = false, message = "解密失败" }, token);
                                Logger.Warn($"无法解密来自客户端 {client.Client.RemoteEndPoint} 的数据，连接关闭。");
                                break;
                            }
                        }
                        else if (_packetIntegrityProvider != null)
                        {
                            receivedData = _packetIntegrityProvider.Unprotect(receivedData);
                            if (receivedData == null)
                            {
                                await SendJsonAsync(client, new { message_request = false, message = "完整性校验失败" }, token);
                                Logger.Warn($"客户端 {client.Client.RemoteEndPoint} 的数据包完整性校验失败，连接关闭。");
                                break;
                            }
                        }

                        // 状态机处理：握手头 -> 确认 -> 等待包体 -> 进入中间件
                        await ProcessByConnectionStateAsync(client, receivedData, token);

                        // 30s 超时检查
                        if (_connStates.TryGetValue(client, out var st))
                        {
                            var idle = DateTime.UtcNow - st.LastActivityUtc;
                            if (idle > TimeSpan.FromSeconds(30))
                            {
                                Logger.Warn($"客户端 {client.Client.RemoteEndPoint} 超时无活动，断开。");
                                break;
                            }
                        }
                    }
                }
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                Logger.Info("[socket] 客户端已断开连接。");
            }
            catch (OperationCanceledException)
            {
                Logger.Info("[socket] 客户端处理已取消。");
            }
            catch (Exception ex)
            {
                Logger.Error($"[socket] 处理客户端时发生错误。异常信息: {ex}");
            }
            finally
            {
                // Trigger  ClientDisconnectedMiddleware（独立模式无 DI，因此不再从 ServiceProvider 获取，改由服务钩子承担）
                // 如果未来需要依然支持中间件，可在 Runner 注入一个列表，这里遍历执行。

                // Trigger disconnect hooks
                _ = Task.Run(() => TriggerDisconnectHooks(client), token);

                _connectedClients.TryRemove(client, out _);
                _connStates.TryRemove(client, out _);

                Logger.Info($"客户端 {client.Client.RemoteEndPoint} 已从连接列表移除。当前总连接数: {_connectedClients.Count}");
                client.Close();
                Logger.Info("[socket] 客户端已断开连接。");
            }
        }

        /// <summary>
        /// 处理数据包数据，包括处理分包和粘包的情况
        /// </summary>
        /// <param name="client">客户端</param>
        /// <param name="newData">新接收到的数据</param>
        /// <param name="token">取消令牌</param>
        private async Task ProcessPacketData(DrxTcpClient client, byte[] newData, CancellationToken token)
        {
            // 旧逻辑已废弃（4字节长度头），由状态机在读循环中直接处理。
            Logger.Warn($"检测到旧的 ProcessPacketData 路径被调用，已不再使用。");
            await Task.CompletedTask;
        }

        /// <summary>
        /// 处理数据包队列，提取完整的数据包
        /// </summary>
        /// <param name="client">客户端</param>
        /// <param name="packetQueue">数据包队列</param>
        /// <param name="token">取消令牌</param>
        private async Task ProcessPacketQueue(DrxTcpClient client, Queue<byte[]> packetQueue, CancellationToken token)
        {
            // 合并队列中的所有数据
            var allData = new List<byte>();
            while (packetQueue.Count > 0)
            {
                allData.AddRange(packetQueue.Dequeue());
            }

            if (allData.Count == 0)
                return;

            var dataArray = allData.ToArray();
            int offset = 0;

            // 循环提取完整的数据包
            while (offset < dataArray.Length)
            {
                // 检查是否有足够的数据读取包头（4字节长度）
                if (offset + 4 > dataArray.Length)
                {
                    // 数据不完整，将剩余数据重新入队等待下次处理
                    var remainingData = new byte[dataArray.Length - offset];
                    Array.Copy(dataArray, offset, remainingData, 0, remainingData.Length);
                    packetQueue.Enqueue(remainingData);
                    break;
                }

                // 读取包长度（前4字节）
                int packetSize = BitConverter.ToInt32(dataArray, offset);
                int totalPacketSize = packetSize + 4; // 包长度 + 4字节的长度头

                // 检查是否有完整的数据包
                if (offset + totalPacketSize > dataArray.Length)
                {
                    // 数据包不完整，将剩余数据重新入队等待下次处理
                    var remainingData = new byte[dataArray.Length - offset];
                    Array.Copy(dataArray, offset, remainingData, 0, remainingData.Length);
                    packetQueue.Enqueue(remainingData);
                    break;
                }

                // 提取完整的数据包内容（不包括长度头）
                var packetData = new byte[packetSize];
                Array.Copy(dataArray, offset + 4, packetData, 0, packetSize);

                // 处理完整的数据包
                await ProcessCompletePacket(client, packetData, token);

                // 移动到下一个数据包
                offset += totalPacketSize;
            }
        }

        /// <summary>
        /// 处理完整的数据包
        /// </summary>
        /// <param name="client">客户端</param>
        /// <param name="packetData">完整的数据包数据</param>
        /// <param name="token">取消令牌</param>
        private async Task ProcessCompletePacket(DrxTcpClient client, byte[] packetData, CancellationToken token)
        {
            try
            {
                // Trigger receive hooks
                await TriggerReceiveHooks(client, packetData, token);

                // --- Message Middleware Pipeline ---
                var messageContext = new MessageContext(this, client, packetData, token);
                foreach (var middleware in _messageMiddlewares)
                {
                    try
                    {
                        await middleware(messageContext);
                        if (messageContext.IsHandled)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"消息中间件处理客户端 {client.Client.RemoteEndPoint} 时发生异常。异常信息: {ex}");
                        // We can choose to break or continue; for robustness, we'll just log and continue processing.
                    }
                }

                if (messageContext.IsHandled)
                {
                    return;
                }
                // --- End of Message Middleware ---

                // The command handling logic is now inside CommandHandlingService,
                // which is called as part of the service receive hooks.
                // No further processing is needed here.
            }
            catch (Exception ex)
            {
                Logger.Error($"处理来自客户端 {client.Client.RemoteEndPoint} 的完整数据包时发生错误。异常信息: {ex}");
            }
        }

        public async Task SendResponseAsync(DrxTcpClient client, SocketStatusCode code, CancellationToken token, params object[] args)
        {
            if (client == null || !client.Connected)
            {
                Logger.Warn("无法向已断开连接的客户端发送响应。");
                return;
            }

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
                dict["status_code"] = (int)code;
                rawMessage = System.Text.Json.JsonSerializer.Serialize(dict);
            }
            else
            {
                var messageParts = new List<string> { ((int)code).ToString() };
                messageParts.AddRange(args.Select(a => a?.ToString() ?? string.Empty));
                rawMessage = string.Join("|", messageParts);
            }

            byte[] packBytes = Encoding.UTF8.GetBytes(rawMessage);
            int size = packBytes.Length;
            byte[] sizeBytes = BitConverter.GetBytes(size);
            byte[] sendBytes = new byte[sizeBytes.Length + packBytes.Length];
            Array.Copy(sizeBytes, 0, sendBytes, 0, sizeBytes.Length);
            Array.Copy(packBytes, 0, sendBytes, sizeBytes.Length, packBytes.Length);

            // 发送前触发 send hooks（原始包格式）
            await TriggerSendHooks(client, sendBytes, token);

            if (_packetEncryptor != null)
            {
                sendBytes = _packetEncryptor.Encrypt(sendBytes);
            }
            else if (_packetIntegrityProvider != null)
            {
                sendBytes = _packetIntegrityProvider.Protect(sendBytes);
            }

            try
            {
                var stream = client.GetStream();
                await stream.WriteAsync(sendBytes, 0, sendBytes.Length, token);
            }
            catch (Exception ex)
            {
                Logger.Error($"向客户端 {client.Client.RemoteEndPoint} 发送响应失败。异常信息: {ex}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            _cancellationTokenSource?.Cancel();
            _listener?.Stop();

            if (_udpClient != null)
            {
                try { _udpClient.Close(); } catch { }
                try { _udpClient.Dispose(); } catch { }
                _udpClient = null;
                _udpLocalEndPoint = null;
            }

            if (_timerTasks != null && _timerTasks.Count > 0)
            {
                try
                {
                    Task.WaitAll(_timerTasks.ToArray(), cancellationToken);
                }
                catch { }
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            _listener?.Stop();

            if (_udpClient != null)
            {
                try { _udpClient.Dispose(); } catch { }
                _udpClient = null;
                _udpLocalEndPoint = null;
            }
        }

        /// <summary>
        /// 如果服务创建了 UDP 监听，该属性返回本地监听端点；否则返回 null。
        /// </summary>
        public IPEndPoint? UdpLocalEndPoint => _udpLocalEndPoint;

        public void DisconnectClient(DrxTcpClient client)
        {
            _connectedClients.TryRemove(client, out _);
            client.Close();
        }

        #region Service Hook Triggers

        private async Task TriggerConnectHooks(DrxTcpClient client, CancellationToken token)
        {
            foreach (var service in _socketServices)
            {
                try
                {
                    service.OnClientConnect(this, client);
                    await service.OnClientConnectAsync(this, client, token);
                }
                catch (Exception ex)
                {
                    Logger.Error($"ISocketService 服务 {service.GetType().Name} 的 OnClientConnect 钩子发生错误。异常信息: {ex}");
                }
            }
        }

        private async Task TriggerDisconnectHooks(DrxTcpClient client)
        {
            foreach (var service in _socketServices)
            {
                try
                {
                    service.OnClientDisconnect(this, client);
                    await service.OnClientDisconnectAsync(this, client);
                }
                catch (Exception ex)
                {
                    Logger.Error($"ISocketService 服务 {service.GetType().Name} 的 OnClientDisconnect 钩子发生错误。异常信息: {ex}");
                }
            }
        }

        private async Task TriggerReceiveHooks(DrxTcpClient client, ReadOnlyMemory<byte> data, CancellationToken token)
        {
            foreach (var service in _socketServices)
            {
                try
                {
                    service.OnServerReceive(this, client, data);
                    await service.OnServerReceiveAsync(this, client, data, token);
                }
                catch (Exception ex)
                {
                    Logger.Error($"ISocketService 服务 {service.GetType().Name} 的 OnServerReceive 钩子发生错误。异常信息: {ex}");
                }
            }
        }

        private async Task TriggerSendHooks(DrxTcpClient client, ReadOnlyMemory<byte> data, CancellationToken token)
        {
            foreach (var service in _socketServices)
            {
                try
                {
                    service.OnServerSend(this, client, data);
                    await service.OnServerSendAsync(this, client, data, token);
                }
                catch (Exception ex)
                {
                    Logger.Error($"ISocketService 服务 {service.GetType().Name} 的 OnServerSend 钩子发生错误。异常信息: {ex}");
                }
            }
        }

        #endregion

        // 发送 JSON（对象或字符串），应用 send hooks 与可选加密/完整性
        private Task SendJsonAsync(DrxTcpClient client, object obj, CancellationToken token)
        {
            string json = obj is string s ? s : System.Text.Json.JsonSerializer.Serialize(obj);
            var bytes = Encoding.UTF8.GetBytes(json);
            return SendRawAsync(client, bytes, token);
        }

        // 发送原始字节，统一应用 TriggerSendHooks 与加密/完整性
        private async Task SendRawAsync(DrxTcpClient client, byte[] payload, CancellationToken token)
        {
            // send hooks 传入明文字节（与客户端保持一致）
            await TriggerSendHooks(client, payload, token);

            byte[] sendBytes = payload;
            if (_packetEncryptor != null)
            {
                sendBytes = _packetEncryptor.Encrypt(sendBytes);
            }
            else if (_packetIntegrityProvider != null)
            {
                sendBytes = _packetIntegrityProvider.Protect(sendBytes);
            }

            try
            {
                var stream = client.GetStream();
                await stream.WriteAsync(sendBytes, 0, sendBytes.Length, token);
            }
            catch (Exception ex)
            {
                Logger.Error($"向客户端 {client.Client.RemoteEndPoint} 发送数据失败。异常信息: {ex}");
            }
        }

        // 按连接状态机处理收到的数据块：AwaitHeader -> AwaitBody
        private async Task ProcessByConnectionStateAsync(DrxTcpClient client, byte[] newData, CancellationToken token)
        {
            if (!_connStates.TryGetValue(client, out var state))
            {
                state = new ConnectionState();
                _connStates[client] = state;
            }

            state.LastActivityUtc = DateTime.UtcNow;
            state.Buffer.AddRange(newData);

            while (true)
            {
                if (state.Current == ConnectionState.Phase.AwaitHeader)
                {
                    // 试图将缓存整体解析为 UTF8 JSON：{"packetSize":int}
                    string headerText;
                    try
                    {
                        headerText = Encoding.UTF8.GetString(state.Buffer.ToArray());
                    }
                    catch
                    {
                        await SendJsonAsync(client, new { message_request = false, message = "解码失败" }, token);
                        return;
                    }

                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(headerText);
                        if (!doc.RootElement.TryGetProperty("packetSize", out var ps) || ps.ValueKind != System.Text.Json.JsonValueKind.Number)
                        {
                            await SendJsonAsync(client, new { message_request = false, message = "握手头缺少 packetSize" }, token);
                            return;
                        }

                        int size = ps.GetInt32();
                        if (size < 0 || size > 100 * 1024 * 1024)
                        {
                            await SendJsonAsync(client, new { message_request = false, message = "无效的 packetSize" }, token);
                            return;
                        }

                        // 握手成功：回复 allow，并进入 AwaitBody
                        state.ExpectedBodySize = size;
                        state.Buffer.Clear(); // 清空缓存以接收包体
                        await SendJsonAsync(client, new { message_request = true }, token);
                        state.Current = ConnectionState.Phase.AwaitBody;
                        state.LastActivityUtc = DateTime.UtcNow;
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        // JSON 未完整，继续等待更多字节
                        break;
                    }
                }
                else // AwaitBody
                {
                    if (state.Buffer.Count < state.ExpectedBodySize)
                    {
                        // 继续等待更多包体字节
                        break;
                    }

                    // 取出完整包体
                    var body = state.Buffer.Take(state.ExpectedBodySize).ToArray();
                    var remaining = state.Buffer.Skip(state.ExpectedBodySize).ToList();
                    state.Buffer.Clear();
                    state.Buffer.AddRange(remaining);

                    // 触发接收钩子与消息中间件（明文字节）
                    await TriggerReceiveHooks(client, body, token);

                    var messageContext = new MessageContext(this, client, body, token);
                    foreach (var middleware in _messageMiddlewares)
                    {
                        try
                        {
                            await middleware(messageContext);
                            if (messageContext.IsHandled)
                            {
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"消息中间件处理客户端 {client.Client.RemoteEndPoint} 时发生异常。异常信息: {ex}");
                        }
                    }

                    // 处理完一条消息后，回到 AwaitHeader（新一轮请求）
                    state.Current = ConnectionState.Phase.AwaitHeader;
                    state.ExpectedBodySize = 0;
                    state.LastActivityUtc = DateTime.UtcNow;

                    // 如果缓存还有数据，继续 while 循环尝试解析下一条；否则退出
                    if (state.Buffer.Count == 0)
                    {
                        break;
                    }
                }
            }
        }
    }
}