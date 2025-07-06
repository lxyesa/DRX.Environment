using Drx.Sdk.Network.Socket;
using Drx.Sdk.Network.Session;
using Web.KaxServer.Models;
using Web.KaxServer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Web.KaxServer.SocketCommands
{
    public static class CommandRegistry
    {
        public static void RegisterCommands(this SocketServerBuilder socket)
        {
            // 验证资产，使用方式：-validasset assetId|userName
            socket.RegisterCommand("validasset", async (server, client, args, rawMessage) =>
            {
                if (args.Length < 2 || string.IsNullOrEmpty(args[0]) || string.IsNullOrEmpty(args[1]))
                {
                    await server.SendResponseAsync(client, SocketStatusCode.Error_MissingArguments, CancellationToken.None);
                    return;
                }

                var userService = server.Services.GetRequiredService<IUserService>() as UserService;
                var userSession = userService?.GetUserById(int.Parse(args[0]));
                if (userSession == null)
                {
                    await server.SendResponseAsync(client, SocketStatusCode.Failure_UserNotFound, CancellationToken.None);
                    return;
                }

                var assetId = int.Parse(args[1]);
                var asset = userSession.OwnedAssets.FirstOrDefault(a => a.Key == assetId);
                if (asset.Value <= DateTime.Now)
                {
                    await server.SendResponseAsync(client, SocketStatusCode.Failure_AssetInvalid, CancellationToken.None);
                    return;
                }

                await server.SendResponseAsync(client, SocketStatusCode.Success_General, CancellationToken.None);
            });

            socket.RegisterCommand("validmc", async (server, client, args, rawMessage) =>
            {
                var logger = server.Services.GetRequiredService<ILogger<SocketServerService>>();
                var userService = server.Services.GetRequiredService<IUserService>() as UserService;

                if (args.Length < 3 || string.IsNullOrEmpty(args[0]) || string.IsNullOrEmpty(args[1]) || string.IsNullOrEmpty(args[2]))
                {
                    await server.SendResponseAsync(client, SocketStatusCode.Error_MissingArguments, CancellationToken.None);
                    return;
                }

                var validMcUsername = args[0];
                if (!int.TryParse(args[1], out int validMcAssetId))
                {
                    await server.SendResponseAsync(client, SocketStatusCode.Error_InvalidArguments, CancellationToken.None);
                    return;
                }
                var machineCodeToValidate = args[2];

                try
                {
                    // 使用新的数据库系统查找用户
                    var userData = userService?.GetAllUsers().FirstOrDefault(u => 
                        u.Username.Equals(validMcUsername, StringComparison.OrdinalIgnoreCase));

                    if (userData == null)
                    {
                        logger.LogWarning("[socket] validmc: User not found in database for '{userName}'.", validMcUsername);
                        await server.SendResponseAsync(client, SocketStatusCode.Failure_UserNotFound, CancellationToken.None);
                        return;
                    }

                    // 检查用户是否拥有有效的资产许可证
                    if (!userData.OwnedAssets.TryGetValue(validMcAssetId, out var expiry) || expiry <= DateTime.Now)
                    {
                        logger.LogWarning("[socket] validmc: User '{userName}' does not have a valid license for asset '{assetId}'.", validMcUsername, validMcAssetId);
                        await server.SendResponseAsync(client, SocketStatusCode.Failure_AssetInvalid, CancellationToken.None);
                        return;
                    }

                    // 验证机器码
                    if (userData.McaCodes.TryGetValue(validMcAssetId, out var storedMachineCode) && !string.IsNullOrEmpty(storedMachineCode))
                    {
                        if (storedMachineCode.Equals(machineCodeToValidate, StringComparison.OrdinalIgnoreCase))
                        {
                            await server.SendResponseAsync(client, SocketStatusCode.Success_Verified, CancellationToken.None);
                        }
                        else
                        {
                            logger.LogWarning("[socket] validmc: Machine code mismatch for user '{userName}', asset '{assetId}'.", validMcUsername, validMcAssetId);
                            await server.SendResponseAsync(client, SocketStatusCode.Failure_MachineCodeMismatch, CancellationToken.None);
                        }
                    }
                    else
                    {
                        // 绑定新的机器码
                        logger.LogInformation("[socket] validmc: Binding new machine code for user '{userName}', asset '{assetId}'.", validMcUsername, validMcAssetId);
                        userData.McaCodes[validMcAssetId] = machineCodeToValidate;

                        // 保存用户数据
                        userService.SaveUser(userData);

                        await server.SendResponseAsync(client, SocketStatusCode.Success_BoundAndVerified, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[socket] Error processing validmc request for user '{userName}'.", validMcUsername);
                    await server.SendResponseAsync(client, SocketStatusCode.Error_InternalServerError, CancellationToken.None);
                }
            });
        }
    }
} 