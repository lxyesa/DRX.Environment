# KaxSocket 服务器架构分析

## 项目概述

**KaxSocket** 是一个基于 .NET 9.0 的 HTTP 服务器应用，主要功能是提供用户管理、资源管理（Asset）和 CDK（兑换码）管理的后端服务。该项目采用模块化设计，分离了业务逻辑、数据模型和网络通信层。

---

## 核心架构

### 1. 入口点：`Program.cs`

**职责**：应用启动、权限检查、服务器初始化

**主要流程**：

```
Main()
  ├─ 权限检查 (GlobalUtility.IsAdministrator)
  │   └─ 若无管理员权限 → 以管理员权限重启
  │
  ├─ 配置初始化 (ConfigUtility)
  │   ├─ 读取 uploadToken（上传令牌）
  │   ├─ 读取 version（版本号）
  │   └─ 若不存在则生成默认值
  │
  ├─ HTTP 服务器创建 (DrxHttpServer)
  │   ├─ 监听地址：http://+:8462/
  │   ├─ 设置静态文件根路径：Views/
  │   │
  │   ├─ 注册路由（HTML 页面）
  │   │   ├─ GET / → index.html
  │   │   ├─ GET /login → login.html
  │   │   ├─ GET /register → register.html
  │   │   ├─ GET /cdk/admin → cdkadmin.html
  │   │   ├─ GET /asset/admin → assetadmin.html
  │   │   ├─ GET /profile → profile.html
  │   │   └─ GET /profile/{uid} → profile.html
  │   │
  │   ├─ 注册 HTTP 处理器
  │   │   ├─ DLTBModPackerHttp（DLTB Mod 相关 API）
  │   │   └─ KaxHttp（Kax 核心 API）
  │   │
  │   ├─ 注册命令处理器
  │   │   └─ KaxCommandHandler（控制台命令）
  │   │
  │   └─ 定时任务（每 60 秒执行一次）
  │       └─ KaxGlobal.CleanUpAssets()（清理过期资源）
  │
  └─ 启动服务器 (server.StartAsync)
```

**关键代码片段**：
```csharp
// 权限检查
if (!GlobalUtility.IsAdministrator())
{
    _ = GlobalUtility.RestartAsAdministratorAsync();
    Environment.Exit(0);
}

// 配置读写
var uploadToken = ConfigUtility.Read("configs.ini", "upload_token", "general");
ConfigUtility.Push("configs.ini", "upload_token", uploadToken, "general");

// 服务器启动
var server = new DrxHttpServer(new[] { "http://+:8462/" });
await server.StartAsync();
```

---

## 数据模型层

### 2. 数据模型（`Model/` 目录）

#### 2.1 基础模型：`DataModel.cs`

```csharp
public abstract class DataModel : IDataBase
{
    public int Id { get; set; }  // 数据库主键
}
```

**继承关系**：
- `DataModel` ← `ModInfo`（模组信息）
- `DataModel` ← `UserData`（用户数据）

#### 2.2 用户数据模型：`UserData`

**字段**：
| 字段 | 类型 | 说明 |
|------|------|------|
| `Id` | int | 主键 |
| `UserName` | string | 用户名 |
| `PasswordHash` | string | 密码哈希（SHA256） |
| `Email` | string | 邮箱 |
| `RegisteredAt` | long | 注册时间戳 |
| `LastLoginAt` | long | 最后登录时间戳 |
| `LoginToken` | string | JWT 登录令牌 |
| `DisplayName` | string | 显示名称 |
| `Signature` | string | 个性签名 |
| `Bio` | string | 个人简介 |
| `PermissionGroup` | UserPermissionGroup | 权限组（Console/Root/Admin/User） |
| `Status` | UserStatus | 用户状态（封禁等） |
| `ActiveAssets` | TableList<ActiveAssets> | 激活的资源列表 |
| `RecentActivity` | int | 最近活动计数 |
| `ResourceCount` | int | 资源数量 |

**权限组枚举**：
```csharp
enum UserPermissionGroup
{
    Console = 0,  // 控制台权限
    Root = 1,     // 根权限
    Admin = 2,    // 管理员权限
    User = 100    // 普通用户
}
```

#### 2.3 资源模型：`AssetModel.cs`

```csharp
public class AssetModel : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; }           // 资源名称
    public string Version { get; set; }        // 版本
    public string Author { get; set; }         // 作者
    public string Description { get; set; }    // 描述
    public long LastUpdatedAt { get; set; }    // 最后更新时间戳
    public bool IsDeleted { get; set; }        // 软删除标记
    public long DeletedAt { get; set; }        // 删除时间戳
}
```

#### 2.4 CDK 模型：`CdkModel.cs`

```csharp
public class CdkModel : IDataBase
{
    public int Id { get; set; }                // 主键
    public string Code { get; set; }           // CDK 码（唯一）
    public string Description { get; set; }    // 描述
    public bool IsUsed { get; set; }           // 是否已使用
    public long CreatedAt { get; set; }        // 创建时间戳
    public long UsedAt { get; set; }           // 使用时间戳
    public string UsedBy { get; set; }         // 使用者用户名
    public int AssetId { get; set; }           // 关联资源 ID
    public int ContributionValue { get; set; } // 贡献值
    public long ExpiresInSeconds { get; set; } // 有效期（秒）
}
```

---

## 业务逻辑层

### 3. 全局业务管理：`KaxGlobal.cs`

**职责**：集中管理数据库、用户操作、资源操作

**数据库实例**：
```csharp
public static readonly SqliteV2<UserData> UserDatabase 
    = new SqliteV2<UserData>("kax_users.db", AppDomain.CurrentDomain.BaseDirectory);

public static readonly SqliteV2<CdkModel> CdkDatabase 
    = new SqliteV2<CdkModel>("cdk.db", AppDomain.CurrentDomain.BaseDirectory);

public static readonly SqliteV2<AssetModel> AssetDataBase 
    = new SqliteV2<AssetModel>("assets.db", AppDomain.CurrentDomain.BaseDirectory);
```

**核心方法**：

| 方法 | 功能 |
|------|------|
| `BanUser(userName, reason, durationSeconds)` | 封禁用户 |
| `UnBanUser(userName)` | 解除封禁 |
| `GenerateLoginToken(user)` | 生成 JWT 令牌 |
| `SetUserPermissionGroup(userName, group)` | 设置权限组 |
| `AddActiveAssetToUser(userName, assetId, duration)` | 为用户添加激活资源 |
| `CleanUpAssets()` | 清理过期资源（定时任务） |

---

## HTTP 处理层

### 4. HTTP 请求处理器

#### 4.1 `KaxHttp.cs`（核心 API，1611 行）

**职责**：处理用户认证、注册、登录、资源管理等 HTTP 请求

**关键特性**：

1. **JWT 认证配置**
```csharp
JwtHelper.Configure(new JwtHelper.JwtConfig
{
    SecretKey = "A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6",
    Issuer = "KaxSocket",
    Audience = "KaxUsers",
    Expiration = TimeSpan.FromHours(1)
});
```

2. **限流机制**
```csharp
[HttpHandle("/api/user/register", "POST", 
    RateLimitMaxRequests = 3, 
    RateLimitWindowSeconds = 60)]
public static async Task<HttpResponse> PostRegister(HttpRequest request)
```

3. **主要 API 端点**：
   - `POST /api/user/register` - 用户注册（限流：3次/60秒）
   - `POST /api/user/login` - 用户登录
   - `GET /api/user/profile` - 获取用户资料
   - `POST /api/user/profile` - 更新用户资料
   - `POST /api/user/password` - 修改密码
   - `GET /api/asset/*` - 资源相关 API
   - `POST /api/cdk/*` - CDK 管理 API

4. **权限检查**
```csharp
private static async Task<bool> IsCdkAdminUser(string? userName)
{
    var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName))
        .FirstOrDefault();
    var g = user?.PermissionGroup;
    return g == UserPermissionGroup.Console 
        || g == UserPermissionGroup.Root 
        || g == UserPermissionGroup.Admin;
}
```

5. **HTTP 中间件**
```csharp
[HttpMiddleware]
public static HttpResponse Echo(HttpRequest request, Func<HttpRequest, HttpResponse> next)
{
    Logger.Info($"收到 HTTP 请求: {request.Method} {request.Path}");
    return next(request);
}
```

6. **限流回调**
```csharp
public static HttpResponse RateLimitCallback(int count, HttpRequest request, OverrideContext ctx)
{
    if (count > 20)
    {
        var userName = JwtHelper.ValidateToken(userToken)?.Identity?.Name ?? "未知用户";
        _ = KaxGlobal.BanUser(userName, "短时间内请求过于频繁，自动封禁。", 60);
        return new HttpResponse(429, "请求过于频繁，您的账号暂时被封禁。");
    }
    return new HttpResponse(429, "请求过于频繁，请稍后再试。");
}
```

#### 4.2 `DLTBModPackerHttp.cs`（DLTB Mod 相关 API）

**职责**：处理 DLTB（某游戏）Mod 打包器相关的 HTTP 请求

**主要功能**：
- Mod 版本管理
- Mod 上传/下载
- Mod 元数据管理

---

## 命令处理层

### 5. 控制台命令处理：`KaxCommandHandler.cs`

**职责**：处理服务器控制台命令

**可用命令**：

| 命令 | 参数 | 说明 | 权限 |
|------|------|------|------|
| `ban` | `<username> <reason> <duration>` | 封禁用户 | 开发者 |
| `unban` | `<username>` | 解除封禁 | 开发者 |
| `setpermission` | `<username> <group>` | 设置权限组 | 开发者 |
| `addasset` | `<username> <assetId> <duration>` | 添加激活资源 | 开发者 |
| `help` | 无 | 显示帮助 | 所有用户 |

**实现示例**：
```csharp
[Command("ban <username> <reason> <duration>", "helper:封禁用户", "仅限开发者使用")]
public static void Cmd_BanUser(string userName, string reason, long durationSeconds)
{
    KaxGlobal.BanUser(userName, reason, durationSeconds).Wait();
}

[Command("unban <username>", "helper:解除用户封禁", "仅限开发者使用")]
public static void Cmd_UnBanUser(string userName)
{
    KaxGlobal.UnBanUser(userName).Wait();
}
```

---

## SDK 依赖层

### 6. 核心 SDK 组件

#### 6.1 `Drx.Sdk.Shared.Utility` - 通用工具

**GlobalUtility**（全局工具）
```csharp
public static class GlobalUtility
{
    // 检查是否以管理员权限运行
    public static bool IsAdministrator()
    
    // 以管理员权限重启当前进程
    public static async Task<bool> RestartAsAdministratorAsync(string[]? args = null)
    
    // 运行外部命令
    public static ProcessResult RunCommand(string command, string? workingDirectory = null)
}
```

**ConfigUtility**（配置管理）
```csharp
public static class ConfigUtility
{
    // 支持三种格式：INI、JSON、XML
    enum StorageFormat { JSON, INI, XML }
    
    // 读取配置值
    public static string? Read(string filePath, string key, string group = "default")
    
    // 写入配置值
    public static void Push(string filePath, string key, string value, string group = "default")
    
    // 获取整个分组
    public static Dictionary<string, string> GetGroup(string filePath, string group)
}
```

**CommonUtility**（通用工具）
```csharp
public static class CommonUtility
{
    // 生成通用代码（如 CDK 码）
    public static string GenerateGeneralCode(string prefix, int length, int segmentLength, 
        bool includeNumbers, bool includeLetters)
    
    // 计算 SHA256 哈希
    public static string ComputeSHA256Hash(string input)
    
    // 验证邮箱格式
    public static bool IsValidEmail(string email)
}
```

#### 6.2 `Drx.Sdk.Network.V2.Web` - HTTP 服务器框架

**DrxHttpServer**（HTTP 服务器，4581 行）
```csharp
public class DrxHttpServer : IAsyncDisposable
{
    // 添加路由
    public void AddRoute(HttpMethod method, string path, 
        Func<HttpRequest, IActionResult> handler)
    
    // 从程序集注册处理器（自动扫描 [HttpHandle] 特性）
    public void RegisterHandlersFromAssembly(Type markerType)
    
    // 注册命令处理器
    public void RegisterCommandsFromType(Type commandType)
    
    // 定时任务
    public void DoTicker(int intervalMs, Func<DrxHttpServer, Task> callback)
    
    // 启动服务器
    public async Task StartAsync()
}
```

**关键特性**：
- 自动路由发现（通过 `[HttpHandle]` 特性）
- 限流控制（RateLimitMaxRequests、RateLimitWindowSeconds）
- HTTP 中间件支持（`[HttpMiddleware]`）
- 异步请求处理
- Cookie 管理
- 文件上传/下载

#### 6.3 `Drx.Sdk.Network.DataBase.Sqlite.V2` - 数据库访问

**SqliteV2<T>**（泛型 SQLite 数据库访问）
```csharp
public class SqliteV2<T> where T : class, IDataBase, new()
{
    // 查询所有记录
    public async Task<List<T>> SelectAllAsync()
    
    // 条件查询
    public async Task<List<T>> SelectWhereAsync(string columnName, object value)
    
    // 插入记录
    public async Task<int> InsertAsync(T entity)
    
    // 更新记录
    public async Task<bool> UpdateAsync(T entity)
    
    // 删除记录
    public async Task<bool> DeleteAsync(int id)
}
```

---

## 文件结构

```
KaxSocket/
├── Program.cs                          # 应用入口
├── KaxSocket.csproj                    # 项目配置
├── KaxGlobal.cs                        # 全局业务管理
├── DLTBModPackerGlobal.cs              # DLTB Mod 全局管理
├── PreserveHandlers.cs                 # Trimmer 保留声明
│
├── Model/                              # 数据模型
│   ├── DataModel.cs                    # 基础模型
│   ├── AssetModel.cs                   # 资源模型
│   └── CdkModel.cs                     # CDK 模型
│
├── Handlers/                           # HTTP 处理器
│   ├── KaxHttp.cs                      # 核心 API（1611 行）
│   ├── DLTBModPackerHttp.cs            # DLTB Mod API
│   ├── api文档.md                      # API 文档
│   └── Command/
│       └── KaxCommandHandler.cs        # 控制台命令
│
├── Views/                              # 前端页面
│   ├── index.html
│   ├── login.html
│   ├── register.html
│   ├── profile.html
│   ├── cdkadmin.html
│   └── assetadmin.html
│
└── test_db/                            # 测试数据库
```

---

## 调用关系图

### 启动流程调用链

```
Program.Main()
  │
  ├─→ GlobalUtility.IsAdministrator()
  │     └─→ WindowsIdentity.GetCurrent()
  │
  ├─→ GlobalUtility.RestartAsAdministratorAsync()
  │     └─→ ProcessStartInfo (UAC 提升)
  │
  ├─→ ConfigUtility.Read("configs.ini", "upload_token", "general")
  │     └─→ 读取 INI 文件
  │
  ├─→ CommonUtility.GenerateGeneralCode("UPL", 8, 4, true, true)
  │     └─→ 生成随机码
  │
  ├─→ ConfigUtility.Push("configs.ini", "upload_token", uploadToken, "general")
  │     └─→ 写入 INI 文件
  │
  ├─→ new DrxHttpServer(prefixes)
  │     └─→ 初始化 HTTP 服务器
  │
  ├─→ server.AddRoute(HttpMethod.Get, "/", ...)
  │     └─→ 注册静态路由
  │
  ├─→ server.RegisterHandlersFromAssembly(typeof(KaxHttp))
  │     ├─→ 扫描 [HttpHandle] 特性
  │     ├─→ 扫描 [HttpMiddleware] 特性
  │     └─→ 自动注册所有处理方法
  │
  ├─→ server.RegisterCommandsFromType(typeof(KaxCommandHandler))
  │     └─→ 扫描 [Command] 特性
  │
  ├─→ server.DoTicker(60000, async (s) => await KaxGlobal.CleanUpAssets())
  │     └─→ 注册定时任务
  │
  └─→ await server.StartAsync()
        └─→ 启动 HTTP 监听
```

### HTTP 请求处理流程

```
HTTP 请求到达
  │
  ├─→ DrxHttpServer.OnRequest()
  │     │
  │     ├─→ [HttpMiddleware] Echo()
  │     │     └─→ 记录请求日志
  │     │
  │     ├─→ 限流检查 (RateLimit)
  │     │     ├─→ 若超限 → RateLimitCallback()
  │     │     │     └─→ 若超过 20 次 → KaxGlobal.BanUser()
  │     │     └─→ 若正常 → 继续处理
  │     │
  │     ├─→ 路由匹配
  │     │     ├─→ 静态路由 (HTML 文件)
  │     │     │     └─→ HtmlResultFromFile
  │     │     │
  │     │     └─→ 动态路由 ([HttpHandle] 处理器)
  │     │           ├─→ KaxHttp.PostRegister()
  │     │           │     ├─→ 验证邮箱 (CommonUtility.IsValidEmail)
  │     │           │     ├─→ 计算密码哈希 (CommonUtility.ComputeSHA256Hash)
  │     │           │     ├─→ 插入用户 (KaxGlobal.UserDatabase.InsertAsync)
  │     │           │     └─→ 返回 HttpResponse
  │     │           │
  │     │           ├─→ KaxHttp.PostLogin()
  │     │           │     ├─→ 查询用户 (KaxGlobal.UserDatabase.SelectWhereAsync)
  │     │           │     ├─→ 验证密码
  │     │           │     ├─→ 生成 JWT (JwtHelper.GenerateToken)
  │     │           │     └─→ 返回令牌
  │     │           │
  │     │           ├─→ KaxHttp.GetProfile()
  │     │           │     ├─→ 验证 JWT 令牌
  │     │           │     ├─→ 查询用户信息
  │     │           │     └─→ 返回用户资料
  │     │           │
  │     │           └─→ ... 其他 API 处理器
  │     │
  │     └─→ 返回 HttpResponse
  │
  └─→ 响应发送给客户端
```

### 数据库操作流程

```
KaxGlobal.UserDatabase (SqliteV2<UserData>)
  │
  ├─→ SelectAllAsync()
  │     └─→ SELECT * FROM UserData
  │
  ├─→ SelectWhereAsync("UserName", userName)
  │     └─→ SELECT * FROM UserData WHERE UserName = ?
  │
  ├─→ InsertAsync(user)
  │     └─→ INSERT INTO UserData (...)
  │
  ├─→ UpdateAsync(user)
  │     └─→ UPDATE UserData SET ... WHERE Id = ?
  │
  └─→ DeleteAsync(id)
        └─→ DELETE FROM UserData WHERE Id = ?
```

---

## 关键业务流程

### 用户注册流程

```
1. 客户端发送 POST /api/user/register
   {
     "username": "user123",
     "password": "pass123",
     "email": "user@example.com"
   }

2. KaxHttp.PostRegister() 处理
   ├─ 验证邮箱格式 (CommonUtility.IsValidEmail)
   ├─ 检查用户名是否存在
   ├─ 计算密码哈希 (CommonUtility.ComputeSHA256Hash)
   ├─ 创建 UserData 对象
   ├─ 插入数据库 (KaxGlobal.UserDatabase.InsertAsync)
   └─ 返回成功响应

3. 数据库存储
   kax_users.db
   ├─ Id: 1
   ├─ UserName: "user123"
   ├─ PasswordHash: "abc123def456..."
   ├─ Email: "user@example.com"
   ├─ RegisteredAt: 1708108800
   └─ ... 其他字段
```

### 用户登录流程

```
1. 客户端发送 POST /api/user/login
   {
     "username": "user123",
     "password": "pass123"
   }

2. KaxHttp.PostLogin() 处理
   ├─ 查询用户 (KaxGlobal.UserDatabase.SelectWhereAsync)
   ├─ 验证密码哈希
   ├─ 检查用户是否被封禁
   ├─ 生成 JWT 令牌 (JwtHelper.GenerateToken)
   ├─ 更新 LastLoginAt 和 LoginToken
   ├─ 保存到数据库
   └─ 返回令牌

3. 客户端使用令牌
   Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

### CDK 兑换流程

```
1. 管理员生成 CDK
   ├─ 生成唯一码 (CommonUtility.GenerateGeneralCode)
   ├─ 设置关联资源 (AssetId)
   ├─ 设置有效期 (ExpiresInSeconds)
   ├─ 插入数据库 (KaxGlobal.CdkDatabase.InsertAsync)
   └─ cdk.db 存储

2. 用户兑换 CDK
   ├─ 发送 POST /api/cdk/redeem
   ├─ 验证 CDK 码
   ├─ 检查是否已使用
   ├─ 检查是否过期
   ├─ 标记为已使用 (IsUsed = true)
   ├─ 记录使用者 (UsedBy = userName)
   ├─ 为用户添加资源 (KaxGlobal.AddActiveAssetToUser)
   └─ 返回成功

3. 资源激活
   ├─ 创建 ActiveAssets 记录
   ├─ 设置过期时间
   ├─ 添加到 UserData.ActiveAssets
   └─ 用户可使用该资源
```

---

## 数据库设计

### 三个主要数据库文件

| 文件名 | 表名 | 用途 |
|--------|------|------|
| `kax_users.db` | UserData | 存储用户账户、权限、资源 |
| `cdk.db` | CdkModel | 存储 CDK 码及兑换记录 |
| `assets.db` | AssetModel | 存储资源元数据 |

### 用户状态管理

```csharp
public class UserStatus
{
    public bool IsBanned { get; set; }           // 是否被封禁
    public long BannedAt { get; set; }           // 封禁时间戳
    public long BanExpiresAt { get; set; }       // 封禁过期时间戳
    public string BanReason { get; set; }        // 封禁原因
}
```

---

## 安全机制

### 1. 权限控制

```csharp
// 权限组分级
enum UserPermissionGroup
{
    Console = 0,   // 最高权限（控制台）
    Root = 1,      // 根权限
    Admin = 2,     // 管理员权限
    User = 100     // 普通用户（最低权限）
}

// 权限检查示例
private static async Task<bool> IsCdkAdminUser(string? userName)
{
    var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName))
        .FirstOrDefault();
    return user?.PermissionGroup >= UserPermissionGroup.Admin;
}
```

### 2. 限流保护

```csharp
// 注册 API 限流：3 次/60 秒
[HttpHandle("/api/user/register", "POST", 
    RateLimitMaxRequests = 3, 
    RateLimitWindowSeconds = 60)]

// 超限自动封禁
if (count > 20)
{
    await KaxGlobal.BanUser(userName, "短时间内请求过于频繁，自动封禁。", 60);
}
```

### 3. 密码安全

```csharp
// 使用 SHA256 哈希存储密码
user.PasswordHash = CommonUtility.ComputeSHA256Hash(password);

// 验证时重新计算哈希
if (userExists.PasswordHash == CommonUtility.ComputeSHA256Hash(password))
{
    // 密码正确
}
```

### 4. JWT 认证

```csharp
// 配置 JWT
JwtHelper.Configure(new JwtHelper.JwtConfig
{
    SecretKey = "A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6",
    Issuer = "KaxSocket",
    Audience = "KaxUsers",
    Expiration = TimeSpan.FromHours(1)  // 1 小时过期
});

// 生成令牌
var token = JwtHelper.GenerateToken(user.Id.ToString(), user.UserName, user.Email);

// 验证令牌
var principal = JwtHelper.ValidateToken(token);
```

---

## 项目依赖关系

### NuGet 包依赖

```xml
<!-- JWT 令牌处理 -->
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.14.0" />

<!-- 图形绘制（用于头像等） -->
<PackageReference Include="System.Drawing.Common" Version="6.0.0" />
```

### 项目引用

```xml
<!-- SDK 网络库 -->
<ProjectReference Include="..\..\..\Environments\SDK\Drx.Sdk.Network\Drx.Sdk.Network.csproj" />
```

**Drx.Sdk.Network 包含**：
- `Drx.Sdk.Network.V2.Web` - HTTP 服务器框架
- `Drx.Sdk.Network.DataBase.Sqlite.V2` - SQLite 数据库访问
- `Drx.Sdk.Network.Email` - 邮件功能
- 其他网络相关组件

---

## 文件组织结构

```
KaxSocket/
├── Program.cs                          # 应用入口（~80 行）
│   └─ 职责：启动、权限检查、服务器初始化
│
├── KaxGlobal.cs                        # 全局业务管理（~472 行）
│   └─ 职责：数据库管理、用户操作、资源操作
│
├── DLTBModPackerGlobal.cs              # DLTB Mod 全局管理
│   └─ 职责：Mod 信息数据库管理
│
├── PreserveHandlers.cs                 # Trimmer 保留声明
│   └─ 职责：为 NativeAOT 编译保留元数据
│
├── Model/                              # 数据模型层
│   ├── DataModel.cs                    # 基础模型（104 行）
│   ├── AssetModel.cs                   # 资源模型
│   └── CdkModel.cs                     # CDK 模型
│
├── Handlers/                           # HTTP 处理器层
│   ├── KaxHttp.cs                      # 核心 API（1611 行）
│   │   ├─ 用户认证（注册、登录）
│   │   ├─ 用户资料管理
│   │   ├─ 资源管理
│   │   ├─ CDK 管理
│   │   └─ 权限检查
│   │
│   ├── DLTBModPackerHttp.cs            # DLTB Mod API
│   │   └─ Mod 版本管理、上传、下载
│   │
│   └── Command/
│       └── KaxCommandHandler.cs        # 控制台命令（70 行）
│           ├─ ban/unban 用户
│           ├─ 设置权限组
│           ├─ 添加资源
│           └─ 帮助信息
│
├── Views/                              # 前端静态文件
│   ├── index.html                      # 首页
│   ├── login.html                      # 登录页
│   ├── register.html                   # 注册页
│   ├── profile.html                    # 用户资料页
│   ├── cdkadmin.html                   # CDK 管理页
│   └── assetadmin.html                 # 资源管理页
│
├── KaxSocket.csproj                    # 项目配置
│   ├─ 目标框架：net9.0-windows
│   ├─ 运行时：win-x64
│   └─ 发布为 NativeAOT
│
└── test_db/                            # 测试数据库目录
```

---

## 关键技术点

### 1. 异步编程

所有数据库操作和 HTTP 处理都使用 `async/await`：

```csharp
// 异步查询
var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName))
    .FirstOrDefault();

// 异步插入
await KaxGlobal.UserDatabase.InsertAsync(newUser);

// 异步更新
await KaxGlobal.UserDatabase.UpdateAsync(user);
```

### 2. 特性驱动的处理器注册

使用反射自动发现和注册处理器：

```csharp
// HTTP 处理器特性
[HttpHandle("/api/user/register", "POST", RateLimitMaxRequests = 3)]
public static async Task<HttpResponse> PostRegister(HttpRequest request)

// 命令处理器特性
[Command("ban <username> <reason> <duration>", "helper:封禁用户")]
public static void Cmd_BanUser(string userName, string reason, long durationSeconds)

// HTTP 中间件特性
[HttpMiddleware]
public static HttpResponse Echo(HttpRequest request, Func<HttpRequest, HttpResponse> next)
```

### 3. 泛型数据库访问

```csharp
// 泛型约束：T 必须实现 IDataBase 接口
public class SqliteV2<T> where T : class, IDataBase, new()
{
    // 通用的 CRUD 操作
}

// 使用示例
var userDb = new SqliteV2<UserData>("kax_users.db", basePath);
var cdkDb = new SqliteV2<CdkModel>("cdk.db", basePath);
var assetDb = new SqliteV2<AssetModel>("assets.db", basePath);
```

### 4. 配置管理

支持多种格式的配置文件：

```csharp
// INI 格式（默认）
ConfigUtility.Push("configs.ini", "upload_token", "UPL_ABC123", "general");
var token = ConfigUtility.Read("configs.ini", "upload_token", "general");

// JSON 格式
ConfigUtility.Push("config.json", "key", "value", "section", 
    ConfigUtility.StorageFormat.JSON);

// XML 格式
ConfigUtility.Push("config.xml", "key", "value", "section", 
    ConfigUtility.StorageFormat.XML);
```

### 5. 权限管理

基于权限组的访问控制：

```csharp
// 检查是否为 CDK 管理员
private static async Task<bool> IsCdkAdminUser(string? userName)
{
    var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName))
        .FirstOrDefault();
    
    return user?.PermissionGroup >= UserPermissionGroup.Admin;
}

// 在 API 中使用
if (!await IsCdkAdminUser(userName))
{
    return new HttpResponse(403, "权限不足");
}
```

---

## 运行流程总结

### 启动阶段

1. **权限检查** → 若无管理员权限则重启
2. **配置初始化** → 读取或创建 configs.ini
3. **服务器创建** → 初始化 DrxHttpServer
4. **路由注册** → 注册静态路由和动态处理器
5. **命令注册** → 注册控制台命令
6. **定时任务** → 启动资源清理任务
7. **启动监听** → 开始接收 HTTP 请求

### 请求处理阶段

1. **请求到达** → DrxHttpServer 接收
2. **中间件处理** → Echo 中间件记录日志
3. **限流检查** → 检查请求频率
4. **路由匹配** → 查找对应的处理器
5. **业务处理** → 执行 KaxHttp 或 DLTBModPackerHttp 中的方法
6. **数据库操作** → 通过 KaxGlobal 访问数据库
7. **响应返回** → 返回 HttpResponse

### 关键时间点

- **定时任务** → 每 60 秒执行一次 `KaxGlobal.CleanUpAssets()`
- **JWT 过期** → 1 小时后令牌失效
- **用户封禁** → 默认 60 秒后自动解除
- **CDK 有效期** → 由 `ExpiresInSeconds` 字段控制

---

## 扩展点

### 添加新的 API 端点

```csharp
// 在 KaxHttp.cs 中添加
[HttpHandle("/api/custom/endpoint", "POST", RateLimitMaxRequests = 10)]
public static async Task<HttpResponse> PostCustomEndpoint(HttpRequest request)
{
    // 处理逻辑
    return new HttpResponse(200, "Success");
}
```

### 添加新的控制台命令

```csharp
// 在 KaxCommandHandler.cs 中添加
[Command("custom <param>", "helper:自定义命令", "所有用户可用")]
public static void Cmd_Custom(string param)
{
    Console.WriteLine($"参数: {param}");
}
```

### 添加新的数据模型

```csharp
// 创建新模型
public class CustomModel : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; }
    // ... 其他字段
}

// 在 KaxGlobal.cs 中添加
public static readonly SqliteV2<CustomModel> CustomDatabase 
    = new SqliteV2<CustomModel>("custom.db", AppDomain.CurrentDomain.BaseDirectory);
```

---

## 总结

**KaxSocket** 是一个设计良好的模块化 HTTP 服务器应用，具有以下特点：

✅ **清晰的分层架构**
- 入口层（Program.cs）
- 业务逻辑层（KaxGlobal.cs）
- 数据模型层（Model/）
- HTTP 处理层（Handlers/）
- SDK 依赖层（Drx.Sdk.*）

✅ **完善的安全机制**
- JWT 认证
- 权限分级控制
- 限流保护
- 密码哈希存储
- 用户封禁机制

✅ **灵活的扩展性**
- 特性驱动的处理器注册
- 泛型数据库访问
- 支持多种配置格式
- 易于添加新的 API 和命令

✅ **生产级别的特性**
- 异步编程
- 定时任务
- 软删除支持
- 详细的日志记录
- NativeAOT 编译支持

✅ **完整的功能集**
- 用户管理（注册、登录、资料）
- 资源管理（Asset）
- CDK 兑换系统
- Mod 管理（DLTB）
- 权限管理
- 控制台命令行界面
