# Session 目录 - 会话管理

## 概述
Session 目录实现了 HTTP 会话管理系统，用于追踪和管理用户会话状态。

## 文件说明

### SessionManager.cs
**会话管理器**
- 集中管理所有活跃的会话
- 处理会话的创建、获取、更新和删除
- 特点：
  - 线程安全的会话存储
  - 自动过期清理
  - 会话数据隔离
  - 多种会话存储后端支持

**主要方法：**
- `CreateSession(userId)` - 创建新会话
- `GetSession(sessionId)` - 获取会话
- `UpdateSession(sessionId, data)` - 更新会话数据
- `DeleteSession(sessionId)` - 删除会话
- `GetAllSessions()` - 获取所有活跃会话
- `CleanupExpiredSessions()` - 清理过期会话

**会话属性：**
- `Id` - 会话唯一标识
- `UserId` - 关联用户 ID
- `CreatedAt` - 创建时间
- `LastActivityAt` - 最后活动时间
- `ExpiresAt` - 过期时间
- `Data` - 会话数据（字典）
- `IsValid` - 会话是否有效

## 会话工作流

```
用户登录
    ↓
创建会话 (Create)
    ↓
返回 sessionId 给客户端
    ↓
客户端在 Cookie 或 Header 中存储 sessionId
    ↓
后续请求附加 sessionId
    ↓
验证会话 (Get)
    ↓
执行操作
    ↓
更新会话活动时间 (Update)
    ↓
用户登出
    ↓
删除会话 (Delete)
```

## 会话存储

支持多种存储后端：

1. **内存存储** (默认)
   - 快速但进程重启后丢失
   - 适合单机开发和测试

2. **数据库存储**
   - 持久化
   - 支持分布式部署
   - 参见 Drx.Sdk.Network 中的数据库相关模块

3. **分布式缓存** (Redis 等)
   - 支持微服务架构
   - 可选集成

## 会话安全

1. **HttpOnly** - 防止 JavaScript 窃取
2. **Secure** - 仅通过 HTTPS 传输
3. **SameSite** - 防止 CSRF 攻击
4. **过期管理** - 自动过期和刷新
5. **数据加密** - 敏感数据加密存储

## 使用场景

1. **用户认证** - 维持登录状态
2. **购物车** - 存储用户购物信息
3. **个性化** - 存储用户偏好设置
4. **权限管理** - 存储用户权限信息
5. **审计日志** - 记录用户活动

## 与其他模块的关系

- **与 Auth 的关系** - JWT 可作为会话令牌
- **与 Configs 的关系** - Session/CookieOptions 配置
- **与 Protocol 的关系** - HttpRequest/HttpResponse 携带会话 ID
- **与 Server 的关系** - DrxHttpServer 集成会话管理
- **与 Client 的关系** - DrxHttpClient 自动管理会话 Cookie

## 最佳实践

1. **会话超时** - 设置合理的会话超时时间
2. **并发** - 使用线程安全的数据结构
3. **活动检测** - 每次请求更新最后活动时间
4. **清理** - 定期清理过期会话
5. **监控** - 监控会话数量和存储使用

## 相关文档
- 参见 [../Guides/SessionSystem.md](../Guides/SessionSystem.md) 详细说明
- 参见 [../Auth/README.md](../Auth/README.md) 了解认证集成
