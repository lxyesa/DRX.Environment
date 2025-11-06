# HttpClient 开发人员指南

本文档为 `HttpClient` 类的开发/使用指南（中文）。覆盖公共 API 总表、参数/返回/行为契约、进阶功能（流式上传/下载/进度/取消）、实现要点、示例与常见问题，方便开发者快速上手并排查问题。

## 目录

- 概述
- 输入/输出契约
- 公共 API 总表（方法摘要）
- 逐方法详细说明（参数 / 返回 / 行为 / 示例）
- 进阶主题：流式上传、下载与进度报告
- 并发与资源管理（通道、信号量、后台任务）
- 边界情况、性能与安全建议
- 使用示例
- 文件位置

---

## 概述

`Drx.Sdk.Network.V2.Web.HttpClient` 是对 `System.Net.Http.HttpClient` 的封装，提供：

- 统一的 `SendAsync` 接口，接受自定义 `HttpRequest` 并返回 `HttpResponse`。
- 多种便捷 `SendAsync` 重载（string/byte[]/object）以及 Get/Post/Put/Delete 快捷方法。
- 流式文件上传（支持进度回调与取消）和带 metadata 的上传封装。
- 下载文件到本地或写入目标流（支持进度与取消、原子替换目标文件）。
- 内部使用 Channel 与 Semaphore 控制并发并有后台处理任务。
- 字符集安全处理（`EnsureAsciiHeaderValue`）以避免不可 ASCII 头部导致的问题。

目标：为上层业务提供健壮、易用且对大文件友好的 HTTP 客户端操作。

---

## 输入/输出契约

- 输入：方法接收 URL/路径、可选 headers、query、body（string/bytes/object/Stream）、上传描述（UploadFileDescriptor）、进度回调、取消令牌等。
- 输出：统一使用 `HttpResponse` 返回（包含 StatusCode、Body、BodyBytes、Reason、可能的反序列化对象等）。
- 成功准则：请求在超时/取消/异常外能正确得到服务器响应并将响应体与字节保存到 `HttpResponse` 中。流式操作应在取消或异常时及时释放资源。
- 错误模式：异常会以相应异常类型抛出（如 `HttpRequestException`、`OperationCanceledException`）并记录日志；`SendAsync` 内部会捕获并封装错误为 `HttpResponse`（视具体实现）。

---

## 公共 API 总表

| 方法 | 签名（简化） | 描述 |
|---|---:|---|
| 构造函数 | `HttpClient()` | 使用默认 `System.Net.Http.HttpClient` 并启动后台请求处理通道。 |
| 构造函数 | `HttpClient(string baseAddress)` | 指定基础地址的构造函数。 |
| SendAsync (string body) | `Task<HttpResponse> SendAsync(HttpMethod method, string url, string? body = null, NameValueCollection? headers = null, NameValueCollection? query = null)` | 使用字符串 body 发送请求，默认以 JSON 发送。 |
| SendAsync (bytes) | `Task<HttpResponse> SendAsync(HttpMethod method, string url, byte[]? bodyBytes, ...)` | 使用字节数组 body。 |
| SendAsync (object) | `Task<HttpResponse> SendAsync(HttpMethod method, string url, object? bodyObject, ...)` | 使用对象 body（序列化为 JSON）。 |
| UploadFileAsync (path) | `Task<HttpResponse> UploadFileAsync(string url, string filePath, ...)` | 将本地文件上传到目标 URL（进度/取消）。 |
| UploadFileAsync (stream) | `Task<HttpResponse> UploadFileAsync(string url, Stream fileStream, string fileName, ...)` | 将给定流作为文件上传（不关闭传入流）。 |
| UploadFileWithMetadataAsync | `Task<HttpResponse> UploadFileWithMetadataAsync(string url, Stream fileStream, string fileName, object? metadata = null, ...)` | 上传流并附带 metadata（序列化为 JSON）。 |
| GetAsync/PostAsync/PutAsync/DeleteAsync | 快捷方法 | 简化调用，内部调用 `SendAsyncInternal`。 |
| SendAsync(HttpRequest) | `Task<HttpResponse> SendAsync(HttpRequest request)` | 接受自定义 `HttpRequest` 的统一接口，自动处理 upload 描述与 body 类型。 |
| DownloadFileAsync | `Task DownloadFileAsync(string url, string destPath, IProgress<long>? progress = null, CancellationToken ct = default)` | 下载远程文件至本地（支持原子替换与进度/取消）。 |
| DownloadToStreamAsync | `Task DownloadToStreamAsync(string url, Stream destination, IProgress<long>? progress = null, CancellationToken ct = default)` | 将远程文件流写入目标流（不会关闭目标流）。 |
| SetDefaultHeader | `void SetDefaultHeader(string name, string value)` | 设置内部 `HttpClient` 的默认请求头（会确保 ASCII）。 |
| SetTimeout | `void SetTimeout(TimeSpan timeout)` | 设置底层 HttpClient 的超时时间。 |
| DisposeAsync | `ValueTask DisposeAsync()` | 异步释放资源并停止后台处理。 |

内部/私有关键元素：
- `_requestChannel`：用于队列化请求任务，Bounded Channel，默认长度 100。
- `_semaphore`：并发控制，`MaxConcurrentRequests = 10`（可改动）。
- `ProgressableStreamContent`：用于包装上传流以上报进度（不会在 Dispose 时关闭底层流）。

---

## 逐方法详细说明

注意：下文参数与行为基于 `HttpClient.cs` 源码实现（摘要描述）。

### 构造函数 HttpClient()

签名：`HttpClient()`

行为：
- 创建内部 `System.Net.Http.HttpClient` 实例。
- 创建有界 `Channel<HttpRequestTask>`（容量 100，FullMode=Wait）。
- 创建 `SemaphoreSlim(MaxConcurrentRequests)`（默认 10）。
- 创建 `CancellationTokenSource` 并启动后台 `ProcessRequestsAsync` 任务以处理入队请求。

注意：如需自定义并发或 Channel 行为，需修改源码或提供外部封装。


### 构造函数 HttpClient(string baseAddress)

签名：`HttpClient(string baseAddress)`

行为：
- 将内部 `_httpClient.BaseAddress` 设为 `new Uri(baseAddress)`。
- 与默认构造类似初始化 Channel、Semaphore、CTS、后台任务。

异常：当 `baseAddress` 不是合法 URI 时会抛出 `ArgumentException`（或 `UriFormatException`）并记录日志。


### EnsureAsciiHeaderValue

签名：`private static string EnsureAsciiHeaderValue(string? value)`

描述：
- 确保 header 值仅含 ASCII 字符，若包含非 ASCII 字符则以 UTF-8 字节并将不可安全字符编码为 `%HH` 形式（类似百分号编码），保留常见安全字符。
- 用于避免某些 HTTP 实现/代理对非 ASCII header 值的拒绝或异常。

注意：并不会对所有字符做全面的 RFC5987 编码，仅用于提高兼容性。


### SendAsync 重载组

签名概览：
- `Task<HttpResponse> SendAsync(HttpMethod method, string url, string? body = null, NameValueCollection? headers = null, NameValueCollection? query = null)`
- `Task<HttpResponse> SendAsync(HttpMethod method, string url, byte[]? bodyBytes, ...)`
- `Task<HttpResponse> SendAsync(HttpMethod method, string url, object? bodyObject, ...)`

行为：
- 三个便捷入口最终调用 `SendAsyncInternal`，该方法统一构建 `HttpRequestMessage` 并执行网络请求，读取响应体为字符串与字节并构造 `HttpResponse` 返回。
- 当传入 `bodyObject` 时，会序列化为 JSON（默认 Content-Type 可能设置为 `application/json`，视实现而定）。

错误处理：
- `SendAsyncInternal` 捕获 `HttpRequestException`、`TaskCanceledException`（通常代表超时或取消）以及其它 `Exception` 并记录日志；返回或抛出视实现细节而定（源码片段显示会创建并返回 `HttpResponse` 或在某些情况抛出）。


### UploadFileAsync（文件路径）

签名：`Task<HttpResponse> UploadFileAsync(string url, string filePath, string fieldName = "file", NameValueCollection? headers = null, NameValueCollection? query = null, IProgress<long>? progress = null, CancellationToken cancellationToken = default)`

行为：
- 验证 `filePath` 存在，否则抛出 `FileNotFoundException`。
- 打开文件流并调用下一个重载 `UploadFileAsync(string, Stream, string, ...)`，**注意**：此方法会打开流并在方法结束后自动关闭（using）。


### UploadFileAsync（Stream）

签名：`Task<HttpResponse> UploadFileAsync(string url, Stream fileStream, string fileName, string fieldName = "file", ...)`

参数：
- `fileStream`：要上传的流。不能为 null，调用方负责管理该流的生命周期（方法结束后不会自动关闭该流，除非前一个重载打开并使用 using）。
- `fileName`：上传时使用的文件名（若为空则使用 `file`）。
- `progress`：IProgress<long> 回调，报告已上传字节数。
- `cancellationToken`：支持取消操作。

行为：
- 构造 `MultipartFormDataContent`，创建 `ProgressableStreamContent` 封装传入流以支持进度回调。
- 构建 `HttpRequestMessage`（POST）并使用 `_httpClient.SendAsync` 发送。
- 读取响应的文本与字节，并构造 `HttpResponse` 返回。

错误处理：
- 捕获异常并在日志中记录（实现片段中省略了具体逻辑）。

示例：
```csharp
using var fs = File.OpenRead("c:\\path\\to\\file.bin");
var resp = await client.UploadFileAsync("/upload", fs, "file.bin", progress: new Progress<long>(p => Console.WriteLine(p)));
```


### ProgressableStreamContent

类型：内部私有类，继承 `Stream`，用于包装上传源流。

目的：
- 在读取时上报进度（通过 IProgress<long>），并支持取消令牌。
- 不在 Dispose 时关闭底层流（便于调用方决定流的生命周期）。

实现要点：
- 在 `Read`/`ReadAsync` 中统计已读字节并调用 `_progress.Report(total)`。
- 抛出或响应传入的 `CancellationToken` 来实现取消。

注意：该类并非 `HttpContent`，源码将其作为底层流传给 `StreamContent`。


### SendAsyncInternal

签名（私有）：`Task<HttpResponse> SendAsyncInternal(HttpMethod method, string url, string? body, byte[]? bodyBytes, object? bodyObject, NameValueCollection? headers, NameValueCollection? query, HttpRequest.UploadFileDescriptor? uploadFile)`

行为摘要：
- 统一负责构建 `HttpRequestMessage`，处理 `uploadFile`（若存在则构造 multipart/form-data），设置 headers 与 content。
- 对于 bodyObject/bytes/body 设置合适的 Content 与 Content-Type。
- 发送请求并读取结果：`response.Content.ReadAsStringAsync()` 与 `ReadAsByteArrayAsync()`，构造并返回 `HttpResponse`（包含 BodyBytes、Body、StatusCode、ReasonPhrase）。
- 在发送/读取期间处理异常并将错误信息记录到日志。

特殊处理：上传场景时会使用 `ProgressableStreamContent` 以支持进度回调与取消。


### ProcessRequestsAsync / ExecuteRequestAsync

描述：
- 如果外部或内部使用 Channel 模式排队请求，后台任务会从 `_requestChannel` 读取 `HttpRequestTask` 并调用 `ExecuteRequestAsync` 执行实际请求。
- `ExecuteRequestAsync` 的逻辑类似 `SendAsyncInternal`，但以 `HttpRequestTask`（内部结构）为输入，执行后通过 `TaskCompletionSource` 返回 `HttpResponse` 给入队者。

目的：
- 提供请求排队与并发限制（通过 `_semaphore`）机制，避免同时发起过多并发请求导致资源耗尽或拥堵。


### GetAsync/PostAsync/PutAsync/DeleteAsync（快捷方法）

行为：
- 简化对 `SendAsync` 的调用，依据传入 body 类型分派到相应的 `SendAsync` 重载。
- 捕获异常并记录日志（源码中省略了具体返回逻辑，在调用时请注意可能的异常抛出）。


### DownloadFileAsync

签名：`Task DownloadFileAsync(string url, string destPath, IProgress<long>? progress = null, CancellationToken cancellationToken = default)`

行为要点：
- 发送 GET 请求获取远程内容流。若目标目录不存在则尝试创建。
- 将内容下载到临时文件（在同目录下），分块写入（默认缓冲 81920 字节），在完成后尝试原子替换目标文件（例如通过 File.Move / Replace 或先删除再重命名，源码中有更完整的原子替换逻辑）。
- 在下载期间调用 `progress.Report(totalDownloaded)`。
- 支持取消：若 `cancellationToken` 被触发会抛出 `OperationCanceledException` 并在 catch 中进行清理（删除临时文件等）。

错误与清理：
- 在异常或取消时尽量删除临时文件以避免磁盘垃圾。


### DownloadToStreamAsync

签名：`Task DownloadToStreamAsync(string url, Stream destination, IProgress<long>? progress = null, CancellationToken ct = default)`

行为：
- 将远程响应流逐块写入目标 `destination` 流。
- 不会在方法结束时关闭 `destination`，调用方负责清理。
- 支持进度回调与取消。

异常处理：
- 在取消时抛出 `OperationCanceledException`。


### SetDefaultHeader

签名：`void SetDefaultHeader(string name, string value)`

行为：
- 将 name/value 添加到 `_httpClient.DefaultRequestHeaders`，并通过 `EnsureAsciiHeaderValue` 处理值以保证兼容性。

注意：某些受限 headers（如 Content-Length）不可直接设置到 DefaultRequestHeaders，添加时需要 try/catch。


### SetTimeout

签名：`void SetTimeout(TimeSpan timeout)`

行为：设置 `_httpClient.Timeout`。


### DisposeAsync

签名：`ValueTask DisposeAsync()`

行为：
- 取消后台 token，等待 `_processingTask` 完成并 dispose `_httpClient`、释放 Semaphore 等。
- 尽量保证正在进行的请求被正确取消或完成并释放资源。

---

## 进阶主题：流式上传/下载与进度报告

1. 流式上传
- 使用 `UploadFileAsync(url, Stream, fileName, ...)` 并传入 `IProgress<long>` 来获取上传进度。
- 对于巨大文件，优先使用流上传以避免整块加载到内存。`ProgressableStreamContent` 会在读取源流时上报进度。
- 当上传时提供 `CancellationToken`，可在任意时刻取消上传请求。

2. 流式下载
- 使用 `DownloadFileAsync` 下载到文件或 `DownloadToStreamAsync` 写入任意流。
- 下载过程中应配合 `IProgress<long>` 和 `CancellationToken` 使用以增强用户体验与可控性。

3. multipart/form-data
- 上传实现使用 `MultipartFormDataContent` 且将文件部分包裹到 `StreamContent`（源流由 `ProgressableStreamContent` 包装）。
- 若需上传 metadata，使用 `UploadFileWithMetadataAsync`，该方法会将 metadata 序列化为 JSON 并作为表单字段 `metadata` 或 `metadata` 字段发送（源码示例中）。

---

## 并发与资源管理

- Channel: `_requestChannel`（bounded）用于将请求任务排队在后台处理，避免短时间内大量同步请求导致阻塞。
- Semaphore: `_semaphore`（MaxConcurrentRequests=10）控制同时执行请求的数量。
- 后台任务: `_processingTask` 负责读取 channel 并调用 `ExecuteRequestAsync`。

建议：在高并发场景可考虑调整 `MaxConcurrentRequests` 或对 Channel 容量做适当扩容；也可以对 `HttpClient` 实例做分级池化以提高吞吐。

---

## 边界情况、性能与安全建议

- Header 编码：通过 `EnsureAsciiHeaderValue` 编码 header 值中非 ASCII 字符以避免底层实现抛异常或异常行为。
- 大体积 Body：避免将大文件或大体积 body 以 object/string 形式传入；优先使用流接口上传以节约内存。
- Timeout 与取消：始终为可能的长请求设置合理超时并在调用层传入 `CancellationToken`；`HttpClient.Timeout` 可作为全局超时设置。
- 原子下载：`DownloadFileAsync` 在写入完成后以临时文件替换目标文件以减少部分写入导致的数据损坏。
- 异常与重试：对网络异常（如 transient 错误）可在上层实现重试逻辑，注意避免重复上传导致的幂等性问题。
- 安全传输：在包含敏感数据时请使用 HTTPS 并验证服务器证书（默认 `HttpClient` 验证证书）。

---

## 使用示例

1) 基本 GET
```csharp
var client = new Drx.Sdk.Network.V2.Web.HttpClient("https://api.example.com/");
var resp = await client.GetAsync("/health");
Console.WriteLine(resp.StatusCode);
```

2) POST JSON
```csharp
var payload = new { name = "alice", age = 30 };
var resp = await client.PostAsync("/users", payload);
Console.WriteLine(resp.Body);
```

3) 流式上传并显示进度
```csharp
using var fs = File.OpenRead("bigfile.bin");
var progress = new Progress<long>(p => Console.WriteLine($"uploaded: {p}"));
var resp = await client.UploadFileAsync("/upload", fs, "bigfile.bin", progress: progress, cancellationToken: CancellationToken.None);
```

4) 下载到文件并显示进度
```csharp
var p = new Progress<long>(d => Console.WriteLine($"downloaded: {d}"));
await client.DownloadFileAsync("https://cdn.example.com/big.zip", "C:\\downloads\\big.zip", p, CancellationToken.None);
```

---

## 常见问题与排查建议

- 超时或 TaskCanceledException：检查 `SetTimeout` 的设置、请求是否长时间阻塞、或是否传入了已取消的 `CancellationToken`。
- 上传进度不更新：确认使用了 `UploadFileAsync` 的 `Stream` 能正确读取，且 `ProgressableStreamContent` 的读取路径被触发（例如没有提前将流 Position 移至末尾）。
- 下载完成但文件损坏：检查是否在写入过程中出现异常导致临时文件未完整替换目标文件，确认磁盘空间与权限。
- 非 ASCII header 导致异常：请使用 `SetDefaultHeader` 或在单次请求中调用 `EnsureAsciiHeaderValue` 处理头值。

---

## 文件位置

- 源码：`Library/Environments/SDK/Drx.Sdk.Network/V2/Web/HttpClient.cs`
- 本文档：`Library/Environments/SDK/Drx.Sdk.Network/V2/Web/HttpClient.DEVGUIDE.md`

---

如果你希望，我可以：
- 将关键方法（如 `UploadFileAsync`、`DownloadFileAsync`）抽成更详细的序列图或流程图；
- 根据 本文档 为这些方法补充 XML 注释并在源码中插入（生成 Intellisense）；
- 为核心路径（上传/下载、multipart）添加小型集成测试示例并运行验证。

请选择下一步我应执行的操作。