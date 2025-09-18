using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.V2.Socket.Handler;
using Drx.Sdk.Shared;
using System.Linq;

namespace Drx.Sdk.Network.V2.Socket.Server;

/// <summary>
/// 网络对象基类，包含可复用的处理器管理与 Tick 主循环逻辑。
/// 该类尽量保持与现有代码兼容：处理器注册/注销、线程同步对象、Tick 调度等通用功能放在此处。
/// 注：具体的网络 I/O（如 TCP listener、UDP socket 等）由子类实现。
/// </summary>
public class NetworkObject
{
	// 线程同步对象（保护子类对共享集合的访问）
	// 注：保留名称 _sync 以兼容子类中已有的 lock(_sync) 用法
	protected readonly object _sync = new();

	// 注册的处理器列表（子类与基类共享）
	protected readonly List<IServerHandler> _handlers = new();

	// 消息队列与处理（基类负责管理）：使用 object 作为客户端标识，子类可以传入具体客户端对象
	protected readonly System.Collections.Concurrent.BlockingCollection<(byte[] Payload, object Client)> _messageQueue;
	protected CancellationTokenSource? _queueCts;
	protected Task? _queueTask;
	/// <summary>队列上限，子类可修改，默认 1000</summary>
	public int QueueCapacity { get; set; } = 1000;

	/// <summary>
	/// Tick 速率（每秒触发次数），子类可以在构造前修改或在运行时读取。
	/// </summary>
	public int Tick { get; set; } = 20;

	/// <summary>
	/// 基类的 Tick 委托（传入当前 NetworkObject 实例）
	/// </summary>
	public delegate void NetworkTickHandler(NetworkObject self);

	/// <summary>
	/// OnNetworkTick 事件：每秒按 Tick 次数触发，子类可以在 TickLoop 中调用（或直接使用基类的 TickLoop）。
	/// </summary>
	public event NetworkTickHandler? OnNetworkTick;

	/// <summary>
	/// 基类提供的 Tick 主循环实现，按 Tick 值触发 OnTick 事件。
	/// 子类可直接运行此方法（例如：Task.Run(()=> TickLoop(cts.Token))）。
	/// </summary>
	protected async Task TickLoop(CancellationToken token)
	{
		if (Tick <= 0) return;
		var intervalMs = 1000.0 / Tick;
		var sw = System.Diagnostics.Stopwatch.StartNew();
		long ticks = 0;
		try
		{
			while (!token.IsCancellationRequested)
			{
				var target = (long)Math.Round(ticks * intervalMs);
				var elapsed = sw.ElapsedMilliseconds;
				var delay = target - elapsed;
				if (delay > 0) await Task.Delay((int)delay, token);

				try
				{
					OnNetworkTick?.Invoke(this);
				}
				catch { }

				ticks++;
				await Task.Yield();
			}
		}
		catch (OperationCanceledException) { }
		catch { }
	}

	/// <summary>
	/// 注册一个处理器实例，优先级高的在前（从子类原有实现移植过来）。
	/// </summary>
	/// <typeparam name="T">处理器类型</typeparam>
	/// <param name="args">构造函数参数</param>
	public void RegisterHandler<T>(params object[] args) where T : IServerHandler
	{
		try
		{
			var obj = Activator.CreateInstance(typeof(T), args) as IServerHandler;
			if (obj == null) throw new ArgumentException("Handler must implement IServerHandler");

			lock (_sync)
			{
				var existing = _handlers.FindAll(h => h.GetType() == typeof(T));
				if (existing.Count > 0)
				{
					Logger.Warn($"Handler of type {typeof(T).FullName} is already registered. Replacing the old one.");
					foreach (var e in existing) _handlers.Remove(e);
				}

				int index = _handlers.FindIndex(h => h.GetPriority() < obj.GetPriority());
				if (index >= 0) _handlers.Insert(index, obj);
				else _handlers.Add(obj);
			}
		}
		catch (MissingMethodException ex)
		{
			var constructors = typeof(T).GetConstructors();
			var descriptions = constructors.Select(c => $"({string.Join(", ", c.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
			var message = $"Constructor not found or parameters do not match. Available constructors: {string.Join("; ", descriptions)}";
			throw new ArgumentException(message, ex);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to register handler: " + ex.Message);
			throw;
		}
	}

	/// <summary>
	/// 注销指定类型的处理器实例。
	/// </summary>
	public void UnregisterHandler<T>() where T : IServerHandler
	{
		lock (_sync)
		{
			_handlers.RemoveAll(h => h is T);
		}
	}

	/// <summary>
	/// 获取所有注册的处理器实例。
	/// </summary>
	public List<IServerHandler> GetHandlers()
	{
		lock (_sync) { return new List<IServerHandler>(_handlers); }
	}

	/// <summary>
	/// 获取指定类型的处理器实例，若不存在则返回 null。
	/// </summary>
	public T GetHandler<T>() where T : class, IServerHandler
	{
		lock (_sync) { return _handlers.OfType<T>().FirstOrDefault()!; }
	}

	/// <summary>
	/// 注销所有处理器实例。
	/// </summary>
	public void UnregisterAllHandlers()
	{
		lock (_sync) { _handlers.Clear(); }
	}

	/// <summary>
	/// 基类构造阶段需要初始化消息队列时调用的辅助方法（子类构造器应在设置 QueueCapacity 后调用）。
	/// </summary>

	/// <summary>
	/// 将消息入队，子类在接收到解包后的 payload 应调用该方法，基类会负责序列化调用 handlers。
	/// </summary>
	protected bool EnqueueMessage(byte[] payload, object client)
	{
		try
		{
			// 延迟初始化 BlockingCollection（避免在字段声明时依赖 QueueCapacity 尚未配置的情况）
			if (_messageQueue == null)
			{
				// 使用反射赋值到 readonly 字段不适合，这里通过包装属性模拟：直接 new 并启动队列处理任务
			}
		}
		catch { }
		return TryAddToQueue(payload, client);
	}

	// internal helper to hold queue instance created lazily
	private System.Collections.Concurrent.BlockingCollection<(byte[] Payload, object Client)>? _lazyMessageQueue;

	private bool TryAddToQueue(byte[] payload, object client)
	{
		try
		{
			if (_lazyMessageQueue == null)
			{
				// 初始化
				_lazyMessageQueue = new System.Collections.Concurrent.BlockingCollection<(byte[] Payload, object Client)>(
					new System.Collections.Concurrent.ConcurrentQueue<(byte[] Payload, object Client)>(), QueueCapacity);
				_queueCts = new CancellationTokenSource();
				_queueTask = Task.Run(() => ProcessQueue(_queueCts.Token));
			}
			return _lazyMessageQueue.TryAdd((payload, client));
		}
		catch { return false; }
	}

	/// <summary>
	/// 基类的队列处理循环：按序从队列取出消息并调用 handlers（子类提供的 client 对象会传给 handler）。
	/// </summary>
	protected void ProcessQueue(CancellationToken token)
	{
		var queue = _lazyMessageQueue;
		if (queue == null) return;
		try
		{
			foreach (var item in queue.GetConsumingEnumerable(token))
			{
				try
				{
					lock (_sync)
					{
						// handlers 既支持 DrxTcpClient 也支持 DrxUdpClient（在接口中有重载）
						var tcpClient = item.Client as Drx.Sdk.Network.V2.Socket.Client.DrxTcpClient;
						if (tcpClient != null)
						{
							foreach (var h in _handlers)
							{
								try { h.OnServerReceiveAsync(item.Payload, tcpClient); } catch { }
							}
							continue;
						}

						var udpClient = item.Client as Drx.Sdk.Network.V2.Socket.Client.DrxUdpClient;
						if (udpClient != null)
						{
							foreach (var h in _handlers)
							{
								try { h.OnServerReceiveAsync(item.Payload, udpClient); } catch { }
							}
						}
					}
				}
				catch { }
			}
		}
		catch (OperationCanceledException) { }
		catch { }
	}

	/// <summary>
	/// 关闭并清理消息队列（同步）。供子类在 Stop 时调用。
	/// </summary>
	protected void ShutdownQueue()
	{
		try
		{
			if (_queueCts != null)
			{
				_queueCts.Cancel();
				try { _lazyMessageQueue?.CompleteAdding(); } catch { }
				try { _queueTask?.Wait(2000); } catch { }
			}
		}
		catch { }
	}

	/// <summary>
	/// 关闭并清理消息队列（异步）。供子类在 StopAsync 时调用。
	/// </summary>
	protected async Task ShutdownQueueAsync()
	{
		try
		{
			if (_queueCts != null)
			{
				_queueCts.Cancel();
				try { _lazyMessageQueue?.CompleteAdding(); } catch { }
				if (_queueTask != null) await Task.Run(() => _queueTask.Wait(2000));
			}
		}
		catch { }
	}

	/// <summary>
	/// 将原始负载打包为传输格式（默认使用 V2 Packetizer）。
	/// 子类或平台可重写以使用不同的封包逻辑。
	/// </summary>
	/// <param name="raw">原始要发送的负载</param>
	/// <returns>传输用的数据包</returns>
	protected virtual byte[] Packetize(byte[] raw)
	{
		try { return Drx.Sdk.Network.V2.Socket.Packet.Packetizer.Pack(raw); } catch { return raw; }
	}

	/// <summary>
	/// 将接收到的原始字节解包为应用层负载（默认使用 V2 Packetizer）。
	/// 子类可重写以实现不同的解包行为（例如流式分帧）。
	/// </summary>
	/// <param name="raw">从传输层接收到的原始字节</param>
	/// <returns>解包后的应用层负载</returns>
	protected virtual byte[] UnpackPacket(byte[] raw)
	{
		try { return Drx.Sdk.Network.V2.Socket.Packet.Packetizer.Unpack(raw); } catch { return raw; }
	}
}
