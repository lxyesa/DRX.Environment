# DrxHttpClient.DEVGUIDE.md

## Overview
DrxHttpClient 是 DRX.Environment 框架中的 HTTP 客户端类，用于发送各类异步 HTTP 请求。它封装了常见的 HTTP 操作，支持以字符串、字节数组或对象作为请求体发送，支持流式文件上传（带进度和取消），并支持下载文件到本地或写入目标流。客户端使用内部的 System.Net.Http.HttpClient，并通过通道和信号量管理并发请求（最大并发数为 10）。所有注释和文档均使用中文。

## Inputs / Outputs Contract
- **Inputs**:
  - `url`: 目标 URL 或相对路径（字符串）。
  - `method`: HTTP 方法（如 GET, POST, PUT, DELETE）。
  - `body`: 请求体，可为字符串、字节数组或对象（对象将被序列化为 JSON）。
  - `headers`: 可选的请求头集合（NameValueCollection）。
  - `query`: 可选的查询参数集合（NameValueCollection）。
  - `uploadFile`: 可选的上传文件描述符（HttpRequest.UploadFileDescriptor），用于流式上传。
  - `progress`: 可选的进度回调（IProgress<long>），报告上传/下载字节数。
  - `cancellationToken`: 可选的取消令牌（CancellationToken）。
- **Outputs**:
  - `HttpResponse`: 包含状态码（int）、响应体（string）、响应字节（byte[]）、反序列化对象（object，若为 JSON）、响应头（NameValueCollection）和原因短语（string）。
- **Success criteria**: HTTP 状态码在 200-299 范围内，响应成功解析。
- **Error modes / Exceptions**:
  - `HttpRequestException`: 网络错误。
  - `TaskCanceledException`: 请求超时或被取消。
  - `ArgumentException`: 无效参数（如无效 URI）。
  - `FileNotFoundException`: 上传文件不存在。
  - `ArgumentNullException`: 必需参数为 null。

## Public API Summary
| Name | Signature | Description | Returns | Errors |
|---|---|---|---|---|
| DrxHttpClient() | DrxHttpClient() | 默认构造函数，启动请求处理通道。 | void | Exception |
| DrxHttpClient(string) | DrxHttpClient(string baseAddress) | 指定基础地址的构造函数。 | void | ArgumentException, Exception |
| SendAsync(HttpMethod, string, string?, NameValueCollection?, NameValueCollection?) | Task<HttpResponse> SendAsync(HttpMethod method, string url, string? body, NameValueCollection? headers, NameValueCollection? query) | 发送请求，使用字符串作为请求体。 | Task<HttpResponse> | HttpRequestException, TaskCanceledException, Exception |
| SendAsync(HttpMethod, string, byte[]?, NameValueCollection?, NameValueCollection?) | Task<HttpResponse> SendAsync(HttpMethod method, string url, byte[]? bodyBytes, NameValueCollection? headers, NameValueCollection? query) | 发送请求，使用字节数组作为请求体。 | Task<HttpResponse> | HttpRequestException, TaskCanceledException, Exception |
| SendAsync(HttpMethod, string, object?, NameValueCollection?, NameValueCollection?) | Task<HttpResponse> SendAsync(HttpMethod method, string url, object? bodyObject, NameValueCollection? headers, NameValueCollection? query) | 发送请求，使用对象作为请求体（序列化为 JSON）。 | Task<HttpResponse> | HttpRequestException, TaskCanceledException, Exception |
| UploadFileAsync(string, string, string, NameValueCollection?, NameValueCollection?, IProgress<long>?, CancellationToken) | Task<HttpResponse> UploadFileAsync(string url, string filePath, string fieldName, NameValueCollection? headers, NameValueCollection? query, IProgress<long>? progress, CancellationToken cancellationToken) | 上传本地文件。 | Task<HttpResponse> | FileNotFoundException, HttpRequestException, TaskCanceledException, Exception |
| UploadFileAsync(string, Stream, string, string, NameValueCollection?, NameValueCollection?, IProgress<long>?, CancellationToken) | Task<HttpResponse> UploadFileAsync(string url, Stream fileStream, string fileName, string fieldName, NameValueCollection? headers, NameValueCollection? query, IProgress<long>? progress, CancellationToken cancellationToken) | 上传流作为文件。 | Task<HttpResponse> | ArgumentNullException, HttpRequestException, TaskCanceledException, Exception |
| GetAsync(string, NameValueCollection?, NameValueCollection?) | Task<HttpResponse> GetAsync(string url, NameValueCollection? headers, NameValueCollection? query) | 发送 GET 请求。 | Task<HttpResponse> | Exception |
| PostAsync(string, object?, NameValueCollection?, NameValueCollection?) | Task<HttpResponse> PostAsync(string url, object? body, NameValueCollection? headers, NameValueCollection? query) | 发送 POST 请求。 | Task<HttpResponse> | Exception |
| PutAsync(string, object?, NameValueCollection?, NameValueCollection?) | Task<HttpResponse> PutAsync(string url, object? body, NameValueCollection? headers, NameValueCollection? query) | 发送 PUT 请求。 | Task<HttpResponse> | Exception |
| DeleteAsync(string, NameValueCollection?, NameValueCollection?) | Task<HttpResponse> DeleteAsync(string url, NameValueCollection? headers, NameValueCollection? query) | 发送 DELETE 请求。 | Task<HttpResponse> | Exception |
| DownloadFileAsync(string, string, IProgress<long>?, CancellationToken) | Task DownloadFileAsync(string url, string destPath, IProgress<long>? progress, CancellationToken cancellationToken) | 下载文件到本地路径。 | Task | OperationCanceledException, Exception |
| DownloadToStreamAsync(string, Stream, IProgress<long>?, CancellationToken) | Task DownloadToStreamAsync(string url, Stream destination, IProgress<long>? progress, CancellationToken cancellationToken) | 下载到目标流。 | Task | ArgumentNullException, OperationCanceledException, Exception |
| SetDefaultHeader(string, string) | void SetDefaultHeader(string name, string value) | 设置默认请求头。 | void | Exception |
| SetTimeout(TimeSpan) | void SetTimeout(TimeSpan timeout) | 设置超时时间。 | void | Exception |
| AutoManageCookies | bool AutoManageCookies { get; set; } | 是否自动管理 Cookie（跟踪 Set-Cookie 并在请求时发送 Cookie）。默认 true。 | bool | - |
| SessionCookieName | string SessionCookieName { get; set; } | 会话 Cookie 名称，默认 "session_id"，可按服务端配置调整。 | string | - |
| SessionHeaderName | string? SessionHeaderName { get; set; } | 可选：会话 header 名称（例如 "X-Session-Id"），设置后会在请求中自动写入会话 id（若存在且调用方未显式设置该 header）。 | string? | - |
| GetSessionId(string? forUrl = null) | string? GetSessionId(string? forUrl = null) | 从内部 CookieContainer 获取当前会话 id（基于 SessionCookieName）。 | string? | - |
| SetSessionId(string sessionId, string? domain = null, string path = "/") | void SetSessionId(string sessionId, string? domain = null, string path = "/") | 将会话 id 写入内部 CookieContainer，便于后续请求携带该 cookie。 | void | - |
| ClearCookies() | void ClearCookies() | 清空内部 CookieContainer 中的 cookie（可能有并发/生效域限制，慎用）。 | void | - |
| ExportCookies() | string ExportCookies() | 导出 BaseAddress 域下的 cookie 为 JSON 字符串，便于持久化或传递。 | string | - |
| ImportCookies(string json) | void ImportCookies(string json) | 从 JSON 导入 cookie（兼容 ExportCookies 的格式），将 cookie 加入内部 CookieContainer。 | void | - |
| SendAsync(HttpRequest) | Task<HttpResponse> SendAsync(HttpRequest request) | 统一的发送接口，使用 HttpRequest 对象。 | Task<HttpResponse> | ArgumentNullException, Exception |
| UploadFileWithMetadataAsync(string, string, object?, NameValueCollection?, IProgress<long>?, CancellationToken) | Task<HttpResponse> UploadFileWithMetadataAsync(string url, string filePath, object? metadata, NameValueCollection? headers, IProgress<long>? progress, CancellationToken cancellationToken) | 上传文件并附带 metadata。 | Task<HttpResponse> | FileNotFoundException, Exception |
| UploadFileWithMetadataAsync(string, Stream, string, object?, NameValueCollection?, IProgress<long>?, CancellationToken) | Task<HttpResponse> UploadFileWithMetadataAsync(string url, Stream fileStream, string fileName, object? metadata, NameValueCollection? headers, IProgress<long>? progress, CancellationToken cancellationToken) | 上传流并附带 metadata。 | Task<HttpResponse> | ArgumentNullException, Exception |
| DisposeAsync() | ValueTask DisposeAsync() | 异步释放资源。 | ValueTask | - |

## Methods (detailed)

### DrxHttpClient()
- **Parameters**: None.
- **Returns**: void.
- **Behavior**: 初始化内部 HttpClient，创建请求通道和信号量，启动后台请求处理任务。
- **Example**:
```csharp
var client = new DrxHttpClient();
```
- **Notes / Edge cases**: 并发请求数限制为 10。

### DrxHttpClient(string baseAddress)
- **Parameters**:
  - `baseAddress` (string): 基础地址，必须为有效 URI。
- **Returns**: void.
- **Behavior**: 初始化 HttpClient 并设置 BaseAddress。
- **Example**:
```csharp
var client = new DrxHttpClient("https://api.example.com");
```
- **Notes / Edge cases**: 若 baseAddress 无效，抛出 ArgumentException。

### SendAsync(HttpMethod, string, string?, NameValueCollection?, NameValueCollection?)
- **Parameters**:
  - `method` (HttpMethod): HTTP 方法。
  - `url` (string): URL。
  - `body` (string?): 请求体。
  - `headers` (NameValueCollection?): 请求头。
  - `query` (NameValueCollection?): 查询参数。
- **Returns**: Task<HttpResponse>.
- **Behavior**: 发送请求，请求体作为 JSON 发送。
- **Example**:
```csharp
var response = await client.SendAsync(HttpMethod.Post, "https://api.example.com/data", "{\"key\":\"value\"}");
if (response.StatusCode == 200) { /* success */ }
```
- **Notes / Edge cases**: 头值自动转义为 ASCII。

### AutoManageCookies / SessionCookieName / SessionHeaderName

- **AutoManageCookies**: 布尔值，控制客户端是否使用内部的 CookieContainer 自动管理 cookie（包括接收响应的 Set-Cookie 并在后续请求时发送对应 cookie）。默认 true。对于需要由外部独立管理 cookie 的场景可设置为 false。
- **SessionCookieName**: 字符串，表示会话 cookie 的名称（例如服务端默认使用 "session_id"）。客户端用于查找/写入会话 cookie 的名称，默认 "session_id"。
- **SessionHeaderName**: 可选字符串，如果设置（例如 "X-Session-Id"），客户端在发送请求时会在 header 中注入当前会话 id（当且仅当该 header 未被调用方显式设置时），以兼容某些以 header 传递会话令牌的服务端实现。

注意：AutoManageCookies/SessionHeaderName 的实现依赖于内部的 HttpClientHandler + CookieContainer，默认只针对 `BaseAddress` 或显式 domain 生效；若要在没有 BaseAddress 的情况下可靠管理多域 cookie，请显式用 `SetSessionId`/`ImportCookies` 注入或调整为在调用层管理会话 token。

### GetSessionId(string? forUrl = null)

- **Purpose**: 从内部 CookieContainer 中读取当前会话 id，基于 `SessionCookieName`。若指定 `forUrl` 则优先在该 URL 对应域下查找。
- **Returns**: 找到则返回会话 id，否则返回 null。
- **Edge cases**: 当 `AutoManageCookies` 为 false 时返回 null；在无 `BaseAddress` 且未指定 `forUrl` 时无法读取 cookie。

示例：
```csharp
var sid = client.GetSessionId();
if (sid != null) Console.WriteLine("当前会话: " + sid);
```

### SetSessionId(string sessionId, string? domain = null, string path = "/")

- **Purpose**: 将会话 id 写入内部 CookieContainer，用于后续请求自动携带该 cookie。
- **Parameters**:
  - `sessionId`: 会话值（必填）。
  - `domain`: 可选域（例如 "https://api.example.com" 或 "api.example.com"）。若不填且 `BaseAddress` 存在会使用 `BaseAddress`。
  - `path`: cookie 的 path，默认 "/"。

示例：
```csharp
client.SetSessionId("abcdef12345", "api.example.com");
```

### ClearCookies()

- **Purpose**: 清空内部 CookieContainer 中的 cookie（注意实现为替换容器引用的保守策略，可能不会影响已有 HttpClient 已绑定的 handler 行为）。
- **注意**: 在高并发或对 cookie 生效域有严格要求的场景请谨慎使用。

### ExportCookies() / ImportCookies(string json)

- **Purpose**: 导出/导入 cookie，便于持久化或跨进程传递。
- **格式**: JSON 数组，每项包含 Name/Value/Domain/Path/Expires/Secure/HttpOnly 字段。`ExportCookies` 目前仅导出 `BaseAddress` 域下的 cookie。
- **示例**:
```csharp
var json = client.ExportCookies();
// 程序重启后
client.ImportCookies(json);
```

注意：.NET 的 CookieContainer 对域集合的访问有限，若需要导出跨多个域的全部 cookie，需要在应用层维护映射或使用更复杂的实现（反射风险较高，不建议在库层默认采用）。

### SendAsync(HttpMethod, string, byte[]?, NameValueCollection?, NameValueCollection?)
- **Parameters**: 类似上述，bodyBytes 为字节数组。
- **Returns**: Task<HttpResponse>.
- **Behavior**: 发送字节数组作为请求体。
- **Example**:
```csharp
var data = Encoding.UTF8.GetBytes("test");
var response = await client.SendAsync(HttpMethod.Post, "https://api.example.com/upload", data);
```
- **Notes / Edge cases**: 适用于二进制数据。

### SendAsync(HttpMethod, string, object?, NameValueCollection?, NameValueCollection?)
- **Parameters**: 类似，bodyObject 为对象。
- **Returns**: Task<HttpResponse>.
- **Behavior**: 对象序列化为 JSON。
- **Example**:
```csharp
var obj = new { Name = "Test" };
var response = await client.SendAsync(HttpMethod.Post, "https://api.example.com/obj", obj);
```
- **Notes / Edge cases**: 使用 System.Text.Json 序列化。

### UploadFileAsync(string, string, string, NameValueCollection?, NameValueCollection?, IProgress<long>?, CancellationToken)
- **Parameters**:
  - `url` (string): 上传 URL。
  - `filePath` (string): 本地文件路径。
  - `fieldName` (string): 表单字段名。
  - `headers` (NameValueCollection?): 请求头。
  - `query` (NameValueCollection?): 查询参数。
  - `progress` (IProgress<long>?): 进度回调。
  - `cancellationToken` (CancellationToken): 取消令牌。
- **Returns**: Task<HttpResponse>.
- **Behavior**: 上传本地文件，使用 multipart/form-data。
- **Example**:
```csharp
var progress = new Progress<long>(bytes => Console.WriteLine($"Uploaded: {bytes}"));
var response = await client.UploadFileAsync("https://api.example.com/upload", "C:\\file.txt", "file", null, null, progress, CancellationToken.None);
```
- **Notes / Edge cases**: 文件必须存在，否则 FileNotFoundException。

### UploadFileAsync(string, Stream, string, string, NameValueCollection?, NameValueCollection?, IProgress<long>?, CancellationToken)
- **Parameters**: 类似，fileStream 为流。
- **Returns**: Task<HttpResponse>.
- **Behavior**: 上传流作为文件。
- **Example**:
```csharp
using var stream = File.OpenRead("file.txt");
var response = await client.UploadFileAsync("https://api.example.com/upload", stream, "file.txt", "file");
```
- **Notes / Edge cases**: 调用方负责流生命周期。

### GetAsync(string, NameValueCollection?, NameValueCollection?)
- **Parameters**:
  - `url` (string): URL。
  - `headers` (NameValueCollection?): 请求头。
  - `query` (NameValueCollection?): 查询参数。
- **Returns**: Task<HttpResponse>.
- **Behavior**: 发送 GET 请求。
- **Example**:
```csharp
var response = await client.GetAsync("https://api.example.com/data");
```
- **Notes / Edge cases**: 无请求体。

### PostAsync(string, object?, NameValueCollection?, NameValueCollection?)
- **Parameters**: 类似，body 为对象。
- **Returns**: Task<HttpResponse>.
- **Behavior**: 发送 POST 请求，支持多种 body 类型。
- **Example**:
```csharp
var response = await client.PostAsync("https://api.example.com/data", new { Key = "Value" });
```
- **Notes / Edge cases**: body 可为 string, byte[], object 或 null。

### PutAsync(string, object?, NameValueCollection?, NameValueCollection?)
- **Parameters**: 类似 POST。
- **Returns**: Task<HttpResponse>.
- **Behavior**: 发送 PUT 请求。
- **Example**:
```csharp
var response = await client.PutAsync("https://api.example.com/data/1", new { Name = "Updated" });
```
- **Notes / Edge cases**: 类似 POST。

### DeleteAsync(string, NameValueCollection?, NameValueCollection?)
- **Parameters**: 类似 GET。
- **Returns**: Task<HttpResponse>.
- **Behavior**: 发送 DELETE 请求。
- **Example**:
```csharp
var response = await client.DeleteAsync("https://api.example.com/data/1");
```
- **Notes / Edge cases**: 无请求体。

### DownloadFileAsync(string, string, IProgress<long>?, CancellationToken)
- **Parameters**:
  - `url` (string): 文件 URL。
  - `destPath` (string): 本地路径。
  - `progress` (IProgress<long>?): 进度回调。
  - `cancellationToken` (CancellationToken): 取消令牌。
- **Returns**: Task.
- **Behavior**: 下载文件到本地，尝试原子替换。
- **Example**:
```csharp
await client.DownloadFileAsync("https://example.com/file.zip", "C:\\downloads\\file.zip", progress, CancellationToken.None);
```
- **Notes / Edge cases**: 若文件存在，尝试 File.Replace，否则 File.Move。

### DownloadToStreamAsync(string, Stream, IProgress<long>?, CancellationToken)
- **Parameters**: 类似，destination 为流。
- **Returns**: Task.
- **Behavior**: 下载到目标流。
- **Example**:
```csharp
using var dest = File.Create("file.zip");
await client.DownloadToStreamAsync("https://example.com/file.zip", dest, progress, CancellationToken.None);
```
- **Notes / Edge cases**: 调用方负责关闭流。

### SetDefaultHeader(string, string)
- **Parameters**:
  - `name` (string): 头名。
  - `value` (string): 头值。
- **Returns**: void.
- **Behavior**: 设置默认请求头。
- **Example**:
```csharp
client.SetDefaultHeader("Authorization", "Bearer token");
```
- **Notes / Edge cases**: 值自动转义。

### SetTimeout(TimeSpan)
- **Parameters**:
  - `timeout` (TimeSpan): 超时时间。
- **Returns**: void.
- **Behavior**: 设置 HttpClient 超时。
- **Example**:
```csharp
client.SetTimeout(TimeSpan.FromSeconds(30));
```
- **Notes / Edge cases**: 默认超时为 HttpClient 默认。

### SendAsync(HttpRequest)
- **Parameters**:
  - `request` (HttpRequest): 请求对象。
- **Returns**: Task<HttpResponse>.
- **Behavior**: 统一接口，支持上传文件。
- **Example**:
```csharp
var req = new HttpRequest { Url = "https://api.example.com", Method = "POST", Body = "data" };
var response = await client.SendAsync(req);
```
- **Notes / Edge cases**: 若 Body 为流，自动构建 UploadFile。

### UploadFileWithMetadataAsync(string, string, object?, NameValueCollection?, IProgress<long>?, CancellationToken)
- **Parameters**: 类似 UploadFileAsync，metadata 为对象。
- **Returns**: Task<HttpResponse>.
- **Behavior**: 上传文件并附加 metadata。
- **Example**:
```csharp
var metadata = new { Description = "Test file" };
var response = await client.UploadFileWithMetadataAsync("https://api.example.com/upload", "file.txt", metadata);
```
- **Notes / Edge cases**: metadata 序列化为 JSON。

### UploadFileWithMetadataAsync(string, Stream, string, object?, NameValueCollection?, IProgress<long>?, CancellationToken)
- **Parameters**: 类似，fileStream 为流。
- **Returns**: Task<HttpResponse>.
- **Behavior**: 上传流并附加 metadata。
- **Example**:
```csharp
using var stream = File.OpenRead("file.txt");
var response = await client.UploadFileWithMetadataAsync("https://api.example.com/upload", stream, "file.txt", metadata);
```
- **Notes / Edge cases**: 类似上述。

### DisposeAsync()
- **Parameters**: None.
- **Returns**: ValueTask.
- **Behavior**: 释放资源，停止后台任务。
- **Example**:
```csharp
await client.DisposeAsync();
```
- **Notes / Edge cases**: 异步释放。

## Advanced Topics
- **流式上传**: 支持大文件上传，通过 ProgressableStreamContent 报告进度，不关闭底层流。
- **并发管理**: 使用 SemaphoreSlim 限制并发请求数为 10，防止资源耗尽。
- **取消和超时**: 支持 CancellationToken 和 HttpClient.Timeout。
- **原子下载**: DownloadFileAsync 使用临时文件和 File.Replace 实现原子替换。
- **Metadata 附加**: 在 multipart 上传中，body/bodyObject/bodyBytes 作为 metadata 字段附加。

## Concurrency & Resource Management
- **并发**: 最大 10 个并发请求，通过信号量控制。
- **资源**: HttpClient 单例，请求通道容量 100，信号量管理。
- **释放**: 实现 IAsyncDisposable，正确释放 HttpClient 和 CTS。
- **流管理**: 上传/下载流由调用方管理，方法不关闭传入流。

## Edge Cases, Performance & Security
- **Edge Cases**: 无效 URI 抛出异常；文件不存在抛出 FileNotFoundException；响应非 JSON 时 BodyObject 为 null。
- **Performance**: 使用异步 I/O，缓冲区 81920 字节；进度报告不阻塞。
- **Security**: 头值转义为 ASCII；不存储敏感数据；支持 HTTPS（通过 HttpClient）。

## Usage Examples
### 基本 GET 请求
```csharp
using var client = new DrxHttpClient();
var response = await client.GetAsync("https://httpbin.org/get");
Console.WriteLine(response.Body);
```

### 上传文件
```csharp
using var client = new DrxHttpClient();
var progress = new Progress<long>(b => Console.WriteLine($"Progress: {b}"));
var response = await client.UploadFileAsync("https://httpbin.org/post", "example.txt", "file", null, null, progress, CancellationToken.None);
```

### 下载文件
```csharp
using var client = new DrxHttpClient();
await client.DownloadFileAsync("https://httpbin.org/image/png", "downloaded.png");
```

## Troubleshooting / FAQ
- **Q: 请求超时？** A: 检查 SetTimeout() 或网络连接。
- **Q: 上传失败？** A: 确保文件存在，检查权限。
- **Q: 响应解析失败？** A: 检查 Content-Type，若非 JSON，BodyObject 为 null。
- **Q: 并发过多？** A: 减少并发请求，或增加 MaxConcurrentRequests（但需修改源码）。
- **Q: 进度不报告？** A: 确保提供 IProgress<long> 实例。

## File Location
- **Path**: `Library/Environments/SDK/Drx.Sdk.Network/V2/Web/DrxHttpClient.cs`
- **Namespace**: `Drx.Sdk.Network.V2.Web`
- **Dependencies**: `Drx.Sdk.Shared`, `System.Net.Http`, `Newtonsoft.Json`

## Next Steps
- **Tests**: 添加单元测试覆盖成功和错误路径，使用 xUnit 或 NUnit。
- **XML Docs**: 生成 XML 文档注释。
- **Examples**: 创建示例应用在 `Examples/` 目录。
- **Benchmarks**: 使用 BenchmarkDotNet 测试性能。
- **CI**: 在构建管道中运行测试和 lint。