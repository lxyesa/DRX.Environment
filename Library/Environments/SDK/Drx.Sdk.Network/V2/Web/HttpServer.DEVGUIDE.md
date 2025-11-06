# HttpServer.DEVGUIDE.md

## 概要
`HttpServer` 是一个基于 `HttpListener` 的轻量化 HTTP 服务器封装，位于 `Drx.Sdk.Network.V2.Web` 命名空间。该类适用于嵌入式服务或工具型服务器，提供：

- 基于路由模板的同步/异步请求处理注册（支持参数提取）
- 原始处理器（Raw）支持，直接访问 `HttpListenerContext`，适合流式上传/下载场景
- 文件路由与流式文件返回（支持 Range 请求、Content-Disposition 与带宽限制钳制）
- 从程序集自动注册带 `HttpHandle` 特性的方法
- 内部基于 Channel、Semaphore 和自定义线程池进行请求调度，具备最大并发限制与队列策略

## 输入 / 输出契约
- 主要输入/输出：
  - 注册器（AddRoute/AddRawRoute/AddFileRoute 等）接受路径模板和处理委托；处理委托接收 `HttpRequest`（或 `HttpListenerContext`）并返回 `HttpResponse`。
  - `StartAsync()` 启动监听并处理请求；`Stop()` 停止并释放资源。

- 成功条件：路由处理函数返回的 `HttpResponse` 被 `SendResponse` 正确发送到客户端；流式文件返回会在后台异步写入完成。
- 错误模式：
  - 路由签名或返回类型不匹配会被忽略并记录日志
  - 写入响应期间的客户端断开会被识别为断连（IsClientDisconnect），并记录但不会导致服务器崩溃

## 公共 API 概览
| 名称 | 签名 | 描述 | 返回 | 错误 |
|---|---:|---|---|---|
| ctor | HttpServer(IEnumerable<string> prefixes, string? staticFileRoot = null) | 构造函数，传入监听前缀（如 "http://localhost:8080/"）和可选静态文件根 | HttpServer | — |
| AddRawRoute | void AddRawRoute(string path, Func<HttpListenerContext, Task> handler) | 添加原始路由，处理器直接操作 `HttpListenerContext` | void | 参数校验失败时静默返回 |
| AddStreamUploadRoute | void AddStreamUploadRoute(string path, Func<HttpRequest, Task<HttpResponse>> handler) | 添加流式上传路由，内部包装为 raw handler | void | — |
| AddRoute (sync/async) | void AddRoute(HttpMethod method, string path, Func<HttpRequest, HttpResponse> handler) / void AddRoute(HttpMethod method, string path, Func<HttpRequest, Task<HttpResponse>> handler) | 添加同步/异步路由，支持模板参数 | void | — |
| AddFileRoute | void AddFileRoute(string urlPrefix, string rootDirectory) | 将 URL 前缀映射到本地目录（流式返回文件） | void | — |
| RegisterHandlersFromAssembly | static void RegisterHandlersFromAssembly(Assembly assembly, HttpServer server) | 扫描并注册带 `HttpHandleAttribute` 的静态方法 | void | 反射异常会被捕获并记录 |
| StartAsync | Task StartAsync() | 启动服务器并开始处理请求 | Task | 启动异常会抛出 |
| Stop | void Stop() | 停止服务器并释放资源 | void | — |
| SetPerMessageProcessingDelay | void SetPerMessageProcessingDelay(int ms) | 设置每条消息的最小处理延迟（毫秒），<=0 表示关闭 | void | 参数异常会被记录 |
| GetPerMessageProcessingDelay | int GetPerMessageProcessingDelay() | 获取当前每条消息最小处理延迟（毫秒） | int | — |
| SaveUploadFile | static HttpResponse SaveUploadFile(HttpRequest request, string savePath, string fileName = "upload_") | 将请求中的上传流保存为文件 | HttpResponse | IO 错误会返回 500 响应 |
| CreateFileResponse | static HttpResponse CreateFileResponse(string filePath, string? fileName = null, string contentType = "application/octet-stream", int bandwidthLimitKb = 0) | 为流式下载创建 HttpResponse（带文件流） | HttpResponse | 文件不存在返回 404 |
| GetFileStream | static Stream GetFileStream(string filePath) | 安全打开文件为只读流 | Stream 或 null | 文件打开失败返回 null |

## 方法详细说明
### 构造与配置
- 构造：`HttpServer(IEnumerable<string> prefixes, string? staticFileRoot = null)`
  - prefixes: 必需，形如 `http://localhost:8080/`（`HttpListener` 要求以 `/` 结尾）
  - staticFileRoot: 用于 `TryServeStaticFile` 的根目录（映射 `/static/` 前缀）

### 路由注册
- `AddRoute`：支持模板路径（如 `/api/items/{id}`），内部会调用 `CreateParameterExtractor` 生成参数提取函数。
- `AddRawRoute`：适用于需要访问 `HttpListenerContext` 的场景（例如直接操作底层流或处理 multipart/分块数据）。
- `AddStreamUploadRoute`：将上传路由封装为 raw handler，处理函数为 `(HttpRequest) -> Task<HttpResponse>`，便于封装上传流到 `HttpRequest.UploadFile.Stream`。

### 从程序集注册处理器
- `RegisterHandlersFromAssembly(Assembly, HttpServer)` 会扫描静态公共方法并查找 `HttpHandleAttribute` 标注的方法（支持 Raw/StreamUpload/StreamDownload 标记），为无侵入式注册提供便捷方式。

### 启动与请求流（StartAsync / ListenAsync / ProcessRequestsAsync）
- `StartAsync`：
  - 初始化 CancellationTokenSource，启动 _listener，并并行运行监听与处理任务
  - 内部使用 `Channel<HttpListenerContext>` 缓冲来 decouple 接收与处理
  - 使用 `SemaphoreSlim` 控制最大并发请求（默认 100）并将实际处理委托到自定义线程池

### 文件路由与流式发送
- `AddFileRoute(urlPrefix, rootDirectory)`：当请求路径以该 prefix 开头时，后续路径映射为本地文件。适合大文件下载。
- `TryServeFileStream(HttpListenerContext)`：尝试处理带 Range 的文件请求并在后台异步写入响应流；返回 true 表示已接管响应。
- `StreamFileToResponseAsync(...)`：实现文件分块读取并写入 response.OutputStream，支持：
  - 自动设置 Content-Type、Content-Disposition
  - 支持 Range（206 Partial Content）
  - 可选带宽限制（以 KB/s 计）
  - 捕获客户端断连并尽量安静处理

### 请求解析与响应发送
- `ParseRequestAsync(HttpListenerRequest)`：处理标准表单、JSON、以及 multipart（文件上传），并把解析结果封装为 `HttpRequest`（包含 UploadFile 流等）。
- `SendResponse(HttpListenerResponse, HttpResponse)`：统一将 `HttpResponse` 转换为 `HttpListenerResponse`，支持：
  - 直接返回文件流（若 `HttpResponse.FileStream` 不为空）
  - 返回 BodyBytes / BodyObject（序列化为 JSON） / Body 字符串

### 文件保存帮助方法
- `SaveUploadFile(HttpRequest request, string savePath, string fileName = "upload_")`：将 `request.UploadFile.Stream` 保存到磁盘，返回 `HttpResponse` 表示结果。
- `CreateFileResponse(filePath, ...)`：打开文件流并返回封装的 `HttpResponse`，供 `SendResponse` 使用（适合在 handler 中直接返回以触发流式写入）。

## 并发、资源与容错策略
- 请求队列：使用有界 Channel（capacity 1000，满时 DropOldest）来避免 OOM 与瞬时高并发导致的资源耗尽。
- 并发控制：Semaphore 控制最大并发（默认 100），多余请求会等待或被拒绝（视实现）。
- 线程池：内部使用 `ThreadPoolManager` 将耗时任务调度到后台工作线程。
- 错误处理：所有主要入口点都捕获异常并记录日志，避免抛出未处理异常导致进程崩溃。

## 常见边界、性能与安全考虑
- 大文件下载：流式返回（FileStream + async 写）可避免将文件全部加载到内存，但需注意并发读文件句柄数与磁盘 IO。
- Range 支持：实现遵循部分 Range 的语义，但复杂的多段 Range 可能未被覆盖（假设后端只处理单段 Range）。
- 上传保存：`SaveUploadFile` 在写入磁盘时需要确保目标目录权限与剩余磁盘空间可用。
- 静态文件映射：当启用 `_staticFileRoot` 时，`/static/` 前缀将直接映射到文件系统；请确保不要暴露敏感目录。

## 使用示例
### 最简单的服务器（同步路由） — Not verified
```csharp
var prefixes = new[] { "http://localhost:8080/" };
var server = new HttpServer(prefixes, staticFileRoot: "C:\\wwwroot");

server.AddRoute(HttpMethod.Get, "/hello", request =>
{
    return new HttpResponse(200, "Hello world");
});

await server.StartAsync();
// 服务器运行中，按需 Stop()
```

### 注册带特性的处理方法（示例） — Not verified
```csharp
[HttpHandle("/api/upload", "POST", StreamUpload = true)]
public static HttpResponse UploadHandler(HttpRequest req)
{
    // HttpServer.RegisterHandlersFromAssembly 会把该方法注册为流式上传路由
    return HttpServer.SaveUploadFile(req, "C:\\uploads");
}

// 注册
HttpServer.RegisterHandlersFromAssembly(Assembly.GetExecutingAssembly(), server);
```

### 返回大文件（流式）示例 — Not verified
```csharp
server.AddRawRoute("/download/", async ctx =>
{
    var filePath = "C:\\files\\bigfile.zip";
    var resp = HttpServer.CreateFileResponse(filePath, "bigfile.zip", "application/zip");
    server.SendResponse(ctx.Response, resp);
    // 若 SendResponse 在内部选择流式写入，底层会异步写入文件内容
});
```

## 故障排查 / FAQ
- Q: 启动失败并抛出权限或前缀错误？
  - A: 在 Windows 上注册 http 前缀可能需要管理员权限或使用 `netsh http add urlacl` 注册相关 URL ACL。
- Q: 大文件下载中断且抛出 SocketException？
  - A: 可能是客户端主动断开；`IsClientDisconnect` 用于判断并记录，服务器会安静地释放资源。
- Q: 多段 Range 未按预期工作？
  - A: 当前实现主要处理单段 Range，请检查请求头或在必要时扩展 `StreamFileToResponseAsync` 来支持 multipart/byteranges。

## 文件位置
- 源代码：`d:\Code\Library\Environments\SDK\Drx.Sdk.Network\V2\Web\HttpServer.cs`
- 文档（本文件）：`d:\Code\Library\Environments\SDK\Drx.Sdk.Network\V2\Web\HttpServer.DEVGUIDE.md`

## 下一步建议
1. 为 `StreamFileToResponseAsync` 添加单元测试，模拟 Range、断连和带宽限制场景。
2. 在 `Examples/` 添加一个示例项目，展示 `RegisterHandlersFromAssembly` 与 `CreateFileResponse` 的完整流程。
3. 考虑将路由与服务器配置项（最大并发、队列长度、默认带宽限制）参数化，方便运行时调整。
4. 为新加入的“每消息最小处理延迟（SetPerMessageProcessingDelay）”特性增加测试：
  - 单元测试验证在不同配置（0、200、500ms）下单条请求的最短耗时满足期望。
  - 集成测试在并发场景下验证该配置能降低短时 CPU 峰值（可通过统计 worker 吞吐或示例应用的 CPU 使用率观察）。

## 质量门（快速校验）
- Build: `dotnet build DRX.Environment.sln`
- Tests: 建议加入至少 2 个测试：
  1. 单元测试：`CreateFileResponse` 在文件存在/不存在时的行为
  2. 集成测试：启动一个实例并发起一个文件下载请求，验证 Range 与完整下载

## 假设与待确认项
- 假设：`CreateFileResponse` 的带宽限制以 KB/s 为单位实现（源码中通过 sleep/延时控制）。请确认业务期望单位与精度。
- 假设：多段 Range 支持较弱，若需严格支持请在 `StreamFileToResponseAsync` 中增强解析/拼接逻辑。
# HttpServer 开发人员指南

本文档为 `HttpServer` 类的开发/使用指南（中文）。包含全部公共 API 的表格、参数说明、行为契约、示例以及常见边界情况与注意事项，便于开发者快速集成与排查问题。

## 目录

- 概述
- 输入/输出契约（简要）
- 公共 API 总表（带参数与返回值）
- 每个 API 的参数/返回/细节说明
- 路由注解 `HttpHandleAttribute` 说明
- 使用示例
- 边界情况与注意事项
- 常见问题与排查建议

---

## 概述

`HttpServer` 是基于 `HttpListener` 封装的轻量 HTTP 服务组件，提供：

- 路由注册（同步/异步处理）
- 原始路由（直接处理 `HttpListenerContext`）用于流式上传/下载
- 流式文件下载（支持 Range、ETag、Content-Disposition、带宽限制）
- multipart/form-data 解析与流式上传支持
- 从程序集扫描并注册标注方法（`HttpHandleAttribute`）
- 静态文件与文件路由（可映射 URL 前缀到本地目录）

设计重点：以兼顾易用性与对大文件/流场景的友好支持为目标，内部采用通道/线程池/信号量进行并发控制与异步流处理。

## 输入/输出契约（简要）

- 输入：通过构造函数提供监听前缀集合和（可选）静态文件根目录。
- 输出：以 `HttpListener` 响应发送 HTTP 响应（字符串/二进制/文件流）。
- 成功准则：注册路由后，调用 `StartAsync()` 开始监听并正确响应匹配请求。流式传输应在后台异步执行且支持断点续传（Range）和部分响应（206）。
- 错误模式：遇到未捕获异常时返回 500；路由未命中返回 404。流传输阶段若客户端断开则中止并记录日志。

---

## 公共 API 总表

| 方法 | 签名 | 描述 |
|---|---|---|
| 构造函数 | `HttpServer(IEnumerable<string> prefixes, string? staticFileRoot = null)` | 创建 HttpServer 实例并初始化资源。 |
| AddRawRoute | `void AddRawRoute(string path, Func<HttpListenerContext, Task> handler)` | 添加原始路由，处理器接收 `HttpListenerContext`，适合直接读写请求/响应流。 |
| AddStreamUploadRoute | `void AddStreamUploadRoute(string path, Func<HttpRequest, Task<HttpResponse>> handler)` | 添加流式上传路由，处理方法接收 `HttpRequest` 且 `UploadFile.Stream` 指向原始请求流。 |
| AddRoute (sync) | `void AddRoute(HttpMethod method, string path, Func<HttpRequest, HttpResponse> handler)` | 添加同步路由处理器（包装为 Task），用于常规请求-响应。 |
| AddRoute (async) | `void AddRoute(HttpMethod method, string path, Func<HttpRequest, Task<HttpResponse>> handler)` | 添加异步路由处理器。 |
| RegisterHandlersFromAssembly | `static void RegisterHandlersFromAssembly(Assembly assembly, HttpServer server)` | 扫描程序集并注册带 `HttpHandleAttribute` 的静态方法。 |
| StartAsync | `Task StartAsync()` | 启动监听并开始处理请求（异步阻塞直到停止）。 |
| Stop | `void Stop()` | 停止服务器并释放资源。 |
| AddFileRoute | `void AddFileRoute(string urlPrefix, string rootDirectory)` | 将 URL 前缀映射到本地目录，匹配时以流方式返回文件（支持 Range）。 |
| SaveUploadFile | `static HttpResponse SaveUploadFile(HttpRequest request, string savePath, string fileName = "upload_")` | 将请求中的上传流保存为本地文件。 |
| CreateFileResponse | `static HttpResponse CreateFileResponse(string filePath, string? fileName = null, string contentType = "application/octet-stream", int bandwidthLimitKb = 0)` | 快捷构造文件响应，返回 `HttpResponse` 并打开文件流。 |
| GetFileStream | `static Stream GetFileStream(string filePath)` | 打开并返回只读文件流（若失败返回 null）。 |

> 说明：类中还有大量私有/内部方法（如请求解析、流分发、路由匹配等），这些用于实现细节，开发者可参考源代码实现。

---

## 详细 API 说明（参数、返回、行为）

### 构造函数

签名：
`HttpServer(IEnumerable<string> prefixes, string? staticFileRoot = null)`

参数：
- `prefixes`：必须。一个字符串集合，每项为 `HttpListener` 可接受的前缀，如 `"http://localhost:8080/"` 或 `"http://*:8080/"`。
  - 要求：末尾通常需要 `/`，与 `HttpListener` 要求一致。
- `staticFileRoot`：可选。静态文件根目录，用于 `TryServeStaticFile`（当请求以 `/static/` 开头时，从该目录读取文件）。

行为：
- 初始化内部队列、信道、信号量与线程池。
- 不会立即 Start；需显式调用 `StartAsync()`。

错误/异常：
- 构造时不会启动监听，若提供无效前缀，`StartAsync()` 时可能抛出异常。


### AddRawRoute

签名：
`void AddRawRoute(string path, Func<HttpListenerContext, Task> handler)`

参数：
- `path`：路径前缀（例如 `/upload/` 或 `/stream`）。最好以 `/` 开头；内部会尝试补齐。
- `handler`：接收 `HttpListenerContext` 的异步处理委托，可直接访问 `Request` 与 `Response` 流。

返回值：无

行为与注意事项：
- Raw 路由按注册顺序检查，当请求的 AbsolutePath 以 `path` 前缀（忽略大小写）开头时匹配并调用该 handler。
- 适合大文件上传、推送或需要直接控制响应头、状态码、流写入的场景。
- handler 内需负责写入 `context.Response` 并在完成后关闭输出流；若 handler 抛异常，`HttpServer` 会捕获并返回 500。

线程/并发：
- handler 在受限的并发线程池中运行，服务器同时使用信号量限制并发数（MaxConcurrentRequests）。

示例：
```csharp
server.AddRawRoute("/raw/", async ctx => {
    // 读取请求流
    using var ms = new MemoryStream();
    await ctx.Request.InputStream.CopyToAsync(ms);
    // 写回简单响应
    ctx.Response.StatusCode = 200;
    using var sw = new StreamWriter(ctx.Response.OutputStream);
    sw.Write("ok");
});
```


### AddStreamUploadRoute

签名：
`void AddStreamUploadRoute(string path, Func<HttpRequest, Task<HttpResponse>> handler)`

参数：
- `path`：路径前缀，如 `/upload/`。
- `handler`：接收 `HttpRequest` 的异步处理方法，`HttpRequest.UploadFile.Stream` 将直接引用 `HttpListenerRequest.InputStream`（或 multipart 解析后提供的 MemoryStream）。处理方法返回 `HttpResponse` 或 Task<HttpResponse>`。

返回值：无

行为与注意事项：
- 与 Raw 不同：处理器不需要 `HttpListenerContext`，只需通过 `HttpRequest` 获取流与其他元信息。
- 为减少内存占用，默认不将整个请求体读入内存，而是将输入流传递给 handler（适合大文件上传）。
- 当使用 `multipart/form-data` 时，框架会解析并为文件部分构造 `UploadFileDescriptor`（含 MemoryStream）；否则会直接把 `InputStream` 作为 UploadFile.Stream。
- handler 应在完成后返回一个 `HttpResponse`，`HttpServer` 会调用 `SendResponse` 将其写回。

示例：
```csharp
server.AddStreamUploadRoute("/upload/", async req => {
    if (req.UploadFile?.Stream == null) return new HttpResponse(400, "no file");
    // 保存文件到磁盘
    var result = HttpServer.SaveUploadFile(req, "C:\\uploads", "myfile.bin");
    return result;
});
```


### AddRoute（同步）

签名：
`void AddRoute(HttpMethod method, string path, Func<HttpRequest, HttpResponse> handler)`

参数：
- `method`：`System.Net.Http.HttpMethod`，例如 `HttpMethod.Get`。
- `path`：路由模板，支持路径参数占位 `{name}`，例如 `/users/{id}`。
- `handler`：同步处理函数，接收 `HttpRequest` 并返回 `HttpResponse`。

行为：
- 内部将该同步 handler 包装为异步并加入路由表，匹配时会调用 `CreateParameterExtractor` 提取路径参数并填充 `HttpRequest.PathParameters`。

注意：如果需要异步操作（文件 IO、数据库），请使用异步版本 `AddRoute(HttpMethod, string, Func<HttpRequest, Task<HttpResponse>>)`。


### AddRoute（异步）

签名：
`void AddRoute(HttpMethod method, string path, Func<HttpRequest, Task<HttpResponse>> handler)`

参数：同上，但 handler 为异步签名。

行为：
- 路由匹配时会 await handler 的返回值并将结果通过 `SendResponse` 写回。

路由模板细节（`CreateParameterExtractor`）：
- 模板中 `{param}` 会被解析为 `([^/]+)` 的正则捕获组，匹配成功后按名称填充 `request.PathParameters`。
- 若模板不含 `{}` 则直接比较完整路径等于模板才匹配。

示例：
```csharp
server.AddRoute(HttpMethod.Get, "/users/{id}", async req => {
    var id = req.PathParameters["id"];
    return new HttpResponse(200, $"user:{id}");
});
```


### RegisterHandlersFromAssembly

签名：
`static void RegisterHandlersFromAssembly(Assembly assembly, HttpServer server)`

参数：
- `assembly`：要扫描的程序集。
- `server`：目标 `HttpServer` 实例，用于注册发现的处理方法。

行为：
- 扫描 `assembly` 中所有 `public static` 方法，查找带有 `HttpHandleAttribute` 的方法，并按其属性注册为 Raw/StreamUpload/StreamDownload 或常规路由。
- 方法签名要求：
  - Raw/StreamDownload：方法必须接受单个 `HttpListenerContext` 参数，返回 `void` / `Task` / `HttpResponse` / `Task<HttpResponse>`（当前实现支持这些返回类型并作相应包装）。
  - StreamUpload/常规路由：方法必须接受单个 `HttpRequest` 参数，返回 `HttpResponse` 或 `Task<HttpResponse>`。

注意：若方法签名不匹配，注册时会跳过并写 WARN 日志。


### StartAsync

签名：
`Task StartAsync()`

参数：无

返回值：异步任务，内部会启动 `HttpListener` 并同时启动多个处理任务（数量基于 CPU 核心数）。该方法通常会阻塞直到内部任务完成（例如 Stop 被调用或出现未处理异常）。

行为：
- 创建 CancellationTokenSource、启动 `_listener.Start()`。
- 启动多个 `ProcessRequestsAsync` worker（基于 Environment.ProcessorCount）以及 `ListenAsync`。

异常：
- 启动失败时会抛出异常并记录日志。

使用提示：
- 在生产环境中通常在单独的托管线程或宿主生命周期中调用 `StartAsync()`；Stop() 会取消 token 并停止监听。


### Stop

签名：
`void Stop()`

行为：
- 调用 CancellationTokenSource.Cancel()，停止 `HttpListener`，释放信号量与其他资源（消息队列、线程池等）。

注意：Stop 不返回任务；部分正在进行的后台流可能继续并在完成后关闭相应流。


### AddFileRoute

签名：
`void AddFileRoute(string urlPrefix, string rootDirectory)`

参数：
- `urlPrefix`：URL 前缀，如 `/download/`。会强制以 `/` 开头并以 `/` 结尾。
- `rootDirectory`：对应的本地目录，如 `C:\wwwroot`。

行为：
- 当请求路径以该前缀开头时，计算相对路径并映射到本地目录，若文件存在则以流方式分发（触发 `TryServeFileStream`）。

安全：
- 会替换路径分隔并检查是否包含 `..` 防止路径穿越；若包含则返回 400。

支持：
- Range 请求（支持单个范围，如 `bytes=123-`）
- Content-Disposition、ETag、Last-Modified、Accept-Ranges 等默认头部自动添加（调用方可覆盖）


### SaveUploadFile

签名：
`static HttpResponse SaveUploadFile(HttpRequest request, string savePath, string fileName = "upload_")`

参数：
- `request`：含 `UploadFile` 的 `HttpRequest`。
- `savePath`：目标目录。如果为空，则使用 `AppContext.BaseDirectory/uploads`。
- `fileName`：保存文件名，默认前缀 `upload_` 会追加时间戳以避免冲突。

返回值：`HttpResponse`，200 表示成功，400/500 表示错误。

行为：
- 将提供的 `request.UploadFile.Stream` 同步写入到指定文件中（注意：实现为同步写入以保持原行为）。
- 若目录不存在则创建。

注意：
- 若 stream 来自 `HttpListenerRequest.InputStream`（未在内存中复制），请确保调用方在写入过程中不提前关闭流。


### CreateFileResponse

签名：
`static HttpResponse CreateFileResponse(string filePath, string? fileName = null, string contentType = "application/octet-stream", int bandwidthLimitKb = 0)`

参数：
- `filePath`：本地文件完整路径（必须存在）。
- `fileName`：用于提示到客户端的下载名，若空则使用文件名。
- `contentType`：Content-Type，默认 `application/octet-stream`。
- `bandwidthLimitKb`：可选带宽限制（KB/s），0 表示不限制。

返回值：如果文件存在返回 `HttpResponse`（状态 200）并将 `FileStream` 打开交由 `SendResponse` 的后台任务分发；若文件不存在返回 404。

注意：调用方负责该 `HttpResponse` 的 `FileStream` 在最终写完后会被自动关闭（由 `SendResponse` 中的后台任务负责）。


### GetFileStream

签名：
`static Stream GetFileStream(string filePath)`

参数：
- `filePath`：文件路径。

返回值：若打开成功返回只读 `FileStream`，否则返回 null（并在失败时记录日志）。

注意：调用方需负责关闭返回的流。

### SetPerMessageProcessingDelay / GetPerMessageProcessingDelay

签名：
`void SetPerMessageProcessingDelay(int ms)`
`int GetPerMessageProcessingDelay()`

参数：
- `ms`：以毫秒为单位的最小处理耗时阈值。设置为小于等于 0 的值将禁用该功能（默认禁用）。

行为：
- 当启用后，服务器在每条请求的实际处理（即调用 `HandleRequestAsync`）完成后会保证该请求从开始处理到最终完成至少耗时 `ms` 毫秒。实现上在工作线程中使用 `Stopwatch` 计时，并在 finally 块中等待剩余时间，然后释放信号量。
- 该延迟是针对单个请求的额外等待，用于在突发并发下“平滑”请求处理速率，从而减轻短时的 CPU 峰值压力；但会增加单请求的响应延时。

注意事项：
- 延迟等待放在 finally 中，因此即便处理过程中发生异常也会等待并最终释放资源。
- 该设置会增加每请求的延迟（延迟与处理时间的和），因此不适合对低延迟有严格要求的场景。建议用于批量/文件处理或内部服务以降低瞬时 CPU 峰值。

示例：
```csharp
// 将每条消息的最小处理时间设置为 500 ms
server.SetPerMessageProcessingDelay(500);

// 读取当前配置
var cur = server.GetPerMessageProcessingDelay();
Console.WriteLine($"当前每消息最小处理延迟: {cur} ms");
```

---

## 路由特性：HttpHandleAttribute

用于在程序集方法上标注以自动注册到 `HttpServer`。字段与语义：

| 属性 | 类型 | 含义 |
|---|---|---|
| Path | string | 必需。请求路径或模板。 |
| Method | string | 必需。HTTP 方法字符串（例如 "GET"、"POST"）。 |
| Raw | bool | 可选。若为 true，表示以原始处理器注册（方法接受 HttpListenerContext）。 |
| StreamUpload | bool | 可选。用于流式上传注册（方法签名 (HttpRequest)->HttpResponse 或 Task<HttpResponse>）。 |
| StreamDownload | bool | 可选。用于流式下载（方法接受 HttpListenerContext）。 |

签名示例：
```csharp
[HttpHandle("/api/upload", "POST", StreamUpload = true)]
public static async Task<HttpResponse> UploadHandler(HttpRequest req) { ... }
```

方法签名限制：
- StreamUpload / 非 Raw：方法需接受 `HttpRequest` 并返回 `HttpResponse` 或 `Task<HttpResponse>`。
- Raw/StreamDownload：方法需接受 `HttpListenerContext`，返回可为 `void`/`Task`/`HttpResponse`/`Task<HttpResponse>`（框架会尝试封装）。

---

## 使用示例

1) 基本启动
```csharp
var prefixes = new[] { "http://localhost:8080/" };
var server = new HttpServer(prefixes, staticFileRoot: "C:\\wwwroot");

server.AddRoute(HttpMethod.Get, "/hello", req => new HttpResponse(200, "Hello World"));

_ = server.StartAsync();
// 在合适时机调用 server.Stop();
```

2) 添加文件路由并支持 Range
```csharp
server.AddFileRoute("/download/", "C:\\files");
// 请求 /download/bigfile.zip 将触发流式传输，支持 Range 请求
```

3) 流式上传（直接使用 InputStream）
```csharp
server.AddStreamUploadRoute("/upload/", async req => {
    if (req.UploadFile?.Stream == null) return new HttpResponse(400, "no file");
    return HttpServer.SaveUploadFile(req, "C:\\uploads", "data.bin");
});
```

4) 使用特性从程序集注册（示例方法）
```csharp
public static class Handlers {
    [HttpHandle("/api/echo", "POST")]
    public static async Task<HttpResponse> Echo(HttpRequest req) {
        return new HttpResponse(200, req.Body ?? "");
    }
}

// 注册
HttpServer.RegisterHandlersFromAssembly(typeof(Handlers).Assembly, server);
```

5) 每消息最小处理延迟示例
```csharp
// 在服务器运行期间设置或调整每条消息的最小处理延迟
server.SetPerMessageProcessingDelay(500); // 每条消息至少 500 ms

// 查看当前设置
Console.WriteLine($"当前每消息最小处理延迟: {server.GetPerMessageProcessingDelay()} ms");

// 若需禁用
server.SetPerMessageProcessingDelay(0);
```

---

## 边界情况与注意事项

- 并发控制：内部使用 `SemaphoreSlim MaxConcurrentRequests` 控制并发请求，默认值为 100。视负载可调整实现或在构造时修改常量（需源码改动）。
- 大文件传输：文件路由与 CreateFileResponse 使用后台任务分片写出（Buffer=64KB），支持带宽限制与 Range。若客户端断开，后台任务会检测并退出。
- 请求体解析：对 `multipart/form-data` 会解析并把文件部分加载到 MemoryStream；对非 multipart 的大 body，会将 `InputStream` 直接传递给 stream 路由以节约内存。
- 路由模板匹配：`CreateParameterExtractor` 使用正则构建匹配器，`{param}` 不会匹配 `/`；当 template 不含 `{}` 时要求完全相等。
- 响应头添加：`SendResponse` 在添加头时会跳过 `Content-Length`/`Transfer-Encoding` 的重复设置，并对可能引发异常的 header 添加操作做 try/catch。
- 文件名安全：为避免 HTTP 头注入或控制字符问题，文件名通过 `SanitizeFileNameForHeader` 清理控制字符与引号。
- 日志：代码中多处有 `Logger` 调用以记录错误/警告/信息，排查时查看日志尤为重要。
- Stop 是同步方法：会触发取消，但某些后台 IO 任务可能会继续完成后清理自身资源。
 - 每消息最小处理延迟：如果启用 `SetPerMessageProcessingDelay`，会在请求处理完成后补足等待时间以达到设定阈值。这会平滑短时并发但会导致单次请求响应延迟增加，务必在低延迟敏感的场景谨慎使用。

---

## 常见问题与排查建议

- 无法绑定端口/前缀错误：请确认 Windows 下使用 `HttpListener` 时前缀是否以 `/` 结尾且用户拥有绑定权限（必要时使用 `netsh http add urlacl`）。
- 文件下载速度慢：检查 `bandwidthLimitKb` 是否非零；若为 0 则不限制。也可检查是否存在网络或磁盘 IO 瓶颈。
- 上传文件内存占用过高：请优先使用 `AddStreamUploadRoute` 接收原始流；multipart 文件部分默认会被读入 MemoryStream（此为设计 trade-off）。
- 路由不匹配：确认路径模板（是否包含参数），以及注册的 `HttpMethod` 与实际请求方法是否一致。

---

## 文件位置与说明

- 本文档：`Library/Environments/SDK/Drx.Sdk.Network/V2/Web/HttpServer.DEVGUIDE.md`
- 参考实现：`Library/Environments/SDK/Drx.Sdk.Network/V2/Web/HttpServer.cs`

---

## 结语

该指南覆盖了 `HttpServer` 的主要公共 API、参数说明、样例与常见注意点。若你需要我把文档内容同步到 project README、或为每个 public 类型/方法生成 XML 注释（或为其自动生成单元测试），我可以继续执行这些任务。