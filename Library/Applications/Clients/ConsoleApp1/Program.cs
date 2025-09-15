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

// 使用 SocketHost 启动服务，并通过添加一个实现了 UDP 钩子的服务来处理 UDP 包
class Program
{
    static async Task Main(string[] args)
    {
        PacketBuilder builder = new PacketBuilder();
        var buf = builder
            .Add("cmd", "echo")
            .Add("id", 123)
            .Add("flag", true)
            .Add("data", new byte[] { 1, 2, 3, 4 })
            .Build();

        builder.Dump();

        // 本地启动 V2 TCP Server 并注册一个回显 Handler
        var server = new DrxTcpServer();
        server.RegisterHandler("echo", new EchoHandler(server));
        server.Start(5000);
        Console.WriteLine("Server started on port 5000");

        // 在后台线程启动客户端并测试连接与回显
        var clientTask = Task.Run(async () =>
        {
            var client = new Drx.Sdk.Network.V2.Socket.Client.DrxTcpClient();
            var connected = await client.ConnectAsync("127.0.0.1", 5000, 3000);
            Console.WriteLine($"Client connected: {connected}");
            if (!connected) return;

            // 发送数据并等待一次性响应
            await client.PacketC2SAsync(buf, resp =>
            {
                try
                {
                    Console.WriteLine("Client received response: " + Encoding.UTF8.GetString(resp));
                }
                catch { }
            }, timeout: 3000);
        });

        await clientTask;

        // 停止服务器
        server.Stop();
        Console.WriteLine("Server stopped");
    }

    // 回显处理器：收到数据就原样发送回去
    class EchoHandler : DefaultServerHandler
    {
        private readonly DrxTcpServer _server;

        public EchoHandler(DrxTcpServer server)
        {
            _server = server;
        }

        public override bool OnServerReceiveAsync(byte[] data, Drx.Sdk.Network.V2.Socket.Client.DrxTcpClient client)
        {
            try
            {
                var builder = new PacketBuilder();
                builder.Add("echoed_data", "abcdefg");
                _server.PacketS2C(client, builder.Build());
            }
            catch { }
            return true;
        }
    }
}
