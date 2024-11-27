using System;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

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
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][Debug-NetworkNDVServerPacketHandler:19 line] 收到来自 {clientSocket.RemoteEndPoint} 的数据包: {packet.Header}");
        switch (packet.Type)
        {
            case PacketType.Request:
                HandleRequestPacket(clientSocket, packet);
                break;
            case PacketType.Response:
                
                break;
            case PacketType.Command:
                
                break;
            case PacketType.Data:
                
                break;
            case PacketType.Error:
                
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
            var request = packet.GetBodyObject<LoginRequestBody>();
            var ipString = socket?.RemoteEndPoint?.ToString()?.Split(":")[0];
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {request.Username} 正在尝试登录...");

            var (result, user) = await server.GetUserManager()
                .LoginUserAsync(
                        request.Username, request.Password, "machineCode", IPAddress.Parse(ipString!));

            switch (result)
            {
                case UserLoginResult.UserNotFound:
                    await SendAsync(socket!, new LoginResponseBody(
                        token: string.Empty,
                        message: "用户不存在",
                        success: false,
                        username: request.Username,
                        responseCode: ResponseCode.Failure
                    ), PacketType.Error, packet.Key);
                    break;
                case UserLoginResult.WrongPassword:
                    await SendAsync(socket!, "密码错误", PacketType.Error);
                    break;
                case UserLoginResult.AlreadyOnline:
                    await SendAsync(socket!, "用户已在线", PacketType.Error);
                    break;
                case UserLoginResult.Success:
                    await SendAsync(socket!, "登录成功", PacketType.Response);
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
