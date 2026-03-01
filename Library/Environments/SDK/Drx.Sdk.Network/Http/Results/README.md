# Results 目录 - HTTP 操作结果

## 概述
Results 目录定义了各种 HTTP 操作的结果类型，用于简化处理器的返回值表达。

## 文件说明

### BytesResult.cs
**字节结果**
- 返回二进制数据的结果类
- 特点：
  - 支持大文件返回
  - 自定义 MIME 类型
  - 内存高效

**使用场景：**
- 返回图片、视频等二进制数据
- 返回压缩文件
- 返回生成的 PDF 等

**示例：**
```csharp
[HttpHandle("GET", "/api/image/{id}")]
public static async Task<HttpResponse> GetImage(HttpRequest request)
{
    var imageBytes = await File.ReadAllBytesAsync("image.png");
    return new BytesResult(imageBytes, "image/png");
}
```

### 其他常见结果类型 (在 HttpActionResults.cs 中)

#### OkResult
- 返回 200 OK 状态
- 可选的响应体

#### BadRequestResult
- 返回 400 Bad Request 状态
- 用于请求格式错误

#### NotFoundResult
- 返回 404 Not Found 状态
- 资源不存在

#### UnauthorizedResult
- 返回 401 Unauthorized 状态
- 用户未认证

#### ForbiddenResult
- 返回 403 Forbidden 状态
- 用户无权限

#### ConflictResult
- 返回 409 Conflict 状态
- 资源冲突

#### InternalServerErrorResult
- 返回 500 Internal Server Error 状态
- 服务器内部错误

#### RedirectResult
- 返回 3xx 重定向
- 支持 301、302、307 等

#### FileResult
- 返回文件下载
- 支持流式传输
- 可设置文件名

#### JsonResult
- 返回 JSON 数据
- 自动序列化对象

#### ContentResult
- 返回文本内容
- 支持自定义 Content-Type

#### StreamResult
- 返回流数据
- 支持流式传输

## 使用模式

### 返回 JSON
```csharp
[HttpHandle("GET", "/api/users/{id}")]
public static async Task<HttpResponse> GetUser(HttpRequest request)
{
    var user = new { Id = 1, Name = "Alice" };
    return new JsonResult(user);
}
```

### 返回文件
```csharp
[HttpHandle("GET", "/api/download/{fileName}")]
public static async Task<HttpResponse> Download(HttpRequest request)
{
    var filePath = request.Path.Parameters["fileName"];
    return new FileResult(filePath, "File.zip");
}
```

### 返回错误
```csharp
[HttpHandle("POST", "/api/users")]
public static async Task<HttpResponse> CreateUser(HttpRequest request)
{
    if (string.IsNullOrEmpty(request.Body))
        return new BadRequestResult("Body is required");
    // ...
}
```

### 返回重定向
```csharp
[HttpHandle("GET", "/api/old-path")]
public static async Task<HttpResponse> OldEndpoint(HttpRequest request)
{
    return new RedirectResult("/api/new-path");
}
```

### 返回文本
```csharp
[HttpHandle("GET", "/api/status")]
public static async Task<HttpResponse> GetStatus(HttpRequest request)
{
    return new ContentResult("OK", "text/plain");
}
```

## IActionResult 接口

所有结果类都实现 `IActionResult` 接口：

```csharp
public interface IActionResult
{
    Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server);
}
```

这允许自定义结果类型的异步执行。

## 与其他模块的关系

- **与 Protocol 的关系** - 结果转换为 HttpResponse
- **与 Server 的关系** - 服务器执行 IActionResult
- **与 Serialization 的关系** - JsonResult 使用序列化
- **与 Configs 的关系** - 处理器返回结果对象

## 最佳实践

1. **使用专门的结果类** - 而不是手动创建 HttpResponse
2. **适当的状态码** - 使用正确的 HTTP 状态码
3. **一致的格式** - API 返回格式保持一致
4. **文档** - 为 API 文档化返回类型
5. **错误处理** - 返回有意义的错误信息

## 相关文档
- 参见 [../Protocol/HttpResponse.DEVGUIDE.md](../Guides/HttpResponse.DEVGUIDE.md) 了解响应细节
- 参见 [../Server/README.md](../Server/README.md) 了解处理器编写
