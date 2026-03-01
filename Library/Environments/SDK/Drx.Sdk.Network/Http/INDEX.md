# Drx.Sdk.Network.Http - HTTP 框架完整文档

## 🎯 概述

`Drx.Sdk.Network.Http` 是 DRX 框架中的核心 HTTP 模块，提供了完整的、功能丰富的 HTTP 服务器和客户端实现。该模块支持现代 Web 应用开发所需的所有功能：路由、中间件、认证、授权、会话管理、文件上传/下载等。

### 核心特性
- ✅ **高性能 HTTP 服务器** - 基于 System.Net.HttpListener
- ✅ **灵活的路由系统** - 支持动态参数和通配符
- ✅ **中间件系统** - 类似 ASP.NET Core 的中间件管道
- ✅ **完整的认证/授权** - JWT、OAuth 授权码流程
- ✅ **会话管理** - Cookie 会话、分布式支持
- ✅ **文件处理** - 流式上传下载、断点续传
- ✅ **Server-Sent Events** - 实时服务器推送
- ✅ **性能优化** - 对象池、路由缓存、消息队列
- ✅ **ASP.NET Core 集成** - 轻量级 Asp 封装

---

## 📚 快速导航

### 🔰 新手入门
1. **首先理解架构** - 阅读下方的[架构总览](#-架构总览)
2. **选择入口** - 服务器开发 或 客户端开发
3. **查阅相应指南** - 进入对应子目录

### 🖧 模块导航

#### 基础层 (底层协议和支撑)
| 模块 | 描述 | 进入 |
|------|------|------|
| **Protocol** | HTTP 请求/响应核心定义 | [📖 Protocol](Protocol/README.md) |
| **Serialization** | JSON 序列化工具 | [📖 Serialization](Serialization/README.md) |
| **Utilities** | URL、编码等工具方法 | [📖 Utilities](Utilities/README.md) |
| **Models** | 数据模型基类 | [📖 Models](Models/README.md) |

#### 服务器层 (HTTP 服务器实现)
| 模块 | 描述 | 进入 |
|------|------|------|
| **Server** | HTTP 服务器核心实现 | [📖 Server](Server/README.md) |
| **Configs** | 属性和配置定义 | [📖 Configs](Configs/README.md) |
| **Entry** | 路由和中间件入口点 | [📖 Entry](Entry/README.md) |
| **Commands** | 内置命令系统 | [📖 Commands](Commands/README.md) |
| **Results** | HTTP 操作结果类型 | [📖 Results](Results/README.md) |

#### 功能层 (业务功能实现)
| 模块 | 描述 | 进入 |
|------|------|------|
| **Auth** | JWT 认证和令牌桶限流 | [📖 Auth](Auth/README.md) |
| **Authorization** | OAuth 授权码流程 | [📖 Authorization](Authorization/README.md) |
| **Session** | 会话管理系统 | [📖 Session](Session/README.md) |
| **ResourceManagement** | 文件上传/下载管理 | [📖 ResourceManagement](ResourceManagement/README.md) |
| **Sse** | Server-Sent Events 推送 | [📖 Sse](Sse/README.md) |

#### 客户端层 (HTTP 客户端实现)
| 模块 | 描述 | 进入 |
|------|------|------|
| **Client** | 功能完整的 HTTP 客户端 | [📖 Client](Client/README.md) |
| **Asp** | ASP.NET Core 集成 | [📖 Asp](Asp/README.md) |

#### 优化层 (性能和特殊优化)
| 模块 | 描述 | 进入 |
|------|------|------|
| **Performance** | 对象池、缓存、消息队列 | [📖 Performance](Performance/README.md) |

#### 文档中心
| 资源 | 描述 | 进入 |
|------|------|------|
| **Guides** | 详细开发指南索引 | [📖 Guides](Guides/README.md) |

---

## 🏗️ 架构总览

### 分层架构

```
┌─────────────────────────────────────────────────┐
│        应用层 (KaxSocket 等具体应用)          │
│  使用 [HttpHandle] 等属性编写业务逻辑         │
└────────────────┬────────────────────────────────┘
                 │
         ▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼
                 │
┌────────────────────────────────────────────────┐
│         Server 层 (DrxHttpServer)              │
│  • 请求接收和路由匹配                         │
│  • 中间件管道执行                             │
│  • 响应生成和发送                             │
│  • 异常处理                                   │
└────────────┬──────────────────┬────────────────┘
             │                  │
      ▼▼▼▼▼▼▼▼▼           ▼▼▼▼▼▼▼
      认证/授权/           会话管理/
      速率限制             Cookie
             │                  │
             └─────────┬────────┘
                       │
        ▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼
                       │
┌───────────────────────────────────────────────┐
│      Protocol & Entry 层                      │
│  • HttpRequest / HttpResponse                │
│  • RouteEntry / MiddlewareEntry             │
│  • 缓存和优化结构                            │
└───────────┬─────────────────┬─────────────────┘
            │                 │
     ▼▼▼▼▼▼▼▼            ▼▼▼▼▼▼▼▼▼
    序列化/工具       性能优化
    资源管理           对象池/缓存
            │                 │
            └────────┬────────┘
                     │
    ▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼
                     │
       ┌─────────────────────────┐
       │  System.Net.HttpListener │
       │      (OS 层)             │
       └─────────────────────────┘
```

### 客户端架构

```
╔──────────────────────╗
║   应用代码           ║
║ client.GetAsync(...) ║
╚──────────┬───────────╝
           │
           ▼
╔──────────────────────────┐
║  DrxHttpClient           │
│  • SendAsync (多个重载)  │
│  • 文件上传/下载        │
│  • Cookie 管理          │
│  • 进度跟踪            │
└──────────┬──────────────┘
           │
           ▼
╔──────────────────────────┐
║  HttpClient (System)     │
│  • 真实 HTTP 请求        │
└──────────┬──────────────┘
           │
           ▼
       网络传输
```

---

## 🚀 快速示例

### 服务器（3 步）

```csharp
// 1. 创建服务器
var server = new DrxHttpServer(port: 8080);

// 2. 注册处理器
public partial class MyApi 
{
    [HttpHandle("GET", "/api/hello")]
    public static async Task<HttpResponse> SayHello(HttpRequest request)
    {
        return new JsonResult(new { message = "Hello, World!" });
    }
    
    [HttpHandle("POST", "/api/users")]
    public static async Task<HttpResponse> CreateUser(HttpRequest request)
    {
        var user = JsonSerializer.Deserialize<User>(request.Body);
        // 保存用户...
        return new JsonResult(user);
    }
}

// 3. 启动
await server.Start();
```

### 客户端（3 步）

```csharp
// 1. 创建客户端
var client = new DrxHttpClient("http://localhost:8080");

// 2. 发送请求
var response = await client.GetAsync("/api/hello");
Console.WriteLine(response.Body); // {"message":"Hello, World!"}

// 3. 发送 POST
var user = new { Name = "Alice" };
var result = await client.PostAsync("/api/users", user);
```

### 认证示例

```csharp
// 生成 JWT 令牌
var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user123") };
var token = JwtHelper.GenerateToken(claims);

// 在请求中发送
var headers = new NameValueCollection 
{ 
    ["Authorization"] = $"Bearer {token}" 
};
var response = await client.GetAsync("/api/secure", headers);
```

---

## 📊 模块依赖关系

```
┌─────────────┐
│  Protocols  │  (最底层)
└──────┬──────┘
       │
       ├──→ ┌──────────────┐
       │    │ Serialization│
       │    └──────────────┘
       │
       ├──→ ┌─────────────┐
       │    │ Utilities   │
       │    └─────────────┘
       │
       └──→ ┌─────────────┐
            │  Models     │
            └─────────────┘
               │
               ▼
    ┌────────────────────┐
    │  Entry / Configs   │  (中层)
    └──────────┬─────────┘
               │
    ┌──────────┼──────────┐
    │          │          │
    ▼          ▼          ▼
  ┌────┐   ┌────────┐  ┌──────────┐
  │Auth│   │Session │  │Commands  │
  └────┘   └────────┘  └──────────┘
    │          │          │
    └──────────┼──────────┘
               │
               ▼
    ┌────────────────────┐
    │  Server / Client   │  (顶层)
    │  Performance       │
    │  ResourceMgmt      │
    └────────────────────┘
```

---

## 💡 常见任务

### 我想...

#### 构建 REST API
→ 阅读 [Server 目录](Server/README.md) 和 [DrxHttpServer.DEVGUIDE](Guides/DrxHttpServer.DEVGUIDE.md)

#### 实现用户认证
→ 阅读 [Auth 目录](Auth/README.md) 和 [JwtHelper.DEVGUIDE](Guides/JwtHelper.DEVGUIDE.md)

#### 上传/下载文件
→ 阅读 [ResourceManagement 目录](ResourceManagement/README.md) 和 [Client 目录](Client/README.md)

#### 管理用户会话
→ 阅读 [Session 目录](Session/README.md) 和 [SessionSystem.md](Guides/SessionSystem.md)

#### 实现实时推送
→ 阅读 [Sse 目录](Sse/README.md)

#### 调用远程 API
→ 阅读 [Client 目录](Client/README.md) 和 [DrxHttpClient.DEVGUIDE](Guides/DrxHttpClient.DEVGUIDE.md)

#### 限制 API 访问速率
→ 阅读 [Auth 目录](Auth/README.md) 和 [RateLimitCallback.IMPLEMENTATION](Guides/RateLimitCallback.IMPLEMENTATION.md)

#### 优化性能
→ 阅读 [Performance 目录](Performance/README.md)

#### 处理 JSON 数据
→ 阅读 [Serialization 目录](Serialization/README.md) 和 [JSON_SERIALIZATION_GUIDE](Guides/JSON_SERIALIZATION_GUIDE.md)

---

## 🔐 安全最佳实践

1. **总是使用 HTTPS** - 生产环境必须使用 SSL/TLS
2. **验证输入** - 检查所有来自客户端的数据
3. **使用强密钥** - JWT 密钥要足够长和随机
4. **设置 HttpOnly** - Cookie 应设置 HttpOnly 防止 XSS
5. **实现速率限制** - 防止 DDoS 攻击
6. **定期轮换凭证** - JWT 刷新令牌策略
7. **日志审计** - 记录重要操作
8. **CORS 配置** - 严格限制跨域访问

---

## ⚡ 性能优化

1. **启用对象池** - 减少 GC 压力
2. **使用路由缓存** - 加速频繁访问的路由
3. **异步处理** - 不要阻塞线程
4. **压缩响应** - 减少网络流量
5. **连接池** - 客户端复用连接
6. **并发优化** - 根据 CPU 核心调优线程池
7. **监控指标** - 定期检查性能数据

---

## 📖 完整学习路径

### 初级 (1-2 小时)
1. 阅读本文档的快速示例
2. 查看 [Server 目录](Server/README.md) 概览
3. 查看 [Client 目录](Client/README.md) 概览

### 中级 (4-8 小时)
1. 阅读 [DrxHttpServer.DEVGUIDE](Guides/DrxHttpServer.DEVGUIDE.md)
2. 阅读 [DrxHttpClient.DEVGUIDE](Guides/DrxHttpClient.DEVGUIDE.md)
3. 阅读 [JwtHelper.DEVGUIDE](Guides/JwtHelper.DEVGUIDE.md)
4. 深入各功能模块 (Auth, Session, ResourceManagement)

### 高级 (8+ 小时)
1. 研究 Server 的 partial 文件实现
2. 研究 Client 的并发和性能优化
3. 研究 Performance 模块的缓存策略
4. 研究 ResourceManagement 的断点续传
5. 参与框架开发和优化

---

## 🐛 常见问题

### Q: Server 和 Asp 有什么区别？
A: Server 是完整实现，Asp 是轻量级 ASP.NET Core 封装。一般使用 Server。

### Q: 如何实现 CORS？
A: 在中间件中添加 CORS 响应头。参见 Utilities 中的 CORS 工具。

### Q: DrxHttpClient 和 HttpClient 的关系？
A: DrxHttpClient 是对 System.Net.Http.HttpClient 的高级封装。

### Q: 如何处理文件上传？
A: 使用 Client 的 UploadFileAsync，或 Server 的文件处理中间件。

### Q: 支持 WebSocket 吗？
A: 目前不支持，但支持 SSE 作为推送备选方案。

---

## 🔗 相关资源

### 内部资源
- [Framework 层文档](../../Framework/README.md)
- [Drx.Sdk.Shared 文档](../Shared/README.md)
- [Drx.Sdk.Network 总文档](../README.md)

### 外部资源
- [HTTP/1.1 RFC 7230-7235](https://tools.ietf.org/html/rfc7230)
- [REST API 最佳实践](https://restfulapi.net/)
- [JWT 介绍](https://jwt.io/introduction)
- [OWASP 安全指南](https://owasp.org/www-community/)

---

## 📝 版本信息

- **框架版本**: .NET 9.0
- **最后更新**: 2026 年 3 月
- **维护者**: DRX 框架团队

---

## ✨ 快速链接

### 按用途
- 🌐 **Web API** → [Server](Server/README.md) + [Auth](Auth/README.md)
- 📱 **移动应用** → [Client](Client/README.md) + [Auth](Auth/README.md)
- 🔄 **微服务** → [Server](Server/README.md) + [Client](Client/README.md)
- 📊 **数据同步** → [ResourceManagement](ResourceManagement/README.md)
- 🔔 **实时通知** → [Sse](Sse/README.md)

### 按角色
- 👨‍💻 **后端开发** → [Server](Server/README.md) + [Guides](Guides/README.md)
- 🔌 **集成开发** → [Client](Client/README.md) + [Guides](Guides/README.md)
- 🛡️ **安全工程师** → [Auth](Auth/README.md) + [Authorization](Authorization/README.md)
- ⚡ **性能优化** → [Performance](Performance/README.md)

---

**开始探索吧！选择左侧的任何目录，深入了解 DRX Http 框架的强大功能。** 🚀
