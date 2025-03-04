using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static Drx.Sdk.Network.Socket.SocketServer;

namespace Drx.Sdk.Network.Socket
{
    public class SocketClient
    {
        #region 客户端配置属性
        private string serverAddress;
        private int serverPort;
        private int receiveBufferSize;
        private int sendBufferSize;
        private int maxMessageSize;
        private TcpClient? client;
        private bool isConnected;
        private CancellationTokenSource? cancellationSource;

        // 心跳配置
        private int heartbeatInterval = 1000;
        private System.Timers.Timer? heartbeatTimer;
        #endregion

        #region 组件系统
        private ConcurrentDictionary<Type, IComponent> components = new();

        public T AddComponent<T>(T component) where T : IComponent
        {
            if (components.TryAdd(typeof(T), component))
            {
                component.Initialize();
                return component;
            }
            throw new InvalidOperationException($"Component of type {typeof(T).Name} already exists");
        }

        public T GetComponent<T>() where T : IComponent
        {
            if (components.TryGetValue(typeof(T), out var component))
            {
                return (T)component;
            }
            throw new KeyNotFoundException($"Component of type {typeof(T).Name} not found");
        }

        public bool HasComponent<T>() where T : IComponent
        {
            return components.ContainsKey(typeof(T));
        }

        public bool RemoveComponent<T>() where T : IComponent
        {
            if (components.TryRemove(typeof(T), out var component))
            {
                component.Cleanup();
                return true;
            }
            return false;
        }
        #endregion

        #region 构造函数和配置
        public SocketClient(string address = "127.0.0.1", int port = 8080)
        {
            this.serverAddress = address;
            this.serverPort = port;
            this.receiveBufferSize = 1024;
            this.sendBufferSize = 1024;
            this.maxMessageSize = 1024;

            // 初始化默认处理器
            this.messageHandler = new EchoMessageHandler();
            this.messageEncoder = new EchoMessageEncoder();
            this.messageDecoder = new EchoMessageDecoder();

            InitializeHeartbeat();
        }

        public void Configure(string address, int port,
            int receiveBufferSize = 1024, int sendBufferSize = 1024, int maxMessageSize = 1024)
        {
            this.serverAddress = address;
            this.serverPort = port;
            this.receiveBufferSize = receiveBufferSize;
            this.sendBufferSize = sendBufferSize;
            this.maxMessageSize = maxMessageSize;
        }

        private void InitializeHeartbeat()
        {
            heartbeatTimer = new System.Timers.Timer(heartbeatInterval);
            heartbeatTimer.Elapsed += async (s, e) => await SendHeartbeatAsync();
        }
        #endregion

        #region 消息处理接口
        private IMessageHandler messageHandler;
        private IMessageEncoder messageEncoder;
        private IMessageDecoder messageDecoder;

        // 使用与 SocketServer 相同的接口定义
        public interface IMessageHandler
        {
            void HandleMessage(string clientId, object message);
        }

        public interface IMessageEncoder
        {
            byte[] Encode(object message);
        }

        public interface IMessageDecoder
        {
            object Decode(byte[] data);
        }
        #endregion

        #region 事件定义
        public event EventHandler<MessageEventArgs>? MessageReceived;
        public event EventHandler<MessageEventArgs>? MessageSent;
        public event EventHandler<EventArgs>? Connected;
        public event EventHandler<EventArgs>? Disconnected;

        public class MessageEventArgs : EventArgs
        {
            public object? Message { get; set; }
        }
        #endregion

        #region 默认实现
        private class EchoMessageHandler : IMessageHandler
        {
            public void HandleMessage(string clientId, object message)
            {
                Console.WriteLine($"Received message: {message}");
            }
        }

        private class EchoMessageEncoder : IMessageEncoder
        {
            public byte[] Encode(object message) =>
                Encoding.UTF8.GetBytes(message?.ToString() ?? string.Empty);
        }

        private class EchoMessageDecoder : IMessageDecoder
        {
            public object Decode(byte[] data) =>
                Encoding.UTF8.GetString(data);
        }
        #endregion

        #region 客户端操作方法
        public async Task ConnectAsync()
        {
            if (isConnected) return;

            try
            {
                client = new TcpClient();
                await client.ConnectAsync(serverAddress, serverPort);
                isConnected = true;

                cancellationSource = new CancellationTokenSource();
                _ = ReceiveMessagesAsync(cancellationSource.Token);

                heartbeatTimer?.Start();
                Connected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                throw new Exception($"连接服务器失败: {ex.Message}", ex);
            }
        }

        public async Task DisconnectAsync()
        {
            if (!isConnected) return;

            try
            {
                cancellationSource?.Cancel();
                heartbeatTimer?.Stop();

                if (client?.Connected == true)
                {
                    client.Close();
                }

                isConnected = false;
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                throw new Exception($"断开连接失败: {ex.Message}", ex);
            }
            finally
            {
                client?.Dispose();
                cancellationSource?.Dispose();
            }
        }

        public async Task SendMessageAsync(object message)
        {
            if (!isConnected || client?.Connected != true)
                throw new InvalidOperationException("Client is not connected");

            try
            {
                var data = messageEncoder.Encode(message);
                await client.GetStream().WriteAsync(data);
                MessageSent?.Invoke(this, new MessageEventArgs { Message = message });
            }
            catch (Exception ex)
            {
                throw new Exception($"发送消息失败: {ex.Message}", ex);
            }
        }

        private async Task SendHeartbeatAsync()
        {
            if (isConnected)
            {
                try
                {
                    await SendMessageAsync("HEARTBEAT");
                }
                catch (Exception)
                {
                    await DisconnectAsync();
                }
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[receiveBufferSize];

            while (!cancellationToken.IsCancellationRequested && client?.Connected == true)
            {
                try
                {
                    var count = await client.GetStream().ReadAsync(buffer, cancellationToken);
                    if (count == 0) break; // 服务器关闭连接

                    var message = messageDecoder.Decode(buffer[..count]);
                    messageHandler.HandleMessage("server", message);
                    MessageReceived?.Invoke(this, new MessageEventArgs { Message = message });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    await DisconnectAsync();
                    break;
                }
            }
        }
        #endregion

        #region 属性
        public bool IsConnected => isConnected && client?.Connected == true;
        public string ServerAddress => serverAddress;
        public int ServerPort => serverPort;
        #endregion
    }
}
