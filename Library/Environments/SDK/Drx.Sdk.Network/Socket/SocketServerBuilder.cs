using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Drx.Sdk.Network.Security;
using Drx.Sdk.Network.Socket.Middleware;
using Drx.Sdk.Network.Socket.Services;
using System;

namespace Drx.Sdk.Network.Socket
{
    public delegate Task CommandHandler(SocketServerService server, DrxTcpClient client, string[] args, string rawMessage);

    public class SocketServerBuilder
    {
        public Dictionary<string, CommandHandler> CommandHandlers { get; } = new Dictionary<string, CommandHandler>();
        public List<ConnectionMiddleware> ConnectionMiddlewares { get; } = new List<ConnectionMiddleware>();
        public List<MessageMiddleware> MessageMiddlewares { get; } = new List<MessageMiddleware>();
        public List<Type> ServiceTypes { get; } = new List<Type>();
        private readonly IServiceCollection _services;

        public SocketServerBuilder(IServiceCollection services)
        {
            _services = services;
        }

        public SocketServerBuilder RegisterCommand(string command, CommandHandler handler)
        {
            CommandHandlers[command.ToLower()] = handler;
            return this;
        }

        public SocketServerBuilder OnClientDisconnected(Action<SocketServerService, DrxTcpClient> handler)
        {
            _services.AddSingleton(new ClientDisconnectedMiddleware(handler));
            return this;
        }
        
        public SocketServerBuilder OnClientConnected(ConnectionMiddleware handler)
        {
            ConnectionMiddlewares.Add(handler);
            return this;
        }

        public SocketServerBuilder UseMessageMiddleware(MessageMiddleware handler)
        {
            MessageMiddlewares.Add(handler);
            return this;
        }

        public SocketServerBuilder AddService<T>() where T : class, ISocketService
        {
            _services.AddSingleton<T>();
            ServiceTypes.Add(typeof(T));
            return this;
        }

        public SocketServerBuilder WithEncryption<T>() where T : class, IPacketEncryptor
        {
            _services.AddSingleton<IPacketEncryptor, T>();
            return this;
        }

        public SocketServerBuilder WithIntegrityCheck<T>() where T : class, IPacketIntegrityProvider
        {
            _services.AddSingleton<IPacketIntegrityProvider, T>();
            return this;
        }

        /// <summary>
        /// 注册一个定时器，每隔 time 秒执行一次 handler。
        /// </summary>
        /// <param name="time">定时间隔（秒）</param>
        /// <param name="handler">定时回调，参数为 SocketServerService</param>
        public SocketServerBuilder RegisterTimer(int time, Action<SocketServerService> handler)
        {
            // 将定时器注册到 DI 容器，实际启动应在 SocketServerService 内实现
            _services.AddSingleton(new TimerRegistration
            {
                IntervalSeconds = time,
                Handler = handler
            });
            return this;
        }

        /// <summary>
        /// 定时器注册信息
        /// </summary>
        public class TimerRegistration
        {
            public int IntervalSeconds { get; set; }
            public Action<SocketServerService> Handler { get; set; }
        }
    }
} 