# DRX.Environment 会话系统 - API客户端使用指南

## 📖 概述

本示例展示了如何在API客户端（移动App、桌面应用等）中使用DRX.Environment的会话系统。与浏览器不同，API客户端需要手动管理会话Cookie。

## 🚀 快速开始

### 1. 启动服务器

首先启动会话服务器：

```bash
cd Examples/SessionExample
dotnet run --project Program.cs
```

服务器将在 `http://localhost:8080` 启动。

### 2. 运行API客户端示例

```bash
cd Examples/SessionExample
dotnet run --project ApiClientExample.cs
```

## 🔧 API客户端核心功能

### Cookie管理

API客户端需要手动处理HTTP Cookie：

```csharp
// 初始化Cookie容器
var cookieContainer = new CookieContainer();
var handler = new HttpClientHandler { CookieContainer = cookieContainer };
var httpClient = new HttpClient(handler);
```

### 登录流程

```csharp
var client = new SessionApiClient();

// 登录
bool success = await client.LoginAsync("admin", "123456");

// 检查状态
var status = await client.CheckLoginStatusAsync();
if (status.IsLoggedIn) {
    Console.WriteLine($"用户: {status.User}, 自动登录: {status.AutoLogin}");
}
```

### 自动登录

```csharp
// 程序启动时尝试自动登录
bool autoLoginSuccess = await client.TryAutoLoginAsync();
if (autoLoginSuccess) {
    // 用户已登录，可以直接使用
} else {
    // 需要重新登录
}
```

### 会话持久化

Cookie自动保存到本地文件：

```csharp
// Cookie保存到 session_cookies.txt
// 程序重启时自动加载
```

## 📋 完整使用流程

### 基本使用

1. **创建客户端实例**
   ```csharp
   using var client = new SessionApiClient("http://localhost:8080");
   ```

2. **登录**
   ```csharp
   await client.LoginAsync("username", "password");
   ```

3. **执行业务操作**
   ```csharp
   await client.AddToCartAsync("商品名");
   var cart = await client.ViewCartAsync();
   ```

4. **注销**
   ```csharp
   await client.LogoutAsync();
   ```

### 高级功能

#### 自定义Cookie文件位置

```csharp
var client = new SessionApiClient(
    baseUrl: "https://api.example.com",
    cookieFile: "my_app_cookies.txt"
);
```

#### 错误处理

```csharp
try {
    var result = await client.AccessProtectedResourceAsync();
    if (result.Contains("需要登录")) {
        // 重新登录
        await client.LoginAsync(username, password);
    }
} catch (HttpRequestException ex) {
    // 网络错误处理
}
```

#### 会话过期检测

```csharp
var status = await client.CheckLoginStatusAsync();
if (!status.IsLoggedIn) {
    // 会话过期，需要重新登录
    await client.LoginAsync(username, password);
}
```

## 🔒 安全考虑

### Cookie安全

- Cookie文件包含敏感的会话信息
- 在生产环境中应该加密存储
- 定期清理过期Cookie

### HTTPS使用

```csharp
// 生产环境使用HTTPS
var client = new SessionApiClient("https://secure-api.example.com");
```

### 会话劫持防护

服务器端已经实现了基本的防护：

- HttpOnly Cookie（防止JavaScript访问）
- Secure Cookie（仅HTTPS传输）
- SameSite策略（防止CSRF）

## 🧪 测试命令

### 使用curl测试（验证服务器功能）

```bash
# 登录
curl -X POST -d "username=admin&password=123456" http://localhost:8080/login -c cookies.txt

# 检查状态
curl http://localhost:8080/auth/status -b cookies.txt

# 添加商品
curl -X POST "http://localhost:8080/cart/add?item=苹果" -b cookies.txt

# 查看购物车
curl http://localhost:8080/cart -b cookies.txt

# 注销
curl -X POST http://localhost:8080/logout -b cookies.txt
```

### 浏览器测试

打开 `http://localhost:8080/login.html` 进行浏览器测试。

## 📁 文件说明

- `Program.cs` - 服务器示例
- `ApiClientExample.cs` - API客户端示例
- `login.html` - 浏览器客户端示例
- `session_cookies.txt` - 保存的Cookie文件（自动生成）

## 🔄 工作原理

1. **登录时**: 服务器创建会话，设置Cookie，客户端保存Cookie
2. **后续请求**: 客户端在每个请求中发送Cookie
3. **服务器验证**: 通过Cookie找到对应会话，验证用户状态
4. **自动登录**: 客户端启动时加载Cookie，检查会话是否仍然有效

## 🚨 注意事项

- Cookie文件包含敏感信息，不要提交到版本控制系统
- 生产环境中使用HTTPS
- 定期更新会话超时时间
- 实现适当的错误处理和重试机制

## 🎯 最佳实践

1. **单例客户端**: 在应用中保持一个HttpClient实例
2. **自动重登录**: 检测到401时自动尝试重新登录
3. **会话监控**: 定期检查会话状态
4. **优雅降级**: 会话失效时提供适当的用户提示
5. **日志记录**: 记录重要的会话操作用于调试

这样您就可以在任何类型的API客户端中使用DRX.Environment的会话系统了！