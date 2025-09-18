using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.V2.Socket.Client;

namespace Drx.Sdk.Network.V2.Socket.Server;

/// <summary>
/// UDP 服务器实现（精简版）。
/// 特点：
/// - 继承自 <see cref="NetworkObject"/>，复用处理器管理、队列与 Tick 逻辑；
/// - 接收 UDP 报文，先触发 Raw 接收处理（可修改或拦截），再使用 <see cref="UnpackPacket"/> 解包并入队；
/// - 维护一个远端终端（IPEndPoint）到 <see cref="DrxUdpClient"/> 的映射，方便响应；
/// - 不触发客户端 join/leave（ClientConnected/ClientDisconnected）事件，保持无连接语义。
/// </summary>
public class DrxUdpServer : NetworkObject
{
	// 内部 UdpClient
	private UdpClient? _udp;

	// 接收循环控制
	private CancellationTokenSource? _recvCts;
	private Task? _recvTask;

	// 维护一个 endpoint -> 客户端快照 映射（用于回复），但不视为真实连接，也不触发 join/leave
	// key 使用 IPEndPoint.ToString()
	private readonly Dictionary<string, DrxUdpClient> _endpointClients = new();

	public DrxUdpServer()
	{
		// 将基类的 OnNetworkTick 事件保持兼容（如果上层使用）
		base.OnNetworkTick += (obj) => { try { /* no-op for backward compat */ } catch { } };
	}

	/// <summary>
	/// 启动 UDP 服务器并在指定端口监听。
	/// </summary>
	public void Start(int port)
	{
		Stop();
		_udp = new UdpClient(port);

		// 启动 tick loop
		_recvCts = new CancellationTokenSource();
		_recvTask = Task.Run(() => ReceiveLoopAsync(_recvCts.Token));

		// 启动基类的 TickLoop（如果需要）
		try
		{
			var tickCts = new CancellationTokenSource();
			Task.Run(() => base.TickLoop(tickCts.Token));
		}
		catch { }
	}

	/// <summary>
	/// 异步启动（与 Start 行为相同，为兼容）
	/// </summary>
	public async Task StartAsync(int port)
	{
		Start(port);
		await Task.Yield();
	}

	/// <summary>
	/// 停止服务器并清理资源。
	/// </summary>
	public void Stop()
	{
		try { _recvCts?.Cancel(); } catch { }
		try { _udp?.Close(); } catch { }
		try { _recvTask?.Wait(1000); } catch { }

		lock (_sync)
		{
			_endpointClients.Clear();
		}

		try { base.ShutdownQueue(); } catch { }
	}

	public bool IsRunning => _udp != null;

	/// <summary>
	/// 接收循环：不断接收 Datagram，触发 Raw 处理、解包并入队。
	/// 注意：不会触发连接/断开事件。
	/// </summary>
	private async Task ReceiveLoopAsync(CancellationToken token)
	{
		if (_udp == null) return;
		try
		{
			while (!token.IsCancellationRequested)
			{
				UdpReceiveResult res;
				try
				{
					res = await _udp.ReceiveAsync();
				}
				catch (ObjectDisposedException) { break; }
				catch (SocketException) { break; }
				catch (OperationCanceledException) { break; }
				catch { continue; }

				var remote = res.RemoteEndPoint;
				var raw = res.Buffer ?? Array.Empty<byte>();

				// 创建或复用一个 DrxUdpClient 快照用于传递给 handlers（不代表真实连接）
				DrxUdpClient clientSnapshot;
				var key = remote.ToString();
				lock (_sync)
				{
					if (!_endpointClients.TryGetValue(key, out clientSnapshot))
					{
						clientSnapshot = new DrxUdpClient();
						clientSnapshot.SetTag("RemoteEndPoint", remote);
						try { clientSnapshot.SetTag("LocalEndPoint", (_udp.Client.LocalEndPoint)); } catch { }
						_endpointClients[key] = clientSnapshot;
					}
				}

				// 触发 Raw 接收处理器（允许修改或拦截）
				byte[] processedRaw = raw;
				bool proceed = true;
				lock (_sync)
				{
					foreach (var h in _handlers)
					{
						try
						{
							if (!h.OnServerRawReceiveAsync(processedRaw, clientSnapshot, out var outData))
							{
								proceed = false;
								break;
							}
							if (outData != null) processedRaw = outData;
						}
						catch { }
					}
				}
				if (!proceed) continue;

				// 检查 MaxPacketSize（若某个 handler 限制并超出则丢弃该包）
				bool exceed = false;
				lock (_sync)
				{
					foreach (var h in _handlers)
					{
						try
						{
							if (h.MaxPacketSize > 0 && processedRaw.Length > h.MaxPacketSize)
							{
								exceed = true;
								break;
							}
						}
						catch { }
					}
				}
				if (exceed) continue;

				// 解包为应用层负载并入队，基类队列会调用 OnServerReceiveAsync
				byte[] payload;
				try { payload = base.UnpackPacket(processedRaw); } catch { payload = processedRaw; }

				// 将消息入队（使用 DrxUdpClient 作为 client 标识）
				try { EnqueueMessage(payload, clientSnapshot); } catch { }
			}
		}
		catch { }
	}

	/// <summary>
	/// 将数据（应用层负载）发送到指定客户端（根据 client 中的 RemoteEndPoint 标签）。
	/// 在发送前触发 Raw/Send 处理器，允许修改或拦截。
	/// </summary>
	public bool PacketS2C(DrxUdpClient client, byte[] data)
	{
		if (_udp == null || client == null) return false;

		// 获取远端 Endpoint
	var epObj = client.GetTag("RemoteEndPoint");
		if (epObj == null || epObj is not IPEndPoint remoteEp) return false;

		// 先触发 Raw 发送处理器（使用 DrxUdpClient）
		byte[] rawToSend = data;
		bool proceed = true;
		lock (_sync)
		{
			foreach (var h in _handlers)
			{
				try
				{
					if (!h.OnServerRawSendAsync(rawToSend, client, out var outData))
					{
						proceed = false;
						break;
					}
					if (outData != null) rawToSend = outData;
				}
				catch { }
			}
		}
		if (!proceed) return false;

		// 再触发高层的 OnServerSendAsync（允许修改数据）
		try
		{
			lock (_sync)
			{
				foreach (var h in _handlers)
				{
					try
					{
						var outData = h.OnServerSendAsync(rawToSend, client);
						if (outData != null) rawToSend = outData;
					}
					catch { }
				}
			}
		}
		catch { }

		// 打包并发送
		var packet = base.Packetize(rawToSend);
		try
		{
			_udp.Send(packet, packet.Length, remoteEp);
			return true;
		}
		catch { return false; }
	}

	/// <summary>
	/// 广播到所有已知远端（mapping 中的 endpoint）。
	/// </summary>
	public void PacketS2AllC(byte[] data)
	{
		if (_udp == null) return;
		List<DrxUdpClient> clients;
		lock (_sync) { clients = new List<DrxUdpClient>(_endpointClients.Values); }

		foreach (var c in clients)
		{
			try { PacketS2C(c, data); } catch { }
		}
	}
}
