using Drx.Sdk.Network.Socket;
using Drx.Sdk.Network.Socket.Services;
using DRX.Framework;
using KaxServer.Services;

public class SocketClientService : SocketServiceBase
{
    public override void OnClientDisconnect(SocketServerService server, DrxTcpClient client)
    {
        var userId = client.GetMap<int>("user", "user_id");
        var userName = UserManager.GetUserByIdAsync(userId.Result)?.Result?.Username ?? "Unknown User";
        if (userName != "Unknown User")
        {
            Logger.Info($"用户 {userName} (ID: {userId.Result}) 尝试断开连接...");
            Logger.Info($"用户 {userName} 已成功断开连接，正在清理资源...");
            UserManager.LogoutAppAsync(userId.Result).Wait();
            Logger.Info($"用户 {userName} 的资源已清理完毕。");
        }
        base.OnClientDisconnect(server, client);
    }
}