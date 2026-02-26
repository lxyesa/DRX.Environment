using System;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.DataBase;
using Drx.Sdk.Network.Legacy.Socket;
using Drx.Sdk.Network.Legacy.Socket.Services;
using DRX.Framework;
using Web.KaxServer.Models;
using Web.KaxServer.Services.Repositorys;

namespace Web.KaxServer.SocketCommands;

public class ClientCleanService : SocketServiceBase
{
    public override async Task OnClientDisconnectAsync(SocketServerService server, DrxTcpClient client)
    {
        var userID = await client.GetMap<string>("client", "user_id");
        await client.RemoveMap("logout");
        var userData = UserRepository.GetUser(int.Parse(userID));
        if (userData != null)
        {
            userData.ClientOnline[int.Parse(userID)] = false;
            userData.ClientTokens[int.Parse(userID)] = "0";
            UserRepository.SaveUser(userData);
        }
        Logger.Info($"userID: {userID}, Logout");
    }
}
