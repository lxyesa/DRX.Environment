using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DRX.Framework;
using Microsoft.Extensions.Logging;

namespace Drx.Sdk.Network.Socket.Services
{
    /// <summary>
    /// A built-in socket service responsible for parsing and executing registered commands.
    /// </summary>
    public class CommandHandlingService : SocketServiceBase
    {
        private readonly ILogger<CommandHandlingService> _logger;
        private readonly Dictionary<string, CommandHandler> _commandHandlers;

        public CommandHandlingService(ILogger<CommandHandlingService> logger, Dictionary<string, CommandHandler> commandHandlers)
        {
            _logger = logger;
            _commandHandlers = commandHandlers;
        }

        public override async Task OnServerReceiveAsync(SocketServerService server, DrxTcpClient client, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            string rawMessage = Encoding.UTF8.GetString(data.Span);
            // 判断是否为JSON格式命令
            if (string.IsNullOrWhiteSpace(rawMessage) || !rawMessage.TrimStart().StartsWith("{"))
            {
                Logger.Warn($"[socket] Received non-JSON command from {client.Client.RemoteEndPoint}: {rawMessage}");
                return; // 非JSON命令，忽略
            }

            try
            {
                var json = System.Text.Json.JsonDocument.Parse(rawMessage);
                if (!json.RootElement.TryGetProperty("command", out var commandProp))
                {
                    return; // 没有command字段，不处理
                }
                var command = commandProp.GetString()?.ToLower();
                if (string.IsNullOrWhiteSpace(command))
                {
                    return;
                }

                // 解析参数
                List<string> argsList = new List<string>();
                if (json.RootElement.TryGetProperty("args", out var argsProp) && argsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in argsProp.EnumerateArray())
                    {
                        // 支持基本类型和对象，统一转为字符串
                        switch (item.ValueKind)
                        {
                            case System.Text.Json.JsonValueKind.String:
                                var str = item.GetString();
                                argsList.Add(str ?? string.Empty);
                                break;
                            case System.Text.Json.JsonValueKind.Number:
                                argsList.Add(item.GetRawText());
                                break;
                            case System.Text.Json.JsonValueKind.True:
                            case System.Text.Json.JsonValueKind.False:
                                argsList.Add(item.GetRawText());
                                break;
                            default:
                                // 对象或数组，序列化为json字符串
                                argsList.Add(item.GetRawText());
                                break;
                        }
                    }
                }
                var args = argsList.ToArray();

                if (_commandHandlers.TryGetValue(command, out var handler))
                {
                    try
                    {
                        await handler(server, client, args, rawMessage);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occurred while executing command '{command}' for client {clientEndpoint}.", command, client.Client.RemoteEndPoint);
                        await server.SendResponseAsync(client, SocketStatusCode.Error_InternalServerError, cancellationToken, "Command execution failed.");
                    }
                }
                else
                {
                    _logger.LogWarning("Unknown command '{command}' received from {clientEndpoint}.", command, client.Client.RemoteEndPoint);
                    var resp = new {
                        command = command,
                        message = $"Unknown command: {command}",
                        state_code = (int)SocketStatusCode.Error_UnknownCommand
                    };
                    await server.SendResponseAsync(client, SocketStatusCode.Error_UnknownCommand, cancellationToken, resp);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // 非法JSON，忽略
                return;
            }
        }
    }
} 