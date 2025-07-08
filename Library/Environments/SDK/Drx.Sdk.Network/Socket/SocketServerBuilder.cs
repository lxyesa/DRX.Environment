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
    }
} 