using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.Socket;
using Drx.Sdk.Network.Socket.Hosting;
using Drx.Sdk.Network.Socket.Services;
using Drx.Sdk.Network.V2.Socket.Packet;
using Drx.Sdk.Network.V2.Socket.Server;
using Drx.Sdk.Network.V2.Socket.Client;
using Drx.Sdk.Network.V2.Socket.Handler;
using Drx.Sdk.Network.V2.Persistence.Sqlite.Test;
using Drx.Sdk.Network.V2.Persistence.Sqlite;
using Drx.Sdk.Shared.Serialization;
using DrxUdpClient = Drx.Sdk.Network.V2.Socket.Client.DrxUdpClient;
using DrxTcpClient = Drx.Sdk.Network.V2.Socket.Client.DrxTcpClient;
using Drx.Sdk.Shared;

class Program
{
    static void Main(string[] args)
    {
        int port = 8462;
        var text = new Text(port.ToString()).SetColor(ConsoleColor.Red);

        // 记录for开始时间
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 10000000; i++)
        {
            Logger.Info($"KaxSocket server started on port {text}");
        }

        // 记录for循环执行了多少秒
        Logger.Info("完成1千万次日志记录");
        stopwatch.Stop();
        Logger.Info($"耗时: {stopwatch.ElapsedMilliseconds} 毫秒");

        Console.ReadKey();
    }
}
