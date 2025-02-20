using System.Collections.Concurrent;
using System.Net;

namespace Drx.Sdk.Network.Socket
{
    public class SocketServer
    {
        public SocketServer(string address = "127.0.0.1", int port = 8080)
        {
            this.listenAddress = IPAddress.Parse(address);
            this.port = port;
            this.maxConnections = 10;          // 默认最大连接数
            this.receiveBufferSize = 1024;     // 默认接收缓冲区大小
            this.sendBufferSize = 1024;        // 默认发送缓冲区大小
            this.maxMessageSize = 1024;        // 默认最大消息长度

            // 初始化默认处理器
            this.messageHandler = new EchoMessageHandler();
            this.messageEncoder = new EchoMessageEncoder();
            this.messageDecoder = new EchoMessageDecoder();
            this.messageDispatcher = new EchoMessageDispatcher(this);

            InitializeHeartbeat();
        }

        #region 用户载体
        private ConcurrentDictionary<string, ClientInfo> connectedClients = new ConcurrentDictionary<string, ClientInfo>();

        public struct ClientInfo
        {
            public System.Net.Sockets.Socket ClientSocket;
            public string ClientId;
            private ConcurrentDictionary<Type, IComponent> components;

            public ClientInfo(System.Net.Sockets.Socket socket, string id)
            {
                ClientSocket = socket;
                ClientId = id;
                components = new ConcurrentDictionary<Type, IComponent>();
            }

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

            public void CleanupAllComponents()
            {
                foreach (var component in components.Values)
                {
                    component.Cleanup();
                }
                components.Clear();
            }
        }

        #endregion

        #region 服务器配置属性
        private IPAddress listenAddress;
        private int port;
        private int maxConnections;
        private int receiveBufferSize;
        private int sendBufferSize;
        private int maxMessageSize;
        #endregion

        #region 服务器配置方法
        public void Configure(string address, int port, int maxConnections = 10,
            int receiveBufferSize = 1024, int sendBufferSize = 1024, int maxMessageSize = 1024)
        {
            this.listenAddress = IPAddress.Parse(address);
            this.port = port;
            this.maxConnections = maxConnections;
            this.receiveBufferSize = receiveBufferSize;
            this.sendBufferSize = sendBufferSize;
            this.maxMessageSize = maxMessageSize;
        }

        private void InitializeHeartbeat()
        {
            heartbeatTimer = new System.Timers.Timer(heartbeatInterval);
            heartbeatTimer.Elapsed += (s, e) => CheckHeartbeats();
        }

        private void CheckHeartbeats()
        {
            foreach (var client in connectedClients)
            {
                if (!heartbeatFailures.TryGetValue(client.Key, out int failures))
                {
                    heartbeatFailures[client.Key] = 0;
                }

                if (failures >= maxHeartbeatFailures)
                {
                    DisconnectClient(client.Key);
                }
            }
        }

        #endregion

        #region Getters
        public IPAddress ListenAddress => listenAddress;
        public int Port => port;
        public int MaxConnections => maxConnections;
        public int ReceiveBufferSize => receiveBufferSize;
        public int SendBufferSize => sendBufferSize;
        public int MaxMessageSize => maxMessageSize;
        #endregion

        #region 接口定义
        /// <summary>
        /// 定义可添加到客户端的组件接口
        /// </summary>
        public interface IComponent
        {
            /// <summary>
            /// 初始化组件
            /// </summary>
            void Initialize();

            /// <summary>
            /// 清理组件资源
            /// </summary>
            void Cleanup();
        }

        /// <summary>
        /// 定义消息处理器接口
        /// </summary>
        public interface IMessageHandler
        {
            /// <summary>
            /// 处理接收到的消息
            /// </summary>
            /// <param name="clientId">发送消息的客户端ID</param>
            /// <param name="message">消息内容</param>
            void HandleMessage(string clientId, object message);
        }

        /// <summary>
        /// 定义消息编码器接口
        /// </summary>
        public interface IMessageEncoder
        {
            /// <summary>
            /// 将消息对象编码为字节数组
            /// </summary>
            /// <param name="message">要编码的消息对象</param>
            /// <returns>编码后的字节数组</returns>
            byte[] Encode(object message);
        }

        /// <summary>
        /// 定义消息解码器接口
        /// </summary>
        public interface IMessageDecoder
        {
            /// <summary>
            /// 将字节数组解码为消息对象
            /// </summary>
            /// <param name="data">要解码的字节数组</param>
            /// <returns>解码后的消息对象</returns>
            object Decode(byte[] data);
        }

        /// <summary>
        /// 定义消息分发器接口
        /// </summary>
        public interface IMessageDispatcher
        {
            /// <summary>
            /// 将消息分发给适当的处理器
            /// </summary>
            /// <param name="clientId">消息来源的客户端ID</param>
            /// <param name="message">要分发的消息</param>
            void Dispatch(string clientId, object message);
        }

        /// <summary>
        /// 定义连接处理器接口
        /// </summary>
        public interface IConnectionHandler
        {
            /// <summary>
            /// 处理新的客户端连接
            /// </summary>
            /// <param name="clientId">新连接的客户端ID</param>
            void HandleConnection(string clientId);
        }

        /// <summary>
        /// 定义断开连接处理器接口
        /// </summary>
        public interface IDisconnectionHandler
        {
            /// <summary>
            /// 处理客户端断开连接
            /// </summary>
            /// <param name="clientId">断开连接的客户端ID</param>
            void HandleDisconnection(string clientId);
        }

        /// <summary>
        /// 定义异常处理器接口
        /// </summary>
        public interface IExceptionHandler
        {
            /// <summary>
            /// 处理发生的异常
            /// </summary>
            /// <param name="ex">要处理的异常</param>
            void HandleException(Exception ex);
        }

        /// <summary>
        /// 定义日志记录器接口
        /// </summary>
        public interface ILogger
        {
            /// <summary>
            /// 记录日志消息
            /// </summary>
            /// <param name="message">日志消息内容</param>
            /// <param name="level">日志级别，默认为Info</param>
            void Log(string message, LogLevel level = LogLevel.Info);
        }

        /// <summary>
        /// 定义心跳处理器接口
        /// </summary>
        public interface IHeartbeatHandler
        {
            /// <summary>
            /// 处理客户端心跳
            /// </summary>
            /// <param name="clientId">发送心跳的客户端ID</param>
            void HandleHeartbeat(string clientId);
        }

        /// <summary>
        /// 日志级别枚举
        /// </summary>
        public enum LogLevel
        {
            /// <summary>
            /// 调试信息
            /// </summary>
            Debug,

            /// <summary>
            /// 一般信息
            /// </summary>
            Info,

            /// <summary>
            /// 警告信息
            /// </summary>
            Warning,

            /// <summary>
            /// 错误信息
            /// </summary>
            Error
        }
        #endregion

        #region 默认实现
        /// <summary>
        /// 回显消息处理器实现
        /// </summary>
        /// <remarks>
        /// 这是一个基础的消息处理器实现，用于测试和演示目的。
        /// 它简单地将接收到的消息打印到控制台。
        /// </remarks>
        private class EchoMessageHandler : IMessageHandler
        {
            /// <summary>
            /// 处理收到的消息
            /// </summary>
            /// <param name="clientId">发送消息的客户端ID</param>
            /// <param name="message">接收到的消息内容</param>
            public void HandleMessage(string clientId, object message)
            {
                // 简单的回显实现
                Console.WriteLine($"Echo message from {clientId}: {message}");
            }
        }

        /// <summary>
        /// 回显消息编码器实现
        /// </summary>
        /// <remarks>
        /// 将消息对象转换为字节数组的基础实现。
        /// 使用UTF8编码将消息转换为字节。
        /// </remarks>
        private class EchoMessageEncoder : IMessageEncoder
        {
            /// <summary>
            /// 将消息编码为字节数组
            /// </summary>
            /// <param name="message">要编码的消息对象</param>
            /// <returns>编码后的字节数组</returns>
            public byte[] Encode(object message) =>
                System.Text.Encoding.UTF8.GetBytes(message?.ToString() ?? string.Empty);
        }

        /// <summary>
        /// 回显消息解码器实现
        /// </summary>
        /// <remarks>
        /// 将字节数组转换回消息对象的基础实现。
        /// 使用UTF8编码将字节转换为字符串。
        /// </remarks>
        private class EchoMessageDecoder : IMessageDecoder
        {
            /// <summary>
            /// 将字节数组解码为消息对象
            /// </summary>
            /// <param name="data">要解码的字节数组</param>
            /// <returns>解码后的消息对象</returns>
            public object Decode(byte[] data) =>
                System.Text.Encoding.UTF8.GetString(data);
        }

        /// <summary>
        /// 回显消息分发器实现
        /// </summary>
        /// <remarks>
        /// 负责将解码后的消息分发给适当的消息处理器。
        /// 这个基础实现直接将消息转发给服务器的消息处理器。
        /// </remarks>
        private class EchoMessageDispatcher : IMessageDispatcher
        {
            private readonly SocketServer server;

            /// <summary>
            /// 初始化消息分发器
            /// </summary>
            /// <param name="server">所属的Socket服务器实例</param>
            public EchoMessageDispatcher(SocketServer server) => this.server = server;

            /// <summary>
            /// 分发消息到相应的处理器
            /// </summary>
            /// <param name="clientId">消息来源的客户端ID</param>
            /// <param name="message">要分发的消息</param>
            public void Dispatch(string clientId, object message) =>
                server.messageHandler.HandleMessage(clientId, message);
        }
        #endregion

        #region 服务器配置属性
        private IMessageHandler messageHandler;
        private IMessageEncoder messageEncoder;
        private IMessageDecoder messageDecoder;
        private IMessageDispatcher messageDispatcher;
        private IConnectionHandler connectionHandler;
        private IDisconnectionHandler disconnectionHandler;
        private IExceptionHandler exceptionHandler;
        private ILogger logger;
        private IHeartbeatHandler heartbeatHandler;

        // 心跳配置
        private int heartbeatInterval = 1000;
        private int heartbeatTimeout = 5000;
        private int maxHeartbeatFailures = 3;
        private Dictionary<string, int> heartbeatFailures = new();
        private System.Timers.Timer heartbeatTimer;
        #endregion

        #region 事件定义
        /// <summary>
        /// 当收到客户端消息时触发的事件
        /// </summary>
        /// <remarks>
        /// 此事件在服务器成功接收并解码客户端消息后触发。
        /// 事件参数包含发送消息的客户端ID和消息内容。
        /// </remarks>
        public event EventHandler<MessageEventArgs> MessageReceived;

        /// <summary>
        /// 当向客户端发送消息时触发的事件
        /// </summary>
        /// <remarks>
        /// 此事件在服务器成功向客户端发送消息后触发。
        /// 事件参数包含接收消息的客户端ID和消息内容。
        /// </remarks>
        public event EventHandler<MessageEventArgs> MessageSent;

        /// <summary>
        /// 消息事件的参数类
        /// </summary>
        public class MessageEventArgs : EventArgs
        {
            /// <summary>
            /// 获取或设置与消息关联的客户端ID
            /// </summary>
            /// <value>客户端的唯一标识符</value>
            public string? ClientId { get; set; }

            /// <summary>
            /// 获取或设置消息内容
            /// </summary>
            /// <value>消息的实际内容对象</value>
            public object? Message { get; set; }
        }
        #endregion

        #region 服务器操作方法
        /// <summary>
        /// 异步启动服务器
        /// </summary>
        /// <returns>表示异步操作的任务</returns>
        /// <remarks>
        /// 此方法会:
        /// 1. 创建并启动TCP监听器
        /// 2. 启动心跳计时器
        /// 3. 持续接受新的客户端连接
        /// 4. 对每个新连接创建异步处理任务
        /// </remarks>
        public async Task StartAsync()
        {
            try
            {
                var listener = new System.Net.Sockets.TcpListener(listenAddress, port);
                listener.Start();
                heartbeatTimer.Start();

                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client);
                }
            }
            catch (Exception ex)
            {
                exceptionHandler?.HandleException(ex);
            }
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        /// <remarks>
        /// 此方法会:
        /// 1. 停止心跳计时器
        /// 2. 断开所有客户端连接
        /// </remarks>
        public void Stop()
        {
            heartbeatTimer.Stop();
            foreach (var client in connectedClients)
            {
                DisconnectClient(client.Key);
            }
        }

        /// <summary>
        /// 向指定客户端发送消息
        /// </summary>
        /// <param name="clientId">目标客户端的ID</param>
        /// <param name="message">要发送的消息对象</param>
        /// <remarks>
        /// 此方法会:
        /// 1. 检查客户端是否存在
        /// 2. 将消息编码为字节数组
        /// 3. 发送数据到客户端
        /// 4. 触发消息发送事件
        /// </remarks>
        public void SendMessage(string clientId, object message)
        {
            if (connectedClients.TryGetValue(clientId, out var clientInfo))
            {
                var data = messageEncoder.Encode(message);
                clientInfo.ClientSocket.Send(data);
                MessageSent?.Invoke(this, new MessageEventArgs { ClientId = clientId, Message = message });
            }
        }

        /// <summary>
        /// 向所有已连接的客户端广播消息
        /// </summary>
        /// <param name="message">要广播的消息对象</param>
        /// <remarks>
        /// 此方法会:
        /// 1. 将消息编码为字节数组
        /// 2. 遍历所有已连接的客户端
        /// 3. 向每个客户端发送消息
        /// 4. 为每个客户端触发消息发送事件
        /// </remarks>
        public void BroadcastMessage(object message)
        {
            var data = messageEncoder.Encode(message);
            foreach (var client in connectedClients.Values)
            {
                client.ClientSocket.Send(data);
                MessageSent?.Invoke(this, new MessageEventArgs { ClientId = client.ClientId, Message = message });
            }
        }
        #endregion

        #region 客户端操作方法

        /// <summary>
        /// 断开指定客户端连接
        /// </summary>
        /// <param name="clientId">客户端的唯一标识符</param>
        /// <returns>如果成功断开连接返回 true，否则返回 false</returns>
        /// <remarks>
        /// 此方法执行以下操作：
        /// 1. 从已连接客户端集合中移除客户端
        /// 2. 清理客户端的所有组件
        /// 3. 关闭客户端的套接字连接
        /// 4. 触发断开连接处理事件
        /// 5. 清理心跳失败记录
        /// </remarks>
        public bool DisconnectClient(string clientId)
        {
            if (connectedClients.TryRemove(clientId, out var clientInfo))
            {
                try
                {
                    clientInfo.CleanupAllComponents();
                    clientInfo.ClientSocket.Close();
                    disconnectionHandler?.HandleDisconnection(clientId);
                    heartbeatFailures.Remove(clientId);
                    return true;
                }
                catch (Exception ex)
                {
                    exceptionHandler?.HandleException(ex);
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 断开所有已连接的客户端
        /// </summary>
        /// <remarks>
        /// 此方法会遍历所有已连接的客户端，并调用 DisconnectClient 方法断开每个客户端的连接。
        /// 即使某个客户端断开失败，也会继续处理其他客户端。
        /// </remarks>
        public void DisconnectAllClients()
        {
            var clientIds = connectedClients.Keys.ToList();
            foreach (var clientId in clientIds)
            {
                DisconnectClient(clientId);
            }
        }

        /// <summary>
        /// 获取指定客户端的信息
        /// </summary>
        /// <param name="clientId">客户端的唯一标识符</param>
        /// <returns>客户端信息对象，如果客户端不存在则返回 null</returns>
        /// <remarks>
        /// 此方法从已连接客户端集合中获取指定客户端的信息。
        /// ClientInfo 包含客户端的 Socket 连接和组件信息。
        /// </remarks>
        public ClientInfo? GetClientInfo(string clientId)
        {
            connectedClients.TryGetValue(clientId, out var clientInfo);
            return clientInfo;
        }

        /// <summary>
        /// 获取所有已连接客户端的标识符列表
        /// </summary>
        /// <returns>包含所有已连接客户端标识符的只读列表</returns>
        /// <remarks>
        /// 返回的列表是当前已连接客户端的快照，不会随着后续连接状态的变化而更新。
        /// 列表是只读的，无法通过它修改服务器的连接状态。
        /// </remarks>
        public IReadOnlyList<string> GetConnectedClientIds()
        {
            return connectedClients.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// 获取当前连接的客户端数量
        /// </summary>
        /// <value>当前已连接的客户端数量</value>
        /// <remarks>
        /// 此属性返回实时的连接数，会随着客户端的连接和断开而动态变化。
        /// </remarks>
        public int ConnectionCount => connectedClients.Count;


        #endregion

        /// <summary>
        /// 处理客户端连接的异步方法
        /// </summary>
        /// <param name="tcpClient">TCP客户端连接实例</param>
        /// <returns>异步任务</returns>
        /// <remarks>
        /// 此方法负责:
        /// 1. 为新连接创建唯一标识符
        /// 2. 初始化客户端信息
        /// 3. 检查是否超过最大连接数
        /// 4. 添加客户端到连接池
        /// 5. 处理客户端消息
        /// 6. 处理异常情况
        /// 7. 清理断开的连接
        /// </remarks>
        private async Task HandleClientAsync(System.Net.Sockets.TcpClient tcpClient)
        {
            // 生成唯一的客户端标识符
            var clientId = Guid.NewGuid().ToString();
            var clientInfo = new ClientInfo
            {
                ClientSocket = tcpClient.Client,
                ClientId = clientId
            };

            // 检查是否超过最大连接数限制
            if (connectedClients.Count >= maxConnections)
            {
                tcpClient.Close();
                return;
            }

            // 添加到连接池并触发连接事件
            connectedClients.TryAdd(clientId, clientInfo);
            connectionHandler?.HandleConnection(clientId);

            try
            {
                var buffer = new byte[receiveBufferSize];
                while (true)
                {
                    // 异步接收数据
                    var received = await tcpClient.Client.ReceiveAsync(buffer);
                    if (received == 0) break; // 连接关闭

                    // 解码消息并进行分发
                    var message = messageDecoder.Decode(buffer[..received]);
                    messageDispatcher.Dispatch(clientId, message);
                    MessageReceived?.Invoke(this, new MessageEventArgs { ClientId = clientId, Message = message });
                }
            }
            catch (Exception ex)
            {
                // 处理异常情况
                exceptionHandler?.HandleException(ex);
            }
            finally
            {
                // 确保在任何情况下都清理连接
                DisconnectClient(clientId);
            }
        }
    }
}
