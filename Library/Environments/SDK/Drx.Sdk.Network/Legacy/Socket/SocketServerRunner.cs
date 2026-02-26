using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.Security;
using Drx.Sdk.Network.Legacy.Socket.Middleware;
using Drx.Sdk.Network.Legacy.Socket.Services;
using Drx.Sdk.Network.Legacy.Socket.Hosting;

namespace Drx.Sdk.Network.Legacy.Socket
{
    /// <summary>
    /// 独立模式运行器：无需 IServiceCollection/IHostedService。
    /// - 使用 SocketServerBuilder(无参) 注册命令/中间件/服务/加密与完整性/定时器
    /// - 调用 StartAsync/StopAsync 控制生命周期
    /// </summary>
    public sealed class SocketServerRunner : IDisposable
    {
        private readonly SocketServerBuilder _builder;
        private readonly SocketHostOptions _options;
        private readonly ISocketComponentResolver _resolver;

        private SocketServerService _core;
        private CancellationTokenSource _cts;

        public SocketServerRunner(
            SocketServerBuilder builder,
            SocketHostOptions? options = null,
            ISocketComponentResolver? resolver = null)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _options = options ?? new SocketHostOptions();
            _resolver = resolver ?? new DefaultSocketComponentResolver();
        }

        /// <summary>
        /// 当前 Runner 使用的 Builder
        /// </summary>
        public SocketServerBuilder Builder => _builder;

        /// <summary>
        /// 当前运行时核心服务（StartAsync 后可用）
        /// </summary>
        public SocketServerService? Core => _core;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // 解析服务实例（独立模式下通过反射创建）
            var socketServices = CreateSocketServices(_builder.ServiceTypes);

            // 在独立模式首位注入轻量命令分发服务，保持与 ASP 模式类似的“命令优先”策略
            // 注意：消息中间件在 SocketServerService 中先于服务钩子执行；该服务仅在中间件未标记 handled 的情况下发挥作用
            try
            {
                var lw = new Drx.Sdk.Network.Legacy.Socket.Services.LightweightCommandHandlingService(_builder);
                // 确保位于列表首位
                socketServices.RemoveAll(s => s is Drx.Sdk.Network.Legacy.Socket.Services.LightweightCommandHandlingService);
                socketServices.Insert(0, lw);
            }
            catch { /* ignore creation failure to keep server running */ }

            // 可选的加密/完整性组件
            var encryptor = CreateOptional<IPacketEncryptor>(_builder.EncryptorType);
            var integrity = CreateOptional<IPacketIntegrityProvider>(_builder.IntegrityType);

            // 构造核心服务
            _core = new SocketServerService(
                _builder.ConnectionMiddlewares,
                _builder.MessageMiddlewares,
                socketServices,
                encryptor,
                integrity,
                _builder.TimerRegistrations,
                port: _options.Port,
                udpPort: _options.UdpPort
            );

            await _core.StartAsync(_cts.Token);
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            return _core?.StopAsync(cancellationToken) ?? Task.CompletedTask;
        }

        public void Dispose()
        {
            try
            {
                _cts?.Cancel();
            }
            catch { }
            _cts?.Dispose();
            _core?.Dispose();
        }

        private List<ISocketService> CreateSocketServices(List<Type> serviceTypes)
        {
            var list = new List<ISocketService>();
            foreach (var t in serviceTypes ?? Enumerable.Empty<Type>())
            {
                var svc = _resolver.ResolveOrNull(t) as ISocketService ?? _resolver.Create(t) as ISocketService;
                if (svc != null)
                {
                    list.Add(svc);
                }
            }
            return list;
        }

        private T CreateOptional<T>(Type type) where T : class
        {
            if (type == null) return null;
            return _resolver.ResolveOrNull(type) as T ?? _resolver.Create(type) as T;
        }
    }
}