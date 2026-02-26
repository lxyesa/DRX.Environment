using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.Legacy.Socket;
using Drx.Sdk.Network.Legacy.Socket.Services;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Legacy.Socket.Services
{
    /// <summary>
    /// 轻量命令分发服务（独立 Runner 专用）。
    /// 协议：单帧 UTF-8 文本为 JSON 对象：{ "command": string, "args": string[] }
    /// 行为：
    /// - 仅在消息未被中间件标记为 handled 时介入（由 SocketServerService 保证中间件先运行）。
    /// - 解析成功后，按 builder.CommandHandlers 查找并调用对应处理器。
    /// - 未匹配到命令或解析失败时静默跳过，保持与“全部吞错、服务稳定优先”的策略一致。
    /// </summary>
    public sealed class LightweightCommandHandlingService : SocketServiceBase
    {
        private readonly SocketServerBuilder _builder;

        public LightweightCommandHandlingService(SocketServerBuilder builder)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        public override void Execute()
        {
            // 无需同步常驻任务
        }

        public override Task ExecuteAsync(CancellationToken token)
        {
            // 无需异步常驻任务
            return Task.CompletedTask;
        }

        public override void OnClientConnect(SocketServerService server, DrxTcpClient client)
        {
            // no-op
        }

        public override Task OnClientConnectAsync(SocketServerService server, DrxTcpClient client, CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public override void OnClientDisconnect(SocketServerService server, DrxTcpClient client)
        {
            // no-op
        }

        public override Task OnClientDisconnectAsync(SocketServerService server, DrxTcpClient client)
        {
            return Task.CompletedTask;
        }

        public override void OnServerReceive(SocketServerService server, DrxTcpClient client, ReadOnlyMemory<byte> data)
        {
            // 留空：统一在异步钩子中处理，避免重复且便于未来异步扩展
        }

        public override Task OnServerReceiveAsync(SocketServerService server, DrxTcpClient client, ReadOnlyMemory<byte> data, CancellationToken token)
        {
            // 将分发逻辑放在异步钩子，保留“ASYNC 有效”的扩展点
            TryDispatch(server, client, data.Span);
            return Task.CompletedTask;
        }

        public override void OnServerSend(SocketServerService server, DrxTcpClient client, ReadOnlyMemory<byte> data)
        {
            // no-op
        }

        public override Task OnServerSendAsync(SocketServerService server, DrxTcpClient client, ReadOnlyMemory<byte> data, CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public override byte[]? OnUdpReceive(SocketServerService server, System.Net.IPEndPoint remote, ReadOnlyMemory<byte> data)
        {
            // 不干预 UDP 消息
            return null;
        }

        public override Task<byte[]?> OnUdpReceiveAsync(SocketServerService server, System.Net.IPEndPoint remote, ReadOnlyMemory<byte> data, CancellationToken token)
        {
            // 异步默认不处理
            return Task.FromResult<byte[]?>(null);
        }

        private void TryDispatch(SocketServerService server, DrxTcpClient client, ReadOnlySpan<byte> data)
        {
            try
            {
                // 将定长帧按 0x00 去尾得到实际文本
                int len = data.Length;
                while (len > 0 && data[len - 1] == 0x00) len--;
                if (len == 0) return;

                string json = Encoding.UTF8.GetString(data.Slice(0, len));
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("command", out var cmdProp) || cmdProp.ValueKind != JsonValueKind.String)
                    return;

                string command = cmdProp.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(command)) return;

                string[] args = Array.Empty<string>();
                if (root.TryGetProperty("args", out var argsProp) && argsProp.ValueKind == JsonValueKind.Array)
                {
                    var list = new System.Collections.Generic.List<string>();
                    foreach (var a in argsProp.EnumerateArray())
                    {
                        if (a.ValueKind == JsonValueKind.String) list.Add(a.GetString() ?? string.Empty);
                        else list.Add(a.ToString());
                    }
                    args = list.ToArray();
                }

                var key = command.ToLowerInvariant();
                if (_builder.CommandHandlers.TryGetValue(key, out var handler) && handler != null)
                {
                    // rawMessage 传原 JSON 文本
                    _ = Task.Run(() => handler(server, client, args, json));
                }
            }
            catch (Exception ex)
            {
                // 吞异常但记录日志，避免影响主流程
                Logger.Warn($"[LightweightCommandHandlingService] 命令解析/分发异常: {ex.Message}, 原始String: {Encoding.UTF8.GetString(data)}");
            }
        }
    }
}