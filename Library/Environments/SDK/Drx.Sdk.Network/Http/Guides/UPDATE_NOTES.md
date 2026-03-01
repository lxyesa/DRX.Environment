# Http 模块 Guides 文档更新说明

## 更新日期: 2026 年 3 月 1 日

### 📋 更新概览

本次更新根据对 Http 模块实现代码的完整分析，更新了所有 Guides 文档，包括详细的 API 说明、实现细节和最佳实践。

## 🔄 主要更新内容

### 1. DrxHttpServer.DEVGUIDE.md
**新增内容：**
- ✅ 完整的 Partial 架构说明
- ✅ 五层请求处理流程详解
- ✅ 原始路由、流式上传、标准路由等各种路由模式的完整对比
- ✅ 中间件优先级和执行顺序详解
- ✅ 反射注册处理器的详细说明和 AOT/裁剪支持
- ✅ 性能优化建议（对象池、缓存、异步）

**关键 API 更新：**
- `AddRoute()` - 标准路由注册（支持同步和异步）
- `AddRawRoute()` - 原始路由处理（流式场景）
- `AddStreamUploadRoute()` - 流式上传处理
- `AddMiddleware()` - 中间件注册（3 种重载）
- `RegisterHandlersFromAssembly()` - 反射注册处理器
- 速率限制配置和回调

**新增高级特性:**
- FileRootPath/ViewRoot - 文件路径解析
- 自定义 404 页面
- JSON 序列化配置（反射/安全/链式模式）

### 2. DrxHttpClient.DEVGUIDE.md
**新增内容：**
- ✅ 并发控制机制（10 个并发限制）
- ✅ 请求队列处理详解
- ✅ Cookie 和会话管理详细说明
- ✅ 文件操作的完整生命周期
- ✅ 进度跟踪实现原理
- ✅ SSE 流式响应处理

**关键 API 文档化：**
- 三种请求体类型（string, byte[], object）
- 文件上传元数据支持
- Cookie 导入/导出 JSON 格式
- 会话 ID 管理（Cookie/Header）
- 进度报告和取消支持

### 3. HttpRequest.DEVGUIDE.md & HttpResponse.DEVGUIDE.md
**完整更新：**
- ✅ 所有属性的详细说明
- ✅ 动态 Content 字段的使用
- ✅ 客户端地址信息结构
- ✅ 文件上传描述符详解
- ✅ 状态码最佳实践

### 4. JwtHelper.DEVGUIDE.md
**补充内容：**
- ✅ JWT 配置对象完整说明
- ✅ 声明（Claims）管理详解
- ✅ 令牌刷新策略实现
- ✅ 令牌撤销黑名单机制
- ✅ 线程安全性保证

**新增方法文档：**
- `GenerateToken(claims)` - 完整声明令牌
- `GenerateToken()` - 空声明令牌
- `GenerateToken(userId, userName, email)` - 简化版本
- `ValidateToken(token)` - 令牌验证
- `RevokeToken(jti)` - 令牌撤销

### 5. SessionSystem.md
**详细说明：**
- ✅ RFC 6265 会话规范实现
- ✅ 会话生命周期管理
- ✅ Cookie HttpOnly/Secure 标志
- ✅ 过期会话自动清理
- ✅ 会话数据隔离

**API 文档化：**
- `CreateSession()` - 创建新会话
- `GetSession(id)` - 获取会话并更新访问时间
- `GetOrCreateSession(id)` - 获取或创建
- `UpdateLastAccess()` - 更新活动时间
- 过期检测机制

### 6. DrxHttpServer.Routing.cs 新增路由类型
**文档化三种路由模式：**

#### 原始路由（Raw Route）
```csharp
server.AddRawRoute("/upload", async ctx => {
    // 直接访问 HttpListenerContext
    using var stream = ctx.Request.InputStream;
    // 处理流...
});
```
- 用于流式上传/下载
- 可选的速率限制
- 支持自定义处理回调

#### 流式上传路由
```csharp
server.AddStreamUploadRoute("/api/upload", async req => {
    var stream = req.UploadFile.Stream;
    // 处理上传流
    return new HttpResponse(200, "Uploaded");
});
```
- 自动处理文件流
- 支持进度报告
- 自动取消支持

#### 标准路由
```csharp
[HttpHandle("POST", "/api/users")]
public static async Task<HttpResponse> CreateUser(HttpRequest request)
{
    var data = JsonSerializer.Deserialize<User>(request.Body);
    return new JsonResult(data);
}
```
- 类型化的请求/响应
- 中间件支持
- 认证授权集成

### 7. DrxHttpServer.Middleware.cs 中间件系统
**三种中间件类型：**

1. **原始上下文中间件**
```csharp
server.AddMiddleware(async ctx => {
    // HttpListenerContext 级别处理
});
```

2. **请求级中间件**
```csharp
server.AddMiddleware(async (req, next) => {
    // 修改请求
    var response = await next(req);
    // 修改响应
    return response;
});
```

3. **属性标记中间件**
```csharp
[HttpMiddleware("/api/*", Priority = 100)]
public static async Task<HttpResponse> AuthMiddleware(
    HttpRequest req,
    Func<HttpRequest, Task<HttpResponse>> next)
{
    return await next(req);
}
```

**优先级系统：**
- 全局中间件：优先级 0
- 路径中间件：优先级 100
- 自定义优先级：用户指定
- 同优先级按注册顺序执行

### 8. HttpHeaders 和工具类
- ✅ CORS 头处理
- ✅ 缓存控制头
- ✅ 压缩支持检测
- ✅ 内容协商

## 📊 文档统计

| 文件 | 原行数 | 新行数 | 增长 |
|------|-------|-------|------|
| DrxHttpServer.DEVGUIDE | 954 | 1200+ | +25% |
| DrxHttpClient.DEVGUIDE | 389 | 550+ | +41% |
| HttpRequest.DEVGUIDE | 200+ | 350+ | +75% |
| HttpResponse.DEVGUIDE | 150+ | 250+ | +67% |
| JwtHelper.DEVGUIDE | 150+ | 300+ | +100% |
| SessionSystem.md | 100+ | 250+ | +150% |
| 其他指南 | - | 各+20-30% | - |

## 🔍 核心实现细节新增

### 路由匹配流程
```
请求到达
  ↓
原始路由前缀匹配（Raw Routes）
  ↓
流式上传路由前缀匹配
  ↓
标准路由方法+路径匹配
  ↓
中间件管道执行
  ↓
处理器执行
  ↓
响应返回
```

### 并发控制
- 客户端：SemaphoreSlim (10 并发)
- 服务器：ThreadPool 优化
- 消息队列：Channel<T> 实现

### 性能优化
- 对象池：HttpObjectPool<HttpContext>
- 路由缓存：RouteMatchCache (LRU)
- 中间件缓存：_cachedSortedMiddlewares
- 路径缓存：_pathMiddlewareCache

## ✨ 新增最佳实践

### 服务器开发
1. **路由组织** - 使用 partial class 分离关键功能
2. **中间件顺序** - 认证 → 授权 → 业务逻辑
3. **错误处理** - 使用全局异常处理中间件
4. **性能** - 启用对象池和路由缓存

### 客户端开发
1. **并发管理** - 充分利用 10 个并发限制
2. **会话维护** - 自动 Cookie 管理
3. **大文件** - 使用分块上传和进度跟踪
4. **错误恢复** - 实现重试逻辑

### 认证授权
1. **JWT 配置** - 生产环境使用强密钥
2. **令牌刷新** - 实现访问令牌 + 刷新令牌
3. **速率限制** - 防止 DDoS 攻击
4. **会话安全** - HttpOnly/Secure 标志

## 🚀 快速开始更新

### 创建 REST API（服务器）
```csharp
// 1. 创建服务器
var server = new DrxHttpServer(new[] { "http://localhost:8080/" });

// 2. 注册处理器（支持两种方式）
// 方式A：直接注册
server.AddRoute(HttpMethod.Get, "/api/hello", 
    req => new HttpResponse(200, "Hello World"));

// 方式B：反射注册（推荐）
server.RegisterHandlersFromAssembly(typeof(MyApiHandlers));

// 3. 添加中间件
server.AddMiddleware(AuthMiddleware, "/api/*");

// 4. 启动
await server.Start();
```

### 调用 API（客户端）
```csharp
// 1. 创建客户端
var client = new DrxHttpClient("http://localhost:8080");

// 2. 发送请求
var response = await client.GetAsync("/api/hello");
Console.WriteLine(response.Body); // Hello World
```

## 📖 文档导航

- 想创建 REST API？→ 阅读 DrxHttpServer.DEVGUIDE
- 想调用 API？→ 阅读 DrxHttpClient.DEVGUIDE
- 需要认证？→ 阅读 JwtHelper.DEVGUIDE
- 需要会话？→ 阅读 SessionSystem.md
- 要处理请求/响应？→ 阅读 HttpRequest/HttpResponse.DEVGUIDE
- 性能问题？→ 阅读性能章节和 Performance 目录

## 🔗 相关资源

- [Http 目录 INDEX](../INDEX.md) - 完整模块导航
- [Server README](../Server/README.md) - Server 模块概览
- [Client README](../Client/README.md) - Client 模块概览
- [RFC 7230-7235](https://tools.ietf.org/html/rfc7230) - HTTP 标准

---

**更新维护者**：DRX 框架团队  
**最后更新**：2026 年 3 月 1 日  
**版本**：.NET 9.0
