using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Drx.Sdk.Network.V2.Socket.Handler;

namespace Drx.Sdk.Network.V2.Socket.NET2Cpp;

/// <summary>
/// DrxTcpClient 的 UnmanagedCallersOnly 版本，供 C++/CLI 调用的简单包装。
/// 提供创建/释放/Connect/Close/Send/SendAndReceive 同步接口，以及将托管对象导出为 GCHandle 的方法。
/// 数据传递约定：IntPtr 指向内存块格式为 [int length (4 bytes little-endian)] + [payload bytes]。
/// 返回的缓冲由 Marshal.AllocCoTaskMem 分配，需通过 FreePointer 释放。
/// </summary>
public static class DrxTcpClient2Cpp
{
	private static readonly ConcurrentDictionary<IntPtr, Drx.Sdk.Network.V2.Socket.Client.DrxTcpClient> _clients = new();
	private static long _nextId = 1;

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void NativeClientReceiveCallback(IntPtr clientPtr, IntPtr dataPtr);

	// 存储客户端对应的本地 handler 字典（clientHandle -> (name -> handler)）
	private static readonly ConcurrentDictionary<IntPtr, Dictionary<string, NativeClientHandler>> _nativeHandlers = new();

	private class NativeClientHandler : IClientHandler
	{
		public int Priority { get; }
		public int MaxPacketSize => 0;
		private readonly NativeClientReceiveCallback _cb;
		public NativeClientReceiveCallback Callback => _cb;

		public NativeClientHandler(NativeClientReceiveCallback cb, int priority)
		{
			_cb = cb;
			Priority = priority;
		}

		public bool OnClientReceiveAsync(byte[] data)
		{
			try
			{
				var payload = data ?? Array.Empty<byte>();
				int len = payload.Length;
				IntPtr ptr = Marshal.AllocCoTaskMem(4 + len);
				Marshal.WriteInt32(ptr, len);
				if (len > 0) Marshal.Copy(payload, 0, ptr + 4, len);
				// clientPtr = IntPtr.Zero (no managed client), so pass zero
				_cb(IntPtr.Zero, ptr);
				try { Marshal.FreeCoTaskMem(ptr); } catch { }
				return true;
			}
			catch { return false; }
		}

		public byte[] OnClientSendAsync(byte[] data) => data ?? Array.Empty<byte>();
		public void OnClientConnected() { }
		public void OnClientDisconnected() { }
		public void OnClientDisconnectedCompleted() { }
		public bool OnClientRawReceiveAsync(byte[] rawData, out byte[]? modifiedData) { modifiedData = rawData; return true; }
		public bool OnClientRawSendAsync(byte[] rawData, out byte[]? modifiedData) { modifiedData = rawData; return true; }
	}

	private static byte[] ReadBytesWithLength(IntPtr ptr)
	{
		if (ptr == IntPtr.Zero) return Array.Empty<byte>();
		int len = Marshal.ReadInt32(ptr);
		if (len <= 0) return Array.Empty<byte>();
		var buf = new byte[len];
		Marshal.Copy(ptr + 4, buf, 0, len);
		return buf;
	}

	private static IntPtr AllocBytesWithLength(byte[] data)
	{
		int len = data?.Length ?? 0;
		IntPtr ptr = Marshal.AllocCoTaskMem(4 + len);
		Marshal.WriteInt32(ptr, len);
		if (data != null && len > 0)
		{
			Marshal.Copy(data, 0, ptr + 4, len);
		}
		return ptr;
	}

	private static IntPtr NewHandle() => new IntPtr(System.Threading.Interlocked.Increment(ref _nextId));

	[UnmanagedCallersOnly(EntryPoint = "DrxTcpClient_Create")]
	public static IntPtr Create()
	{
		try
		{
			var c = new Drx.Sdk.Network.V2.Socket.Client.DrxTcpClient();
			var h = NewHandle();
			_clients[h] = c;
			return h;
		}
		catch
		{
			return IntPtr.Zero;
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "DrxTcpClient_Release")]
	public static void Release(IntPtr clientHandle)
	{
		if (clientHandle == IntPtr.Zero) return;
		if (_clients.TryRemove(clientHandle, out var c))
		{
			try { c.Close(); } catch { }
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "DrxTcpClient_Connect")]
	public static int Connect(IntPtr clientHandle, IntPtr ipPtr, int port, int timeout)
	{
		if (clientHandle == IntPtr.Zero) return 0;
		if (!_clients.TryGetValue(clientHandle, out var c)) return 0;
		try
		{
			string ip = Marshal.PtrToStringUTF8(ipPtr) ?? string.Empty;
			if (string.IsNullOrEmpty(ip)) return 0;
			// 同步执行异步连接
			var task = c.ConnectAsync(ip, port, timeout);
			var ok = task.GetAwaiter().GetResult();
			return ok ? 1 : 0;
		}
		catch
		{
			return 0;
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "DrxTcpClient_Close")]
	public static void Close(IntPtr clientHandle)
	{
		if (clientHandle == IntPtr.Zero) return;
		if (!_clients.TryGetValue(clientHandle, out var c)) return;
		try { c.Close(); } catch { }
	}

	[UnmanagedCallersOnly(EntryPoint = "DrxTcpClient_IsConnected")]
	public static int IsConnected(IntPtr clientHandle)
	{
		if (clientHandle == IntPtr.Zero) return 0;
		if (!_clients.TryGetValue(clientHandle, out var c)) return 0;
		try { return c.Connected ? 1 : 0; } catch { return 0; }
	}

	[UnmanagedCallersOnly(EntryPoint = "DrxTcpClient_Send")]
	public static int Send(IntPtr clientHandle, IntPtr dataPtr)
	{
		if (clientHandle == IntPtr.Zero) return 0;
		if (!_clients.TryGetValue(clientHandle, out var c)) return 0;
		try
		{
			var data = ReadBytesWithLength(dataPtr);
			// fire-and-forget 异步发送
			_ = Task.Run(async () =>
			{
				try { await c.PacketC2SAsync(data, null, -1); } catch { }
			});
			return 1;
		}
		catch
		{
			return 0;
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "DrxTcpClient_SendAndReceive")]
	public static IntPtr SendAndReceive(IntPtr clientHandle, IntPtr dataPtr, int timeoutMs)
	{
		if (clientHandle == IntPtr.Zero) return IntPtr.Zero;
		if (!_clients.TryGetValue(clientHandle, out var c)) return IntPtr.Zero;
		try
		{
			var data = ReadBytesWithLength(dataPtr);
			var tcs = new TaskCompletionSource<byte[]>();
			void OnResp(byte[] resp) => tcs.TrySetResult(resp);
			// 使用 PacketC2SAsync 并等待响应或超时
			_ = Task.Run(async () =>
			{
				try
				{
					await c.PacketC2SAsync(data, (r) => OnResp(r), timeoutMs);
				}
				catch (Exception ex)
				{
					try { tcs.TrySetException(ex); } catch { }
				}
			});

			var task = tcs.Task;
			if (timeoutMs >= 0)
			{
				if (!task.Wait(timeoutMs)) return IntPtr.Zero;
			}
			else
			{
				task.Wait();
			}

			if (task.IsCompletedSuccessfully)
			{
				var resp = task.Result ?? Array.Empty<byte>();
				return AllocBytesWithLength(resp);
			}
			return IntPtr.Zero;
		}
		catch
		{
			return IntPtr.Zero;
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "DrxTcpClient_GetManagedHandle")]
	public static IntPtr GetManagedHandle(IntPtr clientHandle)
	{
		if (clientHandle == IntPtr.Zero) return IntPtr.Zero;
		if (!_clients.TryGetValue(clientHandle, out var c)) return IntPtr.Zero;
		try
		{
			var gch = GCHandle.Alloc(c, GCHandleType.Normal);
			return GCHandle.ToIntPtr(gch);
		}
		catch
		{
			return IntPtr.Zero;
		}
	}

	// 注册/注销客户端接收回调（支持 name）
	[UnmanagedCallersOnly(EntryPoint = "DrxTcpClient_RegisterReceiveCallback")]
	public static int RegisterReceiveCallback(IntPtr clientHandle, IntPtr namePtr, IntPtr callbackPtr, int priority)
	{
		if (clientHandle == IntPtr.Zero || callbackPtr == IntPtr.Zero) return 0;
		if (!_clients.TryGetValue(clientHandle, out var c)) return 0;
		try
		{
			var cb = Marshal.GetDelegateForFunctionPointer<NativeClientReceiveCallback>(callbackPtr);
			var h = new NativeClientHandler(cb, priority);
			string name = string.Empty;
			try { name = Marshal.PtrToStringUTF8(namePtr) ?? string.Empty; } catch { name = string.Empty; }
			if (string.IsNullOrEmpty(name)) name = "native" + Guid.NewGuid().ToString("N");
			c.RegisterHandler(name, h);
			var dict = _nativeHandlers.GetOrAdd(clientHandle, _ => new Dictionary<string, NativeClientHandler>());
			lock (dict) { dict[name] = h; }
			return 1;
		}
		catch { return 0; }
	}

	[UnmanagedCallersOnly(EntryPoint = "DrxTcpClient_UnregisterReceiveCallback")]
	public static int UnregisterReceiveCallback(IntPtr clientHandle, IntPtr namePtr, IntPtr callbackPtr)
	{
		if (clientHandle == IntPtr.Zero) return 0;
		if (!_clients.TryGetValue(clientHandle, out var c)) return 0;
		try
		{
			string name = string.Empty;
			try { name = Marshal.PtrToStringUTF8(namePtr) ?? string.Empty; } catch { name = string.Empty; }
			var cb = IntPtr.Zero;
			if (callbackPtr != IntPtr.Zero) cb = callbackPtr;

			if (_nativeHandlers.TryGetValue(clientHandle, out var dict))
			{
				lock (dict)
				{
					if (!string.IsNullOrEmpty(name))
					{
						if (dict.TryGetValue(name, out var h))
						{
							dict.Remove(name);
							try { c.UnregisterHandler(name, h); } catch { }
							return 1;
						}
						return 0;
					}
					if (cb != IntPtr.Zero)
					{
						var cbDel = Marshal.GetDelegateForFunctionPointer<NativeClientReceiveCallback>(cb);
						string? foundKey = null;
						NativeClientHandler? foundHandler = null;
						foreach (var kv in dict)
						{
							var h = kv.Value;
							if (h != null && h.Callback != null && Delegate.Equals(h.Callback, cbDel)) { foundKey = kv.Key; foundHandler = h; break; }
						}
						if (foundKey != null && foundHandler != null)
						{
							dict.Remove(foundKey);
							try { c.UnregisterHandler(foundKey, foundHandler); } catch { }
							return 1;
						}
					}
				}
			}
			return 0;
		}
		catch { return 0; }
	}

	[UnmanagedCallersOnly(EntryPoint = "DrxTcpClient_FreeManagedHandle")]
	public static void FreeManagedHandle(IntPtr gchPtr)
	{
		if (gchPtr == IntPtr.Zero) return;
		try
		{
			var gch = GCHandle.FromIntPtr(gchPtr);
			if (gch.IsAllocated) gch.Free();
		}
		catch { }
	}

	[UnmanagedCallersOnly(EntryPoint = "DrxTcpClient_FreePointer")]
	public static void FreePointer(IntPtr ptr)
	{
		if (ptr == IntPtr.Zero) return;
		try { Marshal.FreeCoTaskMem(ptr); } catch { }
	}
}
