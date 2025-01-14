# PacketAPI 文档

## PacketTypes 类

`PacketTypes` 类定义了网络数据包的各种类型常量，用于在数据通信中标识不同类型的包。

### 常量

- **Command**
  - **类型**: `string`
  - **值**: `"command"`
  - **描述**: 表示一个命令类型的数据包。

- **CommandResponse**
  - **类型**: `string`
  - **值**: `"command_response"`
  - **描述**: 表示一个命令响应类型的数据包。

- **Response**
  - **类型**: `string`
  - **值**: `"response"`
  - **描述**: 表示一个通用响应类型的数据包。

- **Message**
  - **类型**: `string`
  - **值**: `"message"`
  - **描述**: 表示一个消息类型的数据包。

## PacketBodyKey 类

`PacketBodyKey` 类定义了数据包主体中常用的键值，便于在处理数据包时统一使用这些键。

### 静态只读字段

#### 命令类型的数据包键值

- **Command**
  - **类型**: `string`
  - **值**: `"command"`
  - **描述**: 用于标识命令的键。

- **CommandArgs**
  - **类型**: `string`
  - **值**: `"command_args"`
  - **描述**: 用于传递命令参数的键。

- **CommandResponse**
  - **类型**: `string`
  - **值**: `"command_result"`
  - **描述**: 用于传递命令执行结果的键。

#### 其他类型的数据包键值

- **Message**
  - **类型**: `string`
  - **值**: `"message"`
  - **描述**: 用于传递消息内容的键。

- **Response**
  - **类型**: `string`
  - **值**: `"response"`
  - **描述**: 用于传递通用响应内容的键。


# DRXServer 类 API 文档

`DRXServer` 是一个抽象服务器类，负责管理客户端连接、事件处理和数据传输。它提供了基础的服务器功能，允许子类扩展和定制特定的行为。

## 类定义

## 构造函数

### `DRXServer()`

初始化 `DRXServer` 的新实例。创建服务器Socket并初始化消息队列。

### `DRXServer(int maxChannels, int maxQueueSize, int defaultDelay)`

使用指定参数初始化 `DRXServer` 的新实例。

- **参数:**
  - `maxChannels` (`int`): 最大通道数。
  - `maxQueueSize` (`int`): 最大队列大小。
  - `defaultDelay` (`int`): 默认延迟时间（毫秒）。

## 事件

### `OnError`

当发生错误时触发。

- **类型:** `EventHandler<NetworkEventArgs>?`

### `OnServerStarted`

服务器启动时触发。

- **类型:** `EventHandler<NetworkEventArgs>?`

### `OnClientConnected`

客户端连接时触发。

- **类型:** `EventHandler<NetworkEventArgs>?`

### `OnClientDisconnected`

客户端断开连接时触发。

- **类型:** `EventHandler<NetworkEventArgs>?`

### `OnDataReceived`

接收到数据时触发。

- **类型:** `EventHandler<NetworkEventArgs>?`

### `OnVerifyClient`

验证客户端时触发。

- **类型:** `EventHandler<NetworkEventArgs>?`

### `OnDataSent`

数据发送完成时触发。

- **类型:** `EventHandler<NetworkEventArgs>?`

### `OnCommandExecuted`

命令执行完成时触发。

- **类型:** `EventHandler<NetworkEventArgs>?`

## 公共方法

### `void Start(string ip, int port)`

启动服务器。

- **参数:**
  - `ip` (`string`): 服务器IP地址。
  - `port` (`int`): 服务器端口。

### `void Stop()`

停止服务器，关闭所有客户端连接并释放资源。

### `bool DisconnectClient(DRXSocket clientSocket)`

断开指定客户端的连接。

- **参数:**
  - `clientSocket` (`DRXSocket`): 要断开的客户端Socket对象。

- **返回值:**
  - `bool`: 如果断开成功返回 `true`，否则返回 `false`。

### `HashSet<DRXSocket> GetConnectedSockets()`

获取所有已连接的客户端Socket。

- **返回值:**
  - `HashSet<DRXSocket>`: 已连接的客户端Socket集合。

### `DRXSocket? GetClientByUID(string uid)`

根据UID获取客户端Socket。

- **参数:**
  - `uid` (`string`): 客户端UID。

- **返回值:**
  - `DRXSocket?`: 对应的客户端Socket，如果未找到则返回 `null`。

### `async Task<byte[]> SendAsync<T>(DRXSocket clientSocket, T packet, string key, int timeout = 0) where T : BasePacket`

发送带请求ID的数据包并等待响应。

- **类型参数:**
  - `T`: 数据包类型，必须继承自 `BasePacket`。

- **参数:**
  - `clientSocket` (`DRXSocket`): 客户端Socket对象。
  - `packet` (`T`): 发送的数据包。
  - `key` (`string`): 加密密钥。
  - `timeout` (`int`, 可选): 等待超时时间（毫秒），默认为 `0` 表示无超时。

- **返回值:**
  - `Task<byte[]>`: 客户端响应的数据包字节数组。

### `async Task BroadcastAsync(NetworkPacket packet)`

向所有已连接的客户端广播数据（不使用数据包校验系统）。

- **参数:**
  - `packet` (`NetworkPacket`): 网络数据包。

### `async Task BroadcastAsync(NetworkPacket packet, string key)`

向所有已连接的客户端广播数据，使用数据包校验系统。

- **参数:**
  - `packet` (`NetworkPacket`): 网络数据包。
  - `key` (`string`): 加密密钥。

## 受保护的方法

### `void BeginCheckBanndedClient()`

开始检查被封禁的客户端。

### `void CheckBanndedClient()`

检查被封禁的客户端，解封或断开连接。

### `async void AcceptCallback(IAsyncResult ar)`

处理客户端连接的回调。

- **参数:**
  - `ar` (`IAsyncResult`): 异步操作结果。

### `DRXSocket? AcceptClientSocket(IAsyncResult ar)`

接受客户端Socket连接。

- **参数:**
  - `ar` (`IAsyncResult`): 异步操作结果。

- **返回值:**
  - `DRXSocket?`: 转换后的 `DRXSocket` 对象，如果失败返回 `null`。

### `async Task HandleNewClientAsync(DRXSocket clientSocket)`

处理新的客户端连接。

- **参数:**
  - `clientSocket` (`DRXSocket`): 客户端Socket对象。

### `async Task InitializeClientSocket(DRXSocket clientSocket)`

初始化客户端Socket。

- **参数:**
  - `clientSocket` (`DRXSocket`): 客户端Socket对象。

### `async Task HandleAcceptErrorAsync(DRXSocket? clientSocket, Exception ex)`

处理接受连接时的错误。

- **参数:**
  - `clientSocket` (`DRXSocket?`): 客户端Socket对象。
  - `ex` (`Exception`): 异常信息。

### `void ContinueAccepting()`

继续接受新的客户端连接。

### `async Task HandleDisconnectAsync(DRXSocket clientSocket)`

处理客户端断开连接。

- **参数:**
  - `clientSocket` (`DRXSocket`): 断开连接的客户端Socket。

### `async Task HandleClientDisconnection(DRXSocket clientSocket)`

处理客户端断开连接的具体逻辑。

- **参数:**
  - `clientSocket` (`DRXSocket`): 断开连接的客户端Socket。

### `async Task HandleDisconnectErrorAsync(DRXSocket clientSocket, Exception ex)`

处理断开连接时的错误。

- **参数:**
  - `clientSocket` (`DRXSocket`): 客户端Socket对象。
  - `ex` (`Exception`): 异常信息。

### `void CloseSocketSafely(Socket socket)`

安全关闭Socket连接。

- **参数:**
  - `socket` (`Socket`): 要关闭的Socket对象。

### `void BeginVerifyClient()`

启动客户端验证任务。

### `async void VerifyClientHeartBeat(object? sender, NetworkEventArgs args)`

验证客户端心跳包。

- **参数:**
  - `sender` (`object?`): 事件发送者。
  - `args` (`NetworkEventArgs`): 网络事件参数。

### `virtual void VerifyClientTask()`

允许子类重写以实现自定义的客户端验证逻辑。

### `void BeginReceiveCommand()`

开始接收命令。

### `void HandleCommandPacket(DRXPacket packet, DRXSocket? socket)`

处理命令数据包。

- **参数:**
  - `packet` (`DRXPacket`): 数据包。
  - `socket` (`DRXSocket?`): 客户端Socket对象。

### `void ExecuteCommandAndRespond(string command, object[] commandArgs, DRXSocket? socket, DRXPacket orgPacket)`

执行命令并响应。

- **参数:**
  - `command` (`string`): 命令。
  - `commandArgs` (`object[]`): 命令参数。
  - `socket` (`DRXSocket?`): 客户端Socket对象。
  - `orgPacket` (`DRXPacket`): 原始数据包。

### `void BeginReceive(DRXSocket clientSocket)`

开始接收数据。

- **参数:**
  - `clientSocket` (`DRXSocket`): 客户端Socket对象。

### `void HandleDataReceived(IAsyncResult ar, DRXSocket clientSocket, byte[] buffer)`

处理接收到的数据。

- **参数:**
  - `ar` (`IAsyncResult`): 异步操作结果。
  - `clientSocket` (`DRXSocket`): 客户端Socket对象。
  - `buffer` (`byte[]`): 数据缓冲区。

### `void ProcessReceivedData(DRXSocket clientSocket, byte[] data)`

处理接收到的数据包。

- **参数:**
  - `clientSocket` (`DRXSocket`): 客户端Socket对象。
  - `data` (`byte[]`): 接收到的数据。

### `byte[] PrepareDataForSend(byte[] data)`

预处理待发送的数据。

- **参数:**
  - `data` (`byte[]`): 发送的数据。

- **返回值:**
  - `byte[]`: 处理后的数据缓冲区。

### `AsyncCallback CreateSendCallback(byte[] buffer)`

创建发送回调。

- **参数:**
  - `buffer` (`byte[]`): 发送缓冲区。

- **返回值:**
  - `AsyncCallback`: 发送完成的回调。

### `bool ValidateClientForSend(DRXSocket clientSocket)`

验证客户端连接状态。

- **参数:**
  - `clientSocket` (`DRXSocket`): 客户端Socket对象。

- **返回值:**
  - `bool`: 如果客户端有效则返回 `true`，否则返回 `false`。

### `void ExecuteSend(DRXSocket clientSocket, byte[] buffer, int length)`

执行实际的数据发送。

- **参数:**
  - `clientSocket` (`DRXSocket`): 客户端Socket对象。
  - `buffer` (`byte[]`): 发送缓冲区。
  - `length` (`int`): 发送的数据长度。

### `void HandleSendCallback(IAsyncResult ar)`

处理发送完成的回调。

- **参数:**
  - `ar` (`IAsyncResult`): 异步操作结果。

### `void OnSendComplete(DRXSocket clientSocket, int bytesSent)`

发送完成时触发。

- **参数:**
  - `clientSocket` (`DRXSocket`): 客户端Socket对象。
  - `bytesSent` (`int`): 发送的字节数。

### `void OnSendError(DRXSocket clientSocket, Exception ex)`

发送错误时触发。

- **参数:**
  - `clientSocket` (`DRXSocket`): 客户端Socket对象。
  - `ex` (`Exception`): 异常信息。

### `void Send(DRXSocket clientSocket, NetworkPacket packet, string key)`

向指定客户端发送数据（已弃用，请使用基于 `SendAndWaitAsync` 的方法）。

- **参数:**
  - `clientSocket` (`DRXSocket`): 客户端Socket对象。
  - `packet` (`NetworkPacket`): 网络数据包。
  - `key` (`string`): 加密密钥。

### `void Send<T>(DRXSocket clientSocket, T packet, string key) where T : BasePacket`

向指定客户端发送泛型数据包（已弃用，请使用基于 `SendAndWaitAsync` 的方法）。

- **类型参数:**
  - `T`: 数据包类型，必须继承自 `BasePacket`。

- **参数:**
  - `clientSocket` (`DRXSocket`): 客户端Socket对象。
  - `packet` (`T`): 数据包对象。
  - `key` (`string`): 加密密钥。

### `void Send<T>(DRXSocket clientSocket, T originPacket, T responsePacket, string key) where T : BasePacket`

发送带请求ID的响应数据包（已弃用，请使用基于 `SendAndWaitAsync` 的方法）。

- **类型参数:**
  - `T`: 数据包类型，必须继承自 `BasePacket`。

- **参数:**
  - `clientSocket` (`DRXSocket`): 客户端Socket对象。
  - `originPacket` (`T`): 原始请求的数据包。
  - `responsePacket` (`T`): 响应的数据包。
  - `key` (`string`): 加密密钥。

## 保护的方法

### `void Initialize()`

初始化服务器组件和事件订阅。

### `void HandleStartupError(Exception ex)`

处理服务器启动时的错误。

- **参数:**
  - `ex` (`Exception`): 异常信息。

### `void HandleStopError(Exception ex)`

处理服务器停止时的错误。

- **参数:**
  - `ex` (`Exception`): 异常信息。

### `void NotifyServerStarted()`

通知服务器已启动，触发 `OnServerStarted` 事件。

## 工具方法

### `IEnumerable<QueueItemInfo> GetQueueStatus()`

获取当前消息队列的状态。

- **返回值:**
  - `IEnumerable<QueueItemInfo>`: 队列项信息集合。

### `void CancelItem(Guid itemId)`

取消指定的队列项。

- **参数:**
  - `itemId` (`Guid`): 队列项的唯一标识符。

### `void OnDestroy()`

销毁时停止服务器。

## 摘要

`DRXServer` 提供了一个全面的服务器管理框架，包括客户端连接管理、数据发送与接收、命令处理、客户端验证及广播功能。通过扩展此类，可以实现定制化的服务器行为，以满足特定的应用需求。

# DRXClient 类 API 文档

`DRXClient` 是一个用于连接和通信服务器的网络客户端类。它继承自 `DRXBehaviour`，提供了连接管理、数据发送与接收以及错误处理等功能。

## 类定义

public class DRXClient : DRXBehaviour


## 构造函数

### `DRXClient(string serverIP, int serverPort, string key)`

初始化 `DRXClient` 的新实例。

- **参数:**
  - `serverIP` (`string`): 服务器的IP地址。
  - `serverPort` (`int`): 服务器的端口号。
  - `key` (`string`): 用于加密的密钥。

## 事件

### `OnReceiveCallback`

- **描述:** 在接收到数据时触发的事件。
- **类型:** `EventHandler<NetworkEventArgs>?`

### `OnErrorCallback`

- **描述:** 在发生错误时触发的事件。
- **类型:** `EventHandler<NetworkEventArgs>?`

### `OnConnectedCallback`

- **描述:** 在成功连接到服务器时触发的事件。
- **类型:** `EventHandler<NetworkEventArgs>?`

### `OnDisconnectedCallback`

- **描述:** 在断开与服务器的连接时触发的事件。
- **类型:** `EventHandler<NetworkEventArgs>?`

### `OnDataSentCallback`

- **描述:** 在数据发送完成时触发的事件。
- **类型:** `EventHandler<NetworkEventArgs>?`

## 属性

### `IsConnected`

- **描述:** 获取客户端的连接状态。
- **类型:** `bool`
- **访问修饰符:** `public`

## 方法

### 连接方法

#### `Connect()`

- **描述:** 连接到服务器。
- **访问修饰符:** `public virtual void`

#### `ConnectCallback(IAsyncResult ar)`

- **描述:** 处理连接回调。
- **参数:**
  - `ar` (`IAsyncResult`): 异步操作结果。
- **访问修饰符:** `protected virtual void`

### 发送方法

#### `SendAsync<T>(T packet, int timeout = 0)`

- **描述:** 发送基于 `BasePacket` 的数据包，并等待服务器的响应。
- **类型参数:**
  - `T`: 数据包类型，必须继承自 `BasePacket`。
- **参数:**
  - `packet` (`T`): 要发送的数据包。
  - `timeout` (`int`, 可选): 等待响应的超时时间（毫秒）。默认为 `0` 表示无超时。
- **返回值:**
  - `Task<byte[]>`: 服务器响应的数据包字节数组。
- **访问修饰符:** `public virtual async Task<byte[]>`
- **说明:** 
  - 该方法会为每个请求生成一个唯一的 `requestID`，并将其添加到数据包的头部。
  - 使用 `TaskCompletionSource<byte[]>` 来等待服务器的响应。
  - 支持通过 `timeout` 参数设置超时时间，超时后会抛出 `TimeoutException`。

#### `SendCallback(IAsyncResult ar)`

- **描述:** 处理发送数据的回调。
- **参数:**
  - `ar` (`IAsyncResult`): 异步操作结果。
- **访问修饰符:** `protected virtual void`

### 接收方法

#### `BeginReceive()`

- **描述:** 开始接收来自服务器的数据。
- **访问修饰符:** `protected virtual void`

#### `ReceiveCallback(IAsyncResult ar)`

- **描述:** 处理接收到的数据回调。
- **参数:**
  - `ar` (`IAsyncResult`): 异步操作结果。
- **访问修饰符:** `protected virtual void`

### 断开连接方法

#### `HandleDisconnect()`

- **描述:** 处理与服务器断开连接的逻辑。
- **访问修饰符:** `protected virtual void`
- **说明:** 
  - 更新连接状态。
  - 触发 `OnDisconnectedCallback` 事件。
  - 关闭 Socket 连接。

#### `Disconnect()`

- **描述:** 主动断开与服务器的连接。
- **访问修饰符:** `public virtual void`

## 使用示例

using NetworkCoreStandard.Common; using NetworkCoreStandard.Common.Enums.Packet;
// 初始化客户端 var client = new DRXClient("127.0.0.1", 8463, "your-encryption-key");
// 订阅事件 client.OnConnectedCallback += (sender, e) => Console.WriteLine("已连接到服务器"); client.OnReceiveCallback += (sender, e) => Console.WriteLine("接收到数据"); client.OnErrorCallback += (sender, e) => Console.WriteLine($"错误: {e.Message}"); client.OnDisconnectedCallback += (sender, e) => Console.WriteLine("已断开与服务器的连接");
// 连接到服务器 client.Connect();
// 构建数据包 var packet = new DRXPacket() { Headers = new PacketObject { { "type", PacketTypes.Command }, }, Body = new PacketObject { { "command", "test" }, { "args", new object[] { "testArg" } } } };
// 发送数据包并等待响应 try { var responseBytes = await client.SendAsync(packet, 5000); // 等待5秒 if (responseBytes != null) { var responsePacket = BasePacket.Unpack(responseBytes, "your-encryption-key"); Console.WriteLine($"响应: {responsePacket?.Body[PacketBodyKey.CommandResponse]}"); } } catch (TimeoutException) { Console.WriteLine("等待响应超时"); } catch (Exception ex) { Console.WriteLine($"发送数据包时发生错误: {ex.Message}"); }
// 主线程阻塞，防止程序提前退出 Console.ReadLine();
// 断开连接 client.Disconnect();


## 摘要

`DRXClient` 提供了一个全面的网络客户端实现，支持与服务器的异步连接、数据发送与接收。通过事件机制，用户可以轻松处理连接、接收数据、发送数据及错误等各种网络事件。`SendAsync` 方法允许发送数据包并等待响应，适用于需要同步通信的场景。同时，`DRXClient` 还提供了断开连接的功能，确保资源的正确管理和释放。

