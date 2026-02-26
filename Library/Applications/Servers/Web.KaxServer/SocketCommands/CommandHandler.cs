using Drx.Sdk.Network.Legacy.Socket;
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
using Drx.Sdk.Network.Legacy.Socket.Services;
using Drx.Sdk.Network.DataBase;
using Web.KaxServer.Services.Repositorys;

namespace Web.KaxServer.SocketCommands
{
    public static class CommandHandler
    {
        public static void RegisterCommands(this SocketServerBuilder socket)
        {
            // 验证资产，使用方式：-validasset assetId|userName
            socket.RegisterCommand("validasset", async (server, client, args, rawMessage) =>
            {
                if (args.Length < 2 || string.IsNullOrEmpty(args[0]) || string.IsNullOrEmpty(args[1]))
                {
                    // 未知参数
                    await server.SendResponseAsync(client, SocketStatusCode.Error_MissingArguments, CancellationToken.None);
                    return;
                }

                var userData = UserRepository.GetUser(args[1]);
                if (userData == null)
                {
                    await server.SendResponseAsync(client, SocketStatusCode.Failure_UserNotFound, CancellationToken.None);
                    return;
                }

                if (userData.OwnedAssets.TryGetValue(int.Parse(args[0]), out DateTime expiryDate) && expiryDate > DateTime.Now)
                {
                    await server.SendResponseAsync(client, SocketStatusCode.Success_General, CancellationToken.None);
                    return;
                }
                
                await server.SendResponseAsync(client, SocketStatusCode.Failure_AssetInvalid, CancellationToken.None);
            });

            // 验证机器码，使用方式：-validmc userName|assetId|machineCode
            // TODO: 等待重构，这个函数目前拥有一个恶行bug
            socket.RegisterCommand("validmc", async (server, client, args, rawMessage) =>
            {
                var logger = server.Services.GetRequiredService<ILogger<SocketServerService>>();
                var userService = server.Services.GetRequiredService<IUserService>() as UserService;

                if (args.Length < 3 || string.IsNullOrEmpty(args[0]) || string.IsNullOrEmpty(args[1]) || string.IsNullOrEmpty(args[2]))
                {
                    await server.SendResponseAsync(client, SocketStatusCode.Error_MissingArguments, CancellationToken.None);
                    return;
                }

                var userID = args[0];
                if (!int.TryParse(args[1], out int validMcAssetId))
                {
                    await server.SendResponseAsync(client, SocketStatusCode.Error_InvalidArguments, CancellationToken.None);
                    return;
                }
                var machineCodeToValidate = args[2];

                try
                {
                    var userData = UserRepository.GetUser(userID);

                    if (userData == null)
                    {
                        logger.LogWarning("[socket] validmc: User not found in database for '{userName}'.", userID);
                        await server.SendResponseAsync(client, SocketStatusCode.Failure_UserNotFound, CancellationToken.None);
                        return;
                    }

                    // 验证用户是否拥有有效的资产许可证
                    if (userData.OwnedAssets.TryGetValue(validMcAssetId, out DateTime expiryDate) && expiryDate > DateTime.Now)
                    {
                        await server.SendResponseAsync(client, SocketStatusCode.Success_General, CancellationToken.None);
                        // 验证机械码（如果机械码存在）
                        if (userData.McaCodes.TryGetValue(validMcAssetId, out string storedMachineCode) && !string.IsNullOrEmpty(storedMachineCode))
                        {
                            if (storedMachineCode.Equals(machineCodeToValidate, StringComparison.OrdinalIgnoreCase))
                            {
                                await server.SendResponseAsync(client, SocketStatusCode.Success_General, CancellationToken.None);
                                logger.LogInformation("[socket] validmc: Machine code verified for user '{userName}', asset '{assetId}'.", userID, validMcAssetId);
                                return;
                            }
                        }
                        else // 如果机械码不存在
                        {
                            // 为这个资源绑定机械码
                            userData.McaCodes[validMcAssetId] = machineCodeToValidate;
                            UserRepository.SaveUser(userData);
                            await server.SendResponseAsync(client, SocketStatusCode.Success_General, CancellationToken.None);
                            logger.LogInformation("[socket] validmc: Binding new machine code for user '{userName}', asset '{assetId}'.", userID, validMcAssetId);
                            return;
                        }
                    }

                    await server.SendResponseAsync(client, SocketStatusCode.Failure_AssetInvalid, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[socket] Error processing validmc request for user '{userName}'.", userID);
                    await server.SendResponseAsync(client, SocketStatusCode.Error_InternalServerError, CancellationToken.None);
                }
            });

            //  用法: login userName|password|assetId
            socket.RegisterCommand("login", async (server, client, args, rawMessage) =>
            {
                var logger = server.Services.GetRequiredService<ILogger<SocketServerService>>();
                var userData = UserRepository.GetUser(args[0]);

                // 通过用户名查找用户数据
                if (userData != null)
                {
                    if (userData.ClientOnline.TryGetValue(int.Parse(args[2]), out bool isOnline) && isOnline)
                    {
                        await server.SendResponseAsync(client, SocketStatusCode.Failure_UserAlreadyLoggedIn, CancellationToken.None);
                        return;
                    }

                    // 验证密码是否正确
                    if (userData?.Password == args[1])
                    {
                        // 验证资产是否有效
                        if (VaildAsset(int.Parse(args[2]), userData))
                        {
                            userData.ClientTokens[int.Parse(args[2])] = Guid.NewGuid().ToString();
                            userData.ClientOnline[int.Parse(args[2])] = true;
                            UserRepository.SaveUser(userData);

                            await client.PushMap<string>("client", "user_id", userData.UserId.ToString());

                            logger.LogInformation("[socket] login: User '{userName}' logged in with asset '{assetId}', token: {token}", args[0], args[2], userData.ClientTokens[int.Parse(args[2])]);
                            await server.SendResponseAsync(client, SocketStatusCode.Success_General, CancellationToken.None, userData.ClientTokens[int.Parse(args[2])]);
                            return;
                        }
                        else    // 资产无效
                        {
                            logger.LogWarning("[socket] login: Asset '{assetId}' is invalid for user '{userName}'.", args[2], args[0]);
                            await server.SendResponseAsync(client, SocketStatusCode.Failure_AssetInvalid, CancellationToken.None);
                            return;
                        }
                    }
                }

                // 用户不存在
                logger.LogWarning("[socket] login: User not found in database for '{userName}'.", args[0]);
                await server.SendResponseAsync(client, SocketStatusCode.Failure_UserNotFound, CancellationToken.None);
                return;
            });

            // 用法: getusercoins userId
            socket.RegisterCommand("getusercoins", async (server, client, args, rawMessage) =>
            {
                var userData = UserRepository.GetUser(int.Parse(args[0]));
                await server.SendResponseAsync(client, SocketStatusCode.Success_General, CancellationToken.None, userData.Coins);
            });
        }

        private static bool VaildAsset(int assetId, UserData userData)
        {
            if (userData.OwnedAssets.TryGetValue(assetId, out DateTime expiryDate) && expiryDate > DateTime.Now)
            {
                return true;
            }

            return false;
        }
    }
}