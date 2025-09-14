using System;
using System.Threading;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Socket.Hosting
{
    /// <summary>
    /// SocketHost 聚合类：持有 Builder 与 Runner 的引用，提供只读 getter 并代理 Start/Stop。
    /// 构造时需传入一个已配置的 SocketServerBuilder。Set 操作应在构造期间完成。
    /// </summary>
    public sealed class SocketHost : IDisposable
    {
        private readonly SocketServerBuilder _builder;
        private readonly SocketServerRunner _runner;

        public SocketHost(SocketServerBuilder builder, SocketHostOptions? options = null)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _runner = new SocketServerRunner(_builder, options);
        }

        /// <summary>
        /// 构建器（只读）
        /// </summary>
        public SocketServerBuilder Builder => _builder;

        /// <summary>
        /// 运行器（只读）
        /// </summary>
        public SocketServerRunner Runner => _runner;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return _runner.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            return _runner.StopAsync(cancellationToken);
        }

        public void Dispose()
        {
            try { _runner.Dispose(); } catch { }
        }
    }
}
