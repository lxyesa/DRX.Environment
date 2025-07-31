using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Drx.Sdk.Network.Session;
using System;
using Drx.Sdk.Network.Socket;
using Drx.Sdk.Network.Security;
using Drx.Sdk.Network.Socket.Middleware;
using Drx.Sdk.Network.Socket.Services;
using System.Linq;
using System.Text.Json;

namespace Drx.Sdk.Network.Socket
{
    public static class SocketServiceExtensions
    {
        public static SocketServerBuilder AddSocketService(this IServiceCollection services)
        {
            var builder = new SocketServerBuilder(services);
            services.AddSingleton(builder);
            services.AddSingleton(provider => provider.GetRequiredService<SocketServerBuilder>().CommandHandlers);

            // Automatically register the command handling service
            services.AddSingleton<CommandHandlingService>();

            services.AddSingleton<IHostedService>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<SocketServerService>>();
                var env = provider.GetRequiredService<IHostEnvironment>();
                var serviceBuilder = provider.GetRequiredService<SocketServerBuilder>();

                var socketServices = serviceBuilder.ServiceTypes
                    .Select(type => provider.GetRequiredService(type) as ISocketService)
                    .Where(s => s != null)
                    .Cast<ISocketService>()
                    .ToList();

                // Ensure CommandHandlingService is present and is first, so it runs before other custom receive hooks
                var commandService = provider.GetRequiredService<CommandHandlingService>();
                socketServices.Remove(commandService);
                socketServices.Insert(0, commandService);

                return new SocketServerService(
                    env,
                    provider,
                    serviceBuilder.ConnectionMiddlewares,
                    serviceBuilder.MessageMiddlewares,
                    socketServices
                );
            });

            return builder;
        }
    }
}