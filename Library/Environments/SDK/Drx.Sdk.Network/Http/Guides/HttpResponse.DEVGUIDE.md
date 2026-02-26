# HttpResponse 开发人员指南

本文档为 `Drx.Sdk.Network.Http.ProtocolResponse` 提供详细说明（中文），覆盖构造器、属性、常用用法与资源释放策略，方便开发者构造与解析响应对象。

## 概述

`HttpResponse` 是框架中统一的响应载体，用于承载从服务器返回的信息（状态码、状态描述、响应头、响应体的多种表示：字符串、字节数组、对象或文件流）。该类型也用于框架内部的路由处理器返回值。

---

## 成员总览

| 成员 | 类型 | 含义 |
|---|---|---|
| StatusCode | int | HTTP 状态码（如 200, 404, 500）。 |
| StatusDescription | string | 状态描述，若未提供，构造器会使用本地默认描述。 |
| Headers | NameValueCollection | 响应头集合。 |
| Body | string | 响应体的文本表示（UTF-8）。 |
| BodyBytes | byte[] | 响应体的字节表示。 |
| BodyObject | object? | 响应体的对象表示（例如反序列化后的对象或待序列化对象）。 |
| Content | dynamic | 便捷的动态容器，默认为 ExpandoObject，用于放置常用视图（Text, Json, Object）。 |
| FileStream | Stream | 当响应为文件流时使用，框架会在后台分段写入并负责关闭流（大多数情况下调用方无需手动关闭来自 CreateFileResponse 的 FileStream）。 |
| BandwidthLimitKb | int | 可选带宽限制（KB/s），0 表示不限制。 |
| IsSuccessStatusCode | bool | 便捷属性，表示 2xx 范围内的成功状态。 |

---

## 构造器说明

1. `HttpResponse()`
   - 默认构造，初始化 `Headers` 与 `Content`（ExpandoObject）。

2. `HttpResponse(int statusCode, string body = "", string? statusDescription = null)`
   - 常用构造器，设置状态码与文本 Body。会把 Body 填入 `Content.Text`（尝试设置）。
   - 若 `statusDescription` 为 null，则使用内置 `GetDefaultStatusDescription`。

3. `HttpResponse(int statusCode, byte[] bodyBytes, string? statusDescription = null)`
   - 使用字节数组作为响应体，会尝试从字节数组推断并填充 `Content.Text`（UTF-8）。

4. `HttpResponse(int statusCode, object? bodyObject, string? statusDescription = null)`
   - 使用对象作为响应体，会把对象放入 `Content.Object`，并尝试序列化为 JSON 放入 `Content.Json`。

---

## FileStream 与 CreateFileResponse 的协作

- 在文件下载场景，通常通过 `HttpServer.CreateFileResponse` 创建一个 `HttpResponse`，其中 `FileStream` 已由框架打开并交由 `SendResponse` 的后台任务分段写入并在完成后关闭。
- 如果你在自定义处理器中直接构建 `HttpResponse` 并赋予 `FileStream`，请确保你了解谁负责关闭该流：
  - 推荐做法：在返回 `HttpResponse` 后不再在调用方显式关闭该流，让 `SendResponse` 的后台写入逻辑负责关闭（与框架一致）。

---

## Headers 与特殊头部处理

- `SendResponse` 在添加头时会跳过 `Content-Length`/`Transfer-Encoding` 等由框架管理的字段，调用方可通过 `Headers` 添加其他自定义头。
- 在添加头时某些头可能会因底层实现限制抛异常（例如非法字符），框架在 `SendResponse` 中已做 try/catch 并记录警告。

---

## 示例

1) 返回简单文本响应
```csharp
return new HttpResponse(200, "ok");
```

2) 返回 JSON 对象
```csharp
var resp = new HttpResponse(200, new { id = 1, name = "alice" });
// 发送时 SendResponse 会把 BodyObject 序列化为 JSON
```

3) 创建文件响应
```csharp
var resp = HttpServer.CreateFileResponse("C:\\files\\big.zip", fileName: "big.zip", contentType: "application/zip", bandwidthLimitKb: 1024);
return resp; // SendResponse 将在后台分块写出并关闭流
```

---

## 资源与 Dispose

- `HttpResponse` 实现了 `IDisposable`，但当前 `Dispose()` 方法为空占位。对于包含 `FileStream` 的响应，框架会在写入完成后关闭流；如果你在测试或特殊场景中手动创建并持有 `HttpResponse`，请在不需要时自行关闭/Dispose 其内部流（例如 `FileStream`）。

---

## 常见问题与排查建议

- 为什么 Body 与 BodyBytes 可能同时为空？答：响应可能使用 `FileStream` 分块写入，或服务器在某些情况下只设置了 BodyObject；检查 `FileStream` 与 `Headers`（如 Content-Type）。
- IsSuccessStatusCode 为 false 但 Body 包含错误描述：请优先检查 StatusCode 并记录服务器返回的 Body 用于诊断。

---

## 文件位置

- 源码：`Library/Environments/SDK/Drx.Sdk.Network/V2/Web/HttpResponse.cs`
- 本文档：`Library/Environments/SDK/Drx.Sdk.Network/V2/Web/HttpResponse.DEVGUIDE.md`

如需将 `Dispose()` 实现改为自动释放 `FileStream`（防止未关闭流泄露），可由我提交小幅修改并运行快速检查。