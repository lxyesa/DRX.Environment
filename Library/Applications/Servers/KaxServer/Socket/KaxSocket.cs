using System.Runtime.Intrinsics.Arm;
using Drx.Sdk.Network.Security;
using Drx.Sdk.Network.Socket;
using DRX.Framework;

public static class KaxSocket
{
    public static readonly SocketServerBuilder builder = new SocketServerBuilder();
    public static readonly SocketServerRunner runner = new SocketServerRunner(builder);
    public static void Initialize()
    {
        // builder.WithEncryption<AesEncryptor>();

        builder.RegisterCommand("ping", async (server, client, args, rawMessage) =>
        {
            Logger.Info($"收到心跳请求: {rawMessage}");
            await server.SendResponseAsync(client,
                SocketStatusCode.Failure_General,
                CancellationToken.None,
                new { command = "pong", message = "心跳响应" });
        });
    }

    public static async Task Start()
    {
        Initialize();
        await runner.StartAsync();
    }
}