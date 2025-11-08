#file:JwtHelper.DEVGUIDE.md

## 概述
`JwtHelper` 是一个静态工具类，位于 `Drx.Sdk.Network.V2.Web` 命名空间下，提供 JWT（JSON Web Token）令牌的生成、验证与相关 HTTP 响应辅助功能。适用于需要用户身份认证、授权的 .NET 网络服务。

## 输入 / 输出契约
- 输入：
  - 用户声明（`IEnumerable<Claim>`）、用户信息（userId, userName, email）、HTTP 请求对象（`HttpRequest`）等
- 输出：
  - JWT 字符串、`ClaimsPrincipal`、未授权 HTTP 响应（`HttpResponse`）
- 成功标准：
  - 正确生成、验证 JWT，返回有效声明主体或令牌字符串
- 错误模式 / 异常：
  - 配置为空抛出 `ArgumentNullException`
  - 验证失败返回 null，不抛出异常

## 公共 API 总览
| 名称 | 签名 | 描述 | 返回值 | 错误 |
|---|---|---|---|---|
| Configure | void Configure(JwtConfig config) | 设置全局 JWT 配置 | void | ArgumentNullException |
| GenerateToken | string GenerateToken(IEnumerable<Claim> claims) | 生成 JWT 令牌 | string | - |
| GenerateToken | string GenerateToken(string userId, string userName, string? email = null) | 简化生成 JWT 令牌 | string | - |
| ValidateToken | ClaimsPrincipal? ValidateToken(string token) | 验证 JWT 令牌 | ClaimsPrincipal/null | - |
| ValidateTokenFromRequest | ClaimsPrincipal? ValidateTokenFromRequest(HttpRequest request) | 从 HTTP 请求头提取并验证 JWT | ClaimsPrincipal/null | - |
| CreateUnauthorizedResponse | HttpResponse CreateUnauthorizedResponse(string message = ...) | 创建 401 未授权响应 | HttpResponse | - |

## 方法详解
### Configure
- 参数
  - config (`JwtConfig`)：JWT 配置对象，包含密钥、发行者、受众、过期时间
- 返回
  - void
- 行为
  - 设置全局 JWT 配置，若参数为 null 抛出异常
- 示例
```csharp
JwtHelper.Configure(new JwtHelper.JwtConfig {
    SecretKey = "mySuperSecretKey",
    Issuer = "MyApp",
    Audience = "MyUsers",
    Expiration = TimeSpan.FromHours(2)
});
```
- 边界/注意
  - 生产环境务必使用强密钥

### GenerateToken(IEnumerable<Claim> claims)
- 参数
  - claims (`IEnumerable<Claim>`)：用户声明集合
- 返回
  - string：JWT 字符串
- 行为
  - 根据声明生成 JWT，使用全局配置
- 示例
```csharp
var claims = new List<Claim> {
    new Claim(ClaimTypes.NameIdentifier, "123"),
    new Claim(ClaimTypes.Name, "Alice")
};
string token = JwtHelper.GenerateToken(claims);
```
- 边界/注意
  - claims 不能为空，否则生成的 token 无法携带身份信息

### GenerateToken(string userId, string userName, string? email = null)
- 参数
  - userId (`string`): 用户ID
  - userName (`string`): 用户名
  - email (`string?`): 邮箱（可选）
- 返回
  - string：JWT 字符串
- 行为
  - 快速生成包含基本身份信息的 JWT
- 示例
```csharp
string token = JwtHelper.GenerateToken("123", "Alice", "alice@example.com");
```
- 边界/注意
  - email 可为 null

### ValidateToken(string token)
- 参数
  - token (`string`): JWT 字符串
- 返回
  - `ClaimsPrincipal`：验证通过返回声明主体，失败返回 null
- 行为
  - 验证签名、发行者、受众、过期等
- 示例
```csharp
var principal = JwtHelper.ValidateToken(token);
if (principal == null) { /* 令牌无效 */ }
```
- 边界/注意
  - 失败时返回 null，不抛异常

### ValidateTokenFromRequest(HttpRequest request)
- 参数
  - request (`HttpRequest`): HTTP 请求对象，需有 Authorization 头
- 返回
  - `ClaimsPrincipal`：验证通过返回声明主体，失败返回 null
- 行为
  - 从请求头提取 Bearer 令牌并验证
- 示例
```csharp
var user = JwtHelper.ValidateTokenFromRequest(request);
if (user == null) { /* 未授权 */ }
```
- 边界/注意
  - Authorization 头格式需为 "Bearer ..."

### CreateUnauthorizedResponse(string message = ...)
- 参数
  - message (`string`): 响应消息，默认“无效或缺失的令牌。”
- 返回
  - `HttpResponse`：401 响应对象
- 行为
  - 构造带 WWW-Authenticate 头的 401 响应
- 示例
```csharp
return JwtHelper.CreateUnauthorizedResponse();
```

## 高级主题
- 支持自定义声明、过期时间、签名算法（如需扩展可修改 JwtConfig）
- 可与 ASP.NET Core 中间件集成，实现自动认证

## 并发与资源管理
- 静态配置线程安全（仅初始化时调用 Configure）
- 无需手动释放资源

## 边界、性能与安全
- 强烈建议生产环境使用高强度密钥
- 令牌过期时间不宜过长
- 验证失败不抛异常，需主动检查 null
- 不支持多重签名或复杂 token 结构（如需请扩展）

## 使用示例
```csharp
// 配置
JwtHelper.Configure(new JwtHelper.JwtConfig { SecretKey = "xxx", ... });
// 生成令牌
string token = JwtHelper.GenerateToken("1", "user");
// 验证令牌
var principal = JwtHelper.ValidateToken(token);
if (principal == null) { /* 处理未授权 */ }
```

## 常见问题 / FAQ
- Q: 验证失败抛异常吗？
  - A: 不抛异常，返回 null。
- Q: 如何自定义声明？
  - A: 使用 `GenerateToken(IEnumerable<Claim>)`。
- Q: 如何集成到 ASP.NET Core？
  - A: 可在中间件中调用 `ValidateTokenFromRequest`。

## 文件位置
- `Library/Environments/SDK/Drx.Sdk.Network/V2/Web/JwtHelper.cs`

## 下一步建议
- 增加单元测试覆盖异常和边界情况
- 补充 XML 注释和示例
- 可扩展支持刷新令牌、黑名单等高级功能

---
如何构建：
- 运行 `dotnet build DRX.Environment.sln`

建议：
- 建议为每个方法编写单元测试（正常/异常路径）
- 可在 `Examples/` 目录下添加集成示例
- 推荐使用静态分析工具检查安全性

快速验证：
- 生成并验证一个 token，确保 principal 不为 null

