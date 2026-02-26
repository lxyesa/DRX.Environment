using System.Runtime.Intrinsics.Arm;
using System.Threading.Tasks;
using Drx.Sdk.Network.Security;
using Drx.Sdk.Network.Tcp.Client;
using Drx.Sdk.Network.Tcp.Handler;
using Drx.Sdk.Network.Tcp.Packet;
using Drx.Sdk.Network.Tcp.Server;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Serialization;

public static class KaxSocket
{
    private static DrxTcpServer _tcpServer;
    public static void Initialize()
    {
        if (_tcpServer != null)
        {
            Logger.Error("KaxSocket already initialized");
            throw new InvalidOperationException("KaxSocket already initialized");
        }
        _tcpServer = new DrxTcpServer();
        _tcpServer.RegisterHandler<CommandHandlerServer>(_tcpServer);

        var cmdHandler = _tcpServer.GetHandler<CommandHandlerServer>();
        cmdHandler.RegisterCommand("ping", PingHandlerAsync);
    }

    private static async Task PingHandlerAsync(DrxTcpClient? client1, DrxUdpClient? client2, DrxSerializationData data)
    {
        if (client1 != null)
        {
            var response = new PacketBuilder2DSD()
            {
                { "response", "pong" },
                { "message", "心跳响应" }
            };
            var packet = response.Build();

            await client1.PacketC2SAsync(packet);
        }
    }

    public static async Task Start()
    {
        if (_tcpServer == null)
        {
            Initialize();
        }
        await _tcpServer!.StartAsync(8463);

        Logger.Info("KaxSocket 服务已启动，监听端口 8463");

        // ---------------------------------------------------------------
        // 测试代码，实际使用时请删除
        // ---------------------------------------------------------------
        
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            var client = new DrxTcpClient();
            await client.ConnectAsync("127.0.0.1", 8463);
            var pingPacket = new PacketBuilder2DSD()
            {
                { "command", "ping" },
                { "message", "心跳请求" }
            };

            await client.PacketC2SAsync(pingPacket.Build(), (response) =>
            {
                var responseData = DrxSerializationData.Deserialize(response);
                if (!responseData.TryGetString("response", out var resp) && resp != "pong")
                {
                    Logger.Error("收到无效的响应");
                    return;
                }
            });

            client.Close();
        });
    }
}