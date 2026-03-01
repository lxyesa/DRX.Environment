# Configs 目录 - 配置和属性定义

## 概述
Configs 目录包含 HTTP 框架所使用的各种配置类和特性（Attribute），用于声明式地配置路由、中间件、认证等功能。

## 文件说明

### HttpHandleAttribute.cs
**HTTP 处理器标记属性**
- 标记方法为 HTTP 请求处理器
- 声明 HTTP 方法和路由路径
- 特点：
  - 支持多个 HTTP 方法
  - 支持动态路由参数
  - 自动扫描和注册

**使用示例：**
```csharp
[HttpHandle("GET", "/api/users/{id}")]
public static async Task<HttpResponse> GetUser(HttpRequest request)
{
    var id = request.Path.Parameters["id"];
    // 处理逻辑...
}

[HttpHandle("POST", "/api/users")]
public static async Task<HttpResponse> CreateUser(HttpRequest request)
{
    // 处理逻辑...
}
```

### HttpMiddlewareAttribute.cs
**HTTP 中间件标记属性**
- 标记方法为 HTTP 中间件
- 在请求处理前/后执行
- 特点：
  - 支持条件中间件
  - 可以中断请求处理
  - 用于认证、日志、防火墙等

**使用示例：**
```csharp
[HttpMiddleware]
public static async Task<bool> AuthenticationMiddleware(HttpRequest request)
{
    // 检查认证
    if (!IsAuthenticated(request))
        return false; // 中断处理
    return true; // 继续处理
}
```

### HttpSseAttribute.cs
**Server-Sent Events 标记属性**
- 标记方法为 SSE 端点
- 用于服务器推送事件
- 特点：
  - 长连接管理
  - 事件编码
  - 自动心跳

### Session.cs
**会话对象**
- 表示用户会话
- 存储会话数据
- 特点：
  - ID 生成
  - 过期管理
  - 数据隔离

**主要属性：**
- `Id` - 会话 ID
- `UserId` - 用户 ID
- `CreatedAt` - 创建时间
- `LastActivityAt` - 最后活动时间
- `ExpiresAt` - 过期时间
- `Data` - 会话数据（字典）

### CookieOptions.cs
**Cookie 配置**
- Cookie 的创建和配置选项
- 特点：
  - SameSite 设置
  - Secure/HttpOnly 标志
  - Domain/Path 配置
  - 过期时间设置

**主要属性：**
- `Name` - Cookie 名称
- `Value` - Cookie 值
- `Domain` - 适用域名
- `Path` - 适用路径
- `Expires` - 过期时间
- `Secure` - 仅 HTTPS 传输
- `HttpOnly` - 禁止 JavaScript 访问
- `SameSite` - 跨站保护

### AuthorizationRecord.cs
**授权记录**
- 存储授权码的相关信息
- 用于 OAuth 流程
- 特点：
  - 授权码存储
  - 过期检查
  - 用户和应用信息

**主要属性：**
- `Code` - 授权码
- `UserName` - 用户名
- `ApplicationName` - 应用名称
- `Scopes` - 授权范围
- `IssuedAt` - 签发时间
- `ExpiresAt` - 过期时间

### CommandParseResult.cs
**命令解析结果**
- 表示命令行解析的结果
- 用于内置命令处理
- 特点：
  - 命令名称
  - 参数列表
  - 支持标志

### OverrideContext.cs
**覆盖上下文**
- 用于覆盖请求/响应处理的默认行为
- 支持自定义处理逻辑

## 使用场景

1. **路由定义** - 使用 HttpHandleAttribute 定义 API 端点
2. **中间件链** - 使用 HttpMiddlewareAttribute 实现请求拦截
3. **会话管理** - Session 对象管理用户会话
4. **Cookie 控制** - CookieOptions 配置安全的 Cookie
5. **OAuth 授权** - AuthorizationRecord 支持第三方授权
6. **SSE 推送** - HttpSseAttribute 实现服务器推送

## 与其他模块的关系

- **与 Server 的关系** - 服务器使用这些配置进行注册和处理
- **与 Auth 的关系** - 认证中间件使用这些配置
- **与 Authorization 的关系** - 授权记录配置
- **与 Entry 的关系** - 这些配置创建 Entry 对象

## 最佳实践

1. **路由设计** - 使用清晰的路由路径，遵循 RESTful 约定
2. **中间件顺序** - 正确排序中间件，确保正确的执行顺序
3. **会话安全** - 使用 HttpOnly 和 Secure 标志保护会话 Cookie
4. **错误处理** - 在中间件中正确处理异常
5. **性能** - 避免在中间件中执行耗时操作

## 相关文档
- 参见 [../Guides/](../Guides/) 了解使用示例
- 参见 [../Server/README.md](../Server/README.md) 了解服务器集成
