using Drx.Sdk.Network.Socket;
using Drx.Sdk.Network.Extensions;
using DRX.Framework;
using KaxServer.Services;
using KaxServer.Models;
using System;

public class SocketCommandRegister
{
    public static void Register(SocketServerBuilder socket)
    {
        socket.RegisterCommand("login", async (server, client, args, rawMessage) =>
        {
            var psw = args.GetJsonField("password") ?? string.Empty;
            var usn = args.GetJsonField("username") ?? string.Empty;

            CommandResult? commandResult = await UserManager.LoginAppAsync(usn, psw);
            var uData = commandResult?.Data as UserData;
            var resultCode = commandResult?.StatusCode ?? SocketStatusCode.Failure_General;
            var resultMessage = commandResult?.Message ?? "登录失败";

            if (uData == null)
            {
                await server.SendResponseAsync(client, resultCode, CancellationToken.None, new { command = "login", message = resultMessage });
                return;
            }

            var token = uData.UserStatusData.AppToken;

            await UserManager.UpdateUserAsync(uData);

            await client.PushMap("user", "user_token", token);
            await client.PushMap("user", "last_heartbeat", DateTime.Now.ToString());
            await client.PushMap("user", "user_id", uData.Id);

            await server.SendResponseAsync(client, SocketStatusCode.Success_General, CancellationToken.None, new { command = "login", message = "登录成功", user_token = token });
        });

        socket.RegisterCommand("heartbeat", async (server, client, args, rawMessage) =>
        {
            await UpdateHeartbeat(client);
            await server.SendResponseAsync(client, SocketStatusCode.Success_General, CancellationToken.None, new { command = "heartbeat", message = "心跳成功" });
        });

        socket.RegisterCommand("getuid", async (server, client, args, rawMessage) =>
        {
            var user_token = client.GetMap<string>("user", "user_token");
            var user_id = client.GetMap<int>("user", "user_id");
            var user = await UserManager.GetUserByAppTokenAsync(user_token.Result);
            if (user == null)
            {
                await server.SendResponseAsync(
                    client,
                    SocketStatusCode.Failure_InvalidCredentials,
                    CancellationToken.None,
                    new { command = "getuid", message = "无效的用户令牌" });
                return;
            }
            await server.SendResponseAsync(
                client,
                SocketStatusCode.Success_General,
                CancellationToken.None,
                new { command = "getuid", message = "获取用户ID成功", user_id = user.Id });
        });

        socket.RegisterTimer(60 * 5, async server =>
        {
            await CheckClientHeartbeats(server);
        });
    }

    private static async Task UpdateHeartbeat(Drx.Sdk.Network.Socket.DrxTcpClient client)
    {
        var time = DateTime.Now.ToString();
        await client.PushMap("user", "last_heartbeat", time);
    }

    private static async Task CheckClientHeartbeats(SocketServerService server)
    {
        var clients = server.ConnectedClients;
        if (clients == null || clients.Count == 0)
            return;

        foreach (var client in clients)
        {
            var lastHeartbeat = client.GetMap<string>("user", "last_heartbeat");
            if (lastHeartbeat == null)
                continue;

            if (!DateTime.TryParse(lastHeartbeat.Result, out DateTime heartbeatTime))
            {
                var login = client.GetMap<string>("user", "user_id");
                if (login.Result == string.Empty)
                {
                    client.Close();
                }
                continue;
            }

            if (DateTime.Now - heartbeatTime > TimeSpan.FromMinutes(5))
            {
                var userId = client.GetMap<int>("user", "user_id");
                if (userId != null)
                {
                    await UserManager.LogoutAppAsync(userId.Result);
                }
                server.DisconnectClient(client);
            }
        }
    }
}