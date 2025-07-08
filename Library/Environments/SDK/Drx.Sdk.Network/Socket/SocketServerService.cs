using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using Drx.Sdk.Network.Session;
using Microsoft.Extensions.DependencyInjection;
using Drx.Sdk.Network.Socket;
using Drx.Sdk.Network.Security;
using Drx.Sdk.Network.Socket.Middleware;
using System.Collections.Concurrent;
using Drx.Sdk.Network.Socket.Services;

namespace Drx.Sdk.Network.Socket
{
    public class SocketServerService : IHostedService, IDisposable
    {
        private readonly ILogger<SocketServerService> _logger;
        private TcpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPacketEncryptor _packetEncryptor;
        private readonly IPacketIntegrityProvider _packetIntegrityProvider;
        private readonly IReadOnlyList<ConnectionMiddleware> _connectionMiddlewares;
        private readonly IReadOnlyList<MessageMiddleware> _messageMiddlewares;
        private readonly ConcurrentDictionary<DrxTcpClient, bool> _connectedClients = new ConcurrentDictionary<DrxTcpClient, bool>();
        private readonly IReadOnlyList<ISocketService> _socketServices;
        private Task _servicesTask;

        public IHostEnvironment Env { get; }
        public SessionManager SessionManager { get; }
        public IServiceProvider Services => _serviceProvider;
        
        /// <summary>
        /// Gets a collection of all currently connected clients.
        /// </summary>
        public ICollection<DrxTcpClient> ConnectedClients => _connectedClients.Keys;

        /// <summary>
        /// 获取指定类型的服务实例
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <returns>服务实例或null</returns>
        public T? GetService<T>() where T : class
        {
            // 首先尝试从依赖注入容器获取服务
            var service = _serviceProvider.GetService<T>();
            if (service != null)
                return service;

            // 如果是ISocketService类型，则尝试从_socketServices列表中查找
            if (typeof(ISocketService).IsAssignableFrom(typeof(T)))
            {
                return _socketServices.FirstOrDefault(s => s is T) as T;
            }

            return null;
        }

        public SocketServerService(
            ILogger<SocketServerService> logger, 
            IHostEnvironment env, 
            SessionManager sessionManager, 
            IServiceProvider serviceProvider,
            IReadOnlyList<ConnectionMiddleware> connectionMiddlewares,
            IReadOnlyList<MessageMiddleware> messageMiddlewares,
            IReadOnlyList<ISocketService> socketServices
            )
        {
            _logger = logger;
            Env = env;
            SessionManager = sessionManager;
            _serviceProvider = serviceProvider;
            _connectionMiddlewares = connectionMiddlewares;
            _messageMiddlewares = messageMiddlewares;
            _socketServices = socketServices;
#pragma warning disable CS8601 // 引用类型赋值可能为 null。
            _packetEncryptor = _serviceProvider.GetService<IPacketEncryptor>();
#pragma warning restore CS8601 // 引用类型赋值可能为 null。
#pragma warning disable CS8601 // 引用类型赋值可能为 null。
            _packetIntegrityProvider = _serviceProvider.GetService<IPacketIntegrityProvider>();
#pragma warning restore CS8601 // 引用类型赋值可能为 null。

            if (_packetEncryptor != null && _packetIntegrityProvider != null)
            {
                throw new InvalidOperationException("Cannot enable both encryption and integrity check simultaneously. Please choose one.");
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _cancellationTokenSource.Token;

            var serviceTasks = _socketServices.Select(s => s.ExecuteAsync(token)).ToList();
            foreach(var service in _socketServices)
            {
                // Run synchronous Execute methods in background threads so they don't block startup
                serviceTasks.Add(Task.Run(() => service.Execute(), token));
            }
            _servicesTask = Task.WhenAll(serviceTasks);

            // 在后台线程中启动监听器
            Task.Run(() => ListenForClients(token), token);

            return Task.CompletedTask;
        }

        private async Task ListenForClients(CancellationToken token)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, 8463); 
                _listener.Start();
                _logger.LogInformation("Socket server started on port 8463.");

                while (!token.IsCancellationRequested)
                {
                    _logger.LogInformation("[socket] waiting for client...");
                    TcpClient tcpClient = await _listener.AcceptTcpClientAsync(token);
                    
                    // 将 TcpClient 转换为 DrxTcpClient
                    DrxTcpClient client = tcpClient.ToDrxTcpClient();
                    
                    _logger.LogInformation("[socket] client connected!");

                    // 在单独的任务中处理客户端
                    _ = HandleClientAsync(client, token);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[socket] socket server stopped by cancellation.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[socket] socket server error.");
            }
            finally
            {
                _listener?.Stop();
                _logger.LogInformation("[socket] socket server stopped.");
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
                        _logger.LogWarning("Connection from {clientEndpoint} was rejected by connection middleware.", client.Client.RemoteEndPoint);
                        client.Close();
                        return; // Stop processing this client
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An exception occurred in a connection middleware for client {clientEndpoint}. Closing connection.", client.Client.RemoteEndPoint);
                    client.Close();
                    return;
                }
            }
            // --- End of Connection Middleware ---

            _connectedClients.TryAdd(client, true);
            _logger.LogInformation("Client {clientEndpoint} added to connection list. Total clients: {clientCount}", client.Client.RemoteEndPoint, _connectedClients.Count);

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

                        if (_packetEncryptor != null)
                        {
                            _logger.LogTrace("Decrypting received data...");
                            receivedData = _packetEncryptor.Decrypt(receivedData);
                            if (receivedData == null)
                            {
                                _logger.LogWarning("Failed to decrypt data from client {clientEndpoint}. Closing connection.", client.Client.RemoteEndPoint);
                                break;
                            }
                        }
                        else if (_packetIntegrityProvider != null)
                        {
                            _logger.LogTrace("Verifying packet integrity...");
                            receivedData = _packetIntegrityProvider.Unprotect(receivedData);
                            if (receivedData == null)
                            {
                                _logger.LogWarning("Packet integrity check failed for client {clientEndpoint}. Tampered packet suspected. Closing connection.", client.Client.RemoteEndPoint);
                                break;
                            }
                        }

                        // Trigger receive hooks
                        await TriggerReceiveHooks(client, receivedData, token);

                        // --- Message Middleware Pipeline ---
                        var messageContext = new MessageContext(this, client, receivedData, token);
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
                                _logger.LogError(ex, "An exception occurred in a message middleware for client {clientEndpoint}.", client.Client.RemoteEndPoint);
                                // We can choose to break or continue; for robustness, we'll just log and continue processing.
                            }
                        }

                        if (messageContext.IsHandled)
                        {
                            _logger.LogTrace("Message from {clientEndpoint} handled by middleware.", client.Client.RemoteEndPoint);
                            continue; // Continue to the next ReadAsync to wait for the next message
                        }
                        // --- End of Message Middleware ---

                        // The command handling logic is now inside CommandHandlingService,
                        // which is called as part of the service receive hooks.
                        // No further processing is needed here.
                    }
                }
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                _logger.LogWarning("[socket] Client disconnected abruptly.");
            }
            catch (OperationCanceledException)
            {
                 _logger.LogInformation("[socket] Client handling cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[socket] error handling client.");
            }
            finally
            {
                // Trigger disconnect hooks
                _ = Task.Run(() => TriggerDisconnectHooks(client), token);

                _connectedClients.TryRemove(client, out _);
                _logger.LogInformation("Client {clientEndpoint} removed from connection list. Total clients: {clientCount}", client.Client.RemoteEndPoint, _connectedClients.Count);
                client.Close();
                _logger.LogInformation("[socket] client disconnected.");
            }
        }

        public async Task SendResponseAsync(DrxTcpClient client, SocketStatusCode code, CancellationToken token, params object[] args)
        {
            if (client == null || !client.Connected)
            {
                _logger.LogWarning("Cannot send response to a disconnected client.");
                return;
            }

            var messageParts = new List<string> { ((int)code).ToString() };
            messageParts.AddRange(args.Select(a => a?.ToString() ?? string.Empty));
            var rawMessage = string.Join("|", messageParts);

            byte[] messageBytes = Encoding.UTF8.GetBytes(rawMessage);

            // Trigger send hooks before encryption/signing
            await TriggerSendHooks(client, messageBytes, token);

            if (_packetEncryptor != null)
            {
                _logger.LogTrace("Encrypting response data...");
                messageBytes = _packetEncryptor.Encrypt(messageBytes);
            }
            else if (_packetIntegrityProvider != null)
            {
                _logger.LogTrace("Signing response data...");
                messageBytes = _packetIntegrityProvider.Protect(messageBytes);
            }

            try
            {
                var stream = client.GetStream();
                await stream.WriteAsync(messageBytes, 0, messageBytes.Length, token);
                _logger.LogInformation("Sent to {clientEndpoint}: {message}", client.Client.RemoteEndPoint, rawMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send response to {clientEndpoint}.", client.Client.RemoteEndPoint);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource?.Cancel();
            _listener?.Stop();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            _listener?.Stop();
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
                    _logger.LogError(ex, "Error in ISocketService OnClientConnect hook for service {serviceType}", service.GetType().Name);
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
                    _logger.LogError(ex, "Error in ISocketService OnClientDisconnect hook for service {serviceType}", service.GetType().Name);
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
                    _logger.LogError(ex, "Error in ISocketService OnServerReceive hook for service {serviceType}", service.GetType().Name);
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
                    _logger.LogError(ex, "Error in ISocketService OnServerSend hook for service {serviceType}", service.GetType().Name);
                }
            }
        }

        #endregion
    }
} 