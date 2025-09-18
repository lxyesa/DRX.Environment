using System;
using Drx.Sdk.Network.V2.Socket.Client;
using Drx.Sdk.Network.V2.Socket.Handler;
using Drx.Sdk.Network.V2.Socket.Packet;
using Drx.Sdk.Network.V2.Socket.Server;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Serialization;

namespace KaxSocket.Command;

public static class CommandHandler
{
    public static void registerAllCommands(DrxTcpServer server)
    {
        server.GetHandler<CommandHandlerServer>()!.RegisterCommand("ping", PingHandlerAsync);
        server.OnTick += Server_OnTick;
    }

    private static void Server_OnTick(DrxTcpServer self)
    {
        self.GetClients().ForEach(client =>
        {
            Logger.Debug("Tick");
            var lastPing = client.GetTag("lastPing");
            if (lastPing != null && lastPing is DateTime lastPingTime)
            {
                if ((DateTime.UtcNow - lastPingTime).TotalSeconds > 30) // 30秒无心跳则断开
                {
                    Logger.Info($"{new Text("Client timed out due to inactivity").SetColor(ConsoleColor.Yellow).SetBold()}：{new Text(client.Client.RemoteEndPoint?.ToString() ?? "Unknown").SetColor(ConsoleColor.Blue).SetBold()}");
                    self.ForceDisconnect(client);
                }
            }
        });
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

            client1.SetTag("lastPing", DateTime.UtcNow);

            await client1.PacketC2SAsync(packet);
        }
    }
}
