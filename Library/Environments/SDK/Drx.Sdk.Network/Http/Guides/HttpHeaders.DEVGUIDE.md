# HttpHeaders 常量说明（开发者指南）

`Drx.Sdk.Network.Http.ProtocolHeaders` 是一个轻量的静态类，定义了框架中用于文件上传场景的若干自定义 HTTP 头部常量。本文件说明这些常量的含义、使用场景与编码约定。

## 常量列表

| 常量 | 值 | 含义 |
|---|---|---|
| X_FILE_NAME | "X-File-Name" | 直接传递文件名的自定义头，用于在服务端知道上传文件的原始名称。通常在流式上传时由客户端设置（若表单中未包含文件名或使用 Raw 上传）。 |
| X_FILE_NAME_BASE64 | "X-File-Name-Base64" | 当文件名包含不可打印字符或需要在头部安全传输时，客户端可将文件名先以 UTF-8 编码后再做 Base64 编码，并在该头中传输，服务端收到后解码为原始文件名。 |
| X_FILE_NAME_ENCODED | "X-File-Name-Encoded" | 类似于 Base64，但用于其他自定义编码方案（如 URL-encode 或百分号编码）。框架或应用层可选择如何解码。 |

## 使用建议

- 当通过 `HttpClient.UploadFileWithMetadataAsync`、`HttpClient.UploadFileAsync` 或自定义客户端进行 Raw/流式上传时，若文件名中可能包含非 ASCII 字符或特殊字符，建议客户端：
  1. 通过 `X-File-Name-Base64` 发送 Base64(encoded UTF-8) 的文件名；或
  2. 通过 `X-File-Name-Encoded` 发送 URL/百分号编码后的名称。
- 服务端（例如 `HttpServer` 或业务处理器）在处理上传请求时应优先检查：
  1. `Headers[HttpHeaders.X_FILE_NAME]`（若存在，则直接使用）；
  2. `Headers[HttpHeaders.X_FILE_NAME_BASE64]`（若存在，先 Base64-decode 再用 UTF-8 解码）；
  3. `Headers[HttpHeaders.X_FILE_NAME_ENCODED]`（若存在，根据约定解码）。

## 编码 / 解码示例（服务端）

C# 示例：
```csharp
string fileName = request.Headers[HttpHeaders.X_FILE_NAME];
if (string.IsNullOrEmpty(fileName))
{
    var b64 = request.Headers[HttpHeaders.X_FILE_NAME_BASE64];
    if (!string.IsNullOrEmpty(b64))
    {
        var bytes = Convert.FromBase64String(b64);
        fileName = System.Text.Encoding.UTF8.GetString(bytes);
    }
    else
    {
        var enc = request.Headers[HttpHeaders.X_FILE_NAME_ENCODED];
        if (!string.IsNullOrEmpty(enc))
        {
            fileName = Uri.UnescapeDataString(enc);
        }
    }
}

if (string.IsNullOrEmpty(fileName)) fileName = "file";
```

## 兼容性与安全

- 头部大小/字符限制：HTTP 头部长度与服务器/代理有关，尽量避免发送极长的文件名（若必须，使用 Base64 并注意代理可能会拦截）。
- 验证：服务端在使用文件名做文件系统操作前，应对文件名进行净化（例如移除控制字符、剪裁路径分隔符），`HttpServer` 中提供了 `SanitizeFileNameForHeader` 工具用于该目的。
- 注入风险：不要直接把头部值拼接进命令或文件路径，始终做白名单或净化处理。

---

## 文件位置

- 源码：`Library/Environments/SDK/Drx.Sdk.Network/V2/Web/HttpHeaders.cs`
- 本文档：`Library/Environments/SDK/Drx.Sdk.Network/V2/Web/HttpHeaders.DEVGUIDE.md`

如需我将这些解码逻辑示例整合进 `HttpClient` / `HttpServer` 的文档示例中或在 `HttpServer` 中调用 `SanitizeFileNameForHeader` 的示例使用处贴上代码注释，我可以继续修改源码注释并运行静态检查。