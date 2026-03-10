# ScriptHttpResponse
> 脚本友好型 HTTP 响应工厂，为 JS/TS 提供静态方法快速构建各类 HttpResponse。

## Classes
| 类名 | 简介 |
|------|------|
| `ScriptHttpResponse` | 静态工厂类，注册为 `HttpResponse` 宿主类型，供脚本通过 `HttpResponse.file()` 等调用。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `file(path)` | `string` | `HttpResponse` | 返回文件内容响应，自动推断 MIME；路径按 ViewRoot→FileRoot→CWD 解析。 |
| `download(path, fileName?)` | `string`, `string?` | `HttpResponse` | 返回文件下载响应（Content-Disposition: attachment）。 |
| `json(data, statusCode?)` | `object?`, `int` | `HttpResponse` | 返回 JSON 序列化响应。 |
| `text(text, statusCode?)` | `string`, `int` | `HttpResponse` | 返回纯文本响应。 |
| `html(html, statusCode?)` | `string`, `int` | `HttpResponse` | 返回 HTML 响应。 |
| `redirect(url, permanent?)` | `string`, `bool` | `HttpResponse` | 返回重定向响应（302/301）。 |
| `status(statusCode, body?)` | `int`, `string?` | `HttpResponse` | 返回指定状态码的响应。 |
| `ok(body?)` | `string?` | `HttpResponse` | 200 OK 快捷方法。 |
| `noContent()` | — | `HttpResponse` | 204 No Content 快捷方法。 |
| `badRequest(message?)` | `string?` | `HttpResponse` | 400 Bad Request 快捷方法。 |
| `unauthorized(message?)` | `string?` | `HttpResponse` | 401 Unauthorized 快捷方法。 |
| `forbidden(message?)` | `string?` | `HttpResponse` | 403 Forbidden 快捷方法。 |
| `notFound(message?)` | `string?` | `HttpResponse` | 404 Not Found 快捷方法。 |
| `serverError(message?)` | `string?` | `HttpResponse` | 500 Internal Server Error 快捷方法。 |

## Usage
```typescript
// 返回文件内容
server.get("/index", (req) => HttpResponse.file("html/index.html"));

// 返回 JSON
server.get("/api/data", (req) => HttpResponse.json({ ok: true }));

// 重定向
server.get("/old", (req) => HttpResponse.redirect("/new"));

// 文件下载
server.get("/download", (req) => HttpResponse.download("reports/annual.pdf"));
```
