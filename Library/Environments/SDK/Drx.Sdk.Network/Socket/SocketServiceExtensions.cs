using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using Drx.Sdk.Network.Security;
using Drx.Sdk.Network.Socket.Services;

namespace Drx.Sdk.Network.Socket
{
    /// <summary>
    /// ASP.NET Core 集成扩展：保持原有 API AddSocketService 不变；
    /// 内部使用 HostedServiceAdapter 适配独立版 SocketServerService 的启动/停止。
    /// </summary>
    public static class SocketServiceExtensions
    {
        public static SocketServerBuilder AddSocketService(this IServiceCollection services)
        {
            var builder = new SocketServerBuilder(services);
            services.AddSingleton(builder);
            services.AddSingleton(provider => provider.GetRequiredService<SocketServerBuilder>().CommandHandlers);

            // 自动注册命令处理服务
            services.AddSingleton<CommandHandlingService>();

            // 以 IHostedService 形式装配（包装独立核心）
            services.AddSingleton<IHostedService>(provider =>
            {
                var env = provider.GetRequiredService<IHostEnvironment>();
                var serviceBuilder = provider.GetRequiredService<SocketServerBuilder>();

                var socketServices = serviceBuilder.ServiceTypes
                    .Select(type => provider.GetRequiredService(type) as ISocketService)
                    .Where(s => s != null)
                    .Cast<ISocketService>()
                    .ToList();

                // 确保命令处理服务优先
                var commandService = provider.GetRequiredService<CommandHandlingService>();
                socketServices.Remove(commandService);
                socketServices.Insert(0, commandService);

                // 从 DI 中可选获取加密/完整性与定时器
                var encryptor = provider.GetService<IPacketEncryptor>();
                var integrity = provider.GetService<IPacketIntegrityProvider>();
                var timers = provider.GetServices<SocketServerBuilder.TimerRegistration>() ?? Enumerable.Empty<SocketServerBuilder.TimerRegistration>();

                // 构造独立核心
                var core = new SocketServerService(
                    serviceBuilder.ConnectionMiddlewares,
                    serviceBuilder.MessageMiddlewares,
                    socketServices,
                    encryptor,
                    integrity,
                    timers,
                    port: 8463,
                    serviceProviderOrNull: provider,
                    environmentName: env.EnvironmentName
                );

                return new HostedServiceAdapter(core);
            });

            return builder;
        }

        /// <summary>
        /// 适配器：把独立的 SocketServerService 暴露为 IHostedService 供 ASP.NET 使用
        /// </summary>
        internal sealed class HostedServiceAdapter : IHostedService, System.IDisposable
        {
            private readonly SocketServerService _core;
            public HostedServiceAdapter(SocketServerService core) => _core = core;
            public Task StartAsync(CancellationToken cancellationToken) => _core.StartAsync(cancellationToken);
            public Task StopAsync(CancellationToken cancellationToken) => _core.StopAsync(cancellationToken);
            public void Dispose() => _core.Dispose();
        }
    }
}