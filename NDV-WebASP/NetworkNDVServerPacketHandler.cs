using System;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using NetworkCoreStandard;
using NetworkCoreStandard.Handler;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils.Builder;

namespace NDV_WebASP;

public class NetworkNDVServerPacketHandler : NetworkServerPacketHandler
{
    public NetworkNDVServerPacketHandler(Socket serverSocket, NetworkServer server) : base(serverSocket,server)
    {
        Console.WriteLine("NetworkNDVServerPacketHandler initialized");
    }

    public override async Task HandlePacketAsync(Socket clientSocket, Socket serverSocket, NetworkPacket packet)
    {
        await base.HandlePacketAsync(clientSocket, serverSocket, packet);

        

        switch (packet.Type)
        {
            case (int)PacketType.Request:
                HandleRequestPacket(clientSocket, packet);
                break;
            case (int)PacketType.Response:

                break;
            case (int)PacketType.Command:

                break;
            case (int)PacketType.Data:

                break;
            case (int)PacketType.Error:

                break;
            case (int)PacketType.Heartbeat:
                var heartbeat = new NetworkPacket()
                {
                    Header = "hb",
                    Body = new NetworkPacketBodyBuilder()
                        .Put("message", "pong")
                        .Builder(),
                    Type = (int)PacketType.Heartbeat
                };
                await SendAsync(clientSocket, heartbeat, PacketType.Heartbeat);
                break;
            default:
                break;
        }
    }

    private async void HandleRequestPacket(Socket clientSocket, NetworkPacket packet)
    {
        
        switch (packet.Header)
        {
            case "Login":
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 客户端 {clientSocket.RemoteEndPoint} 正在尝试登录...");
                await HandleLoginRequest(clientSocket, packet);
                break;
            case "register":
                break;
            default:
                break;
        }
    }

    async Task HandleLoginRequest(Socket socket, NetworkPacket packet)
    {
        try
        {
            var ipString = socket?.RemoteEndPoint?.ToString()?.Split(":")[0];

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 登录请求ID{packet.Key},来自{ipString},"+
                    $"用户名{packet.GetBodyValue("username")}，密码{packet.GetBodyValue("password")} 已被服务器受理");
            var (result, user) = await server.GetUserManager()
                .LoginUserAsync(
                        packet.GetBodyValue("username")?.ToString() ?? throw new ArgumentNullException("username"),
                        packet.GetBodyValue("password")?.ToString() ?? throw new ArgumentNullException("password"),
                        "machineCode", IPAddress.Parse(ipString!));

            switch (result)
            {
                case UserLoginResult.UserNotFound:
                    await SendAsync(socket!, new NetworkPacket(){
                        Header = "error",
                        Body = new NetworkPacketBodyBuilder()
                            .Put("message", "用户不存在")
                            .Builder(),
                        Type = (int)PacketType.Error
                    }, PacketType.Error, packet.Key!);
                    break;
                case UserLoginResult.WrongPassword:
                    await SendAsync(socket!, new NetworkPacket(){
                        Header = "error",
                        Body = new NetworkPacketBodyBuilder()
                            .Put("message", "密码错误")
                            .Builder(),
                        Type = (int)PacketType.Error
                    }, PacketType.Error, packet.Key!);
                    break;
                case UserLoginResult.AlreadyOnline:
                    await SendAsync(socket!, new NetworkPacket(){
                        Header = "error",
                        Body = new NetworkPacketBodyBuilder()
                            .Put("message", "用户已在线")
                            .Builder(),
                        Type = (int)PacketType.Error
                    }, PacketType.Error, packet.Key!);
                    break;
                case UserLoginResult.Success:
                    await SendAsync(socket!, new NetworkPacket(){
                        Header = "success",
                        Body = new NetworkPacketBodyBuilder()
                            .Put("message", "登录成功")
                            .Builder(),
                        Type = (int)PacketType.Error
                    }, PacketType.Error, packet.Key!);
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"详细错误: {ex}");
        }
    }
}
