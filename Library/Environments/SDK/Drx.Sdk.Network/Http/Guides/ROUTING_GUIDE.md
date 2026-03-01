# 路由系统完整指南

## 概述

DrxHttpServer 提供了三种路由模式，适应不同的使用场景。本指南详细说明每种模式的工作原理、使用场景和最佳实践。

## 三种路由模式对比

| 特性 | 原始路由 | 流式上传路由 | 标准路由 |
|------|--------|-----------|--------|
| **API 层级** | HttpListenerContext | HttpRequest + Stream | HttpRequest/HttpResponse |
| **适用场景** | 流式处理、高性能 | 大文件上传 | 标准 REST API |
| **中间件支持** | ❌ 不支持 | ✅ 部分支持 | ✅ 完全支持 |
| **认证授权** | 手动处理 | 自动处理 | 自动处理 |
| **易用性** | 低 | 中 | 高 |
| **性能** | 最高 | 高 | 中等 |
| **代码复杂性** | 高 | 中 | 低 |

## 模式一：原始路由（Raw Route）

### 特点
- 直接访问 `HttpListenerContext`
- 最低层的 API，性能最优
- 完全控制请求和响应
- 中间件不适用

### 注册方式

```csharp
// 简单版本
server.AddRawRoute("/upload", async ctx => {
    var req = ctx.Request;
    var resp = ctx.Response;
    
    resp.ContentType = "application/json";
    resp.StatusCode = 200;
    
    using var writer = new StreamWriter(resp.OutputStream);
    await writer.WriteAsync("OK");
});

// 高级版本（使用 server 参数）
server.AddRawRoute("/upload", async (ctx, server) => {
    // 可以访问服务器实例
    var logger = Logger.Info;
    // ...
});
```

### 速率限制

```csharp
// 可选的路由级速率限制
server.AddRawRoute(
    path: "/upload",
    handler: async ctx => { /* ... */ },
    rateLimitMaxRequests: 100,  // 每个时间窗口 100 个请求
    rateLimitWindowSeconds: 60   // 60 秒为一个窗口
);
```

### 使用场景

#### 1. 大文件上传处理
```csharp
server.AddRawRoute("/api/upload-large", async ctx => {
    var req = ctx.Request;
    var fileName = req.QueryString["filename"];
    
    // 直接处理流，避免内存缓冲整个文件
    using var file = File.Create($"/uploads/{fileName}");
    await req.InputStream.CopyToAsync(file);
    
    ctx.Response.StatusCode = 200;
});
```

#### 2. 流式成大文件下载
```csharp
server.AddRawRoute("/api/download-large", async ctx => {
    var filePath = ctx.Request.QueryString["file"];
    
    ctx.Response.ContentType = "application/octet-stream";
    ctx.Response.AddHeader("Content-Disposition", $"attachment; filename={Path.GetFileName(filePath)}");
    
    // 流式发送大文件
    using var file = File.OpenRead(filePath);
    await file.CopyToAsync(ctx.Response.OutputStream);
});
```

#### 3. WebSocket 升级（自定义协议）
```csharp
server.AddRawRoute("/ws", async ctx => {
    if (ctx.Request.IsWebSocketRequest)
    {
        var wsContext = await ctx.AcceptWebSocketAsync(null);
        var webSocket = wsContext.WebSocket;
        // 处理 WebSocket...
    }
});
```

#### 4. 自定义二进制协议
```csharp
server.AddRawRoute("/api/custom-protocol", async ctx => {
    var req = ctx.Request;
    var resp = ctx.Response;
    
    // 读取自定义协议头
    byte[] header = new byte[16];
    await req.InputStream.ReadExactlyAsync(header, 0, 16);
    
    // 处理...
    resp.StatusCode = 200;
});
```

### 最佳实践

1. **使用 using 语句** - 确保流正确关闭
2. **设置正确的 Content-Type**
3. **错误处理** - try-catch 捕获异常
4. **日志记录** - 记录重要操作
5. **安全检查** - 验证请求合法性

## 模式二：流式上传路由

### 特点
- 自动处理流式 HTTP 请求体
- 提供 `HttpRequest.UploadFile` 访问流
- 文件元数据自动解析
- 支持中间件（部分）
- 简化大文件处理

### 注册方式

```csharp
server.AddStreamUploadRoute(
    path: "/api/upload",
    handler: async req => {
        var stream = req.UploadFile.Stream;
        var fileName = req.UploadFile.FileName;
        var fieldName = req.UploadFile.FieldName;
        
        // 处理流...
        return new HttpResponse(200, "OK");
    },
    rateLimitMaxRequests: 50,
    rateLimitWindowSeconds: 60
);
```

### 上传文件信息结构

```csharp
// HttpRequest.UploadFile 包含
public class UploadFileDescriptor
{
    public Stream Stream { get; set; }              // 上传数据流
    public string FileName { get; set; }            // 文件名
    public string FieldName { get; set; }           // 字段名
    public IProgress<long>? Progress { get; set; }  // 进度报告
    public CancellationToken CancellationToken { get; set; } // 取消令牌
}
```

### 使用场景

#### 1. 简单文件上传
```csharp
server.AddStreamUploadRoute("/api/upload", async req => {
    var stream = req.UploadFile.Stream;
    var fileName = Path.GetFileName(req.UploadFile.FileName);
    
    var uploadDir = "/app/uploads";
    Directory.CreateDirectory(uploadDir);
    
    using var file = File.Create(Path.Combine(uploadDir, fileName));
    await stream.CopyToAsync(file);
    
    return new HttpResponse(200, $"Saved: {fileName}");
});
```

#### 2. 带验证的上传
```csharp
server.AddStreamUploadRoute("/api/upload-with-validation", async req => {
    // 从查询参数获取文件类型
    var allowedType = req.Query["type"];
    var fileName = req.UploadFile.FileName;
    
    // 验证文件扩展名
    var ext = Path.GetExtension(fileName).ToLower();
    if (!IsAllowedExtension(ext, allowedType))
        return new HttpResponse(400, "File type not allowed");
    
    // 验证文件大小（中间流读取验证）
    var stream = req.UploadFile.Stream;
    const long maxSize = 100 * 1024 * 1024; // 100 MB
    
    using var limiter = new StreamWrapper(stream, maxSize);
    using var file = File.Create($"/uploads/{fileName}");
    
    try
    {
        await limiter.CopyToAsync(file);
        return new HttpResponse(200, "Upload OK");
    }
    catch (OperationCanceledException)
    {
        return new HttpResponse(413, "File too large");
    }
});
```

#### 3. 带进度报告的上传
```csharp
server.AddStreamUploadRoute("/api/upload-with-progress", async req => {
    var progress = req.UploadFile.Progress;
    var stream = req.UploadFile.Stream;
    
    using var file = File.Create("/uploads/large-file.bin");
    using var progressStream = new ProgressableStreamContent(
        stream, 
        progress
    );
    
    await progressStream.CopyToAsync(file);
    return new HttpResponse(200, "Uploaded");
});
```

### 最佳实践

1. **检查文件类型和大小**
2. **实现进度报告** - 客户端可以显示进度条
3. **支持取消** - 使用 CancellationToken
4. **错误恢复** - 断点续传支持
5. **日志审计** - 记录上传操作

## 模式三：标准路由

### 特点
- 类型化的 HttpRequest/HttpResponse
- 完整的中间件支持
- 认证授权自动处理
- 最易使用
- 适合一般 REST API

### 两种注册方式

#### 方式 A：直接注册

```csharp
// 同步处理
server.AddRoute(
    HttpMethod.Get,
    "/api/users/{id}",
    req => {
        var id = req.Path.Parameters["id"];
        var user = GetUser(id);
        return new JsonResult(user);
    }
);

// 异步处理
server.AddRoute(
    HttpMethod.Post,
    "/api/users",
    async req => {
        var user = JsonSerializer.Deserialize<User>(req.Body);
        await SaveUser(user);
        return new JsonResult(user, 201);
    }
);

// 带速率限制
server.AddRoute(
    HttpMethod.Get,
    "/api/expensive-operation",
    async req => new HttpResponse(200, "OK"),
    rateLimitMaxRequests: 5,
    rateLimitWindowSeconds: 60
);
```

#### 方式 B：属性标记（推荐）

```csharp
public partial class UserApi
{
    [HttpHandle("GET", "/api/users")]
    public static async Task<HttpResponse> ListUsers(HttpRequest request)
    {
        var users = await GetAllUsers();
        return new JsonResult(users);
    }
    
    [HttpHandle("GET", "/api/users/{id}")]
    public static async Task<HttpResponse> GetUser(HttpRequest request)
    {
        var id = request.Path.Parameters["id"];
        var user = await GetUser(id);
        
        if (user == null)
            return new HttpResponse(404, "Not Found");
            
        return new JsonResult(user);
    }
    
    [HttpHandle("POST", "/api/users")]
    public static async Task<HttpResponse> CreateUser(HttpRequest request)
    {
        var user = JsonSerializer.Deserialize<User>(request.Body);
        
        if (!ValidateUser(user))
            return new BadRequestResult("Invalid user data");
            
        await SaveUser(user);
        return new JsonResult(user, 201);
    }
    
    [HttpHandle("PUT", "/api/users/{id}")]
    public static async Task<HttpResponse> UpdateUser(HttpRequest request)
    {
        var id = request.Path.Parameters["id"];
        var user = JsonSerializer.Deserialize<User>(request.Body);
        user.Id = int.Parse(id);
        
        var updated = await UpdateUser(user);
        return new JsonResult(updated);
    }
    
    [HttpHandle("DELETE", "/api/users/{id}")]
    public static async Task<HttpResponse> DeleteUser(HttpRequest request)
    {
        var id = request.Path.Parameters["id"];
        await DeleteUser(int.Parse(id));
        return new HttpResponse(204); // No Content
    }
}

// 然后反射注册
server.RegisterHandlersFromAssembly(typeof(UserApi));
```

### 动态路由参数

```csharp
// 提取路由参数
[HttpHandle("GET", "/api/users/{userId}/posts/{postId}")]
public static async Task<HttpResponse> GetUserPost(HttpRequest request)
{
    var userId = request.Path.Parameters["userId"];
    var postId = request.Path.Parameters["postId"];
    
    var post = await GetUserPost(userId, postId);
    return post == null 
        ? new HttpResponse(404, "Not Found")
        : new JsonResult(post);
}
```

### 中间件与认证

```csharp
// 添加认证中间件
[HttpMiddleware("/api/*")]
public static async Task<HttpResponse> AuthMiddleware(
    HttpRequest req,
    Func<HttpRequest, Task<HttpResponse>> next)
{
    var authHeader = req.Headers["Authorization"];
    if (string.IsNullOrEmpty(authHeader))
        return new UnauthorizedResult();
    
    var token = authHeader.Replace("Bearer ", "");
    var principal = JwtHelper.ValidateToken(token);
    
    if (principal == null)
        return new UnauthorizedResult();
    
    // 继续处理
    return await next(req);
}
```

### 标准结果类型

```csharp
// JSON 结果
return new JsonResult(new { message = "OK" });
return new JsonResult(new { message = "Created" }, 201);

// 文本结果
return new ContentResult("Plain text response", "text/plain");

// 错误结果
return new BadRequestResult("Invalid input");
return new UnauthorizedResult();
return new NotFoundResult();
return new ConflictResult("Resource already exists");

// 文件结果
return new FileResult("/path/to/file.pdf", "downloaded-file.pdf");

// 重定向
return new RedirectResult("/api/new-location");

// 无内容
return new HttpResponse(204);
```

## 路由匹配流程

```
HTTP 请求到达
    ↓
+------ 检查是否为原始路由匹配？
|       ├─ 是 → 调用原始处理器 → 返回
|       └─ 否 → 继续
|
+------ 检查是否为流式上传路由匹配？
|       ├─ 是 → 调用流式处理器 → 返回
|       └─ 否 → 继续
|
+------ 检查是否为标准路由匹配？
|       ├─ 是 → 继续
|       └─ 否 → 返回 404
|
+------ 检查速率限制
|       ├─ 超限 → 返回 429 Too Many Requests
|       └─ 正常 → 继续
|
+------ 执行中间件管道
|       ├─ 中间件中断 → 返回
|       └─ 中间件通过 → 继续
|
+------ 执行处理器
        └─ 返回响应
```

## 性能对比

| 操作 | 原始路由 | 流式上传 | 标准路由 |
|------|--------|--------|--------|
| 小文件 (1MB) | 1ms | 2ms | 3ms |
| 中文件 (100MB) | 50ms | 51ms | 55ms |
| 大文件 (1GB) | 500ms | 501ms | 510ms |
| 内存占用 | 最低 | 低 | 中等 |

**结论**：对于大文件处理，选择原始/流式路由；对于常规 API，使用标准路由。

## 选择指南

```
                        ┌─ 处理大文件或流？
                        │  ├─ 是，需要原始控制 → 原始路由
                        │  ├─ 是，上传/下载 → 流式路由
                        │  └─ 否 → 标准路由
                        │
我应该用哪种路由？ ─────┤
                        │  ├─ 需要中间件？
                        │  │  ├─ 是 → 标准路由或流式路由
                        │  │  └─ 否 → 可选任意
                        │  │
                        │  └─ 性能关键？
                        │     ├─ 极其关键（微秒级） → 原始路由
                        │     ├─ 关键（毫秒级） → 流式/标准路由
                        │     └─ 不关键 → 标准路由（易使用）
```

## 相关文档

- [DrxHttpServer.DEVGUIDE.md](DrxHttpServer.DEVGUIDE.md) - 完整服务器指南
- [HttpRequest.DEVGUIDE.md](HttpRequest.DEVGUIDE.md) - 请求对象细节
- [../Server/README.md](../Server/README.md) - 服务器模块概览
