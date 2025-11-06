# HttpRequest 开发人员指南

本文档针对 `Drx.Sdk.Network.V2.Web.HttpRequest` 提供详细说明（中文），用于帮助开发者理解请求对象的字段契约、使用场景和注意事项。

## 概述

`HttpRequest` 是框架内部与外部路由处理器之间的标准请求载体。它抽象出常见的 HTTP 元数据（方法、路径、查询、头）、请求体的多种表示（字符串、字节数组、对象、流），以及 multipart/form-data 的表单/文件描述，使路由处理器可以统一处理不同来源的请求。

该类型设计为轻量 POCO，调用者/框架负责流生命周期管理（例如 UploadFileDescriptor.Stream）。

---

## 公共成员总览

| 成员 | 类型 | 含义 |
|---|---|---|
| Method | string | HTTP 方法（如 "GET", "POST"）。 |
| Path | string | 请求路径（通常为绝对路径部分）。 |
| Url | string? | 可选的完整 URL，代理或客户端模式下使用。 |
| Query | NameValueCollection | 查询参数集合。 |
| Headers | NameValueCollection | 请求头集合。 |
| Body | string? | 请求体（文本形式）。 |
| BodyBytes | byte[]? | 请求体的原始字节（若已读取为字节）。 |
| BodyObject | object? | 请求体的对象表示（可序列化/反序列化）。 |
| BodyJson | string | 原始或缓存的 JSON 字符串（如果适用）。 |
| ExtraDataPack | byte[] | 附加的二进制数据包（自定义用途）。 |
| RemoteEndPoint | IPEndPoint | 远端终结点信息（来源）。 |
| PathParameters | Dictionary<string,string> | 路由模板提取的命名参数（例如 `/users/{id}`）。 |
| Form | NameValueCollection | 表单字段集合（multipart 或 x-www-form-urlencoded）。 |
| UploadFile | UploadFileDescriptor? | 上传文件描述，含流、文件名、字段名等。 |
| ListenerContext | HttpListenerContext | 原始 HttpListenerContext（仅服务端实现时设置）。 |

---

## UploadFileDescriptor（上传文件描述）

用于表达在 multipart 或流式上传场景下的文件元信息：

字段：
- `Stream` (Stream)：待上传/接收的流。调用方负责管理该流的生命周期（通常框架不会在内部关闭）。
- `FileName` (string)：文件名，用于 Content-Disposition/保存时提示。
- `FieldName` (string)：表单字段名，默认值为 "file"。
- `Progress` (IProgress<long>?)：可选的上传进度回调（报告已处理字节数）。
- `CancellationToken`：可选取消令牌，用于在上传过程中取消操作。

注意：当 `HttpRequest` 是由 `HttpServer` 构造（从 HttpListener）时：
- 对于非 multipart 的流式上传，框架会将 `HttpListenerRequest.InputStream` 直接赋值给 `UploadFile.Stream`（避免将整个请求体读入内存）。
- 对于 multipart/form-data，框架会逐个解析 section；文件部分通常被读入 `MemoryStream` 再赋值给 `UploadFile.Stream`（此为设计 trade-off，便于处理表单内容）。

---

## 方法：SetDefaultHeader / SetDefaultHeaders

这些方法用于在 `Headers` 中按需添加默认头（仅当目标键不存在时添加），方便在处理流程中合并默认配置而不覆盖已有值。

签名与行为：
- `void SetDefaultHeader(string name, string value)`：如果 `Headers[name] == null`，则添加该键值。
- `void SetDefaultHeaders(NameValueCollection defaults)`：遍历并逐个调用添加（仅在不存在时）。
- `void SetDefaultHeaders(IDictionary<string,string> defaults)`：同上，接受字典类型。

注意：这些方法内部有 try/catch，以避免在异常头值或并发情形下抛出。

---

## 示例

1) 在路由中读取表单/文件：
```csharp
server.AddStreamUploadRoute("/upload/", async req => {
    var name = req.Form["username"];
    var upload = req.UploadFile;
    if (upload?.Stream != null) {
        // 保存上传流
        var resp = HttpServer.SaveUploadFile(req, "C:\\uploads", upload.FileName ?? "upload_");
        return resp;
    }
    return new HttpResponse(400, "no file");
});
```

2) 在中间件设置默认头：
```csharp
req.SetDefaultHeader("X-Request-Id", Guid.NewGuid().ToString());
req.SetDefaultHeaders(new NameValueCollection { {"X-App", "myapp"} });
```

---

## 生命周期与并发注意事项

- `UploadFileDescriptor.Stream` 的生命周期由创建该 `HttpRequest` 的代码负责管理（通常是 `HttpServer` 或上层业务）；不要在处理器中随意关闭外部提供的流，除非明确文档说明。
- `HttpRequest` 本身为短生命周期对象，建议在处理完成后丢弃并释放任何由你创建的附加流/资源。

---

## 常见问题与排查建议

- multipart 文件为何被读入内存？答：框架当前实现为了简化表单解析，会将文件分区读入 MemoryStream；对于非常大的文件请使用 `AddRawRoute` 或确保调用链支持流式处理。
- PathParameters 为空：确认路由模板是否正确注册以及请求路径与模板匹配（`{param}` 不会匹配 `/`）。
- UploadFile.Stream 为 null：说明请求中没有文件部分或解析失败，检查请求的 Content-Type 是否正确。

---

## 文件位置

- 源码：`Library/Environments/SDK/Drx.Sdk.Network/V2/Web/HttpRequest.cs`
- 本文档：`Library/Environments/SDK/Drx.Sdk.Network/V2/Web/HttpRequest.DEVGUIDE.md`

如需我把这些字段转为 XML 注释插入源码（便于 IDE 提示），我可以继续完成该步骤并运行一次快速静态检查。