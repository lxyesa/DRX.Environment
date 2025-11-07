# DrxHttpServer.DEVGUIDE.md

## Overview
DrxHttpServer 是 DRX.Environment 框架中的高性能 HTTP 服务器，基于 HttpListener 实现，支持路由、静态文件、流式上传/下载、原始路由处理、并发控制、动态注册、请求拦截机制等。适用于自定义 Web API、文件服务和大文件流式传输等场景。

## Inputs / Outputs Contract
- **Inputs**:
  - `prefixes`: 监听前缀集合（如 "http://localhost:8080/"）。
  - `staticFileRoot`: 静态文件根目录（可选）。
  - 路由注册参数：HTTP 方法、路径模板、处理委托。
  - 上传/下载流、文件路径、请求体、请求头、查询参数等。
- **Outputs**:
  - 直接写入 HttpListenerContext.Response 或返回 HttpResponse 对象。
  - 支持流式输出、静态文件、JSON、文本等多种响应。
- **Success criteria**: 正确响应 HTTP 请求，状态码 200-299。
- **Error modes / Exceptions**:
  - 启动/停止异常、路由未命中、文件不存在、参数错误、流异常、客户端断开等。

## Public API Summary
| Name | Signature | Description | Returns | Errors |
|---|---|---|---|---|
| DrxHttpServer | DrxHttpServer(IEnumerable<string> prefixes, string? staticFileRoot = null) | 构造函数，初始化监听与静态文件目录 | void | Exception |
| StartAsync | Task StartAsync() | 启动服务器 | Task | Exception |
| Stop | void Stop() | 停止服务器 | void | Exception |
| AddRoute | void AddRoute(HttpMethod, string, Func<HttpRequest, HttpResponse>) | 注册同步路由 | void | Exception |
| AddRoute | void AddRoute(HttpMethod, string, Func<HttpRequest, Task<HttpResponse>>) | 注册异步路由 | void | Exception |
| AddRawRoute | void AddRawRoute(string, Func<HttpListenerContext, Task>) | 注册原始路由（流式处理） | void | - |
| AddStreamUploadRoute | void AddStreamUploadRoute(string, Func<HttpRequest, Task<HttpResponse>>) | 注册流式上传路由 | void | - |
| AddFileRoute | void AddFileRoute(string, string) | 注册文件路由 | void | - |
| SetPerMessageProcessingDelay | void SetPerMessageProcessingDelay(int ms) | 设置每消息最小处理延迟 | void | - |
| GetPerMessageProcessingDelay | int GetPerMessageProcessingDelay() | 获取当前消息延迟 | int | - |
| SetRateLimit | void SetRateLimit(int maxRequests, int timeValue, string timeUnit) | 设置基于IP的请求速率限制 | void | ArgumentException |
| GetRateLimit | (int maxRequests, TimeSpan window) GetRateLimit() | 获取当前速率限制设置 | (int, TimeSpan) | - |
| RegisterHandlersFromAssembly | static void RegisterHandlersFromAssembly(Assembly, DrxHttpServer) | 动态注册带特性的方法 | void | Exception |
| SaveUploadFile | static HttpResponse SaveUploadFile(HttpRequest, string, string) | 保存上传流为文件 | HttpResponse | Exception |
| CreateFileResponse | static HttpResponse CreateFileResponse(string, string?, string, int) | 创建流式下载响应 | HttpResponse | Exception |
| GetFileStream | static Stream GetFileStream(string) | 获取文件流 | Stream/null | Exception |
| AddSessionMiddleware | void AddSessionMiddleware(string cookieName = "session_id", CookieOptions? cookieOptions = null) | 添加会话中间件，自动管理会话 Cookie 和会话数据 | void | - |
| DisposeAsync | ValueTask DisposeAsync() | 释放资源 | ValueTask | - |

## Methods (detailed)

### DrxHttpServer(IEnumerable<string> prefixes, string? staticFileRoot = null)
- **Parameters**: 监听前缀集合、静态文件根目录。
- **Returns**: void
- **Behavior**: 初始化 HttpListener、通道、信号量、线程池等。
- **Example**:
```csharp
var server = new DrxHttpServer(new[] { "http://localhost:8080/" }, "C:/wwwroot");
```

### StartAsync()
- **Parameters**: None
- **Returns**: Task
- **Behavior**: 启动监听、并发处理、后台任务。
- **Example**:
```csharp
await server.StartAsync();
```

### Stop()
- **Parameters**: None
- **Returns**: void
- **Behavior**: 停止监听、释放资源。
- **Example**:
```csharp
server.Stop();
```

### AddRoute(HttpMethod, string, Func<HttpRequest, HttpResponse>)
- **Parameters**: HTTP 方法、路径、同步处理委托。
- **Returns**: void
- **Behavior**: 注册同步路由。
- **Example**:
```csharp
server.AddRoute(HttpMethod.Get, "/hello", req => new HttpResponse(200, "world"));
```
注意：新增重载允许在注册时指定路由级速率限制，例如每个IP在指定秒数内的最大请求数（可选）。
示例：
```csharp
server.AddRoute(HttpMethod.Get, "/hello", req => new HttpResponse(200, "world"), rateLimitMaxRequests: 10, rateLimitWindowSeconds: 60);
```

### AddRoute(HttpMethod, string, Func<HttpRequest, Task<HttpResponse>>)
- **Parameters**: HTTP 方法、路径、异步处理委托。
- **Returns**: void
- **Behavior**: 注册异步路由。
- **Example**:
```csharp
server.AddRoute(HttpMethod.Post, "/api/data", async req => { /* ... */ return new HttpResponse(200, "ok"); });
```
说明：异步 AddRoute 也有可选的重载参数 `rateLimitMaxRequests` 与 `rateLimitWindowSeconds`，用于设置路由级别的速率限制（单位：秒）。

### AddRawRoute(string, Func<HttpListenerContext, Task>)
- **Parameters**: 路径、原始处理委托。
- **Returns**: void
- **Behavior**: 注册原始路由，直接操作 HttpListenerContext，适合流式上传/下载。
- **Example**:
```csharp
server.AddRawRoute("/upload", async ctx => { /* ctx.Request.InputStream ... */ });
```
说明：`AddRawRoute` 也支持可选的路由级速率参数（`rateLimitMaxRequests`, `rateLimitWindowSeconds`），用于对原始路由启用独立限流。

### AddStreamUploadRoute(string, Func<HttpRequest, Task<HttpResponse>>)
- **Parameters**: 路径、处理委托。
- **Returns**: void
- **Behavior**: 注册流式上传路由，自动包装为原始处理。
- **Example**:
```csharp
server.AddStreamUploadRoute("/upload", async req => DrxHttpServer.SaveUploadFile(req, "C:/uploads"));
```
说明：流式上传路由 `AddStreamUploadRoute` 也提供可选的路由级速率限制重载，语义同上。

### AddFileRoute(string, string)
- **Parameters**: URL 前缀、本地目录。
- **Returns**: void
- **Behavior**: 注册文件路由，支持大文件流式下载和 Range。
- **Example**:
```csharp
server.AddFileRoute("/download/", "C:/files");
```

### AddSessionMiddleware(string cookieName = "session_id", CookieOptions? cookieOptions = null)

- **Purpose**: 添加一个会话中间件，用于自动管理会话 Cookie（写入/续期）以及服务器端会话数据（通过 SessionManager）。默认的 cookie 名称为 "session_id"。
- **Parameters**:
  - `cookieName`: 会话 Cookie 名称，默认 "session_id"，可定制以配合客户端或现有系统。
  - `cookieOptions`: 可选的 `CookieOptions`，用于控制 HttpOnly/Secure/SameSite/Path/Domain/Expires 等属性。
- **Behavior**: 中间件会在请求中读取指定名称的 cookie（如存在则尝试加载对应 session），并在响应中设置或续期 cookie；同时可在 `SessionManager` 中创建或获取会话对象，方便路由处理方法读取/写入会话数据。
- **Example**:
```csharp
// 使用默认会话 cookie 名称
server.AddSessionMiddleware();

// 使用自定义 cookie 名称与选项
var opts = new CookieOptions { HttpOnly = true, Secure = true, SameSite = "Lax", Path = "/" };
server.AddSessionMiddleware("my_session", opts);
```

注意：客户端通常只需在请求中携带由服务器设置的会话 cookie（默认名为 `session_id`），或者将会话令牌放入 header（例如 `X-Session-Id`）以兼容没有 cookie 的客户端场景。若客户端使用 `DrxHttpClient`，建议启用 `AutoManageCookies = true` 或在需要时使用 `SetSessionId`/`ImportCookies` 恢复会话。

### SessionManager

服务器内置 `SessionManager` 用于管理服务器端会话对象（`Session`）。`DrxHttpServer.AddSessionMiddleware` 会使用 `SessionManager` 来创建/读取/维护会话。

公开方法（可通过 `DrxHttpServer.SessionManager` 访问）:

- `Session CreateSession()` — 创建并返回一个新的 `Session`，包含新生成的会话 id。
- `Session? GetSession(string id)` — 根据会话 id 返回会话对象，找不到返回 null。
- `Session GetOrCreateSession(string? id)` — 如果 id 为 null 或不存在则创建新会话，否则返回已有会话并更新访问时间。
- `void RemoveSession(string id)` — 移除指定会话。

示例（在路由处理方法中使用）:
```csharp
// 假设中间件已将当前 session id 放入请求上下文或 cookie
var session = server.SessionManager.GetOrCreateSession(sessionIdFromCookie);
session.Data["userId"] = 123;
```

实现注意事项：SessionManager 会自动清理过期会话（基于构造时传入的超时时间），并提供线程安全的并发字典用于会话数据存储。

### SetPerMessageProcessingDelay(int ms)
- **Parameters**: 延迟毫秒数。
- **Returns**: void
- **Behavior**: 设置每条消息最小处理耗时。
- **Example**:
```csharp
server.SetPerMessageProcessingDelay(500);
```

### GetPerMessageProcessingDelay()
- **Parameters**: None
- **Returns**: int
- **Behavior**: 获取当前延迟。
- **Example**:
```csharp
int delay = server.GetPerMessageProcessingDelay();
```

### SetRateLimit(int maxRequests, int timeValue, string timeUnit)
- **Parameters**: 最大请求数、时间值、时间单位（"seconds", "minutes", "hours", "days"）。
- **Returns**: void
- **Behavior**: 设置基于IP的请求速率限制，每个IP在指定时间窗口内最多允许的请求数。若超出则返回429 Too Many Requests。maxRequests为0表示无限制。
- **Example**:
```csharp
server.SetRateLimit(10, 1, "minutes"); // 每分钟最多10个请求
```

### GetRateLimit()
- **Parameters**: None
- **Returns**: (int maxRequests, TimeSpan window)
- **Behavior**: 获取当前速率限制设置。
- **Example**:
```csharp
var (max, window) = server.GetRateLimit();
```

## 路由级限流优先级说明

服务器支持两级限流：

- 路由级限流（优先）：可以通过 `HttpHandle` 特性为单个路由启用，或在调用 `AddRoute` / `AddRawRoute` / `AddStreamUploadRoute` 时指定重载参数。
- 全局限流（回退）：通过 `SetRateLimit` 对所有未启用路由级限流的请求生效。

当路由级限流启用时，服务器会先检查路由级限流；若该路由没有启用路由级限流，则会检查全局限流设置。超限时返回 HTTP 429。此行为保证了对关键或易被滥用的路由可以有更严格或更宽松的策略。

### RegisterHandlersFromAssembly(Assembly, DrxHttpServer)
- **Parameters**: 程序集、服务器实例。
- **Returns**: void
- **Behavior**: 动态注册带 HttpHandle 特性的方法。
- **Example**:
```csharp
DrxHttpServer.RegisterHandlersFromAssembly(typeof(MyApi).Assembly, server);
```

路由级速率限制（通过属性）

DrxHttpServer 支持在通过 `HttpHandle` 特性注册处理方法时为单个路由指定独立的速率限制。
当路由级别启用了限流后，该路由的限流优先于全局 `SetRateLimit` 设置。特性新增的可用属性：

- `RateLimitMaxRequests`：整型，时间窗口内允许的最大请求数（默认 0 表示不启用）。
- `RateLimitWindowSeconds`：整型，时间窗口长度，单位为秒。

示例：

```csharp
[HttpHandle("/api/foo", "GET", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60)]
public static HttpResponse GetFoo(HttpRequest req)
{
  // 每个 IP 在 60 秒内最多允许 10 次访问此路由；超过返回 429
  return new HttpResponse(200, "ok");
}
```

注意：你也可以在程序化注册路由时直接通过 `AddRoute` / `AddRawRoute` / `AddStreamUploadRoute` 的重载传入 `rateLimitMaxRequests` 和 `rateLimitWindowSeconds` 参数来启用路由级限流。

### SaveUploadFile(HttpRequest, string, string)
- **Parameters**: 请求对象、保存目录、文件名。
- **Returns**: HttpResponse
- **Behavior**: 保存上传流为文件。
- **Example**:
```csharp
var resp = DrxHttpServer.SaveUploadFile(req, "C:/uploads");
```

### CreateFileResponse(string, string?, string, int)
- **Parameters**: 文件路径、下载名、Content-Type、带宽限制。
- **Returns**: HttpResponse
- **Behavior**: 创建流式下载响应。
- **Example**:
```csharp
var resp = DrxHttpServer.CreateFileResponse("C:/files/a.zip");
```

### GetFileStream(string)
- **Parameters**: 文件路径。
- **Returns**: Stream/null
- **Behavior**: 获取文件流，失败返回 null。
- **Example**:
```csharp
using var fs = DrxHttpServer.GetFileStream("C:/files/a.txt");
```

### DisposeAsync()
- **Parameters**: None
- **Returns**: ValueTask
- **Behavior**: 释放资源。
- **Example**:
```csharp
await server.DisposeAsync();
```

## Advanced Topics
- **流式上传/下载**: 支持大文件、断点续传（Range）、带宽限制、进度回调。
- **原始路由**: 直接操作 HttpListenerContext，适合自定义协议、WebSocket、长连接等。
- **动态注册**: 通过特性自动注册 API 方法，支持同步/异步、流式、原始等多种签名。
- **多路由/参数提取**: 支持路径参数、正则提取、动态路由。
- **并发控制**: 信号量、线程池、消息队列，最大并发 100。
- **静态文件服务**: 支持 /static/ 路径和自定义文件路由。
- **请求拦截机制**: 基于IP的速率限制，防止滥用，支持灵活的时间窗口设置。

## Concurrency & Resource Management
- **并发**: 最大 100 并发，信号量控制。
- **线程池**: 自定义 ThreadPoolManager，提升多核利用率。
- **资源释放**: Stop/DisposeAsync 释放监听、信号量、队列、线程池。
- **流管理**: 文件流、上传流自动关闭，调用方需注意。

## Edge Cases, Performance & Security
- **Edge Cases**: 路径穿越防护、文件不存在、客户端断开、异常捕获。
- **Performance**: 异步流式传输、缓冲区优化、后台任务。
- **Security**: 路径校验、文件名清理、异常日志。

## Usage Examples
### 基本 GET 路由
```csharp
server.AddRoute(HttpMethod.Get, "/hello", req => new HttpResponse(200, "world"));
```

### 流式上传
```csharp
server.AddStreamUploadRoute("/upload", async req => DrxHttpServer.SaveUploadFile(req, "C:/uploads"));
```

### 静态文件服务
```csharp
server.AddFileRoute("/download/", "C:/files");
```

### 动态注册 API
```csharp
DrxHttpServer.RegisterHandlersFromAssembly(typeof(MyApi).Assembly, server);
```

### 设置速率限制
```csharp
server.SetRateLimit(100, 1, "hours"); // 每小时最多100个请求
```

### 通过属性设置路由级速率限制（示例）
你可以在方法上使用 `HttpHandle` 特性直接声明路由的速率限制：

```csharp
[HttpHandle("/api/upload", "POST", StreamUpload = true, RateLimitMaxRequests = 5, RateLimitWindowSeconds = 60)]
public static async Task<HttpResponse> UploadHandler(HttpRequest req)
{
  // 每个 IP 在 60 秒内最多允许 5 次上传请求
  return await Task.FromResult(new HttpResponse(200, "ok"));
}
```

## Troubleshooting / FAQ
- **Q: 端口被占用？** A: 检查端口冲突，换用其他端口。
- **Q: 路由无响应？** A: 检查路径、方法、注册顺序。
- **Q: 上传失败？** A: 检查目录权限、流是否关闭。
- **Q: 静态文件 404？** A: 检查路径、文件是否存在。
- **Q: 客户端断开？** A: 日志会记录，通常无需处理。

## File Location
- **Path**: `Library/Environments/SDK/Drx.Sdk.Network/V2/Web/DrxHttpServer.cs`
- **Namespace**: `Drx.Sdk.Network.V2.Web`
- **Dependencies**: `System.Net.HttpListener`, `Drx.Sdk.Shared`, `Microsoft.AspNetCore.WebUtilities`

## Next Steps
- **Tests**: 增加单元测试覆盖路由、上传、下载、异常。
- **Examples**: 在 `Examples/` 目录添加服务端 Demo。
- **Benchmarks**: 测试高并发与大文件性能。
- **CI**: 集成自动化测试与构建。
