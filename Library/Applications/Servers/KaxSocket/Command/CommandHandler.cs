using System;
using Drx.Sdk.Network.V2.Socket.Client;
using Drx.Sdk.Network.V2.Socket.Handler;
using Drx.Sdk.Network.V2.Socket.Packet;
using Drx.Sdk.Network.V2.Socket.Server;
using Drx.Sdk.Shared.Serialization;

namespace KaxSocket.Command;

public static class CommandHandler
{
    public static void registerAllCommands(DrxTcpServer server)
    {
        server.GetHandler<CommandHandlerServer>()!.RegisterCommand("ping", PingHandlerAsync);
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
}
