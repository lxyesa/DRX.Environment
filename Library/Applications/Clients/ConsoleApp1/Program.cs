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

// 使用 SocketHost 启动服务，并通过添加一个实现了 UDP 钩子的服务来处理 UDP 包
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("开始测试 SqlitePersistence 实现...");
        Console.WriteLine("=" + new string('=', 50));

        SqlitePersistence sqlite = new SqlitePersistence("test.db");
        sqlite.CreateTable("TestTable");
        sqlite.WriteString("TestTable", "key1", "value1");
        sqlite.WriteInt32("TestTable", "key2", 42);
        sqlite.WriteComposite("TestTable", "key3", (c) =>
        {
            c.Add("field1", "data1");
            c.Add("field2", 12345);
            c.Add("field3", true);
            c.Add("field4", new byte[] { 10, 20, 30 });
            return c;
        });
        Console.WriteLine("读取 key1: " + sqlite.ReadString("TestTable", "key1"));
        Console.WriteLine("读取 key2: " + sqlite.ReadInt32("TestTable", "key2"));
        var composite = sqlite.ReadComposite("TestTable", "key3");
        Console.WriteLine("读取 key3.field1: " + composite?.Get<string>("field1"));
        Console.WriteLine("读取 key3.field2: " + composite?.Get<int>("field2"));
        Console.WriteLine("读取 key3.field3: " + composite?.Get<bool>("field3"));
        Console.WriteLine("读取 key3.field4: " + BitConverter.ToString(composite?.Get<byte[]>("field4") ?? Array.Empty<byte>()));
        sqlite.Dump();

        var packet = new PacketBuilder2DSD()
            .Add("success", true)
            .Add("code", 200)
            .Add("message", "操作成功")
            .Add("data", new byte[] { 1, 2, 3, 4, 5 })
            .Add("value", 3.14f)
            .Build();

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
            await client.PacketC2SAsync(packet, resp =>
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
                var deserialized = DrxSerializationData.Deserialize(data);
                deserialized.TryGetBool("success", out var success);
                deserialized.TryGetInt("code", out var code);
                deserialized.TryGetString("message", out var message);

                Console.WriteLine($"Server received data: success={success}, code={code}, message={message}");

                _server.PacketS2C(client, deserialized.Serialize());
            }
            catch { }
            return true;
        }
    }
}
