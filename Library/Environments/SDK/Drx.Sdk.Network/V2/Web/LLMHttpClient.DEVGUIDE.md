# LLMHttpClient.DEVGUIDE.md

## 概要
LLMHttpClient 是一个面向大语言模型（LLM）场景的 HTTP 客户端扩展，位于 `Drx.Sdk.Network.V2.Web` 命名空间。它继承自项目内部的 `HttpClient`（框架版），并提供：

- 便捷的 POST 请求封装（带默认 model / temperature）
- 基于流式响应的逐块文本读取（IAsyncEnumerable<string>），适用于 SSE / chunked 等实时输出场景
- 若干本地辅助方法用于构建请求体、拼接 URL 及确保头部 ASCII 安全

注：本指南以中文编写，保留源代码中的标识符与签名不翻译。

## 输入 / 输出契约
- 输入（主要公开方法）：
  - `StreamAsync(string url, object? body = null, NameValueCollection? headers = null, NameValueCollection? query = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)`
    - url: 请求 URL 或相对路径
    - body: 可为 string（当作 input）或任意对象（将和默认字段合并），最终会被序列化为 JSON
    - headers/query: 额外请求头或查询参数
    - timeout: 可选超时（方法内部使用 CTS 管控）
    - 返回：IAsyncEnumerable<string>，按接收到的字节块逐步返回文本片段

  - `SendLLMRequestAsync(string url, object? body = null, NameValueCollection? headers = null, NameValueCollection? query = null)`
    - 同上，返回框架内的 `HttpResponse`（同步/异步由基类实现）

- 成功条件：HTTP 响应状态为 2xx（`StreamAsync` 会在接收失败或取消时结束枚举），`SendLLMRequestAsync` 返回的 `HttpResponse` 表示成功状态
- 错误模式：
  - 网络或服务器错误会抛出异常或在 `StreamAsync` 中导致提前结束（调用方应处理 OperationCanceledException / 取消令牌）
  - 非 ASCII 的头部值会被编码为安全的 ASCII 表达

## 公共 API 概览
| 名称 | 签名 | 描述 | 返回 | 错误 |
|---|---:|---|---|---|
| StreamAsync | IAsyncEnumerable<string> StreamAsync(string url, object? body = null, NameValueCollection? headers = null, NameValueCollection? query = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default) | 以流式方式 POST 请求并逐块产出响应文本 | IAsyncEnumerable<string> | 网络错误、取消、状态码非成功（EnsureSuccessStatusCode 会抛异常） |
| SendLLMRequestAsync | Task<HttpResponse> SendLLMRequestAsync(string url, object? body = null, NameValueCollection? headers = null, NameValueCollection? query = null) | 发送非流式请求并返回框架内 HttpResponse | Task<HttpResponse> | 基类 PostAsync 的错误（序列化/网络等） |
| BuildDefaultPayload (internal) | object BuildDefaultPayload(object? body) | 构建默认 payload（加入 model/temperature），并合并传入 body | object | — |
| BuildUrlLocal (internal) | string BuildUrlLocal(string url, NameValueCollection? query) | 将 query 拼接到 url 上 | string | — |
| EnsureAsciiHeaderValue (internal) | string EnsureAsciiHeaderValue(string? value) | 将 header 值编码为安全 ASCII 表达 | string | — |

> 说明：本类还使用了 `System.Net.Http.HttpClient` 的本地实例来支持每次调用自定义超时与取消。

## 方法详述
### StreamAsync(...)
- 参数
  - `url` (string) — 请求 URL 或相对路径
  - `body` (object?) — string 时作为 `input` 字段；若为 IDictionary<string, object> 会合并到默认 payload；其他对象会放到 `data` 字段
  - `headers`、`query` (NameValueCollection?) — 额外头/查询
  - `timeout` (TimeSpan?) — 可选超时
  - `cancellationToken` — 取消控制
- 返回
  - `IAsyncEnumerable<string>` — 按读取到的文本片段异步返回
- 行为
  - 构建请求 URL（含 query）
  - 合并默认 payload（`model`=`gpt-3.5-turbo`, `temperature`=0.7）
  - 使用独立 HttpClient 发起 POST（ResponseHeadersRead）以支持流式读取
  - 逐次读取 response stream 的字节块，按 UTF-8 解码为字符串并 yield 返回
  - 在取消或超时时终止枚举
- 示例（基本使用） — Not verified
```csharp
// 假设 client 为 LLMHttpClient 的实例
await foreach (var chunk in client.StreamAsync("/api/llm/generate", "你好，生成一段介绍语。", timeout: TimeSpan.FromSeconds(30)))
{
    Console.Write(chunk);
}
```
- 错误/边界
  - 当 server 返回非 2xx 时，response.EnsureSuccessStatusCode() 会抛出异常
  - 大文本分片可能被任意切分，调用方需要在上层合并/按行处理

### SendLLMRequestAsync(...)
- 参数与行为
  - 构建默认 payload 并调用基类的 `PostAsync`（基类负责序列化为 JSON 并返回框架内 `HttpResponse`）
- 示例（同步调用） — Not verified
```csharp
var resp = await client.SendLLMRequestAsync("/api/llm/complete", new { prompt = "你好" });
if (resp.StatusCode == 200)
{
    Console.WriteLine("成功");
}
```

### BuildDefaultPayload(...)
- 内部方法，用于合并默认字段（model/temperature）和传入 body。注意：当 body 为对象非 IDictionary 时，会放到 `data` 字段而非直接展开。

### BuildUrlLocal(...) 与 EnsureAsciiHeaderValue(...)
- BuildUrlLocal：将 NameValueCollection 转为 URL 查询字符串并拼接（对键值进行 Uri.Escape）
- EnsureAsciiHeaderValue：将非 ASCII 字符编码为 %XX 形式以保证 header 可用

## 高级使用与实现细节
- 流式读取：目前实现以固定 8KB 缓冲读取，使用 UTF8 解码器逐块解码，末尾会 flush decoder。
- 超时策略：若传入 timeout，会创建 CTS 并使用 cts.CancelAfter(timeout)，同时本地 HttpClient.Timeout 被设为 Infinite，由 CTS 控制超时。
- 头部安全：为避免非 ASCII 值导致 header 添加失败，类中实现了 EnsureAsciiHeaderValue 作为兼容方案。

## 并发与资源管理
- 每次 `StreamAsync` 会创建一个新的 `System.Net.Http.HttpClient` 实例并在结束时 Dispose（注意：频繁创建 HttpClient 可能影响性能，若你的应用高并发建议重用实例并另行实现超时/取消策略）。
- 返回的 IAsyncEnumerable 在枚举结束或异常时会释放底层流与缓冲。

## 常见边界、性能与安全考虑
- 大流量场景下：频繁创建 HttpClient 会触发端口耗尽或影响 DNS 缓存，建议长期运行服务使用单例 HttpClient 并通过 HttpClientFactory 管理。
- 响应分片：后端若按事件流（SSE）发送，调用方需基于 chunk 内容做拼接与解析（当前实现只负责按接收块返回原始字符串片段）。
- 敏感数据：若 headers 中存在敏感信息，EnsureAsciiHeaderValue 并不会加密，仅转义不可见字符，请确保在安全通道（HTTPS）下传输。

## 使用示例（包含错误处理） — Not verified
```csharp
var client = new LLMHttpClient("https://llm.example.com");
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
try
{
    await foreach (var chunk in client.StreamAsync("/v1/stream", new { prompt = "写一段诗" }, timeout: TimeSpan.FromSeconds(30), cancellationToken: cts.Token))
    {
        Console.Write(chunk);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("请求被取消或超时");
}
catch (Exception ex)
{
    Console.WriteLine($"请求失败: {ex}");
}
```

## 故障排查 / FAQ
- Q: 收到的文本断成很多片怎么办？
  - A: 这是正常现象，网络或编码切分会导致分片。上层应在必要时做拼接（例如按换行或事件边界）。
- Q: 遇到 header 添加失败异常？
  - A: 检查 header 值是否包含控制字符；此类通过 EnsureAsciiHeaderValue 会进行编码，但某些后端仍可能拒绝非法头。

## 文件位置
- 源代码：`d:\Code\Library\Environments\SDK\Drx.Sdk.Network\V2\Web\LLMHttpClient.cs`
- 文档（本文件）：`d:\Code\Library\Environments\SDK\Drx.Sdk.Network\V2\Web\LLMHttpClient.DEVGUIDE.md`

## 下一步建议
1. 在项目中添加单元测试（模拟流式响应）验证 `StreamAsync` 的分片/拼接逻辑。
2. 考虑使用 `HttpClientFactory` 重构以避免高并发下的 HttpClient 创建成本。
3. 在示例中提供更完整的拼接示例及 SSE 解析工具函数。

## 质量门（快速校验）
- Build: `dotnet build DRX.Environment.sln`（建议运行，若仓库中存在其他编译错误则需独立处理）
- Tests: 建议新增一个小型示例程序在 `Examples/` 中演示 StreamAsync
