using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        public override async Task OnServerReceiveAsync(SocketServerService server, TcpClient client, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            string rawMessage = Encoding.UTF8.GetString(data.Span);

            if (!rawMessage.StartsWith("-"))
            {
                return; // Not a command, ignore.
            }

            _logger.LogInformation("Processing command from {clientEndpoint}: {message}", client.Client.RemoteEndPoint, rawMessage);

            var commandAndArgs = rawMessage.Substring(1).Split(new[] { ' ' }, 2);
            var command = commandAndArgs[0].ToLower();
            var argsString = commandAndArgs.Length > 1 ? commandAndArgs[1] : string.Empty;

            string[] args = argsString.Split('|');

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
                await server.SendResponseAsync(client, SocketStatusCode.Error_UnknownCommand, cancellationToken, command);
            }
        }
    }
} 