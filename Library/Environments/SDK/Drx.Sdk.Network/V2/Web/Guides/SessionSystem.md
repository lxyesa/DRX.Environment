# DrxHttpServer 会话系统使用指南

## 概述

DrxHttpServer 提供了一个完整的会话管理系统，用于在 HTTP 请求之间保存用户状态数据。会话系统基于 Cookie 机制实现，并支持自动超时清理、并发访问和灵活的数据存储。

---

## 核心组件

### 1. Session 类

表示单个用户会话的对象，包含会话ID、数据存储和时间戳信息。

**主要属性：**
- `Id` (string) - 会话的唯一标识符
- `Data` (ConcurrentDictionary<string, object>) - 会话数据存储，线程安全的
- `Created` (DateTime) - 会话创建时间
- `LastAccess` (DateTime) - 最后访问时间
- `IsNew` (bool) - 标记是否为新创建的会话

**主要方法：**
- `UpdateAccess()` - 更新最后访问时间，标记会话为非新

```csharp
// 会话数据存储示例
var session = ...;
session.Data["user"] = "admin";
session.Data["login_time"] = DateTime.Now;
session.Data["permissions"] = new List<string> { "read", "write" };
```

### 2. SessionManager 类

管理所有活跃会话的核心管理器，自动处理会话的创建、获取、清理等操作。

**主要方法：**
- `CreateSession()` - 创建新会话
- `GetSession(id)` - 根据ID获取会话（不存在返回null）
- `GetOrCreateSession(id)` - 获取或创建会话
- `RemoveSession(id)` - 手动移除会话

**自动特性：**
- 自动清理过期会话（每5分钟执行一次）
- 会话超时可在构造函数中配置（默认30分钟）

### 3. DrxHttpServer 会话支持

服务器通过中间件和属性支持会话管理。

---

## 快速开始

### 步骤 1：启用会话中间件

在启动服务器前调用 `AddSessionMiddleware()` 方法：

```csharp
var server = new DrxHttpServer(new[] { "http://localhost:8080/" });

// 启用会话中间件（使用默认配置）
server.AddSessionMiddleware();

// 或者自定义Cookie选项
var cookieOptions = new CookieOptions
{
    HttpOnly = true,
    Secure = false,  // 本地开发为false，生产环境应为true
    Path = "/",
    MaxAge = TimeSpan.FromMinutes(30)
};
server.AddSessionMiddleware("session_id", cookieOptions);

await server.StartAsync();
```

### 步骤 2：在路由处理中访问会话

通过 `HttpRequest.ResolveSession()` 方法进行获取：

```csharp
server.AddRoute("POST", "/login", (req) =>
{
    // 解析会话
    var session = req.ResolveSession(server);
    
    if (session != null)
    {
        // 存储用户信息到会话
        session.Data["user_id"] = "user123";
        session.Data["username"] = "admin";
        session.Data["login_time"] = DateTime.Now;
        
        return new HttpResponse(200, "登录成功");
    }
    
    return new HttpResponse(500, "会话不可用");
});
```

---

## 完整示例

### 示例 1：基本的登录和权限验证

```csharp
using System;
using System.Collections.Generic;
using Drx.Sdk.Network.V2.Web;

class LoginExample
{
    static async Task Main()
    {
        var server = new DrxHttpServer(new[] { "http://localhost:8080/" });
        
        // 启用会话中间件
        server.AddSessionMiddleware();
        
        // 登录路由
        server.AddRoute("POST", "/login", (req) =>
        {
            var username = req.Query["username"];
            var password = req.Query["password"];
            
            // 简单验证（实际应用应使用密码哈希）
            if (username == "admin" && password == "123456")
            {
                var session = req.ResolveSession(server);
                if (session != null)
                {
                    session.Data["user_id"] = "001";
                    session.Data["username"] = username;
                    session.Data["login_time"] = DateTime.Now;
                    session.Data["roles"] = new List<string> { "admin" };
                    
                    return new HttpResponse(200, "{\"code\":0,\"message\":\"登录成功\"}");
                }
            }
            
            return new HttpResponse(401, "{\"code\":1,\"message\":\"用户名或密码错误\"}");
        });
        
        // 检查登录状态
        server.AddRoute("GET", "/auth/status", (req) =>
        {
            var session = req.ResolveSession(server);
            if (session?.Data.TryGetValue("username", out var username) == true)
            {
                return new HttpResponse(200, 
                    $"{{\"logged_in\":true,\"username\":\"{username}\"}}");
            }
            
            return new HttpResponse(401, 
                "{\"logged_in\":false,\"message\":\"未登录\"}");
        });
        
        // 受保护的资源
        server.AddRoute("GET", "/api/profile", (req) =>
        {
            var session = req.ResolveSession(server);
            
            if (session?.Data.TryGetValue("user_id", out var userId) != true)
            {
                return new HttpResponse(401, "需要登录");
            }
            
            if (session.Data.TryGetValue("roles", out var rolesObj) && 
                rolesObj is List<string> roles && roles.Contains("admin"))
            {
                return new HttpResponse(200, 
                    $"{{\"user_id\":\"{userId}\",\"access_level\":\"admin\"}}");
            }
            
            return new HttpResponse(403, "权限不足");
        });
        
        // 注销
        server.AddRoute("POST", "/logout", (req) =>
        {
            var session = req.ResolveSession(server);
            if (session != null)
            {
                session.Data.Clear();
                return new HttpResponse(200, "注销成功");
            }
            
            return new HttpResponse(500, "会话不可用");
        });
        
        await server.StartAsync();
        Console.WriteLine("服务器已启动 http://localhost:8080/");
        Console.ReadKey();
    }
}
```

### 示例 2：购物车管理

```csharp
// 添加商品到购物车
server.AddRoute("POST", "/cart/add", (req) =>
{
    var session = req.ResolveSession(server);
    if (session == null)
        return new HttpResponse(500, "会话不可用");
    
    // 检查是否登录
    if (!session.Data.ContainsKey("user_id"))
        return new HttpResponse(401, "请先登录");
    
    var productId = req.Query["product_id"];
    var quantity = int.TryParse(req.Query["quantity"], out var q) ? q : 1;
    
    if (string.IsNullOrEmpty(productId))
        return new HttpResponse(400, "缺少product_id参数");
    
    // 获取或初始化购物车
    if (!session.Data.TryGetValue("cart", out var cartObj))
    {
        cartObj = new Dictionary<string, int>();
        session.Data["cart"] = cartObj;
    }
    
    var cart = cartObj as Dictionary<string, int>;
    if (cart.ContainsKey(productId))
    {
        cart[productId] += quantity;
    }
    else
    {
        cart[productId] = quantity;
    }
    
    return new HttpResponse(200, 
        $"{{\"message\":\"已添加\",\"product_id\":\"{productId}\",\"quantity\":{quantity}}}");
});

// 查看购物车
server.AddRoute("GET", "/cart", (req) =>
{
    var session = req.ResolveSession(server);
    if (session == null)
        return new HttpResponse(500, "会话不可用");
    
    if (!session.Data.TryGetValue("cart", out var cartObj))
    {
        return new HttpResponse(200, "{\"items\":[]}");
    }
    
    var cart = cartObj as Dictionary<string, int>;
    var json = System.Text.Json.JsonSerializer.Serialize(new { items = cart });
    return new HttpResponse(200, json);
});

// 清空购物车
server.AddRoute("DELETE", "/cart", (req) =>
{
    var session = req.ResolveSession(server);
    if (session == null)
        return new HttpResponse(500, "会话不可用");
    
    session.Data.Remove("cart");
    
    return new HttpResponse(200, "{\"message\":\"购物车已清空\"}");
});
```

### 示例 3：会话数据持久化到数据库

```csharp
// 扩展会话以存储用户偏好设置
server.AddRoute("POST", "/user/preferences", (req) =>
{
    var session = req.ResolveSession(server);
    if (session == null)
        return new HttpResponse(500, "会话不可用");
    
    if (!session.Data.TryGetValue("user_id", out var userIdObj))
        return new HttpResponse(401, "未登录");
    
    var userId = userIdObj.ToString();
    var theme = req.Query["theme"] ?? "light";
    var language = req.Query["language"] ?? "zh-CN";
    
    // 保存到会话
    var preferences = new Dictionary<string, string>
    {
        { "theme", theme },
        { "language", language }
    };
    session.Data["preferences"] = preferences;
    
    // 在实际应用中，这里应该同时保存到数据库
    // await SaveUserPreferencesAsync(userId, preferences);
    
    return new HttpResponse(200, "{\"message\":\"偏好设置已保存\"}");
});

// 获取用户偏好设置
server.AddRoute("GET", "/user/preferences", (req) =>
{
    var session = req.ResolveSession(server);
    if (session == null)
        return new HttpResponse(500, "会话不可用");
    
    if (session.Data.TryGetValue("preferences", out var prefsObj) && 
        prefsObj is Dictionary<string, string> prefs)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(prefs);
        return new HttpResponse(200, json);
    }
    
    // 返回默认值
    var defaultPrefs = new { theme = "light", language = "zh-CN" };
    var defaultJson = System.Text.Json.JsonSerializer.Serialize(defaultPrefs);
    return new HttpResponse(200, defaultJson);
});
```

---

## 高级用法

### 1. 直接访问 SessionManager

通过 `DrxHttpServer.SessionManager` 属性直接访问管理器：

```csharp
// 获取所有活跃会话数
var sessionManager = server.SessionManager;
var session = sessionManager.GetSession(sessionId);

// 手动创建会话
var newSession = sessionManager.CreateSession();

// 手动移除会话
sessionManager.RemoveSession(sessionId);
```

### 2. 使用 GetSessionById 便捷方法

```csharp
// 从服务器直接获取会话
var session = server.GetSessionById("session_id_string");
```

### 3. 会话超时配置

在创建服务器时指定超时时间（分钟）：

```csharp
// 配置会话超时为60分钟
var server = new DrxHttpServer(
    new[] { "http://localhost:8080/" },
    staticFileRoot: null,
    sessionTimeoutMinutes: 60  // 自定义超时时间
);
```

### 4. 自定义 Cookie 选项

```csharp
var customCookieOptions = new CookieOptions
{
    HttpOnly = true,          // 防止 JavaScript 访问
    Secure = true,            // 仅通过 HTTPS 传输（生产环境）
    SameSite = "Strict",      // CSRF 保护
    Path = "/",               // Cookie 路径
    Domain = ".example.com",  // Cookie 域名
    MaxAge = TimeSpan.FromHours(24),  // 24小时有效期
    Expires = DateTime.UtcNow.AddHours(24)
};

server.AddSessionMiddleware("sid", customCookieOptions);
```

### 5. 存储复杂对象到会话

```csharp
// 定义数据模型
public class UserInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime LastLogin { get; set; }
}

// 存储到会话
var userInfo = new UserInfo
{
    Id = "user123",
    Name = "张三",
    Email = "zhangsan@example.com",
    LastLogin = DateTime.Now
};

session.Data["user_info"] = userInfo;

// 从会话中检索
if (session.Data.TryGetValue("user_info", out var obj) && 
    obj is UserInfo retrievedUser)
{
    Console.WriteLine($"用户: {retrievedUser.Name}");
}
```

---

## Cookie 管理

### 会话 Cookie 的工作流程

1. **第一次请求**：客户端没有会话Cookie
   - 服务器创建新会话，生成唯一ID
   - 通过 Set-Cookie 响应头发送会话Cookie
   - 浏览器自动保存Cookie

2. **后续请求**：客户端发送Cookie
   - 浏览器自动在每个请求中包含会话Cookie
   - 服务器通过Cookie ID识别并恢复会话
   - 会话数据在请求间保持

3. **会话过期**：自动清理机制
   - SessionManager 每5分钟扫描过期会话
   - 客户端Cookie自动过期（由浏览器管理）

### Cookie 安全建议

```csharp
// 生产环境配置示例（HTTPS）
var productionCookieOptions = new CookieOptions
{
    HttpOnly = true,      // ✓ 防止XSS窃取
    Secure = true,        // ✓ 仅通过HTTPS传输
    SameSite = "Strict",  // ✓ 防止CSRF攻击
    Path = "/",
    MaxAge = TimeSpan.FromHours(1)
};

server.AddSessionMiddleware("sid", productionCookieOptions);
```

---

## 常见问题

### Q1：如何在多个请求处理中共享会话数据？

**A**：会话数据存储在服务器的`SessionManager`中，通过Cookie的会话ID在请求间保持一致。只要客户端带上相同的会话Cookie，就能访问相同的会话数据：

```csharp
// 请求1：设置数据
session.Data["user"] = "admin";

// 请求2：读取数据（相同会话ID）
if (session.Data.TryGetValue("user", out var user))
{
    // user == "admin"
}
```

### Q2：如何处理会话超时？

**A**：SessionManager自动清理过期会话。前端应处理401响应，提示用户重新登录：

```csharp
server.AddRoute("GET", "/api/data", (req) =>
{
    var session = req.ResolveSession(server);
    
    if (session == null || !session.Data.ContainsKey("user_id"))
    {
        return new HttpResponse(401, "会话已过期，请重新登录");
    }
    
    return new HttpResponse(200, "数据");
});
```

### Q3：如何在分布式环境中使用会话？

**A**：当前 SessionManager 是内存型实现，仅适用于单机环境。分布式场景可考虑：
- 使用 Redis 替代内存会话存储
- 实现自定义的分布式 SessionManager
- 使用 JWT Token 代替服务器端会话

### Q4：如何防止会话被窃取？

**A**：采用多重安全措施：

```csharp
// 1. 启用 HTTPS
var cookieOptions = new CookieOptions
{
    Secure = true,        // 仅通过HTTPS
    HttpOnly = true,      // 防止JavaScript访问
    SameSite = "Strict"   // CSRF防护
};

// 2. 在会话中记录额外信息进行验证
session.Data["ip"] = req.ClientAddress.Ip;
session.Data["user_agent"] = req.Headers["User-Agent"];

// 3. 定期验证会话信息
if (session.Data.TryGetValue("ip", out var storedIp) && 
    storedIp.ToString() != req.ClientAddress.Ip)
{
    // 会话可能被劫持，要求重新登录
    return new HttpResponse(401, "会话验证失败");
}
```

### Q5：内存会话数据是否可以持久化？

**A**：可以借助 `DataPersistentManager` 进行持久化。示例：

```csharp
// 定期将会话数据保存到数据库
// 这需要自定义实现
foreach (var kvp in server.SessionManager._sessions)
{
    var session = kvp.Value;
    // 保存session.Data到数据库
}
```

---

## 性能优化

### 1. 减少会话查询

使用缓存减少对 SessionManager 的查询：

```csharp
// 缓存会话对象以减少字典查询
private Session _cachedSession;

server.AddRoute("POST", "/api/action", (req) =>
{
    if (_cachedSession?.Id == req.SessionId)
    {
        // 直接使用缓存的会话对象
        var session = _cachedSession;
    }
    else
    {
        var session = req.ResolveSession(server);
        _cachedSession = session;
    }
    
    // ... 业务逻辑
});
```

### 2. 会话数据结构优化

优先使用基本类型或小对象，避免存储大型数据结构：

```csharp
// ✓ 推荐：存储ID引用
session.Data["user_id"] = "123";

// ✗ 不推荐：存储大型对象
session.Data["large_data"] = largeObjectGraph;  // 占用内存
```

### 3. 定期清理过期会话

SessionManager 自动处理，但可在需要时手动触发：

```csharp
// 如需手动优化，可定期创建新的SessionManager实例
// 这会清空所有会话，仅在必要时使用
```

---

## 最佳实践总结

| 实践项 | 建议 | 原因 |
|------|------|------|
| 启用 Cookie 中间件 | ✓ 必需 | 会话系统的基础 |
| 启用 HttpOnly | ✓ 建议 | 防止 XSS 攻击 |
| 启用 Secure(HTTPS) | ✓ 生产必须 | 防止中间人攻击 |
| 设置合理的超时时间 | ✓ 推荐30-60分钟 | 平衡安全与用户体验 |
| 在会话中存储最小必要数据 | ✓ 推荐 | 节省内存和网络 |
| 定期更新 LastAccess | ✓ 自动 | 会话管理器自动处理 |
| 验证会话所有权 | ✓ 推荐 | 检测会话劫持 |
| 生产环境使用分布式会话 | ✓ 推荐 | 支持水平扩展 |

---

## 相关资源

- [HttpRequest 文档](HttpRequest.DEVGUIDE.md)
- [HttpResponse 文档](HttpResponse.DEVGUIDE.md)
- [DrxHttpServer 开发指南](DrxHttpServer.DEVGUIDE.md)
- [完整会话示例](../../../Examples/SessionExample/Program.cs)
