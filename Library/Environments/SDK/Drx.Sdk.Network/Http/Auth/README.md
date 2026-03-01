# Auth 目录 - 认证和令牌管理

## 概述
Auth 目录提供了 JWT 认证和令牌桶速率限制的实现，是 HTTP 服务安全的关键组件。

## 文件说明

### JwtHelper.cs
**JWT 识别和令牌管理**
- 提供 JWT 令牌的生成、验证和撤销功能
- 支持自定义配置（SecretKey、Issuer、Audience、Expiration）
- 支持令牌撤销（黑名单机制）
- 线程安全的令牌管理
- 特点：
  - 基于 `System.IdentityModel.Tokens.Jwt`
  - 支持声明（Claims）管理
  - ConcurrentDictionary 存储撤销令牌

**主要方法：**
- `GenerateToken(claims)` - 生成 JWT 令牌
- `ValidateToken(token)` - 验证 JWT 令牌有效性
- `RevokeToken(jti)` - 撤销指定的令牌
- `Configure(config)` - 设置全局 JWT 配置

**JwtConfig 配置项：**
- `SecretKey` - 签名密钥（生产环境应使用强密钥）
- `Issuer` - 令牌发行者（默认："DrxHttpServer"）
- `Audience` - 令牌受众（默认："DrxUsers"）
- `Expiration` - 令牌过期时间（默认：1 小时）

### TokenBucket.cs
**令牌桶速率限制实现**
- 实现令牌桶算法进行速率限制
- 用于 API 限流和 DDoS 防护
- 支持并发访问

**主要功能：**
- 限制请求速率
- 基于时间窗口的令牌补充
- 并发安全

## 使用场景

1. **用户认证** - 生成和验证用户身份令牌
2. **会话管理** - 基于 JWT 的会话维护
3. **API 安全** - 通过令牌控制访问权限
4. **速率限制** - 防止 API 被恶意调用
5. **令牌撤销** - 在用户登出或密码修改时撤销令牌

## 典型工作流

```
用户登录 -> 生成 JWT -> 客户端存储 -> 后续请求附加令牌 -> 验证令牌 -> 处理请求
                    ↓
                令牌过期或被撤销 -> 返回 401/403 -> 用户重新登录
```

## 与其他模块的关系

- **与 Protocol 的关系** - JWT 通常在请求头（Authorization）中携带
- **与 Server 的关系** - DrxHttpServer 整合认证检查
- **与 Authorization 的关系** - 配合 OAuth 授权码流程
- **与 Configs 的关系** - HttpMiddlewareAttribute 用于认证中间件

## 最佳实践

1. **生产环境** - 使用强密钥和 HTTPS
2. **令牌刷新** - 实现短期访问令牌 + 长期刷新令牌模式
3. **时钟同步** - 确保服务器时钟同步以避免令牌妙值
4. **黑名单管理** - 定期清理已过期的撤销令牌

## 相关文档
- 参见 [../Guides/JwtHelper.DEVGUIDE.md](../Guides/JwtHelper.DEVGUIDE.md) 了解详细用法
