# DrxHttpClient v2.0 — C++ 开发者指南

> 适用版本: v2.0 | 平台: Windows (x86/x64) | 标准: C++17  
> 依赖: `winhttp.lib`, `bcrypt.lib` (MSVC 已内置 `#pragma comment`)

---

## 目录

1. [快速上手](#1-快速上手)
2. [核心类型](#2-核心类型)
3. [基本请求](#3-基本请求)
4. [请求配置](#4-请求配置)
5. [文件上传与下载](#5-文件上传与下载)
6. [Cookie 管理](#6-cookie-管理)
7. [重试策略](#7-重试策略)
8. [SSL / TLS](#8-ssl--tls)
9. [代理设置](#9-代理设置)
10. [取消令牌 (CancelToken)](#10-取消令牌-canceltoken)
11. [Server-Sent Events (SSE)](#11-server-sent-events-sse)
12. [请求队列](#12-请求队列)
13. [日志系统](#13-日志系统)
14. [错误处理](#14-错误处理)
15. [线程安全说明](#15-线程安全说明)
16. [v2.0 常见迁移问题](#16-v20-常见迁移问题)

---

## 1. 快速上手

### 1.1 包含头文件

```cpp
#include "DrxHttpClient.hpp"
using namespace drx::sdk::network::http;
```

### 1.2 最简 GET 请求

```cpp
DrxHttpClient client;
auto resp = client.get("https://api.example.com/ping");

if (resp.ok()) {
    std::cout << resp.bodyAsString() << "\n";
} else {
    std::cerr << "HTTP " << resp.statusCode << "\n";
}
```

### 1.3 带 BaseAddress 的客户端

```cpp
DrxHttpClient client("https://api.example.com");

// 以下两种写法等价:
client.get("/users");
client.get("https://api.example.com/users");
```

> **注意:** `DrxHttpClient` 不可复制也不可移动，通常以局部变量或 `unique_ptr` 持有。

### 1.4 配置控制台输出为 UTF-8（支持中文显示）

在 Windows 上，VS 调试控制台默认使用系统 ANSI 编码（中文系统为 GBK），导致中文响应显示乱码。

**解决方案：** 在 `main()` 最开始调用 `setupConsoleUtf8()`（v2.0 新增）：

```cpp
#include "DrxHttpClient.hpp"
using namespace drx::sdk::network::http;

int main()
{
    setupConsoleUtf8();  // 一行代码配置控制台编码
    
    DrxHttpClient client("https://api.example.com");
    auto resp = client.get("/get-chinese-text");
    
    std::cout << resp.bodyAsString() << std::endl;  // 中文正常显示 ✅
    return 0;
}
```

`setupConsoleUtf8()` 会自动执行以下操作：
- 设置控制台输入 codepage: `SetConsoleCP(CP_UTF8)`
- 设置控制台输出 codepage: `SetConsoleOutputCP(CP_UTF8)`
- 配置 `stdout`/`stderr` 为 UTF-8 文本模式（无 BOM）

> **原理:** `bodyAsString()` 返回的是 UTF-8 编码的字符串；配置控制台编码后，输出才能正确解释为 UTF-8，从而显示中文。若未配置，控制台会按系统默认 ANSI 编码解读 UTF-8 字节，导致乱码。

---

## 2. 核心类型

### `HttpResponse`

| 成员            | 类型                     | 说明                                   |
|----------------|--------------------------|----------------------------------------|
| `statusCode`   | `int`                    | HTTP 状态码（如 200、404）              |
| `bodyBytes`    | `std::vector<uint8_t>`   | 原始响应体，适合二进制                  |
| `headers`      | `Headers`                | 响应头 map（`std::map<string, string>`） |
| `reasonPhrase` | `std::string`            | 状态文本（如 "OK"）                     |
| `ok()`         | `bool`                   | 状态码 200–299 返回 true               |
| `bodyAsString()` | `std::string`          | 将 `bodyBytes` 按需转为 UTF-8 字符串   |

```cpp
auto resp = client.get("https://api.example.com/data");
std::string json = resp.bodyAsString();    // 文本
std::vector<uint8_t>& raw = resp.bodyBytes; // 二进制
```

### `HttpRequest`（高级组装）

```cpp
HttpRequest req;
req.method  = "POST";
req.url     = "/submit";
req.body    = R"({"key":"value"})";
req.headers = {{"X-Custom", "value"}};
req.query   = {{"page", "1"}};

auto resp = client.send(req);
```

---

## 3. 基本请求

### GET

```cpp
// 无参数
client.get("/api/resource");

// 带 Query 参数
client.get("/api/search", {}, {{"q", "hello"}, {"page", "2"}});

// 带自定义头
client.get("/api/resource", {{"Authorization", "Bearer token123"}});
```

### POST

```cpp
// JSON body (自动添加 Content-Type: application/json)
client.post("/api/create", R"({"name":"Alice"})");

// 二进制 body
std::vector<uint8_t> data = { 0x01, 0x02, 0x03 };
client.post("/api/upload", data);
```

### PUT / PATCH / DELETE / HEAD

```cpp
client.put("/api/resource/1",  R"({"name":"Bob"})");
client.patch("/api/resource/1", R"({"name":"Carol"})");
client.del("/api/resource/1");
client.head("/api/resource");  // 只获取头，无 body
```

### 完整参数签名

所有便捷方法均支持以下可选参数（按顺序）：

```
(url, [body/bodyBytes], [headers], [queryParams], [cancelToken*])
```

---

## 4. 请求配置

### 默认请求头

每次请求都会自动附加，适合设置鉴权头、User-Agent 等：

```cpp
client.setDefaultHeader("Authorization", "Bearer my_token");
client.setDefaultHeader("Accept", "application/json");

// 移除
client.removeDefaultHeader("Authorization");
```

### 超时设置

```cpp
client.setTimeout(5000);          // 5 秒 (毫秒)
client.setTimeoutSeconds(3.5);    // 3.5 秒
```

> 超时应用于 WinHTTP 的连接、发送、接收四个阶段，设为 0 表示无超时。

---

## 5. 文件上传与下载

### 5.1 单文件上传（multipart/form-data）

```cpp
// 从文件路径上传
client.uploadFile("https://api.example.com/upload",
                  "C:/data/photo.jpg",
                  "file" /* form field name */);

// 从内存上传
std::vector<uint8_t> buf = /* ... */;
client.uploadFile("https://api.example.com/upload",
                  buf.data(), buf.size(),
                  "photo.jpg", "file");
```

### 5.2 上传文件 + JSON 元数据

```cpp
client.uploadFileWithMetadata(
    "/api/upload",
    "C:/data/report.pdf",
    R"({"category":"finance","year":2025})"
);
```

### 5.3 下载文件

```cpp
// 简单下载
client.downloadFile("https://cdn.example.com/file.zip",
                    "C:/downloads/file.zip");

// 带进度回调
client.downloadFile(url, destPath, {}, {},
    [](int64_t current, int64_t total) {
        if (total > 0)
            printf("%.1f%%\n", 100.0 * current / total);
    });
```

### 5.4 下载并校验 SHA256

```cpp
std::string hash = client.downloadFileWithHash(
    "https://cdn.example.com/setup.exe",
    "C:/temp/setup.exe",
    "a3f5c8d1..." /* 预期 SHA256 hex，可留空跳过校验 */
);
std::cout << "文件哈希: " << hash << "\n";
```

> 哈希不匹配时自动删除文件并抛出 `std::runtime_error`。

### 5.5 下载并获取元数据

```cpp
DownloadResult result = client.downloadFileWithMetadata(url, destPath);

std::cout << "Content-Type: " << result.contentType  << "\n"
          << "ETag:         " << result.etag          << "\n"
          << "SHA256:       " << result.fileHash       << "\n"
          << "字节数:        " << result.downloadedBytes << "\n";
```

### 5.6 下载到流

```cpp
std::ostringstream oss;
client.downloadToStream("https://api.example.com/report.csv", oss);
std::string csv = oss.str();
```

### 5.7 进度回调签名

```cpp
using ProgressCallback = std::function<void(int64_t current, int64_t total)>;
// current: 已传输字节
// total:   总字节，未知时为 -1
```

---

## 6. Cookie 管理

### 自动管理（默认开启）

客户端自动从响应解析 `Set-Cookie` 并在后续请求中携带：

```cpp
client.setAutoManageCookies(true);  // 默认 true
```

### Session Cookie

```cpp
// 设置 session cookie 名称（默认 "session_id"）
client.setSessionCookieName("PHPSESSID");

// 手动写入
client.setSessionId("abc123xyz");

// 读取
std::string sid = client.getSessionId();
```

### Session Header

如果服务端使用 Header 而非 Cookie 传递 Token：

```cpp
client.setSessionHeaderName("X-Auth-Token");
client.setSessionId("Bearer abc123");
// 之后每个请求自动附加: X-Auth-Token: Bearer abc123
```

### 导入 / 导出 Cookie（持久化）

```cpp
// 导出为 JSON 字符串
std::string json = client.exportCookies();
save_to_file("cookies.json", json);

// 下次启动时导入
std::string json = load_from_file("cookies.json");
client.importCookies(json);
```

导出格式示例：
```json
[{"Name":"session_id","Value":"abc","Domain":"api.example.com",
  "Path":"/","Secure":false,"HttpOnly":true}]
```

### 清空 Cookie

```cpp
client.clearCookies();
```

---

## 7. 重试策略

### 默认策略

默认不重试（`maxRetries = 0`）。可重试状态码：5xx、408、429。

### 快速配置

```cpp
// 最多重试 3 次，首次延迟 500ms，指数退避
client.setRetryPolicy(3, 500, true);
```

### 自定义策略

```cpp
RetryPolicy policy;
policy.maxRetries       = 5;
policy.baseDelayMs      = 200;
policy.exponentialBackoff = true;
policy.shouldRetry = [](int code) {
    return code == 503 || code == 429;
};
client.setRetryPolicy(policy);
```

> 指数退避延迟 = `baseDelayMs * 2^attempt`，例如 500ms → 1s → 2s → 4s。

---

## 8. SSL / TLS

### 忽略证书错误（仅用于开发/调试）

```cpp
client.setIgnoreSslErrors(true);
```

> **警告:** 生产环境请勿启用，会忽略过期证书、CN 不匹配等所有 SSL 错误。

常见 SSL 相关错误码：

| 错误码  | 含义                              |
|--------|-----------------------------------|
| 12175  | SSL 握手失败（通用），可尝试 `setIgnoreSslErrors(true)` 调试 |
| 12038  | 证书 CN 不匹配                    |
| 12044  | 证书已过期                        |
| 12057  | 证书已吊销                        |
| 12157  | TLS 通道错误                      |

---

## 9. 代理设置

```cpp
client.setProxy("http://proxy.corp.com:8080");
client.setProxy("http://user:pass@proxy.corp.com:8080");
```

代理信息通过 `WinHTTP` 的 `WINHTTP_OPTION_PROXY` 应用于整个会话。

---

## 10. 取消令牌 (CancelToken)

`CancelToken` 是线程安全的，可在多个请求间共享。

```cpp
CancelToken token;

// 在另一线程中发起请求
std::thread t([&]() {
    try {
        auto resp = client.get("/api/slow-endpoint", {}, {}, &token);
    } catch (const std::runtime_error& e) {
        std::cout << "请求被取消: " << e.what() << "\n";
    }
});

// 主线程取消
std::this_thread::sleep_for(std::chrono::seconds(1));
token.cancel();
t.join();

// 重置后可复用
token.reset();
```

> 取消只是设置标志位，WinHTTP 底层 I/O 不会被强制中断，实际取消发生在下一次检查点（读取循环中）。

---

## 11. Server-Sent Events (SSE)

```cpp
CancelToken cancel;

std::thread sseThread([&]() {
    client.connectSse(
        "/api/events",
        // onEvent 回调
        [](const SseEvent& e) {
            std::cout << "[" << e.event << "] " << e.data << "\n";
        },
        // shouldStop 回调 (返回 true 则停止)
        [&]() { return cancel.isCancelled(); },
        {{"Authorization", "Bearer token"}}
    );
});

// 停止 SSE
cancel.cancel();
sseThread.join();
```

`SseEvent` 字段：

| 字段    | 类型          | 说明                        |
|--------|---------------|-----------------------------|
| `event` | `std::string` | 事件类型，默认为 `"message"` |
| `data`  | `std::string` | 数据载荷，多行用 `\n` 连接   |
| `id`    | `std::string` | 事件 ID                     |
| `retry` | `int`         | 重连建议间隔（ms），-1 表示未设置 |

---

## 12. 请求队列

适合需要批量发送请求、控制并发的场景。

```cpp
// 启动队列，最大并发 5
client.startQueue(5);

// 入队
for (int i = 0; i < 100; ++i) {
    HttpRequest req;
    req.method = "GET";
    req.url = "/api/item/" + std::to_string(i);

    client.enqueue(req, [i](HttpResponse resp) {
        printf("item %d -> %d\n", i, resp.statusCode);
    });
}

// 停止队列（等待所有请求完成）
client.stopQueue();
```

> `stopQueue()` 会阻塞直到所有已入队请求处理完毕，析构时也会自动调用。

---

## 13. 日志系统

```cpp
client.setLogCallback([](LogLevel level, const std::string& msg) {
    const char* prefix = "?";
    switch (level) {
        case LogLevel::Debug: prefix = "DBG"; break;
        case LogLevel::Info:  prefix = "INF"; break;
        case LogLevel::Warn:  prefix = "WRN"; break;
        case LogLevel::Error: prefix = "ERR"; break;
    }
    printf("[%s] %s\n", prefix, msg.c_str());
});
```

日志级别覆盖：

| 级别    | 示例内容                                         |
|--------|--------------------------------------------------|
| Debug  | `GET https://...`、`Response: 200 OK`、header 设置 |
| Info   | 上传/下载完成、SSE 连接/断开、代理设置             |
| Warn   | 重试信息（含延迟和状态码）                         |
| Error  | 目前通过异常抛出，不经日志回调                     |

---

## 14. 错误处理

所有错误均以 `std::runtime_error` 抛出，不使用错误码返回。

```cpp
try {
    auto resp = client.get("https://api.example.com/data");
    // 注意: 4xx/5xx 不抛异常，需检查 resp.ok() 或 resp.statusCode
} catch (const std::runtime_error& e) {
    // 网络错误、DNS 失败、SSL 错误、文件 I/O 错误等
    std::cerr << "请求失败: " << e.what() << "\n";
}
```

**常见错误信息对照：**

| 错误消息片段                      | 原因                                   | 解决方案 |
|----------------------------------|----------------------------------------|---------|
| `NAME_NOT_RESOLVED`              | DNS 解析失败，检查 host 地址            | 验证 URL 是否正确，检查网络连接 |
| `CANNOT_CONNECT`                 | 连接被拒绝或无法到达目标               | 检查目标服务是否运行、防火墙设置 |
| `SECURE_FAILURE` / `error=12175` | SSL 证书问题，可设置 `setIgnoreSslErrors` 调试 | 检查证书有效性或开发时临时忽略 |
| `Request cancelled`              | `CancelToken::cancel()` 被调用         | 检查取消逻辑 |
| `Hash mismatch`                  | `downloadFileWithHash` 校验失败        | 确认源文件未损坏 |
| `Upload file not found`          | 上传源文件不存在                        | 检查文件路径 |
| **响应中文显示乱码**              | 控制台编码未配置为 UTF-8               | 在 `main()` 最开始调用 `setupConsoleUtf8()` |

---

## 15. 线程安全说明

| 操作                              | 线程安全 |
|----------------------------------|----------|
| 并发调用 `get`/`post` 等请求方法  | ✅ 安全  |
| 设置/读取默认 Header             | ✅ 安全（内部加锁） |
| Cookie 读写（`setSessionId` 等）  | ✅ 安全  |
| `setLogCallback`                 | ✅ 安全  |
| 复制/移动 `DrxHttpClient` 对象   | ❌ 不支持 |
| 销毁正在进行 SSE 的对象           | ⚠️ 需先通过 `CancelToken` 停止 SSE |

> 请求队列的工作线程通过 `join` 而非 `detach` 管理，析构时会等待所有任务完成。

---

## 16. v2.0 常见迁移问题

### `body` 字段已变更

v1.x 的 `resp.body` (string) 现在为延迟方法，原始 bytes 在 `resp.bodyBytes`：

```cpp
// v1.x (已废弃但保留兼容)
std::string s = resp.body();

// v2.0 推荐
std::string s = resp.bodyAsString();
std::vector<uint8_t>& b = resp.bodyBytes;
```

### public 字段移除

v1.x 直接访问 `client.ignoreSslErrors = true` 等 public 字段已移除，改用 get/set：

```cpp
// v1.x (已不支持)
client.ignoreSslErrors = true;

// v2.0
client.setIgnoreSslErrors(true);
```

### 请求队列不再 detach

v1.x 的队列工作线程使用 `detach`，可能导致野指针。v2.0 改为 `join` 追踪，析构安全。

---

## 附录：完整示例

```cpp
#include "DrxHttpClient.hpp"
#include <iostream>

using namespace drx::sdk::network::http;

int main() {
    // 配置控制台编码为 UTF-8，支持中文显示
    setupConsoleUtf8();
    
    DrxHttpClient client("https://jsonplaceholder.typicode.com");

    // 日志
    client.setLogCallback([](LogLevel lv, const std::string& msg) {
        if (lv >= LogLevel::Info)
            std::cout << "[HTTP] " << msg << "\n";
    });

    // 超时 + 重试
    client.setTimeout(10000);
    client.setRetryPolicy(3, 500, true);

    // 默认头
    client.setDefaultHeader("Accept", "application/json");

    // GET
    auto resp = client.get("/todos/1");
    if (resp.ok()) {
        std::cout << "Body: " << resp.bodyAsString() << "\n";
    }

    // POST
    auto post = client.post("/posts",
        R"({"title":"foo","body":"bar","userId":1})");
    std::cout << "Created: " << post.statusCode << "\n";

    // 下载文件（带进度）
    CancelToken cancel;
    client.downloadFile(
        "https://speed.cloudflare.com/__down?bytes=1048576",
        "C:/temp/test.bin",
        {}, {},
        [](int64_t cur, int64_t tot) {
            if (tot > 0)
                printf("\r下载: %.1f%%", 100.0 * cur / tot);
        },
        &cancel
    );
    std::cout << "\n完成\n";

    return 0;
}
```

---

*DrxHttpClient v2.0 — Windows 平台 C++ HTTP 客户端*
