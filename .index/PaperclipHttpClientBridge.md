# HttpClientBridge
> HTTP 客户端脚本桥接层，提供 fetch 风格静态 API 供 JS/TS 脚本发送 HTTP 请求

## Classes
| 类名 | 简介 |
|------|------|
| `HttpClientBridge` | 静态类，包装 DrxHttpClient 提供 GET/POST/PUT/DELETE/上传/下载 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `create(baseAddress?)` | `baseAddress:string?` | `DrxHttpClient` | 创建独立 HTTP 客户端实例 |
| `get(url)` | `url:string` | `Task<HttpResponse>` | 共享实例 GET 请求 |
| `getWithHeaders(url, headers)` | `url:string, headers:object?` | `Task<HttpResponse>` | GET + 自定义头 |
| `post(url, body?)` | `url:string, body:object?` | `Task<HttpResponse>` | POST 请求 |
| `postWithHeaders(url, body, headers)` | `url:string, body:object?, headers:object?` | `Task<HttpResponse>` | POST + 自定义头 |
| `put(url, body?)` | `url:string, body:object?` | `Task<HttpResponse>` | PUT 请求 |
| `putWithHeaders(url, body, headers)` | `url:string, body:object?, headers:object?` | `Task<HttpResponse>` | PUT + 自定义头 |
| `del(url)` | `url:string` | `Task<HttpResponse>` | DELETE 请求 |
| `delWithHeaders(url, headers)` | `url:string, headers:object?` | `Task<HttpResponse>` | DELETE + 自定义头 |
| `downloadFile(url, destPath)` | `url:string, destPath:string` | `Task` | 下载文件到本地 |
| `uploadFile(url, filePath, fieldName?)` | `url:string, filePath:string, fieldName:string` | `Task<HttpResponse>` | 上传文件 |
| `instanceGet(client, url)` | `client:DrxHttpClient, url:string` | `Task<HttpResponse>` | 实例 GET |
| `instancePost(client, url, body?)` | `client:DrxHttpClient, url:string, body:object?` | `Task<HttpResponse>` | 实例 POST |
| `instancePut(client, url, body?)` | `client:DrxHttpClient, url:string, body:object?` | `Task<HttpResponse>` | 实例 PUT |
| `instanceDelete(client, url)` | `client:DrxHttpClient, url:string` | `Task<HttpResponse>` | 实例 DELETE |
| `setDefaultHeader(client, name, value)` | `client:DrxHttpClient, name:string, value:string` | `void` | 设置默认头 |
| `setTimeout(client, seconds)` | `client:DrxHttpClient, seconds:double` | `void` | 设置超时 |
| `instanceDownloadFile(client, url, dest)` | `client:DrxHttpClient, url:string, dest:string` | `Task` | 实例下载 |
| `instanceUploadFile(client, url, path, field?)` | `client:DrxHttpClient, url:string, path:string, field:string` | `Task<HttpResponse>` | 实例上传 |

## Usage
```typescript
// 快捷用法
const res = await HttpClient.get("https://api.example.com/data");
console.log(res.Body);

// 独立实例
const client = HttpClient.create("https://api.example.com");
HttpClient.setTimeout(client, 30);
const res2 = await HttpClient.instancePost(client, "/users", { name: "Alice" });
```
