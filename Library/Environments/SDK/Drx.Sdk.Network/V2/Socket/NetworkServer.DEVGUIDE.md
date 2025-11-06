# NetworkServer.DEVGUIDE.md

## 概述
NetworkServer 提供一个同时支持 TCP 和 UDP 的服务器实现。它封装了 `TcpListener` 与 UDP 原始 `Socket`，对外提供启动/停止、客户端连接事件、接收队列、僵尸连接扫描、以及面向 TCP/UDP 的发送/广播方法。设计目标是作为 SDK 中可重用的服务器骨架，便于在上层实现协议解析与连接管理。

> 位置: `Library/Environments/SDK/Drx.Sdk.Network/V2/Socket/NetworkServer.cs`

## 输入 / 输出 合约
- 输入:
  - 构造: `IPEndPoint localEndPoint`, `enableTcp`, `enableUdp`。
  - StartAsync / Stop 控制服务器生命周期。
  - 发送方法接收数据字节数组和目标 clientId / IPEndPoint。
- 输出:
  - 事件: `OnClientConnected(clientId, remote)`, `OnClientDisconnected(clientId, remote)`, `OnDataReceived(clientId, remote, data)`, `OnError(Exception)`。
  - 通过 `TryDequeue(out ReceivedPacket)` 可拉取内部统一的接收队列以便批量或外部处理。
- 成功判定:
  - StartAsync 后 AcceptLoop / UdpReceiveLoop 启动；客户端连接到达时触发 OnClientConnected。
- 常见错误模式:
  - Socket 绑定失败（端口被占用）-> OnError 报错。
  - 客户端异常导致 ReadAsync 抛出 -> OnError 报错并断开该客户端。

## 公共 API 概览
| 名称 | 签名 | 描述 | 返回 | 错误/异常 |
|---|---:|---|---:|---|
| 构造函数 | `NetworkServer(IPEndPoint localEndPoint, bool enableTcp = true, bool enableUdp = true)` | 创建服务器实例 | — | ArgumentNullException |
| StartAsync | `Task StartAsync()` | 启动监听（TCP）与接收（UDP）循环 | Task | SocketException 等，通过 OnError 报告 |
| Stop | `void Stop()` | 停止所有循环、关闭 Socket、清理客户端状态 | — | — |
| SendToTcpClient | `void SendToTcpClient(string clientId, byte[] data)` | 向指定 TCP 客户端发送数据（异步写入流） | — | KeyNotFoundException 如果 clientId 不存在 |
| BroadcastTcp | `void BroadcastTcp(byte[] data)` | 向所有 TCP 客户端广播 | — | — |
| SendToUdp | `void SendToUdp(IPEndPoint remote, byte[] data)` | 向指定 UDP 端点发送数据（同步） | — | — |
| BroadcastUdp | `void BroadcastUdp(byte[] data)` | 向已记录的所有 UDP 客户端广播数据 | — | — |
| GetTcpClientIds | `IEnumerable<string> GetTcpClientIds()` | 返回当前 TCP 客户端 Id 列表 | IEnumerable<string> | — |
| GetUdpClientKeys | `IEnumerable<string> GetUdpClientKeys()` | 返回已知 UDP 客户端键（endpoint.ToString()） | IEnumerable<string> | — |
| IsTcpClientConnected | `bool IsTcpClientConnected(string clientId)` | 查询指定 TCP 客户端是否仍然 connected | bool | — |
| TryDequeue | `bool TryDequeue(out ReceivedPacket packet)` | 从内部接收队列中取出下一条消息 | bool + out | — |
| Dispose | `void Dispose()` | 停止并释放资源 | — | — |

## 方法详解
### NetworkServer(IPEndPoint localEndPoint, bool enableTcp = true, bool enableUdp = true)
- 参数
  - `localEndPoint` — 本地绑定地址和端口。
  - `enableTcp`、`enableUdp` — 是否启用对应协议。
- 行为
  - 保存配置，延迟启动监听直到调用 `StartAsync()`。

### Task StartAsync()
- 行为
  - 如果启用 TCP: 创建 TcpListener 并启动 Accept 循环 (`AcceptLoopAsync`)。
  - 如果启用 UDP: 创建原始 UDP Socket 并启动接收循环 (`UdpReceiveLoopAsync`)。
  - 启动僵尸扫描任务 (`ZombieScanLoopAsync`) 用于周期性剔除长时间无活动的客户端（受 `InactivityTimeout` 控制）。
- 错误处理
  - 启动过程中如遇异常会通过 `OnError` 传出，StartAsync 本身返回 Task 并完成，但错误以事件通知为主。

### void Stop()
- 行为
  - 取消全局 CancellationTokenSource，停止所有循环，关闭 Listener/Socket，并清理客户端集合。
  - 对已连接的 TCP 客户端会调用 `TryCloseTcpClient` 关闭连接。

### AcceptLoopAsync & HandleNewTcpClientAsync
- AcceptLoop: 循环等待并接受 TcpClient，接收到新连接会交给 `HandleNewTcpClientAsync` 处理。
- HandleNewTcpClientAsync: 为每个 TcpClient 分配唯一 ID，创建状态并放入 `_tcpClients`。使用 `NetworkStream.ReadAsync` 循环读取数据，读取到后把数据封装成 `ReceivedPacket` 入队并触发 `OnDataReceived`。
- 客户端断连或异常时会清理 `_tcpClients` 并触发 `OnClientDisconnected`。

### UdpReceiveLoopAsync
- 使用 `ReceiveMessageFromAsync` 接收 UDP 数据并记录发送端（endpoint）到 `_udpClients`（基于 endpoint.ToString() 作为 key）。
- 接收后同样将 `ReceivedPacket` 入队并触发 `OnDataReceived`。
- 发生 SocketException 或其他异常会通过 `OnError` 通知并短暂延迟后继续循环。

### ZombieScanLoopAsync
- 周期性扫描 TCP/UDP 客户列表，若某个客户端最后活动时间超过 `InactivityTimeout`，则认为僵尸并移除：
  - TCP: 触发 OnError(TimeoutException) -> 移除并关闭连接 -> 触发 OnClientDisconnected
  - UDP: 直接从 `_udpClients` 移除并触发 OnClientDisconnected
- 通过 `ZombieScanInterval` 控制扫描频率。

### 发送 API
- SendToTcpClient: 异步向客户端的 NetworkStream 写入数据（不等待完成），若 clientId 不存在会抛出 KeyNotFoundException。
- BroadcastTcp: 对所有 TCP 客户进行 SendToTcpClient。
- SendToUdp: 调用 `_udpSocket.SendTo` 同步发送。
- BroadcastUdp: 遍历 `_udpClients` 并调用 SendToUdp。
- 注意: TCP 写入使用 `stream.WriteAsync(...).ConfigureAwait(false)` 但没有等待或捕获返回的 Task（fire-and-forget），这可能隐藏写入异常；建议在需要可靠性时改为等待或处理返回 Task 的异常。

## 并发、队列与资源管理
- 内部使用 `ConcurrentDictionary` 保持客户端状态，使用 `ConcurrentQueue<ReceivedPacket>` 作为统一接收队列，适合生产/消费模式。
- Accept/Handle/UDP 接收都在独立 Task 中运行，事件回调可能来自不同线程，订阅者需意识到线程切换。
- Stop/Dispose 会取消 CTS 并尝试关闭所有 socket，但停止流程应保证幂等性。

## 性能与可扩展性建议
- ReceiveBuffer 默认 8KB，可根据协议调整（例如更大或更小）以减少分片或内存浪费。
- 对于高并发 TCP 场景，考虑限制并发读取/写入的并发度，或采用 SocketAsyncEventArgs 池化以减少分配。
- Broadcast 操作在大量客户端时可能耗时，可考虑并行发送或分批发送。

## 常见问题与排查
- 端口绑定失败: 检查是否已有进程占用该端口（Windows: netstat -ano），确认权限。
- 客户端马上断开: 检查客户端与服务器的协议/心跳，或因为 ReadAsync 返回 0 导致断开。
- 写入失败未捕获: 由于 `WriteAsync(...).ConfigureAwait(false)` 未等待结果，写入异常可能仅触发 Task 异常未被处理，建议改为捕获或等待 Task。

## 使用示例（简短）
```csharp
var ep = new IPEndPoint(IPAddress.Loopback, 9000);
using var server = new NetworkServer(ep, enableTcp: true, enableUdp: true);
server.OnClientConnected += (id, remote) => Console.WriteLine($"client {id} connected from {remote}");
server.OnDataReceived += (id, remote, data) => Console.WriteLine($"recv {data.Length} bytes from {id}");
server.OnError += ex => Console.WriteLine("server err:" + ex.Message);
await server.StartAsync();

// 在某处发送
foreach(var id in server.GetTcpClientIds())
{
    server.SendToTcpClient(id, Encoding.UTF8.GetBytes("hello"));
}

// 停止
server.Stop();
```

## 快速验证
- 构建解决方案: 在 `d:\Code` 目录运行 `dotnet build DRX.Environment.sln`。
- 可编写一个短的 Console App（放在 Examples/NetworkServerExample）用于开启服务器并连接一个简单客户端以验证事件流与发送/接收行为。

## 建议的单元测试
- StartAsync/Stop 的生命周期测试（启动后 accept/udp 循环应能运行且 Stop 能清理）
- TCP 客户端连接、发送、断开以及服务器端收到数据并触发 OnClientConnected/OnDataReceived/OnClientDisconnected
- UDP 收发测试（记录 RemoteEndPoint 并能 Broadcast）
- 僵尸扫描测试：模拟最后活动时间超过 InactivityTimeout 的客户端是否被移除并触发断开

## 潜在改进（小而安全的 PR）
- 将 `SendToTcpClient` 内部的 `WriteAsync` 改为显式等待或捕获异常的版本，避免未观察到的 Task 异常。
- 支持配置化 BufferPool（ArrayPool<byte>）以减少 GC 分配。
- 在接收事件触发前提供数据解码或分包钩子以支持上层协议复用。

## 文件位置
- `Library/Environments/SDK/Drx.Sdk.Network/V2/Socket/NetworkServer.cs`

---
文档生成注: 文档中若有对实现行为的假设已用显式描述说明（例如 SendToTcpClient 的 fire-and-forget 行为），请人工确认是否需修改实现以匹配期望语义。