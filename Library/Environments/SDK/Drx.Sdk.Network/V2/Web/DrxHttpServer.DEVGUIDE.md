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
| AddDataPersistent | void AddDataPersistent<T>(T entity, string id) where T : Models.DataModelBase | 向服务器内存分组添加实体（按 id 分组），随后可调用持久化接口保存为 {id}.db | void | ArgumentException, InvalidOperationException |
| ExistsDataPersistent | bool ExistsDataPersistent<T>(string id, string? basePath = null) where T : Models.DataModelBase, new() | 检查指定 id 的持久化分组数据文件是否存在（优先使用 server.FileRootPath） | bool | - |
| LoadDataPersistent | void LoadDataPersistent<T>(string id, string? basePath = null) where T : Models.DataModelBase, new() | 从持久化存储加载指定 id 的分组数据到服务器内存（浅拷贝或替换内存中当前分组，行为见实现） | void | IOException, InvalidOperationException |
| GetDataPersistent | List<T> GetDataPersistent<T>(string id) where T : Models.DataModelBase | 获取指定 id 的分组数据（浅拷贝），若不存在返回空列表 | List<T> | ArgumentException, InvalidOperationException |
| UpdateOrCreateDataPersistent | void UpdateOrCreateDataPersistent<T>(string id, string? basePath = null) | 将指定 id 的分组持久化为 {id}.db（同步阻塞），优先使用 server.FileRootPath 或传入的 basePath | void | IOException, InvalidOperationException |
| SetPerMessageProcessingDelay | void SetPerMessageProcessingDelay(int ms) | 设置每消息最小处理延迟 | void | - |
| GetPerMessageProcessingDelay | int GetPerMessageProcessingDelay() | 获取当前消息延迟 | int | - |
| SetRateLimit | void SetRateLimit(int maxRequests, int timeValue, string timeUnit) | 设置基于IP的请求速率限制 | void | ArgumentException |
| GetRateLimit | (int maxRequests, TimeSpan window) GetRateLimit() | 获取当前速率限制设置 | (int, TimeSpan) | - |
| OnGlobalRateLimitExceeded | Func<int, HttpRequest, Task>? OnGlobalRateLimitExceeded { get; set; } | 全局速率限制触发时的异步回调（参数：触发次数, HttpRequest） | settable async callback | - |
| OnRouteRateLimitExceeded | Func<int, HttpRequest, string, Task>? OnRouteRateLimitExceeded { get; set; } | 路由级速率限制触发时的异步回调（参数：触发次数, HttpRequest, routeKey） | settable async callback | - |
| RegisterHandlersFromAssembly | void RegisterHandlersFromAssembly(Type markerType, bool emitLinkerDescriptor = false, string? descriptorPath = null, bool emitPreserveSource = false, string? preserveSourcePath = null) | 动态注册带特性的方法（按类型标记扫描并注册该类型的处理方法与中间件，并可选择生成 linker 描述或 Preserve 源以支持裁剪） | void | Exception |
| Response | void Response(HttpListenerContext, HttpResponse) | 由处理器直接同步发送响应的便利方法 | void | - |
| ResponseAsync | Task ResponseAsync(HttpListenerContext, IActionResult) | 由处理器异步发送 IActionResult 的便利方法 | Task | - |
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

### AuthorizationManager

- **Purpose**: 管理短生命周期的授权码/授权记录，适用于实现 OAuth 风格的授权码流程或临时授权确认页面。
- **Location**: 服务器通过 `DrxHttpServer.AuthorizationManager` 暴露该功能，内部使用线程安全的 `ConcurrentDictionary` 存储记录并通过定时器清理过期项。

公开方法（可通过 `DrxHttpServer.AuthorizationManager` 访问）:

- `string GenerateAuthorizationCode(string userName, string applicationName, string applicationDescription = "", string scopes = "")`
  - 生成新的唯一授权码并在内存中保存一条授权记录。
  - 返回值为授权码字符串（例如用于构造授权页面 URL）。

- `AuthorizationRecord? GetAuthorizationRecord(string code)`
  - 根据授权码获取对应记录，若记录不存在或已过期则返回 null。

- `bool CompleteAuthorization(string code)`
  - 将授权记录标记为已完成（`IsAuthorized=true`）并记录完成时间；返回是否成功完成。

数据结构与生命周期:

- `AuthorizationRecord` 包含授权码 (`Code`)、申请用户名 (`UserName`)、应用名/描述、创建时间 (`CreatedAt`)、有效期（分钟）与权限范围 (`Scopes`) 等字段；提供 `IsExpired` 快速判断是否过期。
- 默认授权码有效期为 5 分钟（可在 `AuthorizationManager` 构造时调整）。
- `AuthorizationManager` 启动后台定时器（每分钟）清理过期记录，避免内存长期累积。

示例流程（高层）:

1. 客户端在服务端请求生成授权码：
   - 服务端调用 `server.AuthorizationManager.GenerateAuthorizationCode(userName, appName, desc, scopes)` 并返回一个 URL（比如 `https://example.com/authorize?code={code}`）给客户端。
2. 用户在浏览器打开该 URL，服务器端根据 `code` 获取记录并展示授权确认页面（页面可从 localStorage/sessionStorage 读取登录 token 并提交）。
3. 页面提交 `code` 与用户的登录 token 至后端确认接口；后端使用 `GetAuthorizationRecord(code)` 验证并调用 `CompleteAuthorization(code)` 完成授权流程。

安全与推荐实践：

- 授权码应为不可预测的高熵字符串（如 GUID 或更强随机值），并仅在短期内有效。
- 授权确认时务必验证提交的登录 token 与授权记录中的 `UserName` 匹配，防止横向授权劫持。
- 在生产环境强烈建议通过 HTTPS 传输授权 URL 与令牌。页面端不应把登录 token 暴露给第三方脚本。
- 若需要跨实例持久化授权状态或更长生命周期，请把 `AuthorizationRecord` 持久化到数据库并在 `AuthorizationManager` 中改为从数据库查询。

示例（伪代码）：

```csharp
// 生成授权码并返回给客户端
var code = server.AuthorizationManager.GenerateAuthorizationCode(userName, "MyApp", "描述", "read_profile,write_data");
var url = $"https://yourhost/authorize?code={code}";

// 在授权页面处理确认时（后端）
var record = server.AuthorizationManager.GetAuthorizationRecord(code);
if (record != null && JwtHelper.ValidateToken(token)?.Identity?.Name == record.UserName) {
    server.AuthorizationManager.CompleteAuthorization(code);
}
```

注意：`AuthorizationManager` 只提供内存级别的短期授权管理；若需做跨进程或持久化支持，请扩展为数据库后端或分布式缓存。

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

### 速率限制触发回调（新）

为了在发生速率限制（全局或路由级）时提供额外的可扩展性，服务器新增了两个可注册的异步回调属性：

- `OnGlobalRateLimitExceeded` — 签名为 `Func<int, HttpRequest, Task>?`。当基于 IP 的全局限流被触发时异步调用（若已注册）。
  - 参数：
    - `int`：触发次数（计算方式为当前队列中已记录的请求数 + 1，即包括本次尝试）。
    - `HttpRequest`：触发限流的请求对象（包含路径、头、查询、会话等上下文）。

- `OnRouteRateLimitExceeded` — 签名为 `Func<int, HttpRequest, string, Task>?`。当某一路由的限流被触发时异步调用（若已注册）。
  - 参数：
    - `int`：触发次数（同上）。
    - `HttpRequest`：触发限流的请求对象。
    - `string`：routeKey（内部用于标识路由的字符串，例如 `ROUTE:GET:/api/foo` 或 `RAW:/upload`）。

行为说明：
- 回调为可选（可为 null）；如果未注册回调，服务器仍按原有逻辑返回 HTTP 429。回调不会改变当前请求的返回行为（回调是异步通知，非决策路径）。
- 回调在检测到超限时通过后台任务异步执行（使用 `Task.Run`），以避免阻塞请求处理线程；回调抛出异常会被捕获并记录为警告，不会影响主流程。
- 触发次数用于帮助统计、告警或自适应策略（例如在回调中把信息记录到监控系统、触发告警或调整防护策略）。

示例：注册回调并简单记录

```csharp
// 全局限流触发回调
server.OnGlobalRateLimitExceeded = async (count, req) =>
{
    // 简单记录：IP、路径、触发次数
    var ip = req.ClientAddress?.Ip ?? "unknown";
    Logger.Info($"全局速率限制触发: ip={ip}, path={req.Path}, count={count}");
    await Task.CompletedTask;
};

// 路由级别触发回调
server.OnRouteRateLimitExceeded = async (count, req, routeKey) =>
{
    Logger.Info($"路由速率限制触发: route={routeKey}, path={req.Path}, count={count}");
    // 可在此处把信息上报到监控系统或触发自动化响应
    await Task.CompletedTask;
};
```

扩展建议：如果需要更丰富的上下文（例如请求头、时间窗、阈值等），可以将回调签名扩展为接受自定义事件类型（例如 `RateLimitEvent`），我可以按需把文档与代码统一改为事件对象。当前实现以最小入侵的属性回调形式提供通知能力。

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

### RegisterHandlersFromAssembly(Type markerType, bool emitLinkerDescriptor = false, string? descriptorPath = null, bool emitPreserveSource = false, string? preserveSourcePath = null)
- **Parameters**: 
  - `markerType`：用于标识目标类型（只扫描并注册该类型自身的方法与中间件），此方式便于在裁剪场景下通过裁剪注解保留必要元数据。
  - `emitLinkerDescriptor`：可选，是否为该类型生成 linker 描述文件（linker.xml），以便在发布/裁剪时保留反射访问目标。
  - `descriptorPath`：可选，linker 描述文件的输出路径。
  - `emitPreserveSource`：可选，是否生成带 DynamicDependency 注解的 Preserve 源文件以辅助裁剪保留。
  - `preserveSourcePath`：可选，Preserve 源文件的输出路径。
- **Returns**: void
- **Behavior**: 仅扫描并注册传入类型（markerType）自身声明的带 `HttpHandleAttribute` / `HttpMiddlewareAttribute` 的方法。注册过程中会根据方法签名自动选择包装器（支持 HttpRequest/HttpListenerContext、同步/异步、IActionResult 等签名），并可选择生成 linker 描述或 Preserve 源以支持 ILLink 裁剪场景。
- **Example**:
```csharp
// 推荐的用法：传入类型标记（仅扫描该类型），而不是传入整个 Assembly
server.RegisterHandlersFromAssembly(typeof(MyApi));
// 若需要同时生成 linker 描述文件：
server.RegisterHandlersFromAssembly(typeof(MyApi), emitLinkerDescriptor: true, descriptorPath: "linker.MyApi.xml");
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

## Attributes (特性)

下面两个特性是与动态注册/中间件相关的公开 API：`HttpHandleAttribute` 用于标注处理方法，`HttpMiddlewareAttribute` 用于标注中间件方法。`DrxHttpServer.RegisterHandlersFromAssembly` 会扫描程序集并根据这些特性注册对应的路由或中间件。

### HttpHandleAttribute
- **Purpose**: 标注一个静态方法为 HTTP 路由处理器，支持普通路由、原始（Raw）处理、流式上传/下载等不同语义。
- **使用场景**: 通过反射自动注册路由（`RegisterHandlersFromAssembly`），或者作为示例/文档标注。

构造与属性（公共 API）:

| 名称 | 类型 | 说明 |
|---|---:|---|
| HttpHandleAttribute(string path, string method) | ctor | 必需：路由路径模板（前缀或包含参数的模板），以及 HTTP 方法字符串（如 "GET"、"POST"） |
| Raw | bool | 若为 true，方法可直接接收 `HttpListenerContext` 进行流式/低级别处理（默认 false）。 |
| StreamUpload | bool | 标记该处理方法用于流式上传（服务端从请求流读取大文件）。等价于 Raw 的语义扩展，用于可读性。 |
| StreamDownload | bool | 标记用于流式下载（服务端直接写入响应流）。等价于 Raw 的语义扩展。 |
| RateLimitMaxRequests | int | 可选：路由级速率限制（窗内最大请求数），0 表示不启用。 |
| RateLimitWindowSeconds | int | 可选：速率限制窗口（秒）。

方法签名支持（被标注的方法可以是下列任意一种签名）：

- 同步普通路由：
  - `public static HttpResponse Handler(HttpRequest req)`
- 异步普通路由：
  - `public static Task<HttpResponse> Handler(HttpRequest req)`
- 原始/流式处理（Raw / StreamUpload / StreamDownload）：
  - `public static void Handler(HttpListenerContext ctx)`
  - `public static Task Handler(HttpListenerContext ctx)`

示例：
```csharp
[HttpHandle("/api/hello", "GET")]
public static HttpResponse Hello(HttpRequest req)
{
    return new HttpResponse(200, "hello world");
}

[HttpHandle("/upload", "POST", StreamUpload = true, RateLimitMaxRequests = 5, RateLimitWindowSeconds = 60)]
public static async Task<HttpResponse> Upload(HttpRequest req)
{
    // 处理 req.UploadFile.Stream
    return await Task.FromResult(new HttpResponse(200, "ok"));
}

[HttpHandle("/raw", "POST", Raw = true)]
public static async Task RawHandler(HttpListenerContext ctx)
{
    // 直接操作 ctx.Request/InputStream/Response
}
```

行为与注意事项：

- `RegisterHandlersFromAssembly` 会根据方法签名自动选择包装器：若方法接收 `HttpListenerContext` 则作为 Raw 处理注册；若方法接收 `HttpRequest` 则包装为普通路由处理（支持同步与异步返回）。
- 路由模板支持简单路径参数（例如 `/users/{id}`），框架会在匹配时提取并把参数提供给处理流程（通过 `HttpRequest` 的参数集合或路由参数字典）。
- 当使用路由级速率限制（`RateLimitMaxRequests`/`RateLimitWindowSeconds`）时，注册过程会把这些值保存到路由元数据中，运行时优先检查路由级限流。超限返回 HTTP 429。
- 若方法签名与支持的签名不匹配，注册时会跳过并在日志中记录警告（建议在编译前手工检查）。

验证状态：示例签名在本仓库反射注册代码路径上设计并手工检查；建议在实际服务中对复杂签名（如泛型/额外参数）做编译期或单元测试验证。

### HttpMiddlewareAttribute
- **Purpose**: 标注一个方法为 HTTP 中间件，用于在请求处理链上拦截、修改或短路请求/响应流程。支持传统的基于 `HttpListenerContext` 的中间件和新的基于 `HttpRequest` + `next` 的中间件签名。

构造与属性（公共 API）:

| 名称 | 类型 | 说明 |
|---|---:|---|
| HttpMiddlewareAttribute(string? path = null) | ctor | 可选：路径前缀，null/空 为全局中间件 |
| Path | string? | 与构造参数一致，用于限定中间件只作用于指定前缀路径（前缀匹配） |
| Priority | int | 优先级，-1 表示使用默认（默认全局 0，路由中间件 100）。数值越小优先级越高（越先执行）。 |
| OverrideGlobal | bool | 若为 true，强制将该中间件置为最高优先级（覆盖全局配置）。 |

支持的中间件方法签名（注册时会自动识别并包装）：

- 传统/低级中间件（旧风格）:
  - `public static Task Middleware(HttpListenerContext ctx)`
  - `public static void Middleware(HttpListenerContext ctx)`

- 新式基于请求/管道的中间件（推荐）：
  - 同步 next、同步返回：
    - `public static HttpResponse Middleware(HttpRequest req, Func<HttpRequest, HttpResponse> next)`
  - 异步 next 或异步返回：
    - `public static Task<HttpResponse> Middleware(HttpRequest req, Func<HttpRequest, Task<HttpResponse>> next)`
    - `public static Task<HttpResponse?> Middleware(HttpRequest req, Func<HttpRequest, Task<HttpResponse?>> next)`

示例（属性标注与多种签名）：
```csharp
[HttpMiddleware] // 全局中间件，旧风格
public static async Task Logger(HttpListenerContext ctx)
{
    Console.WriteLine($"{ctx.Request.RemoteEndPoint}");
    await Task.Yield();
}

[HttpMiddleware("/api", Priority = 50)] // 仅匹配 /api 前缀
public static HttpResponse Echo(HttpRequest req, Func<HttpRequest, HttpResponse> next)
{
    // 在调用下一个处理器前后可做处理
    var resp = next(req);
    // 修改或包装返回
    return resp;
}

[HttpMiddleware(null)] // 异步新式中间件
public static async Task<HttpResponse> Auth(HttpRequest req, Func<HttpRequest, Task<HttpResponse>> next)
{
    // 检查授权
    if (!JwtHelper.ValidateTokenFromRequest(req))
        return HttpResponse.Unauthorized(); // 假设框架提供便捷构造

    return await next(req);
}
```

行为与注意事项：

- `RegisterHandlersFromAssembly` 在加载中间件时会尝试识别上述签名并将其封装为内部 `MiddlewareEntry`：
  - 旧式 `HttpListenerContext` 中间件直接作为 `Handler` 存储并调用（兼容性保证）。
  - 新式 `HttpRequest` 中间件會被包装为 `RequestMiddleware`，并在请求解析（构造 `HttpRequest`）后参与管道执行。新式中间件支持短路（直接返回 `HttpResponse`），或通过 `next` 继续调用下游处理器。
- 约定：当中间件返回 `null`（或 `Task<HttpResponse?>` 的结果为 null）时，表示中间件自身已经直接写入了响应（例如通过 `HttpListenerContext.Response`），管道将停止后续处理。
- 路径匹配：若中间件标注了 `Path`，仅在请求 `Path` 以该前缀开头时生效（前缀匹配，区分大小写根据实现）。
- 优先级与顺序：
  - `Priority` 决定中间件的相对执行顺序（小值优先）。
  - 若 `Priority` 相同，`AddOrder`（注册顺序）决定先后。`OverrideGlobal` 可用于将路由中间件提升为更高优先级。
- 兼容性：框架保持对旧式中间件的兼容，同时推荐使用新式签名以便在中间件内部直接处理 `HttpRequest`（更轻量、类型安全）。

常见陷阱与建议：

- 中间件应尽量避免阻塞调用（使用异步模式）。
- 在中间件中若需要访问低级 `HttpListenerContext`（例如直接写入响应流、使用非标准头部或控制连接），请使用旧式签名并在方法内直接操作 `ctx`。
- 若中间件需要短路响应（例如鉴权失败），直接返回 `HttpResponse`（或 `Task.FromResult(HttpResponse)`）而不要调用 `next`。

验证状态：中间件签名的识别与封装已在 `DrxHttpServer` 的注册逻辑中实现并在本仓库中通过手工编译验证。建议为关键中间件添加单元测试以覆盖短路、异常和并发场景。

## Examples: 属性组合（快速参考）

```csharp
// 同时声明路由与中间件（示例）
[HttpMiddleware(null)]
public static async Task<HttpResponse> Timing(HttpRequest req, Func<HttpRequest, Task<HttpResponse>> next)
{
    var sw = Stopwatch.StartNew();
    var resp = await next(req);
    sw.Stop();
    resp.Headers["X-Elapsed-ms"] = sw.ElapsedMilliseconds.ToString();
    return resp;
}

[HttpHandle("/api/ping", "GET")]
public static HttpResponse Ping(HttpRequest req)
{
    return new HttpResponse(200, "pong");
}
```

### SaveUploadFile(HttpRequest req, string destDirectory, string? suggestedFileName = null)

- Purpose: 将来自客户端的上传内容保存为服务器上的文件。支持常见的上传语义：
  - multipart/form-data（表单上传，含文件字段和其他字段）
  - 原始流（Content-Type 非 multipart 时直接把请求体视为文件流）
  - 可选的 metadata/字段处理与校验

- 参数说明：
  - `req` (`HttpRequest`)：框架封装的请求对象，包含 Headers、Query、Form（如果解析）、BodyStream 等。
  - `destDirectory` (`string`)：目标目录，方法将文件保存到该目录。方法内部应确保目录存在或尝试创建。
  - `suggestedFileName` (`string?`)：可选的建议文件名（优先级低于上传表单内的文件名）；如果未提供，方法将使用表单提供的文件名或生成唯一文件名。

- 返回值：`HttpResponse` — 表示处理结果。典型成功返回为 200/201（含文件信息），失败时返回相应错误码与诊断信息（400/403/413/500 等）。

详细接收与保存流程（从接收到请求开始）：

1. 请求解析与表单识别
  - 若 `Content-Type` 为 `multipart/form-data`，框架应先解析表单边界并枚举每个部分（part）。每个 part 有 headers（如 Content-Disposition、Content-Type）和对应的内容流。
  - 若 `Content-Type` 不是 multipart，且路由/中间件未特殊解析，则将整个请求体视为单个文件流（raw upload）。此时 `suggestedFileName` 或查询/头部中指定的文件名将用于保存时命名。

2. 字段选择与文件部分识别
  - 对于 multipart，查找常用文件字段名（如 `file`、`upload`、或可由调用者在 req.Query/req.Headers 指定的字段名）。若存在多个文件字段，可按策略保存全部或只保存第一个（由实现或调用方决定）。

3. 校验（必须且重要）
  - 大小限制：在保存前或写入过程中检查单文件大小和总上传大小，若超过 `MaxUploadSize`（可配置），返回 413 Payload Too Large。
  - 文件类型限制：可根据 Content-Type 或扩展名校验白名单/黑名单（例如只允许 .zip/.png）。
  - 路径安全：绝对禁止使用上传提供的文件名直接作为路径，必须清理文件名，移除路径分隔符，或只保留文件名部分以防止路径穿越。
  - 权限与磁盘空间：检查目标目录权限和可用磁盘空间（如可用，才继续写入）。

4. 临时文件写入与原子替换
  - 推荐写入到临时文件（例如 `{destDirectory}/.{guid}.tmp`），写入完成后再做原子替换（例如 `File.Replace` 或 `File.Move`）到最终文件名，避免部分写入被客户端误认为上传完成。
  - 在 Windows 上请注意目标文件可能被占用，替换时需要处理异常并选择合适的回退策略。

5. 流式写入与进度回调
  - 写入过程中不要将整个文件加载到内存，使用缓冲区（例如 64KB 或 81920 字节）从请求流复制到目标流。
  - 如果框架支持进度通知，应在每次写入后更新进度（比如通过事件、回调或 IProgress<long>）。
  - 支持客户端中断：当写入发生异常（例如客户端断开导致读取失败）时，应删除临时文件并返回相应错误或中止响应处理。

6. 元数据保存
  - 如果请求中包含额外表单字段（例如 `metadata` JSON 字段），可把这些字段序列化并与文件关联（写入数据库、生成 sidecar 文件或保存到文件名附带的 .meta 文件）。

7. 返回值语义
  - 成功：返回 201 Created（如果创建了新文件）或 200 OK（覆盖既有文件），并在响应体中返回 JSON 描述（例如 { fileName, size, path, md5, uploadTime }）。
  - 校验失败：返回 400 Bad Request（非法字段/文件类型等）。
  - 超大：413 Payload Too Large。
  - 无权限或路径问题：403 Forbidden 或 500 Internal Server Error（并记录详细日志）。

示例实现（伪代码，说明关键步骤）：

```csharp
public static HttpResponse SaveUploadFile(HttpRequest req, string destDirectory, string? suggestedFileName = null)
{
    // 1. 确保目标目录存在
    Directory.CreateDirectory(destDirectory);

    // 2. 判断是否 multipart
    if (req.IsMultipart)
    {
        var part = req.GetFilePart("file") ?? req.FileParts.FirstOrDefault();
        if (part == null) return new HttpResponse(400, "no file part");

        var originalFileName = SanitizeFileName(part.FileName) ?? suggestedFileName ?? GenerateUniqueName();
        var tempPath = Path.Combine(destDirectory, "." + Guid.NewGuid().ToString() + ".tmp");

        using (var outFs = File.Create(tempPath))
        {
            // 可配置的最大大小校验
            CopyStreamWithLimit(part.Stream, outFs, maxBytes: MaxUploadSize, progress: null);
        }

        var finalPath = Path.Combine(destDirectory, originalFileName);
        File.Move(tempPath, finalPath); // 或 File.Replace

        var info = new { fileName = originalFileName, size = new FileInfo(finalPath).Length };
        return new HttpResponse(201, JsonConvert.SerializeObject(info), contentType:"application/json");
    }

    // 处理 raw stream
    var fileName = SanitizeFileName(suggestedFileName) ?? GenerateUniqueName();
    var tmp = Path.Combine(destDirectory, "." + Guid.NewGuid().ToString() + ".tmp");
    using (var outFs = File.Create(tmp))
    {
        CopyStreamWithLimit(req.BodyStream, outFs, maxBytes: MaxUploadSize, progress: null);
    }
    var dest = Path.Combine(destDirectory, fileName);
    File.Move(tmp, dest);
    return new HttpResponse(201, JsonConvert.SerializeObject(new { fileName = fileName }), contentType: "application/json");
}

// 辅助函数：清理文件名，移除路径，限制长度等
private static string SanitizeFileName(string? name) { /* ... */ }
private static void CopyStreamWithLimit(Stream src, Stream dst, long maxBytes, IProgress<long>? progress) { /* ... */ }
```

示例：在路由中注册上传处理器

```csharp
server.AddStreamUploadRoute("/upload", async req => {
    // 可在此处进行鉴权、速率限制或路径计算
    var resp = DrxHttpServer.SaveUploadFile(req, "C:/uploads");
    return await Task.FromResult(resp);
});
```

安全建议与注意事项：
- 路径穿越：绝对不要信任客户端提供的文件名；必须使用 `Path.GetFileName(...)` / 正则或白名单清理。
- 权限控制：确保保存目录权限受限，避免任意文件写入到系统敏感目录。
- 扫描与后处理：对于可执行文件或敏感类型，上传完成后可触发病毒扫描、类型检查或异步处理队列。
- 磁盘配额：对于多租户或受限环境，实施磁盘配额并在上传前检查剩余空间。

测试要点（建议）
- 编写测试覆盖以下场景：
  1. multipart/form-data 上传成功并返回 201。
  2. raw 上传成功（Content-Type: application/octet-stream）。
  3. 超大文件返回 413 并不留下临时文件。
  4. 恶意文件名（含 ../ 或 \ 等）被正确清理，不写出到外部目录。
  5. 多并发上传对服务器资源（句柄、磁盘、内存）影响可控。

常见错误与故障排查：
- 问题：临时文件残留。原因：写入异常或未捕获的取消。解决：捕获异常并在 finally 中删除临时文件。
- 问题：File.Move/Replace 失败（目标被占用）。解决：在替换时重试若干次或采用不同的命名策略（例如附带时间戳），并记录失败原因。

总结：`SaveUploadFile` 是一个把上传数据安全可靠保存到磁盘的核心工具，调用方应在路由/中间件层面负责鉴权、大小/类型策略配置；而该方法负责流式保存、临时文件与原子替换、错误回滚与清理等实现细节。

### CreateFileResponse(string filePath, string? downloadName, string contentType, int bandwidthLimitKbps)

- Purpose: 为大文件/流式下载创建一个标准化的 HttpResponse 对象，支持 Range 请求（断点续传）、带宽限速、ETag/Last-Modified 缓存协商、以及合理的错误码（404/416/206/304/500）。

- 参数说明：
  - `filePath` (string)：服务器本地文件的绝对路径，必须可读。
  - `downloadName` (string?)：建议客户端保存的文件名（用于 Content-Disposition），如果为 null 或空，则默认使用文件在磁盘上的文件名。
  - `contentType` (string)：响应的 MIME 类型（例如 `application/zip`、`application/octet-stream`、`image/png` 等）。如果为空，可使用 `application/octet-stream` 作为默认值。
  - `bandwidthLimitKbps` (int)：以 KB/s 为单位的带宽上限；0 表示不限制（无限速）。此参数用于在服务器端粗略限制单个响应的传输速率以避免占满带宽。

- 返回值：`HttpResponse` — 一个封装了响应状态码、头集合以及流式 Body 的对象；由服务器框架负责把该 HttpResponse 写入底层 `HttpListenerContext.Response`。

详细流程（从接收到请求开始）：

1. 接收请求 & 路由匹配
  - 当 `HttpListener` 收到请求并被解析为 `HttpRequest`（或框架的请求封装）后，路由器根据路径匹配到对应的处理方法（例如 `AddRoute(HttpMethod.Get, "/download/{id}", handler)`）。

2. 中间件检查（在处理器执行前）
  - 全局或路由级中间件可以在此阶段进行鉴权、限流、会话检查、日志记录等；若中间件决定拒绝请求，则应直接返回相应的 `HttpResponse`（例如 401/403/429），不再调用 `CreateFileResponse`。

3. 处理方法调用 CreateFileResponse
  - 处理器通常会根据请求参数、模组 id 等计算出服务器上的文件路径，然后调用：
    ```csharp
    return DrxHttpServer.CreateFileResponse(filePath, downloadName, contentType, bandwidthLimitKbps);
    ```
  - `CreateFileResponse` 会立即进行必要的本地文件检查（是否存在、是否可读），并根据请求中是否带有 `Range` / `If-Range` / `If-Modified-Since` 等头决定返回哪种响应。

4. If-Modified / 缓存协商
  - 如果请求包含 `If-None-Match` 或 `If-Modified-Since`，`CreateFileResponse` 会对文件的 ETag 或最后修改时间进行比对：
    - 如果文件未修改，返回 304 Not Modified（空响应体，带上必要的缓存头）。
    - 否则继续进入流式返回流程。

5. Range 支持（断点续传）
  - 如果请求包含合法的 `Range` 头（例如 `Range: bytes=1000-` 或 `Range: bytes=0-499`）：
    - 解析出起始/结束字节索引；如果索引有效且在文件范围内，返回状态码 206 Partial Content，并在响应头中写入 `Content-Range: bytes {start}-{end}/{total}`，以及 `Accept-Ranges: bytes` 和 `Content-Length` 为实际返回的字节数。
    - 如果 `Range` 无效（超出文件长度或语法错误），返回 416 Range Not Satisfiable，并在 `Content-Range` 中写入 `bytes */{total}`。

6. 响应头组装（CreateFileResponse 会设置下列常用头）
  - `Content-Type`: 使用 `contentType`（若未指定则 `application/octet-stream`）。
  - `Content-Disposition`: `attachment; filename="{downloadName}"`（含适当的 URL/ASCII 转义以兼容旧版浏览器）。
  - `Content-Length`: 对于 200 或 206 返回时，表示本次传输的字节数（若使用 chunked 则以 Transfer-Encoding 为准）。
  - `Accept-Ranges: bytes`：表明支持 Range。
  - `Content-Range`：在 206 或 416 时按规范填写。
  - `ETag` / `Last-Modified`：用于缓存协商。
  - `Cache-Control`：可按实现加上 `no-cache` 或其他策略（视项目安全/性能策略）。

7. 流式传输实现要点
  - `CreateFileResponse` 不会一次性把整个文件读入内存，而是返回一个指向文件流的 Body（通常为包装后的 `Stream` 或一个委托，由框架在写入响应时逐块复制）。复制逻辑通常采用固定缓冲区（例如 64KB 或 81920 bytes）。
  - 支持从文件的指定偏移量开始（用于 Range），通过 `FileStream.Seek(start, SeekOrigin.Begin)` 定位。读取并写入时若检测到客户端断开（写入异常或取消令牌触发），应中止传输并关闭流。
  - 带宽限速（`bandwidthLimitKbps`）的实现思路：每次发送固定大小的块（例如 16KB/32KB/64KB），计算所允许的发送速率并在循环中通过 Task.Delay 等手段插入短暂等待以达到近似的 KB/s 限制。实现应尽量平滑、并适配不同的 chunk 大小；注意限速会对吞吐量与延迟产生影响。

8. 错误处理与状态码
  - 文件不存在或无法访问 -> 返回 404 Not Found。
  - Range 无效 -> 返回 416 Range Not Satisfiable（并在 `Content-Range` 中返回 `*/{total}`）。
  - 在传输过程中发生未捕获异常 -> 返回 500 Internal Server Error（框架应记录完整异常栈并关闭流）。

9. 资源释放与并发
  - 返回的 `HttpResponse` 中若包含未关闭的 `FileStream`，框架在响应写入完成或异常发生后应负责关闭流并释放文件句柄，避免句柄泄漏。
  - 对于大并发场景，建议在服务器层面对并发下载数量做限制（例如信号量或并发队列），以避免磁盘或网络饱和。

示例：在路由中直接返回 CreateFileResponse（典型用法）

```csharp
// 简单示例：在路由处理方法中返回文件（支持断点续传）
server.AddRoute(HttpMethod.Get, "/download/{id}", req => {
    // 由请求参数计算文件在服务器上的实际路径
    var filePath = Path.Combine("C:/files", req.PathParameters["id"] + ".zip");
    var downloadName = Path.GetFileName(filePath);
    // contentType 可按扩展名判断或写死为 application/octet-stream
    return DrxHttpServer.CreateFileResponse(filePath, downloadName, "application/zip", 0);
});
```

示例：在更复杂的异步处理器中使用（带限速）

```csharp
server.AddRoute(HttpMethod.Get, "/bigfile", async req => {
    var path = "C:/bigdata/large.bin";
    // 限速为 200 KB/s
    int limitKbps = 200;
    return DrxHttpServer.CreateFileResponse(path, "large.bin", "application/octet-stream", limitKbps);
});
```

实现注意（供维护者参考）：
- 当支持 `If-None-Match` / `If-Modified-Since` 时，若返回 304 请确保不要附带文件流体；框架应返回空体并保留合适的缓存头。
- 带宽限速应在流复制层进行，避免在主线程长时间阻塞。可使用异步读取 + Task.Delay 控制速率。
- `Content-Disposition` 中的文件名对不同浏览器/客户端需做额外编码（例如 RFC 5987 或双写法：`filename` + `filename*`）。库中可提供通用工具方法封装该处理。

测试要点（建议）
- 覆盖以下场景的单元/集成测试：
  1. 普通 200 全量下载（小文件）。
  2. Range 请求返回 206，且 `Content-Range` 与 `Content-Length` 正确。
  3. 无效 Range 返回 416。
  4. If-Modified-Since / If-None-Match 返回 304。
  5. 带宽限速生效（用计时断言近似校验吞吐）。
  6. 客户端中断时服务器正确关闭文件句柄且不抛资源泄漏异常。

常见坑
- 对于非常大的文件（多 GB），不要在内存中构造整个响应体；务必使用流式写入并限制并发数。
- 在 Windows 下打开 FileStream 时注意使用 FileOptions.SequentialScan 与合适的 FileShare 模式，以提升大文件读取性能并允许并发读。

总结：`CreateFileResponse` 提供一个统一、安全且功能完备的方式来生成流式下载响应；在调用方只需传入文件路径、期望的下载名、MIME 类型和可选带宽限制，框架将负责 Range、缓存协商、头部组装、流式传输与资源回收等细节。

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

### IActionResult 与 新签名示例

下面示例展示如何使用框架新增的 `IActionResult` 签名（同步/异步）以及何时使用原始 `HttpListenerContext` 签名。它们可以直接通过 `RegisterHandlersFromAssembly` 自动注册，或者以编程方式通过 `AddRoute` / `AddRawRoute` 注册。

1) 同步返回 HTML 页面（使用 `HtmlResult` 简便构造）

```csharp
// 直接在路由中返回 HTML
server.AddRoute(HttpMethod.Get, "/page", req =>
  new HtmlResult("<!doctype html><html><body><h1>Hello from DrxHttpServer</h1></body></html>"));

// 或使用路径返回 HTML
server.AddRoute(HttpMethod.Get, "/page2", req =>
  new HtmlResultFromFile("/index.html")); // <-- 使用 "/index.html" 的时候需要使用 server.FileRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"); 来指定根目录
```

2) 异步返回 JSON（Task<IActionResult>）

```csharp
[HttpHandle("/api/hello-async", "GET")]
public static async Task<IActionResult> HelloAsync(HttpRequest req, DrxHttpServer server)
{
  // 异步场景下可以访问 server 注入对象（例如用于读取共享资源或服务）
  await Task.Yield();
  return new JsonResult(new { message = "hello", path = req.Path });
}
```

### DataPersistent 管理器（DataPersistentManager）

- 目的：在服务器内存中按字符串 id 分组保存 `Models.DataModelBase` 派生对象列表，并提供把某个分组持久化到磁盘的能力，文件名默认为 `{id}.db`。
- 存储实现：内存集合（线程安全） + 持久化使用 `SqliteUnified<T>`（详见 `Library/Environments/SDK/Drx.Sdk.Network/DataBase/Sqlite/SqliteUnified.DEVGUIDE.md`）。

公开 API（DrxHttpServer 对外暴露的简易包装）：

- `void AddDataPersistent<T>(T entity, string id) where T : Models.DataModelBase` — 将实体加入指定分组；若分组不存在则创建。要求同一分组内元素类型一致，否则抛出 `InvalidOperationException`。
- `bool ExistsDataPersistent<T>(string id, string? basePath = null) where T : Models.DataModelBase, new()` — 检查指定 id 的持久化数据文件是否存在（优先检查 `DrxHttpServer.FileRootPath`，否则使用 `basePath`）；返回 true 表示存在。
- `void LoadDataPersistent<T>(string id, string? basePath = null) where T : Models.DataModelBase, new()` — 从持久化存储加载指定 id 的分组数据到服务器内存（会替换或初始化内存中的该分组），若文件不存在或读取失败会抛出异常。
- `List<T> GetDataPersistent<T>(string id) where T : Models.DataModelBase` — 返回指定分组的浅拷贝列表（若分组不存在返回空列表）。
- `void UpdateOrCreateDataPersistent(string id, string? basePath = null)` — 将分组持久化为 `{id}.db`，同步阻塞。存储路径优先使用 `DrxHttpServer.FileRootPath`（若已设置），否则使用 `basePath`（若传入），最后由 `SqliteUnified` 决定默认位置。

行为说明与注意事项：

- 类型一致性：向同一个 `id` 分组添加不同具体类型（例如 `UserData` 与 `OrderData`）会引发异常；请确保分组语义一致。
- 持久化策略：当前实现为同步阻塞：`UpdateOrCreateDataPersistent` 会先清空目标数据库表（调用 `SqliteUnified.ClearAsync`），再逐条 `Push` 当前内存列表。若需要非阻塞或批量优化，可考虑在后台任务中异步执行或实现批量插入。
- 并发策略：内存集合对每个分组使用锁保护以保证并发安全。对于高并发写场景，建议将写入操作先缓存在队列并由单独持久化线程串行化处理，以降低 SQLite 写锁冲突。
- 错误与回退：持久化过程中若发生异常（文件权限、IO 错误或 SqliteException），当前实现会把异常向上抛出；调用方（或上层路由）应捕获并记录，或实现重试/退避策略。

示例：在启动或路由中使用（同步示例）

```csharp
// 假设 UserData : Models.DataModelBase
var server = new DrxHttpServer(new[] { "http://localhost:8080/" }, staticFileRoot: "C:/wwwroot");

// 添加用户数据到名为 "users" 的分组
server.AddDataPersistent(new UserData { /* ... */ }, "users");

// 获取当前分组（浅拷贝）
var users = server.GetDataPersistent<UserData>("users");

// 持久化到 C:/wwwroot/users.db（如果 server.FileRootPath 已设置）
server.UpdateOrCreateDataPersistent<T>("users");
```

示例：检查并加载已有持久化数据，然后读取

```csharp
// 检查是否存在已持久化的 users.db
if (server.ExistsDataPersistent<UserData>("users"))
{
  // 从磁盘加载到内存（若发生 IO 错误会抛出异常）
  server.LoadDataPersistent<UserData>("users");

  // 现在可从内存读取
  var users = server.GetDataPersistent<UserData>("users");
}
```

改进建议：

- 提供异步版本 `UpdateOrCreateDataPersistentAsync(string id, string? basePath = null, CancellationToken token)`，避免在请求线程上阻塞。可在内部使用 `SqliteUnified<T>.PushAsync` 与批量事务优化。
- 在服务器关闭时自动把所有分组持久化（作为优雅关闭流程的一部分），或提供定时器周期性保存功能以降低数据丢失风险。
- 为每个分组支持按需合并而非先 Clear 的策略（例如基于 Id 更新/插入），以保留数据库中已存在但当前未加载到内存的条目。


3) 在路由中返回文件下载（推荐使用 `IActionResult` 的 `FileResult`，保持与旧 API 兼容）

```csharp
// 使用 IActionResult/ FileResult 返回文件下载（推荐）
server.AddRoute(HttpMethod.Get, "/download/{name}", req =>
{
    var filePath = Path.Combine("C:/files", req.PathParameters["name"]);
    var downloadName = Path.GetFileName(filePath);
    // FileResult 是 IActionResult 的实现，框架会在执行时把文件流和头正确写出并支持 Range
    return new FileResult(filePath, downloadName, "application/octet-stream", bandwidthLimitKbps: 0);
});

// 兼容旧版：仍可直接返回 HttpResponse（CreateFileResponse），框架会继续支持此写法
// return DrxHttpServer.CreateFileResponse(filePath, downloadName, "application/octet-stream", 0);
```

4) 原始/低级处理器示例（直接操作 `HttpListenerContext`，并使用 `ResponseAsync` 便捷地发送 `IActionResult`）

```csharp
[HttpHandle("/raw-echo", "POST", Raw = true)]
public static async Task RawEcho(HttpListenerContext ctx, DrxHttpServer server)
{
  // 读取请求体并直接写回
  using var ms = new MemoryStream();
  await ctx.Request.InputStream.CopyToAsync(ms);
  var body = System.Text.Encoding.UTF8.GetString(ms.ToArray());

  // 使用框架提供的便捷方法把 IActionResult 写回到 HttpListenerContext
  await server.ResponseAsync(ctx, new ContentResult($"You said: {body}", "text/plain; charset=utf-8"));
}
```

5) 通过属性批量注册：把带 `HttpHandle` 的类所在程序集注册到服务器

```csharp
// 在启动处把 API 类所在程序集注册到 server
server.RegisterHandlersFromAssembly(typeof(MyApi));
```

说明：
- 推荐在大多数业务路由中使用 `IActionResult`（或 `HttpResponse`）签名，便于框架统一处理 Content-Type、Status、流响应与异步行为。 
- 仅在需要直接控制底层流、实现自定义协议或 WebSocket 握手时使用 Raw (`HttpListenerContext`) 签名。


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
server.RegisterHandlersFromAssembly(typeof(MyApi));
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