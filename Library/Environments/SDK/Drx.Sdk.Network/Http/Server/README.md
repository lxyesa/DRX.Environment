# Server 目录 - HTTP 服务器核心实现

## 概述
Server 目录包含 DrxHttpServer 的完整实现，提供高性能、功能完整的 HTTP 服务器框架，支持路由、中间件、认证、速率限制等功能。

## 文件结构说明

### 核心文件 - DrxHttpServer.cs 及其 Partial 类

#### DrxHttpServer.cs
**HTTP 服务器主类和核心声明**
- ListenerContext 管理
- 基础初始化和生命周期

#### DrxHttpServer.Routing.cs
**路由管理和匹配**
- 路由表管理
- 请求路由匹配
- URL 路径解析和参数提取
- 支持通配符和正则表达式路由

#### DrxHttpServer.HandlerRegistration.cs
**处理器注册机制**
- 自动扫描和注册标记为 `[HttpHandle]` 的方法
- 支持 partial 类处理器组织
- 处理器生命周期管理

#### DrxHttpServer.RequestProcessing.cs
**核心请求处理流程**
- 请求接收和解析
- 请求分发到处理器
- 响应返回
- 异常处理

#### DrxHttpServer.Middleware.cs
**中间件系统**
- 中间件注册和执行
- 请求前处理（Pre-middleware）
- 请求后处理（Post-middleware）
- 中间件链式执行

#### DrxHttpServer.RateLimit.cs
**速率限制实现**
- 基于 IP 地址的限流
- 基于用户的限流
- TokenBucket 算法
- 自动封禁恶意 IP

#### DrxHttpServer.SessionAuthCommands.cs
**会话认证命令**
- 会话相关的内置命令
- 认证相关的内置命令
- 用户管理命令

#### DrxHttpServer.FileServing.cs
**静态文件服务**
- 提供本地文件下载
- MIME 类型自动检测
- 缓存支持
- 范围请求支持

#### DrxHttpServer.ResourceManagement.cs
**资源上传/下载管理**
- 资源上传队列管理
- 资源下载队列管理
- 断点续传支持
- 资源元数据管理

#### DrxHttpServer.Sse.cs
**Server-Sent Events 实现**
- 长连接管理
- 事件推送
- 心跳保活

#### DrxHttpServer.Email.cs
**电子邮件发送**
- 集成电子邮件功能（可选）
- 验证码发送
- 通知电子邮件

### 相关类

#### IActionResult
**操作结果接口**
- 所有 HTTP 操作结果都实现此接口
- 支持异步转换为 HttpResponse

## 主要功能

### 1. 路由系统
- 基于 path 和 method 的匹配
- 支持动态路由参数
- 缓存提高匹配性能

```csharp
// 示例：
[HttpHandle("GET", "/api/users/{id}")]
public static async Task<HttpResponse> GetUser(HttpRequest request) { }
```

### 2. 中间件系统
- 请求前/后处理
- 支持异步中间件
- 可中断中间件链

```csharp
// 示例：
[HttpMiddleware]
public static async Task<bool> AuthMiddleware(HttpRequest request) 
{
    // 验证认证状态
    return true; // 继续处理
}
```

### 3. 认证和授权
- JWT 令牌验证
- 授权码流程
- 会话管理
- 数据库集成

### 4. 速率限制
- 基于 IP 的限流
- 基于用户的限流
- 自动封禁
- 可配置的限制规则

### 5. 会话系统
- Cookie 会话
- 会话持久化
- 会话过期管理
- 跨域会话支持

### 6. 文件服务
- 静态文件托管
- 大文件下载
- 范围请求
- 缓存控制

### 7. 资源管理
- 流式上传/下载
- 断点续传
- 进度跟踪
- 清理过期资源

### 8. Server-Sent Events
- 长连接推送
- 事件广播
- 客户端删除通知

## 服务器生命周期

```
新建 DrxHttpServer
  ↓
配置路由/中间件
  ↓
Start() - 启动监听
  ↓
等待请求...
  ↓
接收请求
  ↓
匹配路由
  ↓
执行中间件
  ↓
执行处理器
  ↓
返回响应
  ↓
Stop() - 停止监听
  ↓
Dispose() - 释放资源
```

## 使用场景

1. **RESTful API 服务** - 构建完整的 REST API
2. **微服务** - 作为微服务的网关或独立服务
3. **实时推送** - 使用 SSE 进行服务器推送
4. **文件服务** - 提供文件上传/下载服务
5. **第三方集成** - 与第三方应用集成
6. **后台任务** - HTTP 服务的异步任务管理

## 与其他模块的关系

- **与 Protocol 的关系** - 使用 HttpRequest、HttpResponse
- **与 Auth 的关系** - JWT 认证集成
- **与 Authorization 的关系** - OAuth 授权码流程
- **与 Configs 的关系** - [HttpHandle]、[HttpMiddleware] 属性
- **与 Session 的关系** - 会话管理
- **与 Middleware 的关系** - 中间件系统
- **与 Guides 的关系** - 详细文档

## 性能优化

1. **对象池** - 重用 HttpContext 等对象
2. **路由缓存** - 缓存频繁访问的路由
3. **异步处理** - 非阻塞 I/O
4. **并发控制** - ThreadPool 优化

## 相关文档
- 参见 [../Guides/DrxHttpServer.DEVGUIDE.md](../Guides/DrxHttpServer.DEVGUIDE.md) 详细开发指南
- 参见 [../Configs/](../Configs/) 了解属性配置
- 参见 [../Entry/](../Entry/) 了解各种入口点
