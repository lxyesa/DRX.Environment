# Client 目录 - HTTP 客户端实现

## 概述
Client 目录包含完整的 HTTP 客户端实现，支持各种 HTTP 操作、文件上传/下载、会话管理和进度跟踪。

## 文件结构说明

### 核心文件

#### DrxHttpClient.cs
**HTTP 客户端主类和文档**
- 完整的 HTTP 客户端实现
- 拆分为多个 partial 文件便于维护
- 支持中文注释和文档

**已拆分的部分：**
- **DrxHttpClient.Base.cs** - 字段、构造函数、核心属性
- **DrxHttpClient.Send.cs** - 发送请求的各种重载
- **DrxHttpClient.Upload.cs** - 文件上传功能
- **DrxHttpClient.ResourceUpload.cs** - 资源上传（带回调）
- **DrxHttpClient.Download.cs** - 文件下载功能
- **DrxHttpClient.ResourceDownload.cs** - 资源下载（带回调）
- **DrxHttpClient.Queue.cs** - 后台请求队列处理
- **DrxHttpClient.Cookies.cs** - Cookie 和会话管理
- **DrxHttpClient.Helpers.cs** - 辅助方法和资源释放
- **DrxHttpClient.OpenAuth.cs** - OpenAuth 快速封装（构建授权 URL、交换 code）

### 支持类

#### HttpRequestTask.cs
**内部请求队列条目**
- 表示一个待处理的 HTTP 请求
- 用于后台请求队列管理

#### LLMHttpClient.cs
**LLM 相关的 HTTP 客户端**
- 专门针对大语言模型 API 的客户端
- 可能支持流式响应等 LLM 特定功能

## 主要功能

### 1. 请求发送
- **GET** - 获取资源
- **POST** - 提交数据
- **PUT** - 更新资源
- **DELETE** - 删除资源
- **其他** - 支持自定义 HTTP 方法

### 2. 请求体支持
- **字符串** - 文本内容
- **字节数组** - 二进制数据
- **对象** - 自动 JSON 序列化

### 3. 文件操作
- **上传单个文件** - UploadFileAsync
- **上传流** - 支持 Stream 对象
- **下载到路径** - DownloadFileAsync
- **下载到流** - DownloadToStreamAsync
- **哈希校验** - 文件完整性验证
- **元数据支持** - 上传/下载时附加元数据

### 4. 会话管理
- **Cookie 自动管理** - AutoManageCookies 属性
- **会话 ID 管理** - SetSessionId、GetSessionId
- **Cookie 导入/导出** - 便于持久化
- **会话 Header 支持** - 可配置会话头名称

### 5. 进度跟踪
- **上传进度** - IProgress<long> 回调
- **下载进度** - 实时字节数报告
- **取消支持** - CancellationToken 支持

### 6. 高级功能
- **并发控制** - 内部信号量管理并发数
- **超时配置** - SetTimeout 自定义超时
- **默认请求头** - SetDefaultHeader 全局设置
- **请求队列** - 异步后台处理

### 7. OpenAuth 快速封装
- **CreateOpenAuthState()** - 生成防 CSRF 的 state
- **BuildOpenAuthAuthorizeUrl(...)** - 快速拼装授权跳转地址
- **ExchangeOpenAuthCodeAsync(...)** - 使用授权码交换 access token
- **AuthLogin(serverBaseUrl, appId, scope)** - 一行获取 AuthApp 授权 URL（同步封装，内部调用 `AuthLoginAsync`）
- **AuthLoginAsync(serverBaseUrl, appId, scope)** - 异步获取授权 URL，请求服务端 `/api/oauth/apps/quick-login-url` 并返回完整授权跳转地址

```csharp
// 方式一：完整流程（手动管理 state）
var state = DrxHttpClient.CreateOpenAuthState();
var authUrl = DrxHttpClient.BuildOpenAuthAuthorizeUrl(
	serverBaseUrl: "https://auth.example.com",
	clientId: "demo_client",
	redirectUri: "https://client.example.com/callback",
	state: state,
	scope: "profile"
);

// 浏览器跳转 authUrl，回调拿到 code 后：
var token = await client.ExchangeOpenAuthCodeAsync(
	serverBaseUrl: "https://auth.example.com",
	code: code,
	clientId: "demo_client",
	redirectUri: "https://client.example.com/callback"
);

// 方式二：AuthLogin 快捷方式（服务端自动查找 AppId 对应的配置）
var quickAuthUrl = await client.AuthLogin(
    serverBaseUrl: "https://auth.example.com",
    appId: "demo_client",
    scope: "profile"          // 可选，默认 "profile"
);
// 将 quickAuthUrl 直接返回或重定向给浏览器即可完成登录引导
```

## 并发模型

- **最大并发数** - 10 个请求
- **队列处理** - 使用 Channel 实现请求队列
- **信号量控制** - SemaphoreSlim 管理并发

## 使用场景

1. **REST API 调用** - 与远程 API 通信
2. **文件传输** - 上传/下载大文件
3. **会话维护** - 保持登录状态
4. **进度监控** - 长时间操作的进度显示
5. **LLM 集成** - 与大语言模型 API 交互
6. **ASP.NET Core 集成** - 与 Asp 目录的服务器通信

## 异常处理

支持以下异常类型：
- `HttpRequestException` - 网络错误
- `TaskCanceledException` - 请求超时或被取消
- `FileNotFoundException` - 上传文件不存在
- `ArgumentException` - 无效参数
- `ArgumentNullException` - 必需参数为 null

## 与其他模块的关系

- **与 Protocol 的关系** - 使用 HttpRequest、HttpResponse
- **与 Guides 的关系** - 参见 DrxHttpClient.DEVGUIDE.md
- **与 Asp 的关系** - DrxHttpAspClient 是简化版
- **与 Session 的关系** - Cookie 和会话管理

## 相关文档
- 参见 [../Guides/DrxHttpClient.DEVGUIDE.md](../Guides/DrxHttpClient.DEVGUIDE.md) 了解详细用法
- OpenAPI 文档中有完整的方法签名和示例
