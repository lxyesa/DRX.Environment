# Authorization 目录 - OAuth 风格授权管理

## 概述
Authorization 目录实现了 OAuth 2.0 风格的授权码（Authorization Code）流程，用于管理第三方应用访问用户资源的权限。

## 文件说明

### AuthorizationManager.cs
**授权码生成和管理**
- 实现授权码的生成、验证和过期管理
- 支持第三方应用授权流程
- 自动清理过期的授权码
- 线程安全的并发管理
- 特点：
  - 基于 ConcurrentDictionary 的安全存储
  - 后台定时器自动清理过期记录
  - 支持自定义过期时间

**主要方法：**
- `GenerateAuthorizationCode(userName, applicationName, ...)` - 生成新的授权码
- `GetAuthorizationRecord(code)` - 获取授权记录
- `ExchangeAuthorizationCodeForToken(code, clientSecret)` - 使用授权码交换令牌
- `RevokeAuthorizationCode(code)` - 撤销授权码

**主要参数：**
- `userName` - 授权用户名
- `applicationName` - 第三方应用名称
- `applicationDescription` - 应用描述
- `scopes` - 授权范围（如 "read", "write", "admin"）

## 授权码流程

```
1. 用户进入第三方应用
2. 应用重定向到授权服务器
3. 用户登录并授权
4. 授权服务器生成授权码并重定向回应用
5. 应用后端用授权码交换访问令牌
6. 应用获得访问令牌，可访问用户资源
```

## 使用场景

1. **第三方应用集成** - 允许第三方应用访问用户数据
2. **服务间授权** - 微服务之间的权限管理
3. **API 访问控制** - 限制应用的访问权限
4. **审计跟踪** - 记录哪些应用访问了哪些资源
5. **临时代理访问** - 通过授权码实现临时权限给予

## 与其他模块的关系

- **与 Auth 的关系** - JwtHelper 可用于交换令牌
- **与 Configs 的关系** - AuthorizationRecord 作为配置类存储授权信息
- **与 Server 的关系** - DrxHttpServer 可集成授权检查中间件
- **与 Session 的关系** - 授权信息可与会话关联

## 最佳实践

1. **授权码有效期短** - 通常 10 分钟内必须使用
2. **一次性使用** - 授权码用过后立即过期
3. **PKCE 保护** - 对公开客户端（SPA）使用 PKCE 进行额外保护
4. **Scope 限制** - 严格限制应用权限范围
5. **HTTPS 必需** - 生产环境必须使用 HTTPS 防止中间人攻击

## 相关文档
- 参见 [../Auth/README.md](../Auth/README.md) 了解认证和令牌管理
