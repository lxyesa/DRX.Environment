# Paperclip 脚本运行时 API 参考文档

> **版本**: 基于 `global.d.ts` 类型定义  
> **适用对象**: Paperclip 脚本开发者  
> **最后更新**: 2026-03-10

---

## 目录

1. [全局函数](#全局函数)
2. [HTTP 服务器](#http-服务器)
   - [HttpServer](#httpserver-类)
   - [DrxHttpServer](#drxhttpserver-类)
   - [HttpServerFactory](#httpserverfactory-类)
   - [HttpResponse](#httpresponse-类)
3. [网络客户端](#网络客户端)
   - [HttpClient](#httpclient-类)
   - [TcpClient](#tcpclient-类)
4. [邮件发送](#邮件发送)
5. [数据库操作](#数据库操作)
6. [JSON 处理](#json-处理)
7. [加密与哈希](#加密与哈希)
8. [类型定义](#类型定义)

---

## 全局函数

### `print(value?: any): void`

向控制台输出一个值，等同于 `console.log`。

| 参数 | 类型 | 描述 |
|------|------|------|
| `value` | `any` | 要打印的值，省略时输出空行 |

**示例**:
```typescript
print("Hello, Paperclip!");
print({ name: "test", count: 42 });
print(); // 输出空行
```

---

### `pause(prompt?: string): void`

暂停脚本执行并等待用户按 Enter 键。

| 参数 | 类型 | 描述 |
|------|------|------|
| `prompt` | `string` | 可选提示文本，默认显示 "Press Enter to continue..." |

**示例**:
```typescript
print("处理完成！");
pause("按 Enter 键退出...");
```

---

## HTTP 服务器

Paperclip 提供三种方式创建 HTTP 服务器：
- **HttpServer** — 推荐，链式 API，简洁易用
- **DrxHttpServer** — 底层实例，高级用法
- **HttpServerFactory** — 静态工厂方法

---

### HttpServer 类

脚本友好型 HTTP 服务器，提供链式 API、快捷路由方法、中间件与 SSE 支持。

#### 构造函数

```typescript
// 单前缀
new HttpServer(prefix: string)

// 带配置
new HttpServer(prefix: string, staticFileRoot: string | null, sessionTimeoutMinutes?: number)
```

| 参数 | 类型 | 默认值 | 描述 |
|------|------|--------|------|
| `prefix` | `string` | — | 监听地址，如 `"http://localhost:8080/"` |
| `staticFileRoot` | `string \| null` | `null` | 静态文件根目录路径 |
| `sessionTimeoutMinutes` | `number` | `20` | 会话超时（分钟） |

**完整示例**:
```typescript
const server = new HttpServer("http://localhost:8080/");

server
  .debugMode(true)
  .setFileRoot("./public")
  .setViewRoot("./views")
  .setRateLimit(100, 1, "minutes")
  .get("/ping", (req) => "pong")
  .get("/api/users", (req) => HttpResponse.json([{ id: 1, name: "Alice" }]))
  .post("/api/login", (req) => {
    const body = JSON.parse(req.body);
    req.session.set("user", body.username);
    return HttpResponse.json({ success: true });
  })
  .use((req) => {
    console.log(`${req.method} ${req.url}`);
    return null; // 继续下一个处理
  });

await server.startAsync();
```

#### 属性

| 属性 | 类型 | 描述 |
|------|------|------|
| `Server` | `DrxHttpServer` | 底层服务器实例（只读） |

#### 生命周期方法

| 方法 | 返回值 | 描述 |
|------|--------|------|
| `startAsync()` | `Promise<void>` | 启动服务器并开始监听 |
| `stop()` | `void` | 停止服务器监听 |
| `disposeAsync()` | `Promise<void>` | 释放所有资源 |

#### 配置方法（链式调用）

| 方法 | 参数 | 描述 |
|------|------|------|
| `debugMode(enable)` | `boolean` | 启用/禁用调试模式 |
| `setFileRoot(path)` | `string \| null` | 设置静态文件根目录 |
| `setViewRoot(path)` | `string \| null` | 设置视图模板根目录 |
| `setNotFoundPage(path)` | `string \| null` | 设置自定义 404 页面 |
| `setRateLimit(maxRequests, timeValue, timeUnit)` | `number, number, string` | 配置全局请求限流 |
| `addFileRoute(urlPrefix, rootDirectory)` | `string, string` | 添加额外静态文件路由 |

**限流示例**:
```typescript
// 每分钟最多 100 个请求
server.setRateLimit(100, 1, "minutes");

// 每秒最多 10 个请求
server.setRateLimit(10, 1, "seconds");
```

#### 路由方法（链式调用）

| 方法 | 描述 |
|------|------|
| `get(path, handler)` | 注册 GET 路由 |
| `post(path, handler)` | 注册 POST 路由 |
| `put(path, handler)` | 注册 PUT 路由 |
| `delete(path, handler)` | 注册 DELETE 路由 |
| `patch(path, handler)` | 注册 PATCH 路由 |
| `head(path, handler)` | 注册 HEAD 路由 |
| `options(path, handler)` | 注册 OPTIONS 路由 |
| `map(method, path, handler, ...)` | 注册通用路由 |
| `mapWithRateCallback(...)` | 注册带限流超限回调的路由 |

**路由参数**:
```typescript
// 路径参数使用 :name 语法
server.get("/users/:id", (req) => {
  const userId = req.params.id;
  return HttpResponse.json({ id: userId });
});

// 查询参数
server.get("/search", (req) => {
  const keyword = req.query.q;
  return HttpResponse.json({ keyword });
});
```

**Handler 返回值类型**:
- `HttpResponse` — 完整响应对象
- `string` — 自动转为文本响应
- `object` — 自动转为 JSON 响应
- `null` — 无响应（用于中间件）

#### 中间件方法

```typescript
// 全局中间件
server.use((req) => {
  console.log(`[${new Date().toISOString()}] ${req.method} ${req.url}`);
  return null; // 返回 null 继续下一个处理
});

// 路径范围中间件
server.use((req) => {
  // 仅对 /api/* 路径生效
  return null;
}, "/api");

// 带优先级的中间件（数值越小越先执行）
server.use((req) => {
  // 认证检查
  if (!req.session.get("user")) {
    return HttpResponse.unauthorized("请先登录");
  }
  return null;
}, "/admin", 0, false);
```

#### SSE (Server-Sent Events) 方法

| 方法 | 描述 |
|------|------|
| `getSseClientCount(path?)` | 获取 SSE 连接数 |
| `broadcastSse(path, eventName, data)` | 向指定路径广播 |
| `broadcastSseToAll(eventName, data)` | 向所有客户端广播 |
| `disconnectSseClient(clientId)` | 断开指定客户端 |
| `disconnectAllSseClients(path?)` | 断开所有客户端 |

**SSE 示例**:
```typescript
// 注册 SSE 端点
server.get("/events", (req) => {
  // 返回 SSE 响应（具体实现取决于框架）
});

// 广播消息
await server.broadcastSse("/events", "update", JSON.stringify({ count: 42 }));

// 广播无名事件
await server.broadcastSseToAll(null, "heartbeat");
```

#### 缓存方法

| 方法 | 描述 |
|------|------|
| `clearCache()` | 清空全部静态资源缓存 |
| `invalidateCache(filePath)` | 使指定文件缓存失效 |

---

### DrxHttpServer 类

底层 HTTP 服务器实例。一般通过 `HttpServer` 或 `HttpServerFactory` 使用。

```typescript
const server = new DrxHttpServer(
  ["http://localhost:8080/", "http://localhost:8081/"],
  "./public",
  30
);

await server.StartAsync();
server.DebugMode(true);
server.Stop();
```

| 方法 | 描述 |
|------|------|
| `StartAsync()` | 启动监听 |
| `Stop()` | 停止服务器 |
| `DebugMode(enable)` | 设置调试模式 |
| `ResolveFilePath(pathOrIndicator)` | 解析文件物理路径 |

---

### HttpServerFactory 类

静态工厂类，提供服务器创建、配置、路由注册等全部功能。

#### 创建服务器

```typescript
// 使用前缀数组
const server = HttpServerFactory.Create(
  ["http://localhost:8080/"],
  "./public",
  20
);

// 使用单个前缀
const server = HttpServerFactory.CreateFromPrefix("http://localhost:8080/");
```

#### 生命周期

```typescript
await HttpServerFactory.StartAsync(server);
HttpServerFactory.Stop(server);
await HttpServerFactory.DisposeAsync(server);
```

#### 配置

```typescript
HttpServerFactory.SetDebugMode(server, true);
HttpServerFactory.ConfigurePaths(server, "./public", "./views", "./404.html");
HttpServerFactory.SetRateLimit(server, 100, 1, "minutes");
HttpServerFactory.AddFileRoute(server, "/assets", "./static");
```

#### 路由注册

```typescript
// 同步路由
HttpServerFactory.Map(server, "GET", "/ping", (req) => "pong");

// 异步路由
HttpServerFactory.MapAsync(server, "GET", "/data", async (req) => {
  const data = await fetchData();
  return HttpResponse.json(data);
});

// 带限流的路由
HttpServerFactory.Map(server, "POST", "/api/submit", handler, 10, 60);

// 按函数名注册
function handlePing(req) { return "pong"; }
HttpServerFactory.MapByName(server, "GET", "/ping", "handlePing");
```

#### 中间件注册

```typescript
// 同步中间件
HttpServerFactory.Use(server, (req) => {
  console.log(req.url);
  return null;
});

// 异步中间件
HttpServerFactory.UseAsync(server, async (req) => {
  await logRequest(req);
  return null;
}, "/api", 0);

// 按函数名注册
HttpServerFactory.UseByName(server, "logMiddleware", "/api");
```

#### SSE 管理

```typescript
const count = HttpServerFactory.GetSseClientCount(server, "/events");
await HttpServerFactory.BroadcastSseAsync(server, "/events", "update", data);
await HttpServerFactory.BroadcastSseToAllAsync(server, null, "ping");
HttpServerFactory.DisconnectSseClient(server, clientId);
HttpServerFactory.DisconnectAllSseClients(server, "/events");
```

---

### HttpResponse 类

HTTP 响应工厂类，提供静态方法快速构建各类响应。

#### 文件响应

```typescript
// 返回文件内容（自动推断 Content-Type）
HttpResponse.file("html/index.html")

// 路径解析顺序：ViewRoot → FileRoot → 工作目录 → 绝对路径

// 文件下载
HttpResponse.download("reports/annual.pdf")
HttpResponse.download("reports/annual.pdf", "年度报告.pdf")
```

#### 内容响应

```typescript
// JSON 响应
HttpResponse.json({ users: [], total: 0 })
HttpResponse.json({ error: "not found" }, 404)

// 纯文本响应
HttpResponse.text("Hello, World!")
HttpResponse.text("Error occurred", 500)

// HTML 响应
HttpResponse.html("<h1>Welcome</h1>")
HttpResponse.html("<h1>Error</h1>", 500)
```

#### 重定向

```typescript
// 临时重定向 (302)
HttpResponse.redirect("/new-page")

// 永久重定向 (301)
HttpResponse.redirect("/moved", true)
```

#### 状态码快捷方法

| 方法 | 状态码 | 描述 |
|------|--------|------|
| `ok(body?)` | 200 | 成功响应 |
| `noContent()` | 204 | 无内容响应 |
| `badRequest(message?)` | 400 | 错误请求 |
| `unauthorized(message?)` | 401 | 未授权 |
| `forbidden(message?)` | 403 | 禁止访问 |
| `notFound(message?)` | 404 | 未找到 |
| `serverError(message?)` | 500 | 服务器错误 |
| `status(code, body?)` | 自定义 | 任意状态码 |

**示例**:
```typescript
server.get("/api/protected", (req) => {
  if (!req.session.get("user")) {
    return HttpResponse.unauthorized("请先登录");
  }
  return HttpResponse.json({ data: "secret" });
});

server.delete("/api/resource/:id", (req) => {
  const deleted = deleteResource(req.params.id);
  if (!deleted) {
    return HttpResponse.notFound("资源不存在");
  }
  return HttpResponse.noContent();
});
```

---

## 网络客户端

### HttpClient 类

HTTP 客户端，支持共享实例和独立实例两种模式。

#### 共享实例方法（静态）

```typescript
// GET 请求
const response = await HttpClient.get("https://api.example.com/users");

// 带请求头的 GET
const response = await HttpClient.getWithHeaders(
  "https://api.example.com/users",
  { "Authorization": "Bearer token123" }
);

// POST 请求
const response = await HttpClient.post(
  "https://api.example.com/users",
  { name: "Alice", email: "alice@example.com" }
);

// 带请求头的 POST
const response = await HttpClient.postWithHeaders(
  "https://api.example.com/users",
  { name: "Alice" },
  { "Content-Type": "application/json", "X-API-Key": "key123" }
);

// PUT 请求
await HttpClient.put("https://api.example.com/users/1", { name: "Bob" });

// DELETE 请求
await HttpClient.del("https://api.example.com/users/1");

// 文件操作
await HttpClient.downloadFile("https://example.com/file.zip", "./downloads/file.zip");
await HttpClient.uploadFile("https://api.example.com/upload", "./data.csv", "file");
```

#### 独立实例方法

```typescript
// 创建独立客户端
const client = HttpClient.create("https://api.example.com");

// 配置
HttpClient.setDefaultHeader(client, "Authorization", "Bearer token123");
HttpClient.setTimeout(client, 30); // 30 秒超时

// 发送请求
const response = await HttpClient.instanceGet(client, "/users");
const response = await HttpClient.instancePost(client, "/users", { name: "Alice" });
const response = await HttpClient.instancePut(client, "/users/1", { name: "Bob" });
const response = await HttpClient.instanceDelete(client, "/users/1");

// 文件操作
await HttpClient.instanceDownloadFile(client, "/files/doc.pdf", "./doc.pdf");
await HttpClient.instanceUploadFile(client, "/upload", "./image.png", "image");
```

---

### TcpClient 类

TCP/UDP 网络客户端。

#### 创建客户端

```typescript
// TCP 客户端
const tcpClient = TcpClient.createTcp("127.0.0.1", 9000);

// UDP 客户端
const udpClient = TcpClient.createUdp("127.0.0.1", 9001);
```

#### 连接与断开

```typescript
// 建立连接
const connected = await TcpClient.connect(tcpClient);
if (!connected) {
  console.log("连接失败");
}

// 检查连接状态
if (TcpClient.isConnected(tcpClient)) {
  console.log("已连接");
}

// 断开连接
TcpClient.disconnect(tcpClient);
```

#### 发送数据

```typescript
// 同步发送
TcpClient.sendText(client, "Hello, Server!");
TcpClient.sendBytes(client, [0x48, 0x65, 0x6C, 0x6C, 0x6F]);

// 异步发送
const success = await TcpClient.sendTextAsync(client, "Hello, Server!");
const success = await TcpClient.sendBytesAsync(client, [0x48, 0x65, 0x6C, 0x6C, 0x6F]);
```

#### 配置与释放

```typescript
// 设置超时（秒）
TcpClient.setTimeout(client, 10);

// 释放资源
TcpClient.dispose(client);
```

**完整示例**:
```typescript
const client = TcpClient.createTcp("127.0.0.1", 9000);
TcpClient.setTimeout(client, 5);

try {
  const connected = await TcpClient.connect(client);
  if (connected) {
    await TcpClient.sendTextAsync(client, JSON.stringify({ action: "ping" }));
    console.log("消息已发送");
  }
} finally {
  TcpClient.disconnect(client);
  TcpClient.dispose(client);
}
```

---

## 邮件发送

### Email 类

SMTP 邮件发送器。

#### 创建发送器

```typescript
const sender = Email.createSender(
  "noreply@example.com",    // 发件人地址
  "password123",            // SMTP 密码或授权码
  "smtp.example.com",       // SMTP 服务器（可选）
  587,                      // 端口（可选）
  true,                     // 启用 SSL（可选）
  "系统通知"                 // 显示名称（可选）
);
```

#### 发送邮件

```typescript
// 纯文本邮件
await Email.send(sender, "user@example.com", "测试邮件", "这是邮件正文。");

// HTML 邮件
await Email.sendHtml(
  sender,
  "user@example.com",
  "欢迎注册",
  "<h1>欢迎！</h1><p>感谢您的注册。</p>"
);

// Markdown 邮件（自动转换为 HTML）
await Email.sendMarkdown(
  sender,
  "user@example.com",
  "周报",
  "# 本周总结\n\n- 完成功能 A\n- 修复 Bug B"
);

// 安全发送（失败返回 false，不抛异常）
const success = await Email.trySend(sender, "user@example.com", "通知", "内容");
if (!success) {
  console.log("邮件发送失败");
}
```

---

## 数据库操作

### Database 类

SQLite 原始 SQL 操作接口。

#### 打开数据库

```typescript
// 打开或创建数据库，返回连接字符串
const connStr = Database.open("./data/app.db");
```

#### 执行 SQL

```typescript
// 非查询操作（INSERT/UPDATE/DELETE），返回受影响行数
const affected = Database.execute(
  connStr,
  "INSERT INTO users (name, email) VALUES (@name, @email)",
  { name: "Alice", email: "alice@example.com" }
);

// 查询操作，返回对象数组
const users = Database.query(
  connStr,
  "SELECT * FROM users WHERE age > @age",
  { age: 18 }
);

// 查询单个标量值
const count = Database.scalar(
  connStr,
  "SELECT COUNT(*) FROM users"
);
```

#### 事务

```typescript
// 事务执行多条 SQL（原子操作）
Database.transaction(connStr, [
  "UPDATE accounts SET balance = balance - 100 WHERE id = 1",
  "UPDATE accounts SET balance = balance + 100 WHERE id = 2"
]);
```

#### 其他操作

```typescript
// 获取所有表名
const tables = Database.tables(connStr);
// 返回: ["users", "orders", "products"]

// 关闭连接池
Database.close(connStr);
```

**完整示例**:
```typescript
const db = Database.open("./myapp.db");

// 创建表
Database.execute(db, `
  CREATE TABLE IF NOT EXISTS tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    completed INTEGER DEFAULT 0,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP
  )
`);

// 插入数据
Database.execute(db, 
  "INSERT INTO tasks (title) VALUES (@title)",
  { title: "学习 Paperclip" }
);

// 查询数据
const tasks = Database.query(db, 
  "SELECT * FROM tasks WHERE completed = @completed",
  { completed: 0 }
);

print(tasks);
```

---

## JSON 处理

### Json 类

JSON 序列化与反序列化（.NET 实现）。

```typescript
// 序列化
const jsonStr = Json.stringify({ name: "Alice", age: 30 });
// 返回: '{"name":"Alice","age":30}'

// 美化输出
const prettyJson = Json.stringify({ name: "Alice", age: 30 }, true);
// 返回:
// {
//   "name": "Alice",
//   "age": 30
// }

// 反序列化
const obj = Json.parse('{"name":"Alice","age":30}');

// 从文件读取
const config = Json.readFile("./config.json");

// 写入文件
Json.writeFile("./output.json", { result: "success" }, true);
```

---

## 加密与哈希

### Crypto（全局对象）

运行时注入的加密工具对象，通过 `Crypto` 访问。

#### AES 加密

```typescript
// 使用默认密钥加密
const encrypted = Crypto.aesEncrypt("敏感数据");

// 使用默认密钥解密
const decrypted = Crypto.aesDecrypt(encrypted);

// 生成自定义密钥
const keyPair = Crypto.generateAesKey();
// 返回: { key: "Base64Key...", iv: "Base64IV..." }

// 使用自定义密钥加密
const encrypted = Crypto.aesEncryptWithKey("数据", keyPair.key, keyPair.iv);

// 使用自定义密钥解密
const decrypted = Crypto.aesDecryptWithKey(encrypted, keyPair.key, keyPair.iv);
```

#### 哈希算法

```typescript
// SHA-256（返回 hex 小写）
const hash = Crypto.sha256("hello");
// 返回: "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824"

// MD5（返回 hex 小写）
const hash = Crypto.md5("hello");
// 返回: "5d41402abc4b2a76b9719d911017c592"

// HMAC-SHA256 签名（返回 Base64）
const signature = Crypto.hmacSha256("message", keyBase64);
```

#### Base64 编码

```typescript
// 编码
const encoded = Crypto.base64Encode("Hello, 世界!");

// 解码
const decoded = Crypto.base64Decode(encoded);
```

#### 随机数生成

```typescript
// 生成随机字节（默认 32 字节，返回 Base64）
const randomBytes = Crypto.randomBytes();
const randomBytes16 = Crypto.randomBytes(16);

// 生成 UUID v4
const uuid = Crypto.uuid();
// 返回: "550e8400-e29b-41d4-a716-446655440000"
```

---

## 类型定义

### HttpRequest

路由处理函数的请求参数类型。

```typescript
interface HttpRequest {
  readonly method: string;        // HTTP 方法
  readonly url: string;           // 请求路径
  readonly headers: Record<string, string>;  // 请求头
  readonly query: Record<string, string>;    // 查询参数
  readonly params: Record<string, string>;   // 路由参数
  readonly body: string;          // 请求体原始字符串
  readonly remoteAddress: string; // 客户端 IP
  readonly session: HttpSession;  // 会话对象
}
```

**使用示例**:
```typescript
server.post("/api/users", (req: HttpRequest) => {
  console.log(`来自 ${req.remoteAddress} 的请求`);
  console.log(`Method: ${req.method}`);
  console.log(`Content-Type: ${req.headers["content-type"]}`);
  
  const body = JSON.parse(req.body);
  return HttpResponse.json({ received: body });
});
```

### HttpSession

会话管理接口。

```typescript
interface HttpSession {
  get(key: string): any;      // 获取会话值
  set(key: string, value: any): void;  // 设置会话值
  remove(key: string): void;  // 删除会话值
  clear(): void;              // 清空会话
  readonly id: string;        // 会话 ID
}
```

**使用示例**:
```typescript
server.post("/api/login", (req) => {
  const { username, password } = JSON.parse(req.body);
  
  if (authenticate(username, password)) {
    req.session.set("user", username);
    req.session.set("loginTime", Date.now());
    return HttpResponse.json({ success: true });
  }
  
  return HttpResponse.unauthorized("用户名或密码错误");
});

server.get("/api/profile", (req) => {
  const user = req.session.get("user");
  if (!user) {
    return HttpResponse.unauthorized("请先登录");
  }
  return HttpResponse.json({ user });
});

server.post("/api/logout", (req) => {
  req.session.clear();
  return HttpResponse.ok("已退出登录");
});
```

### SseClient

SSE 客户端信息。

```typescript
interface SseClient {
  readonly clientId: string;  // 客户端唯一标识
  readonly path: string;      // 连接的 SSE 端点路径
}
```

### AesKeyPair

AES 密钥对（Base64 编码）。

```typescript
interface AesKeyPair {
  key: string;  // AES 密钥（Base64）
  iv: string;   // 初始化向量（Base64）
}
```

---

## 快速入门示例

### 创建简单 Web 服务

```typescript
const server = new HttpServer("http://localhost:3000/");

// 配置
server
  .debugMode(true)
  .setFileRoot("./public")
  .setViewRoot("./views");

// 静态页面
server.get("/", (req) => HttpResponse.file("index.html"));

// API 路由
server.get("/api/time", (req) => {
  return HttpResponse.json({ time: new Date().toISOString() });
});

// 带参数的路由
server.get("/api/users/:id", (req) => {
  const userId = req.params.id;
  // 从数据库获取用户...
  return HttpResponse.json({ id: userId, name: "User " + userId });
});

// POST 路由
server.post("/api/users", (req) => {
  const user = JSON.parse(req.body);
  // 保存用户...
  return HttpResponse.json({ id: 1, ...user }, 201);
});

// 错误处理中间件
server.use((req) => {
  try {
    return null; // 继续处理
  } catch (e) {
    return HttpResponse.serverError(e.message);
  }
});

// 启动
await server.startAsync();
print("服务器运行在 http://localhost:3000/");
```

### 数据库 CRUD 示例

```typescript
const db = Database.open("./app.db");

// 初始化表
Database.execute(db, `
  CREATE TABLE IF NOT EXISTS notes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    content TEXT,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP
  )
`);

// 创建
function createNote(title: string, content: string) {
  return Database.execute(db,
    "INSERT INTO notes (title, content) VALUES (@title, @content)",
    { title, content }
  );
}

// 读取全部
function getAllNotes() {
  return Database.query(db, "SELECT * FROM notes ORDER BY created_at DESC");
}

// 读取单个
function getNoteById(id: number) {
  const notes = Database.query(db,
    "SELECT * FROM notes WHERE id = @id",
    { id }
  );
  return notes[0] || null;
}

// 更新
function updateNote(id: number, title: string, content: string) {
  return Database.execute(db,
    "UPDATE notes SET title = @title, content = @content WHERE id = @id",
    { id, title, content }
  );
}

// 删除
function deleteNote(id: number) {
  return Database.execute(db,
    "DELETE FROM notes WHERE id = @id",
    { id }
  );
}
```

### 发送系统通知邮件

```typescript
const sender = Email.createSender(
  "system@example.com",
  process.env.SMTP_PASSWORD,
  "smtp.example.com",
  587,
  true,
  "系统通知"
);

async function notifyAdmin(subject: string, message: string) {
  const success = await Email.trySend(
    sender,
    "admin@example.com",
    `[系统] ${subject}`,
    message
  );
  
  if (!success) {
    console.error("通知发送失败");
  }
}

// 使用
await notifyAdmin("服务器启动", "Web 服务已于 " + new Date().toLocaleString() + " 启动");
```

---

## 附录：方法速查表

### HttpServer 方法

| 方法 | 描述 |
|------|------|
| `startAsync()` | 启动服务器 |
| `stop()` | 停止服务器 |
| `disposeAsync()` | 释放资源 |
| `debugMode(enable)` | 调试模式 |
| `setFileRoot(path)` | 静态文件目录 |
| `setViewRoot(path)` | 视图目录 |
| `setNotFoundPage(path)` | 404 页面 |
| `setRateLimit(...)` | 全局限流 |
| `addFileRoute(...)` | 添加文件路由 |
| `get/post/put/delete/patch(...)` | HTTP 方法路由 |
| `map(...)` | 通用路由 |
| `use(...)` | 中间件 |
| `broadcastSse(...)` | SSE 广播 |
| `clearCache()` | 清除缓存 |

### HttpResponse 方法

| 方法 | 描述 |
|------|------|
| `file(path)` | 文件响应 |
| `download(path, name?)` | 下载响应 |
| `json(data, status?)` | JSON 响应 |
| `text(text, status?)` | 文本响应 |
| `html(html, status?)` | HTML 响应 |
| `redirect(url, permanent?)` | 重定向 |
| `ok/noContent/badRequest/...` | 状态码快捷方法 |

### HttpClient 方法

| 方法 | 描述 |
|------|------|
| `create(baseAddress?)` | 创建实例 |
| `get/post/put/del(url, ...)` | HTTP 请求 |
| `*WithHeaders(...)` | 带请求头 |
| `downloadFile/uploadFile(...)` | 文件操作 |
| `instance*(...)` | 实例方法 |
| `setDefaultHeader/setTimeout(...)` | 配置 |

### Database 方法

| 方法 | 描述 |
|------|------|
| `open(path)` | 打开数据库 |
| `execute(conn, sql, params?)` | 执行非查询 |
| `query(conn, sql, params?)` | 查询 |
| `scalar(conn, sql, params?)` | 标量查询 |
| `transaction(conn, sqls)` | 事务 |
| `tables(conn)` | 获取表名 |
| `close(conn)` | 关闭连接 |

---

*文档由 Paperclip 团队维护*
