# 中间件系统完整指南

## 概述

DrxHttpServer 的中间件系统是一个灵活的请求/响应处理管道，受 ASP.NET Core 中间件模型启发。中间件在请求处理前后执行，可以用于认证、授权、日志、错误处理等通用逻辑。

## 核心概念

### 中间件是什么？

中间件是一个处理 HTTP 请求和响应的组件。它可以：
- ✅ 在请求到达处理器前进行预处理（如认证检查）
- ✅ 执行请求后进行后处理（如响应修改）
- ✅ 中止请求处理（如返回错误）
- ✅ 委托给下一个中间件或最终处理器

### 中间件管道

```
请求进入
  ↓
中间件 1（前）
  ↓
中间件 2（前）
  ↓
...
  ↓
最终处理器
  ↓
中间件 2（后）
  ↓
中间件 1（后）
  ↓
响应返回
```

## 三种中间件类型

### 类型 1：原始上下文中间件

处理 `HttpListenerContext` 级别，最低层。

#### 注册方式

```csharp
// 最简单的形式
server.AddMiddleware(async ctx => {
    Console.WriteLine($"Request: {ctx.Request.HttpMethod} {ctx.Request.Url}");
    // 无法修改请求/响应内容，只能进行日志记录等
});

// 带 server 参数
server.AddMiddleware(async (ctx, server) => {
    var logger = server.Logger;
    logger.Info($"Processing request");
});
```

#### 使用场景

1. **简单日志记录**
```csharp
server.AddMiddleware(async ctx => {
    var sw = Stopwatch.StartNew();
    
    // 请求进入
    Logger.Info($">> {ctx.Request.HttpMethod} {ctx.Request.Url}");
    
    // 执行完成后被调用（需要自定义）
    sw.Stop();
    Logger.Info($"<< {ctx.Response.StatusCode} ({sw.ElapsedMilleconds}ms)");
});
```

2. **请求头检查**
```csharp
server.AddMiddleware(async ctx => {
    var userAgent = ctx.Request.Headers["User-Agent"];
    if (string.IsNullOrEmpty(userAgent))
    {
        ctx.Response.StatusCode = 400;
        await using var writer = new StreamWriter(ctx.Response.OutputStream);
        await writer.WriteAsync("User-Agent header required");
    }
});
```

#### 限制
- ❌ 无法访问或修改 `HttpRequest` 和 `HttpResponse` 对象
- ❌ 无法中止中间件链并继续（只能返回响应）
- ❌ 难以进行高级请求/响应修改

### 类型 2：请求级中间件

处理 `HttpRequest`/`HttpResponse` 对象，最常用。

#### 注册方式

```csharp
// 简单形式
server.AddMiddleware(async (req, next) => {
    // 请求前处理
    var response = await next(req);  // 传递给下一个中间件或处理器
    // 响应后处理
    return response;
});

// 带 server 参数
server.AddMiddleware(async (req, next, server) => {
    // 可以访问服务器实例
    return await next(req);
});
```

#### 使用场景

1. **认证中间件**
```csharp
server.AddMiddleware(async (req, next) => {
    // 检查认证
    var authHeader = req.Headers["Authorization"];
    
    if (string.IsNullOrEmpty(authHeader))
    {
        return new UnauthorizedResult();
    }
    
    // 验证令牌
    var token = authHeader.Replace("Bearer ", "");
    var principal = JwtHelper.ValidateToken(token);
    
    if (principal == null)
    {
        return new HttpResponse(401, "Invalid token");
    }
    
    // 继续处理
    return await next(req);
}, path: "/api/*");
```

2. **请求验证中间件**
```csharp
server.AddMiddleware(async (req, next) => {
    // 验证请求体
    if (req.Method == "POST" && string.IsNullOrEmpty(req.Body))
    {
        return new BadRequestResult("Body required");
    }
    
    return await next(req);
}, path: "/api/*");
```

3. **日志中间件**
```csharp
server.AddMiddleware(async (req, next) => {
    var sw = Stopwatch.StartNew();
    
    Logger.Info($">> {req.Method} {req.Path}");
    if (!string.IsNullOrEmpty(req.Body))
    {
        Logger.Debug($"   Body: {req.Body.Substring(0, 100)}");
    }
    
    var response = await next(req);
    
    sw.Stop();
    Logger.Info($"<< {response.StatusCode} ({sw.ElapsedMilleconds}ms)");
    
    return response;
});
```

4. **错误处理中间件（全局）**
```csharp
server.AddMiddleware(async (req, next) => {
    try
    {
        return await next(req);
    }
    catch (FileNotFoundException ex)
    {
        Logger.Warn($"File not found: {ex.Message}");
        return new NotFoundResult();
    }
    catch (ArgumentException ex)
    {
        Logger.Warn($"Argument error: {ex.Message}");
        return new BadRequestResult(ex.Message);
    }
    catch (Exception ex)
    {
        Logger.Error($"Unhandled exception: {ex}");
        return new HttpResponse(500, "Internal Server Error");
    }
}, path: null); // null = 全局中间件
```

5. **速率限制中间件**
```csharp
private static readonly Dictionary<string, int> RequestCounts = new();
private static readonly Timer CleanupTimer = new(_ => RequestCounts.Clear(), 
    null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

server.AddMiddleware(async (req, next) => {
    var ip = req.ClientAddress.Ip ?? "unknown";
    
    if (!RequestCounts.ContainsKey(ip))
        RequestCounts[ip] = 0;
    
    RequestCounts[ip]++;
    
    if (RequestCounts[ip] > 100) // 60 秒内 100 个请求
    {
        return new HttpResponse(429, "Too Many Requests");
    }
    
    return await next(req);
}, path: "/api/*");
```

6. **响应修改中间件**
```csharp
server.AddMiddleware(async (req, next) => {
    // 调用下一个中间件/处理器
    var response = await next(req);
    
    // 修改响应
    response.Headers.Add("X-Custom-Header", "custom-value");
    response.Headers.Add("X-Request-Time", DateTime.UtcNow.ToString());
    
    return response;
});
```

#### 最佳实践

1. **异常处理** - 始终用 try-catch 包裹
2. **日志记录** - 记录关键事件
3. **条件处理** - 只在需要时处理
4. **性能考虑** - 避免重操作
5. **下一个委托** - 始终调用 `next(req)` 除非要中止

### 类型 3：属性标记中间件

通过属性标记进行声明式注册，由框架反射注册。

#### 定义

```csharp
[HttpMiddleware(path: "/api/*", priority: 100)]
public static async Task<HttpResponse> MyMiddleware(
    HttpRequest req,
    Func<HttpRequest, Task<HttpResponse>> next)
{
    // 实现中间件逻辑
    return await next(req);
}
```

#### 完整示例

```csharp
public partial class ApiMiddlewares
{
    // 最高优先级 - 最先执行
    [HttpMiddleware(path: null, priority: 0)]
    public static async Task<HttpResponse> ErrorHandling(
        HttpRequest req,
        Func<HttpRequest, Task<HttpResponse>> next)
    {
        try
        {
            return await next(req);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in {req.Path}: {ex}");
            return new HttpResponse(500, "Internal Server Error");
        }
    }

    [HttpMiddleware(path: "/api/*", priority: 100)]
    public static async Task<HttpResponse> Authentication(
        HttpRequest req,
        Func<HttpRequest, Task<HttpResponse>> next)
    {
        var token = req.Headers["Authorization"]?.Replace("Bearer ", "");
        if (string.IsNullOrEmpty(token) || JwtHelper.ValidateToken(token) == null)
        {
            return new UnauthorizedResult();
        }
        return await next(req);
    }

    [HttpMiddleware(path: "/api/*", priority: 200)]
    public static async Task<HttpResponse> Logging(
        HttpRequest req,
        Func<HttpRequest, Task<HttpResponse>> next)
    {
        var sw = Stopwatch.StartNew();
        var response = await next(req);
        sw.Stop();
        
        Logger.Info($"{req.Method} {req.Path} -> {response.StatusCode} ({sw.ElapsedMilleconds}ms)");
        
        return response;
    }
}

// 反射注册所有中间件
server.RegisterHandlersFromAssembly(typeof(ApiMiddlewares));
```

## 优先级系统

### 优先级规则

- **全局中间件** (path = null): 优先级 0（默认），最先执行
- **路径中间件** (path 指定): 优先级 100（默认）
- **自定义优先级**: 用户指定的值
- **同优先级**: 按注册顺序执行

### 优先级排序

```
优先级     中间件
─────────────────────
-100     关键基础设施（如错误处理）
0        全局中间件
100      路径中间件
200      业务逻辑中间件
```

### 执行顺序示例

```csharp
// 注册顺序
server.AddMiddleware(ErrorHandler, priority: -100);      // 第 1 个
server.AddMiddleware(Logger, priority: 0);               // 第 2 个
server.AddMiddleware(Auth, "/api/*", priority: 100);     // 第 3 个
server.AddMiddleware(Business, "/api/*", priority: 100); // 第 4 个（同优先级，按注册顺序）

// 执行顺序
ErrorHandler 前 → Logger 前 → Auth 前 → Business 前 → 
    处理器执行 → 
Business 后 → Auth 后 → Logger 后 → ErrorHandler 后
```

## 中间件模式

### 1. 洋葱模式（最常见）

```
请求 → 中间件A前 → 中间件B前 → 处理器 → 中间件B后 → 中间件A后 → 响应
```

```csharp
server.AddMiddleware(async (req, next) => {
    Logger.Info("A:: 请求进入");
    var resp = await next(req);
    Logger.Info("A:: 响应离开");
    return resp;
});

server.AddMiddleware(async (req, next) => {
    Logger.Info("B:: 请求进入");
    var resp = await next(req);
    Logger.Info("B:: 响应离开");
    return resp;
});

// 输出顺序:
// A:: 请求进入
// B:: 请求进入
// [处理器执行]
// B:: 响应离开
// A:: 响应离开
```

### 2. 短路模式（中止处理）

```csharp
server.AddMiddleware(async (req, next) => {
    var hasPermission = CheckPermission(req);
    if (!hasPermission)
    {
        // 直接返回，不调用 next
        return new ForbiddenResult("Access denied");
    }
    
    return await next(req); // 有权限，继续
});
```

### 3. 条件分支模式

```csharp
server.AddMiddleware(async (req, next) => {
    if (req.Path.StartsWith("/admin"))
    {
        // 管理员路由特殊处理
        return await AdminAuthMiddleware(req, next);
    }
    
    if (req.Path.StartsWith("/public"))
    {
        // 公开路由
        return await next(req);
    }
    
    // 其他路由需要用户认证
    return await UserAuthMiddleware(req, next);
});
```

## 常见中间件模板

### 时间跟踪中间件

```csharp
[HttpMiddleware]
public static async Task<HttpResponse> TimingMiddleware(
    HttpRequest req,
    Func<HttpRequest, Task<HttpResponse>> next)
{
    var sw = Stopwatch.StartNew();
    var response = await next(req);
    sw.Stop();
    
    response.Headers["X-Response-Time"] = $"{sw.ElapsedMilliseconds}ms";
    return response;
}
```

### CORS 中间件

```csharp
[HttpMiddleware]
public static async Task<HttpResponse> CorsMiddleware(
    HttpRequest req,
    Func<HttpRequest, Task<HttpResponse>> next)
{
    var response = await next(req);
    
    response.Headers["Access-Control-Allow-Origin"] = "*";
    response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE";
    response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
    
    if (req.Method == "OPTIONS")
    {
        return new HttpResponse(204); // No Content
    }
    
    return response;
}
```

### 请求去重中间件

```csharp
private static readonly HashSet<string> ProcessedRequests = new();
private static readonly object Lock = new();

[HttpMiddleware(path: "/api/*")]
public static async Task<HttpResponse> DeduplicationMiddleware(
    HttpRequest req,
    Func<HttpRequest, Task<HttpResponse>> next)
{
    var requestId = req.Headers["X-Request-ID"];
    if (string.IsNullOrEmpty(requestId))
    {
        return new BadRequestResult("X-Request-ID header required");
    }
    
    lock (Lock)
    {
        if (ProcessedRequests.Contains(requestId))
        {
            return new ConflictResult("Duplicate request");
        }
        ProcessedRequests.Add(requestId);
    }
    
    return await next(req);
}
```

### 请求体日志中间件

```csharp
[HttpMiddleware(priority: 999)] // 最后执行
public static async Task<HttpResponse> RequestBodyLoggingMiddleware(
    HttpRequest req,
    Func<HttpRequest, Task<HttpResponse>> next)
{
    // 只记录 POST/PUT 请求
    if ((req.Method == "POST" || req.Method == "PUT") && !string.IsNullOrEmpty(req.Body))
    {
        var logBody = req.Body;
        if (logBody.Length > 200)
            logBody = logBody.Substring(0, 200) + "...";
            
        Logger.Debug($"Request body: {logBody}");
    }
    
    return await next(req);
}
```

## 最佳实践

### ✅ 应该做

1. **单一职责** - 每个中间件做一件事
2. **异常处理** - 始终处理异常
3. **日志记录** - 记录重要事件
4. **尽快返回** - 如果条件不满足，尽快返回
5. **文档** - 解释中间件的用途和副作用

### ❌ 不应该做

1. **重操作** - 避免在中间件中进行复杂计算
2. **状态修改** - 避免修改全局状态
3. **忽略异常** - 不要吞掉异常
4. **阻塞** - 使用异步操作
5. **忘记调用 next** - 除非想中止处理

## 故障排查

### 中间件未执行

**原因**：路径前缀不匹配
```csharp
// ❌ 错误：/users 不匹配 /api/*
server.AddMiddleware(middleware, path: "/api/*");

// ✅ 正确
server.AddMiddleware(middleware, path: "/users*");
```

### 中间件执行顺序错误

**原因**：优先级设置不当
```csharp
// ❌ 错误：认证在日志之后
server.AddMiddleware(Logger, priority: 100);
server.AddMiddleware(Auth, priority: 200);

// ✅ 正确
server.AddMiddleware(Auth, priority: 100);
server.AddMiddleware(Logger, priority: 200);
```

### 响应未修改

**原因**：没有更新返回值
```csharp
// ❌ 错误
server.AddMiddleware(async (req, next) => {
    await next(req); // 忘记返回
});

// ✅ 正确
server.AddMiddleware(async (req, next) => {
    var response = await next(req);
    return response; // 返回响应
});
```

## 相关文档

- [DrxHttpServer.DEVGUIDE.md](DrxHttpServer.DEVGUIDE.md) - 完整服务器指南
- [ROUTING_GUIDE.md](ROUTING_GUIDE.md) - 路由系统指南
- [JwtHelper.DEVGUIDE.md](JwtHelper.DEVGUIDE.md) - 认证示例
