using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Drx.Sdk.Network.Security;
using Drx.Sdk.Network.Socket.Middleware;
using Drx.Sdk.Network.Socket.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Drx.Sdk.Network.Socket
{
    public delegate Task CommandHandler(SocketServerService server, DrxTcpClient client, string[] args, string rawMessage);

    /// <summary>
    /// 支持双轨模式的构建器：
    /// 1) ASP.NET/DI 模式（传入 IServiceCollection）
    /// 2) 独立模式（无参构造），内部记录类型，供 Runner 构造实例
    /// </summary>
    public class SocketServerBuilder
    {
        public Dictionary<string, CommandHandler> CommandHandlers { get; } = new Dictionary<string, CommandHandler>();
        public List<ConnectionMiddleware> ConnectionMiddlewares { get; } = new List<ConnectionMiddleware>();
        public List<MessageMiddleware> MessageMiddlewares { get; } = new List<MessageMiddleware>();
        public List<Type> ServiceTypes { get; } = new List<Type>();

        // 独立模式下记录的可选组件类型
        internal Type EncryptorType { get; private set; }
        internal Type IntegrityType { get; private set; }
        internal List<TimerRegistration> TimerRegistrations { get; } = new List<TimerRegistration>();

        // 可选 DI 容器（仅在 ASP 模式使用）
        private readonly IServiceCollection _services;

        /// <summary>
        /// ASP.NET/DI 构造
        /// </summary>
        public SocketServerBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// 无 DI 构造（独立模式）
        /// </summary>
        public SocketServerBuilder()
        {
        }

        public SocketServerBuilder RegisterCommand(string command, CommandHandler handler)
        {
            CommandHandlers[command.ToLower()] = handler;
            return this;
        }

        /// <summary>
        /// 断开中间件（ASP 模式通过 DI 注册；独立模式由 Runner 注入执行列表）
        /// </summary>
        public SocketServerBuilder OnClientDisconnected(Action<SocketServerService, DrxTcpClient> handler)
        {
            if (_services != null)
            {
                _services.AddSingleton(new ClientDisconnectedMiddleware(handler));
            }
            else
            {
                _detachedClientDisconnectedHandlers.Add(handler);
            }
            return this;
        }

        /// <summary>
        /// 独立模式下暂存的断开处理器集合（ASP 模式不用）
        /// </summary>
        internal readonly List<Action<SocketServerService, DrxTcpClient>> _detachedClientDisconnectedHandlers = new();

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
            if (_services != null)
            {
                _services.AddSingleton<T>();
            }
            ServiceTypes.Add(typeof(T));
            return this;
        }

        public SocketServerBuilder WithEncryption<T>() where T : class, IPacketEncryptor
        {
            if (_services != null)
            {
                _services.AddSingleton<IPacketEncryptor, T>();
            }
            else
            {
                EncryptorType = typeof(T);
            }
            return this;
        }

        public SocketServerBuilder WithIntegrityCheck<T>() where T : class, IPacketIntegrityProvider
        {
            if (_services != null)
            {
                _services.AddSingleton<IPacketIntegrityProvider, T>();
            }
            else
            {
                IntegrityType = typeof(T);
            }
            return this;
        }

        /// <summary>
        /// 注册一个定时器（独立与 ASP 模式均可）
        /// </summary>
        public SocketServerBuilder RegisterTimer(int time, Action<SocketServerService> handler)
        {
            if (_services != null)
            {
                _services.AddSingleton(new TimerRegistration
                {
                    IntervalSeconds = time,
                    Handler = handler
                });
            }
            else
            {
                TimerRegistrations.Add(new TimerRegistration
                {
                    IntervalSeconds = time,
                    Handler = handler
                });
            }
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