# HttpServerFactoryBridge
> DrxHttpServer 的脚本友好工厂桥接，解决 JS 构造参数映射问题。

## Classes
| 类名 | 简介 |
|------|------|
| `HttpServerFactoryBridge` | 为 TS/JS 提供可稳定调用的服务器实例创建入口。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `Create(prefixes, staticFileRoot, sessionTimeoutMinutes)` | `string[]`, `string?`, `int` | `DrxHttpServer` | 使用前缀数组创建服务器实例。 |
| `CreateFromPrefix(prefix, staticFileRoot, sessionTimeoutMinutes)` | `string`, `string?`, `int` | `DrxHttpServer` | 使用单前缀创建服务器实例。 |
| `StartAsync(server)` | `DrxHttpServer` | `Task` | 启动服务器监听。 |
| `Stop(server)` | `DrxHttpServer` | `void` | 停止服务器。 |
| `SetDebugMode(server, enable)` | `DrxHttpServer`, `bool` | `DrxHttpServer` | 设置调试模式并返回实例。 |
| `ConfigurePaths(server, fileRootPath, viewRoot, notFoundPagePath)` | `DrxHttpServer`, `string?`, `string?`, `string?` | `DrxHttpServer` | 批量配置文件根目录/视图根/404页路径。 |
| `SetRateLimit(server, maxRequests, timeValue, timeUnit)` | `DrxHttpServer`, `int`, `int`, `string` | `void` | 配置全局限流。 |
| `AddFileRoute(server, urlPrefix, rootDirectory)` | `DrxHttpServer`, `string`, `string` | `void` | 添加静态文件路由。 |
| `ResolveFilePath(server, pathOrIndicator)` | `DrxHttpServer`, `string` | `string?` | 解析物理文件路径。 |
| `ClearStaticContentCache(server)` | `DrxHttpServer` | `void` | 清空静态资源缓存。 |
| `InvalidateStaticContentCache(server, filePath)` | `DrxHttpServer`, `string` | `void` | 失效指定资源缓存。 |
| `GetSseClientCount(server, path)` | `DrxHttpServer`, `string?` | `int` | 获取 SSE 连接数量。 |
| `BroadcastSseAsync(server, path, eventName, data)` | `DrxHttpServer`, `string`, `string?`, `string` | `Task` | 向指定路径广播 SSE。 |
| `BroadcastSseToAllAsync(server, eventName, data)` | `DrxHttpServer`, `string?`, `string` | `Task` | 向所有 SSE 客户端广播。 |
| `DisconnectSseClient(server, clientId)` | `DrxHttpServer`, `string` | `void` | 断开单个 SSE 客户端。 |
| `DisconnectAllSseClients(server, path)` | `DrxHttpServer`, `string?` | `void` | 断开路径下全部 SSE 客户端。 |
| `DisposeAsync(server)` | `DrxHttpServer` | `Task` | 异步释放服务器资源。 |
| `Map(server, method, path, handler, rateLimitMaxRequests, rateLimitWindowSeconds)` | `DrxHttpServer`, `string`, `string`, `Func<HttpRequest, object?>`, `int`, `int` | `void` | 添加函数驱动同步路由，支持返回 HttpResponse/string/object/null。 |
| `MapAsync(server, method, path, handler, rateLimitMaxRequests, rateLimitWindowSeconds)` | `DrxHttpServer`, `string`, `string`, `Func<HttpRequest, Task<object?>>`, `int`, `int` | `void` | 添加函数驱动异步路由。 |
| `MapWithRateCallback(server, method, path, handler, rateLimitMaxRequests, rateLimitWindowSeconds, rateLimitCallback)` | `DrxHttpServer`, `string`, `string`, `Func<HttpRequest, object?>`, `int`, `int`, `object?` | `void` | 添加同步路由并配置路由级限流回调。 |
| `MapAsyncWithRateCallback(server, method, path, handler, rateLimitMaxRequests, rateLimitWindowSeconds, rateLimitCallback)` | `DrxHttpServer`, `string`, `string`, `Func<HttpRequest, Task<object?>>`, `int`, `int`, `object?` | `void` | 添加异步路由并配置路由级限流回调。 |
| `Use(server, middleware, path, priority, overrideGlobal)` | `DrxHttpServer`, `Func<HttpRequest, object?>`, `string?`, `int`, `bool` | `void` | 添加函数驱动中间件，返回 null 继续 next。 |
| `UseAsync(server, middleware, path, priority, overrideGlobal)` | `DrxHttpServer`, `Func<HttpRequest, Task<object?>>`, `string?`, `int`, `bool` | `void` | 添加异步函数驱动中间件。 |
| `MapByName(server, method, path, functionName, rateLimitMaxRequests, rateLimitWindowSeconds)` | `DrxHttpServer`, `string`, `string`, `string`, `int`, `int` | `void` | 按函数名注册路由：运行时调用 `globalThis[functionName](request)`。 |
| `MapByNameWithRateCallback(server, method, path, functionName, rateLimitMaxRequests, rateLimitWindowSeconds, rateLimitCallback)` | `DrxHttpServer`, `string`, `string`, `string`, `int`, `int`, `object?` | `void` | 按函数名注册路由并配置路由级限流回调。 |
| `UseByName(server, functionName, path, priority, overrideGlobal)` | `DrxHttpServer`, `string`, `string?`, `int`, `bool` | `void` | 按函数名注册中间件：返回 null 继续 next。 |
| `ScriptHttpServer.patch(path, handler)` | `string`, `object` | `ScriptHttpServer` | PATCH 快捷路由注册。 |
| `ScriptHttpServer.head(path, handler)` | `string`, `object` | `ScriptHttpServer` | HEAD 快捷路由注册。 |
| `ScriptHttpServer.options(path, handler)` | `string`, `object` | `ScriptHttpServer` | OPTIONS 快捷路由注册。 |
| `ScriptHttpServer.mapWithRateCallback(method, path, handler, rateLimitMaxRequests, rateLimitWindowSeconds, rateLimitCallback)` | `string`, `string`, `object`, `int`, `int`, `object?` | `ScriptHttpServer` | 实例级路由注册并附带限流超限回调。 |

## Usage
```csharp
var server = HttpServerFactoryBridge.CreateFromPrefix("http://localhost:8080/");
```
