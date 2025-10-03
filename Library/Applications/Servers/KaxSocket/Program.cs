
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Drx.Sdk.Network.V2.Socket;
using Drx.Sdk.Shared.Serialization;

public class Program
{
	// 简单测试：启动 NetworkServer（TCP+UDP），然后用 NetworkClient 发起 TCP 和 UDP 消息
	public static async Task Main(string[] args)
	{
		var localEp = new IPEndPoint(IPAddress.Loopback, 12345);

		var server = new NetworkServer(localEp, enableTcp: true, enableUdp: true);

		server.OnClientConnected += (id, ep) => Console.WriteLine($"Client connected: {id} @ {ep}");
		server.OnClientDisconnected += (id, ep) => Console.WriteLine($"Client disconnected: {id} @ {ep}");
		server.OnDataReceived += OnDataReceived;
		server.OnError += (ex) => Console.WriteLine($"Server error: {ex}");

		await server.StartAsync();

		// 等待服务就绪
		await Task.Delay(200);

		// TCP 客户端测试
		var tcpClient = new NetworkClient(new IPEndPoint(IPAddress.Loopback, 12345), System.Net.Sockets.ProtocolType.Tcp);
		var ok = await tcpClient.ConnectAsync();
		Console.WriteLine($"TCP connected: {ok}");
		if (ok)
		{
			var msg = Encoding.UTF8.GetBytes("Hello from TCP client");
			tcpClient.Send(msg);
		}

		// UDP 客户端测试（不需要 Connect）
		var udpClient = new NetworkClient(new IPEndPoint(IPAddress.Loopback, 12345), System.Net.Sockets.ProtocolType.Udp);
		Console.WriteLine($"UDP client send...");

		DrxSerializationData data = new DrxSerializationData
        {
            { "key1", 18446744073709551615 },
            { "key2", "Hello from UDP client" },
            { "key3", new int[] { 1, 2, 3, 4, 5 } }
        };

		var serialized = data.Serialize();

		udpClient.Send(serialized);

		// 等待接收输出
		await Task.Delay(500000);

		// 清理
		tcpClient.Dispose();
		udpClient.Dispose();

		server.Stop();
	}

	private static void OnDataReceived(string clientId, IPEndPoint remote, byte[] data)
	{
		var deserialized = DrxSerializationData.Deserialize(data);
		deserialized.TryGet("key1", out var key1);
		deserialized.TryGet("key2", out var key2);
		deserialized.TryGet("key3", out var key3);

		Console.WriteLine($"Data from {clientId} @ {remote}: key1={key1.As<ulong>()}, key2={key2.As<string>()}, key3=[{string.Join(",", key3.As<int[]>() ?? new int[0])}]");
	}
}