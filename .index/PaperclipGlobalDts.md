# Paperclip Global Types
> Paperclip 运行时全局类型声明，覆盖内置函数与宿主桥接 API。 

## Classes
| 类名 | 简介 |
|------|------|
| `HttpServer` | 脚本友好 HTTP 服务实例包装（链式 API）。 |
| `DrxHttpServer` | 底层 HTTP 服务类型。 |
| `HttpServerFactory` | HTTP 服务静态工厂与路由/中间件注册入口。 |
| `HttpResponse` | HTTP 响应构造工厂。 |
| `HttpClient` | HTTP 客户端桥接（共享实例 + 独立实例）。 |
| `TcpClient` | TCP/UDP 客户端桥接。 |
| `Email` | SMTP 邮件发送桥接。 |
| `Database` | SQLite 原始 SQL 桥接。 |
| `Json` | .NET JSON 序列化/反序列化桥接。 |
| `Crypto` | 加密与哈希工具桥接。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `print(value?)` | `any` | `void` | 输出文本到控制台。 |
| `pause(prompt?)` | `string?` | `void` | 暂停脚本并等待按键。 |
| `HttpClient.get/post/put/del` | `url, body?` | `Promise<HttpResponse>` | 共享实例发起请求。 |
| `TcpClient.createTcp/createUdp` | `host, port` | `NetworkClient` | 创建网络客户端。 |
| `Email.createSender` | SMTP 参数 | `SmtpEmailSender` | 创建邮件发送器。 |
| `Database.query/execute/scalar` | `conn, sql, params?` | `object[]/number/any` | 执行 SQL 操作。 |
| `Json.stringify/parse` | `value/json` | `string/any` | JSON 序列化与解析。 |
| `Crypto.sha256/aesEncrypt` | 输入文本 | `string` | 哈希与加解密。 |

## Usage
```typescript
const server = new HttpServer("http://localhost:8080/");
server.get("/ping", () => HttpResponse.text("pong"));

const res = await HttpClient.get("https://example.com");
const db = Database.open("data/app.db");
```
