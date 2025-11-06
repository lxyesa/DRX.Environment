# NetworkClient.DEVGUIDE.md

## 概述
NetworkClient 是一个轻量级的网络客户端实现，支持 TCP 和 UDP 两种传输协议。它封装了底层 System.Net.Sockets.Socket，提供连接管理、同步/异步发送、接收回调事件、超时处理和接收循环等功能。适合作为底层通信组件在 SDK 或示例程序中复用。

> 位置: `Library/Environments/SDK/Drx.Sdk.Network/V2/Socket/NetworkClient.cs`

## 输入 / 输出 合约
- 输入:
  - 构造: `IPEndPoint remoteEndPoint`（目标地址:端口），`ProtocolType`（TCP/UDP，默认 TCP）。
  - 方法参数: byte[] 数据（用于 Send/SendAsync），可选的目标 IPEndPoint（UDP 可选）。
- 输出:
  - 事件回调: `OnConnected`, `OnDisconnected`, `OnDataReceived(byte[] data, EndPoint remote)`, `OnError`。
  - Send/SendAsync 返回 bool 或抛出异常表示失败。
- 成功判定:
  - TCP: `ConnectAsync()` 返回 true 且 `Connected` 为 true。
  - UDP: Connect 同步成功或 Send/SendTo 成功返回 true。
- 常见错误模式:
  - 未连接时调用 TCP Send 或 SendAsync -> 抛出 InvalidOperationException。
  - Socket 操作异常（SocketException） -> 通过 `OnError` 通知并可能返回 false。
  - 连接超时 -> 触发 `OnTimeout` 事件并返回 false（TCP 连接）。

## 公共 API 概览
| 名称 | 签名 | 描述 | 返回 | 错误/异常 |
|---|---:|---|---:|---|
| 构造函数 | `NetworkClient(IPEndPoint remoteEndPoint, ProtocolType protocolType = ProtocolType.Tcp)` | 创建客户端并初始化底层 Socket | — | ArgumentNullException |
| ConnectAsync | `Task<bool> ConnectAsync()` | 异步连接到远端（TCP），UDP 则尝试 Connect 并返回结果 | Task<bool> | SocketException、Timeout -> 返回 false |
| Disconnect | `void Disconnect()` | 断开连接并取消接收 | — | ObjectDisposedException |
| Send | `void Send(byte[] data)` | 同步发送（TCP 使用 Send；UDP 使用 SendTo） | — | InvalidOperationException、SocketException |
| SendAsync | `Task<bool> SendAsync(byte[] data, IPEndPoint? target = null)` | 异步发送（支持 UDP 指定目标） | Task<bool> | InvalidOperationException、返回 false 表示失败 |
| GetRawSocket | `Socket GetRawSocket()` | 获取底层 Socket 用于高级操作 | Socket | ObjectDisposedException |
| GetRemoteEndPoint | `IPEndPoint GetRemoteEndPoint()` | 获取远程 EndPoint | IPEndPoint | — |
| GetProtocolType | `ProtocolType GetProtocolType()` | 获取协议类型 | ProtocolType | — |
| Dispose | `void Dispose()` | 释放资源、关闭 socket、取消接收 | — | — |
| 属性: Connected | `bool Connected` | 表示 socket 是否已连接 | bool | — |
| 属性: Timeout | `float Timeout` | 连接超时（秒），必须 >0 | float | ArgumentOutOfRangeException |

## 方法详解
### NetworkClient(IPEndPoint remoteEndPoint, ProtocolType protocolType = ProtocolType.Tcp)
- 参数
  - `remoteEndPoint` (IPEndPoint) — 目标地址和端口，不能为空。
  - `protocolType` (ProtocolType) — 默认 TCP；若为 UDP 则底层 SocketType 为 Dgram。
- 行为
  - 创建内部 Socket 并以给定协议类型初始化。
- 示例
```csharp
var ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9000);
var client = new NetworkClient(ep, ProtocolType.Tcp);
```
- 注意
  - 若 remoteEndPoint 为 null，会抛出 ArgumentNullException。

### Task<bool> ConnectAsync()
- 参数: 无
- 返回: Task<bool> — true 表示已连接
- 行为
  - 对于 UDP: 尝试同步 Connect 到远端（设置默认目标），异常则返回 false。
  - 对于 TCP: 使用 SocketAsyncEventArgs 发起异步连接，支持超时（Timeout 属性，单位秒）。连接成功后会启动接收循环并触发 `OnConnected`。
- 超时/错误处理
  - 超时时会触发 `OnTimeout` 并返回 false。
  - 任何异常都会被捕获并作为 false 返回（同时在调试输出写出异常）。
- 示例（使用）
```csharp
if (await client.ConnectAsync())
{
    Console.WriteLine("connected");
}
else
{
    Console.WriteLine("connect failed or timeout");
}
```
- 边界条件
  - 已 Dispose 时调用会抛出 ObjectDisposedException。

### void Disconnect()
- 行为
  - 如果已连接则 Shutdown、Cancel 接收、Close socket，并触发 `OnDisconnected`。
- 注意
  - 可能抛出 ObjectDisposedException（如果已释放）。

### void Send(byte[] data)
- 参数: `data` (byte[]) — 要发送的字节数组，不能为空（调用者需校验）。
- 行为
  - UDP: 使用 SendTo 直接发送到构造时的 _remoteEndPoint。
  - TCP: 如果未连接则抛出 InvalidOperationException；否则同步发送。
- 错误处理
  - 发生异常会写入 Debug 并向上抛出。
- 示例
```csharp
client.Send(Encoding.UTF8.GetBytes("hello"));
```

### Task<bool> SendAsync(byte[] data, IPEndPoint? target = null)
- 参数
  - `data` (byte[]) — 要发送的数据
  - `target` (IPEndPoint?) — UDP 可选目标；若为 null 且 UDP 模式下 socket 未 Connect 则抛出 InvalidOperationException
- 行为
  - 异步发送，若失败会通过 `OnError` 通知并返回 false。
- 示例
```csharp
var ok = await client.SendAsync(payload);
if (!ok) Console.WriteLine("send failed");
```
- 边界
  - 在 TCP 模式若未连接就调用会抛出 InvalidOperationException。

### ReceiveLoop (内部实现说明)
- 使用 8KB 缓冲区在单独线程上以阻塞 Receive 执行循环。
- 当读到 0 表示对端关闭，会退出循环并触发 `OnDisconnected`。
- 每次接收后会触发 `OnDataReceived`，传入实际字节数组和 RemoteEndPoint。
- 异常（SocketException / ObjectDisposedException / OperationCanceled）会被捕获并通过 `OnError` 或退出循环处理。
- 注意：该接收循环在 TCP 成功连接后启动；UDP 的发送/接收路径通过 Connect/SendTo 或通过外部 socket 操作实现。

### GetRawSocket / GetRemoteEndPoint / GetProtocolType
- 提供对底层 Socket 与配置信息的访问，可用于特殊操作（例如设置 Socket 选项）。
- 使用者需注意并发与生命周期（不要在 Dispose 之后访问）。

### Dispose()
- 行为
  - 标记 Disposed，取消接收，等待接收任务短时完成，释放底层 Socket，并触发 `OnDisconnected`。
- 建议调用时机
  - 在不再使用客户端或应用退出时使用 `Dispose()` 或 `using` 模式。

## 并发、资源与错误处理
- ReceiveLoop 在独立线程（Task.Run）上运行，所有事件回调可能发生在该线程上，订阅者需自行处理线程切换（例如 UI 更新需切回主线程）。
- Send/SendAsync 并未使用内部发送队列，若在多线程并发调用发送可能要外部加锁。
- Dispose/Disconnect 会尝试取消接收并关闭 socket，但接收循环内部也会在异常或取消时触发 OnDisconnected，订阅者需处理重复断开事件的幂等性。

## 安全与性能建议
- 若需要高并发发送，考虑在外层实现发送队列与批量发送以减少系统调用。
- 对于长连接场景，请合理配置心跳与检测策略（组件本身没有心跳实现）。
- 若用于高吞吐场景，考虑减少 GC 压力（复用缓冲区/ArrayPool）。

## 常见问题与排查
- 连接超时/失败：检查目标地址和防火墙、端口是否被占用；确认 `Timeout` 是否合理（默认 5s）。
- 接收不到数据：确保对端确实发送，并且订阅了 `OnDataReceived`；若是 UDP，检查是否需要先 `Connect` 或使用目标地址发送。
- 多次触发 OnDisconnected：接收循环和 Disconnect/Dispose 都可能触发断开事件，订阅者应保证幂等处理。

## 示例代码（简短）
```csharp
// TCP 客户端示例
var ep = new IPEndPoint(IPAddress.Loopback, 9000);
using var client = new NetworkClient(ep, ProtocolType.Tcp);
client.OnConnected += c => Console.WriteLine("connected");
client.OnDataReceived += (c, data, remote) => Console.WriteLine("recv:" + Encoding.UTF8.GetString(data));
client.OnError += (c, ex) => Console.WriteLine("err:" + ex.Message);
if (await client.ConnectAsync())
{
    await client.SendAsync(Encoding.UTF8.GetBytes("hello"));
}
```

## 快速验证（在仓库上下文）
- 构建解决方案：在 `d:\Code` 运行 `dotnet build DRX.Environment.sln`。
- 在 Examples 中查找或新增小 demo 以验证与服务器互通（建议创建一个小 Console 示例）。

## 建议的单元测试
- ConnectAsync 成功/超时场景（可使用本地 loopback + 伪服务器/测试 TcpListener）。
- UDP SendAsync 指定 target/不指定 target 的行为。
- ReceiveLoop 正常接收、远端关闭、异常时的事件调用顺序与幂等性。

## 下一步建议
- 为 Send 增加异步发送队列（线程安全）或文档化多线程注意事项。
- 添加心跳/保持活动的可选功能以支持长连接场景。
- 将重要 public API 添加 XML 注释并生成文档。

---
文档生成器注: 如果你希望我将该 DEVGUIDE 自动加入到仓库的 README 或创建一个示例项目（Examples/NetworkClientDemo），我可以继续创建。