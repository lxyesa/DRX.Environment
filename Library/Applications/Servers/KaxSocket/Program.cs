using System.Threading.Tasks;
using Drx.Sdk.Network.V2.Socket.Client;
using Drx.Sdk.Network.V2.Socket.Handler;
using Drx.Sdk.Network.V2.Socket.Packet;
using Drx.Sdk.Network.V2.Socket.Server;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Serialization;
using KaxSocket.Command;

public class Program
{
    private static DrxTcpServer? _tcpServer;

    public static async Task Main(string[] args)
    {
        if (!Initialize())
        {
            Logger.Error("KaxSocket initialization failed");
            return;
        }

        int port = 8462; // 默认端口
        if (_tcpServer != null)
        {
            await _tcpServer.StartAsync(port);
            Logger.Info($"{new Text("KaxSocket server started on port").SetColor(ConsoleColor.Green).SetBold()}：{new Text(port.ToString()).SetColor(ConsoleColor.Blue).SetBold()}");
            Logger.Info("Input '/stop' to stop the server.");
            Logger.Info("If you don’t want to lose your user data (or anything else you care about), use '/stop' to shut down the server properly — not by freaking clicking the damn '×' button.");

            // ----------------------------------------
            DebugMethod().Wait();   // 连接调试
            // ----------------------------------------

            while (true)
            {
                var input = Console.ReadLine();
                if (input != null && input.Trim().Equals("/stop", StringComparison.OrdinalIgnoreCase))
                {
                    await _tcpServer.StopAsync();
                    Logger.Info("The server has gone to sleep. Don't wake it unless you bring snacks.");
                    Console.ReadKey();
                    break;
                }
            }
        }
    }

    public static bool Initialize()
    {
        try
        {
            if (_tcpServer != null)
            {
                Logger.Error("KaxSocket already initialized");
                throw new InvalidOperationException("KaxSocket already initialized");
            }
            _tcpServer = new DrxTcpServer();
            _tcpServer.RegisterHandler<CommandHandlerServer>(_tcpServer);
            _tcpServer.ClientConnected += OnClientConnected;
            _tcpServer.ClientDisconnected += OnClientDisconnected;
            CommandHandler.registerAllCommands(_tcpServer);

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"KaxSocket initialization failed: {ex.Message}");
            return false;
        }
    }

    private static async Task DebugMethod()
    {
        var client = new DrxTcpClient();
        await client.ConnectAsync("127.0.0.1", 8462);
    }

    private static void OnClientDisconnected(DrxTcpClient client)
    {
        Logger.Info($"Client disconnected: {client.Client.RemoteEndPoint}");
    }

    private static void OnClientConnected(DrxTcpClient client)
    {
        Logger.Info($"Client connected: {client.Client.RemoteEndPoint}");
    }
}
