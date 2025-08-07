Drx.Sdk.Network.Socket 模块说明文档
1. 模块简介
本 Socket 模块为 DRX 平台提供了高性能、可扩展的 TCP Socket 服务端与客户端能力，支持命令分发、中间件扩展、服务注入、加密与完整性校验等特性。适用于需要自定义协议、命令处理、会话管理的网络通信场景。

2. 主要类与接口说明
DrxTcpClient 及扩展方法
定位：扩展自 TcpClient，为每个连接提供线程安全的多级映射存储能力。
主要成员：
PushMap<T>(mapId, mapKey, mapValue)：异步存储映射数据。
GetMap<T>(mapId, mapKey)：异步获取映射数据。
HasMap(mapId) / HasMapKey(mapId, mapKey)：判断映射或键是否存在。
RemoveMapKey(mapId, mapKey) / RemoveMap(mapId)：移除指定键或整个映射。
GetMapKeys(mapId)：获取指定映射下所有键。
ClearAllMaps()：清空所有映射。
扩展方法：
ToDrxTcpClient()：将 TcpClient 转换为 DrxTcpClient，便于统一管理和扩展。
SocketServerBuilder
定位：Socket 服务构建器，负责命令注册、中间件挂载、服务注入、加密/完整性配置等。
主要成员：
RegisterCommand(command, handler)：注册命令及其处理委托。
OnClientConnected(handler)：注册连接建立中间件。
UseMessageMiddleware(handler)：注册消息处理中间件。
AddService<T>()：注入自定义 Socket 服务（需实现 ISocketService）。
WithEncryption<T>() / WithIntegrityCheck<T>()：配置加密或完整性校验（互斥）。
扩展点：命令处理、中间件、服务注册均可链式调用，便于灵活扩展。
SocketServerService
定位：核心 Socket 服务，负责监听端口、管理连接、分发消息、调用中间件与服务钩子。
主要成员：
ConnectedClients：当前所有已连接客户端集合。
GetService<T>()：获取已注册的服务实例。
SendResponseAsync(client, code, token, args)：向客户端发送响应（自动处理加密/签名）。
生命周期方法：StartAsync、StopAsync、Dispose。
扩展点：
连接中间件：ConnectionMiddleware 管道，支持连接前校验、拒绝等。
消息中间件：MessageMiddleware 管道，支持消息预处理、拦截等。
服务钩子：ISocketService 可实现连接、断开、收发等事件钩子。
特殊说明：加密与完整性校验不可同时启用，否则启动时报错。
SocketServiceExtensions
定位：Socket 服务依赖注入扩展，便捷集成到 ASP.NET Core 等支持 DI 的项目。
主要成员：
AddSocketService()：注册 SocketServerBuilder、命令处理服务、核心 Socket 服务等到 DI 容器。
自动收集所有通过 AddService<T>() 注册的自定义服务，并确保命令处理服务优先执行。
SocketStatusCode
定位：Socket 通信状态码枚举，统一服务端与客户端的响应语义。
分组说明：
失败代码（0x20000000 - 0x20FFFFFF）：如通用失败、机械码不匹配、用户不存在、资产无效、会话冲突、用户已登录等。
成功代码（0x21000000 - 0x21FFFFFF）：如通用成功、验证成功、绑定成功、数据查询成功等。
客户端错误（0x22000000 - 0x22FFFFFF）：如未知命令、缺少参数、参数无效等。
服务器错误（0x23000000 - 0x23FFFFFF）：如内部错误等。
3. 使用方式
依赖注入注册（ASP.NET Core）

在 Startup.cs 或 Program.cs 中调用：

services.AddSocketService()
    .RegisterCommand("login", MyLoginHandler)
    .OnClientConnected(MyConnectionMiddleware)
    .UseMessageMiddleware(MyMessageMiddleware)
    .AddService<MyCustomSocketService>()
    .WithEncryption<MyEncryptor>(); // 或 .WithIntegrityCheck<MyIntegrityProvider>()

启动服务

Socket 服务作为 IHostedService 自动启动，无需手动管理生命周期。

命令注册

通过 RegisterCommand 注册命令及处理逻辑，支持参数解析与原始消息访问。

独立模式（无 IServiceCollection/IHostedService）

// 1) 以独立模式构建（不依赖 Microsoft.Extensions.*）
var builder = new SocketServerBuilder();
builder
    .RegisterCommand("login", async (server, client, args, raw) => { /* … */ })
    .OnClientConnected(async ctx => { /* 连接中间件 … */ })
    .UseMessageMiddleware(async ctx => { /* 消息中间件 … */ })
    .AddService<MyCustomSocketService>()   // 需实现 ISocketService
    .WithIntegrityCheck<MyIntegrityProvider>(); // 或 .WithEncryption<MyEncryptor>()

// 2) 运行器启动与停止
var runner = new SocketServerRunner(builder, new Hosting.SocketHostOptions { Port = 8463 });
await runner.StartAsync();
// … 运行中 …
await runner.StopAsync();

说明
- 独立模式不依赖 Microsoft.Extensions.*，适用于控制台/服务/游戏等非 ASP 应用。
- 加密与完整性互斥，二者只能择一，均未配置时为明文协议。
- 端口默认 8463，可通过 SocketHostOptions 配置。

4. 扩展点说明
中间件
连接中间件：可实现连接前身份校验、黑名单过滤等。
消息中间件：可实现消息解包、协议转换、日志等。
服务扩展
实现 ISocketService，可响应连接、断开、收发等事件。
通过 AddService<T>() 注入自定义服务。
命令处理
注册命令时可自定义参数解析、业务逻辑，支持异步处理。
5. 状态码分组说明
失败类
Failure_General、Failure_MachineCodeMismatch、Failure_UserNotFound、Failure_AssetInvalid、Failure_SessionConflict、Failure_UserAlreadyLoggedIn
成功类
Success_General、Success_Verified、Success_BoundAndVerified、Success_DataFound
客户端错误
Error_UnknownCommand、Error_MissingArguments、Error_InvalidArguments
服务器错误
Error_InternalServerError
6. 其他注意事项
加密与完整性校验互斥：同一服务不可同时启用加密与完整性校验，否则启动时报错。
生命周期管理：Socket 服务自动托管于宿主环境，无需手动释放资源。
扩展安全性：中间件与服务钩子异常会被捕获并记录日志，避免影响主流程。
客户端扩展：DrxTcpClient 支持多级映射存储，便于会话、上下文等扩展。
本模块适合需要高度自定义、可扩展的 Socket 通信场景，建议结合实际业务需求灵活配置与扩展。