using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using System.Linq;
using System.Net.Http;

namespace DrxPaperclip.Hosting;

/// <summary>
/// DrxHttpServer 脚本友好工厂桥接层。
/// 关键依赖：Drx.Sdk.Network.Http.DrxHttpServer。
/// </summary>
public static class HttpServerFactoryBridge
{
    private static readonly object EngineSync = new();
    private static IJavaScriptEngine? _engine;

    /// <summary>
    /// 绑定脚本引擎实例，供函数名驱动路由/中间件回调调用。
    /// </summary>
    internal static void BindEngine(IJavaScriptEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        lock (EngineSync)
        {
            _engine = engine;
        }
    }

    /// <summary>
    /// 将脚本返回值转换为 HttpResponse。
    /// </summary>
    private static HttpResponse ToHttpResponse(object? value)
    {
        return value switch
        {
            null => new HttpResponse(204, string.Empty),
            HttpResponse response => response,
            string text => new HttpResponse(200, text),
            byte[] bytes => new HttpResponse(200, bytes),
            _ => new HttpResponse(200, value)
        };
    }

    private static HttpResponse? ToOptionalHttpResponse(object? value)
    {
        return value is null ? null : ToHttpResponse(value);
    }

    /// <summary>
    /// 解析 HTTP 方法字符串。
    /// </summary>
    private static HttpMethod ParseHttpMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("method 不能为空。", nameof(method));
        }

        return new HttpMethod(method.Trim().ToUpperInvariant());
    }

    private static object? InvokeScriptFunctionByName(string functionName, params object?[] args)
    {
        if (string.IsNullOrWhiteSpace(functionName))
        {
            throw new ArgumentException("functionName 不能为空。", nameof(functionName));
        }

        var escapedName = functionName
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal);

        lock (EngineSync)
        {
            if (_engine == null)
            {
                throw new InvalidOperationException("脚本引擎尚未绑定，无法调用函数名回调。");
            }

            _engine.RegisterGlobal("__pc_args", args);
            var script =
                "(() => {\n" +
                $"  const fn = globalThis[\"{escapedName}\"];\n" +
                "  if (typeof fn !== 'function') {\n" +
                $"    throw new Error('回调函数不存在: {escapedName}');\n" +
                "  }\n" +
                "  return fn(...globalThis.__pc_args);\n" +
                "})();";

            return _engine.Execute(script);
        }
    }

    private static object? InvokeFlexibleCallback(object callback, params object?[] args)
    {
        if (callback is Delegate del)
        {
            var parameters = del.Method.GetParameters().Length;
            return parameters switch
            {
                0 => del.DynamicInvoke(),
                1 => del.DynamicInvoke(args.Length > 0 ? args[0] : null),
                2 => del.DynamicInvoke(args.Length > 0 ? args[0] : null, args.Length > 1 ? args[1] : null),
                _ => del.DynamicInvoke(
                    args.Length > 0 ? args[0] : null,
                    args.Length > 1 ? args[1] : null,
                    args.Length > 2 ? args[2] : null)
            };
        }

        var candidates = new object?[][]
        {
            args,
            args.Take(2).ToArray(),
            args.Take(1).ToArray(),
            Array.Empty<object?>()
        };

        Exception? last = null;
        foreach (var candidate in candidates)
        {
            try
            {
                return candidate.Length switch
                {
                    0 => ((dynamic)callback)(),
                    1 => ((dynamic)callback)(candidate[0]),
                    2 => ((dynamic)callback)(candidate[0], candidate[1]),
                    _ => ((dynamic)callback)(candidate[0], candidate[1], candidate[2])
                };
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new InvalidOperationException($"调用速率限制回调失败: {last?.Message}", last);
    }

    private static async Task<object?> AwaitIfTaskAsync(object? value)
    {
        if (value is not Task task)
        {
            return value;
        }

        await task.ConfigureAwait(false);
        return task.GetType().IsGenericType
            ? task.GetType().GetProperty("Result")?.GetValue(task)
            : null;
    }

    private static Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? BuildRateLimitCallback(object? callback)
    {
        if (callback is null)
        {
            return null;
        }

        if (callback is string functionName)
        {
            return async (count, request, context) =>
            {
                var result = await AwaitIfTaskAsync(InvokeScriptFunctionByName(functionName, count, request, context)).ConfigureAwait(false);
                return ToOptionalHttpResponse(result);
            };
        }

        return async (count, request, context) =>
        {
            var result = await AwaitIfTaskAsync(InvokeFlexibleCallback(callback, count, request, context)).ConfigureAwait(false);
            return ToOptionalHttpResponse(result);
        };
    }

    /// <summary>
    /// 使用前缀数组创建 DrxHttpServer 实例。
    /// </summary>
    /// <param name="prefixes">监听前缀数组，例如 http://localhost:8080/。</param>
    /// <param name="staticFileRoot">静态文件根目录（可选）。</param>
    /// <param name="sessionTimeoutMinutes">会话超时分钟数，默认 30。</param>
    /// <returns>可在脚本中继续调用实例方法的 DrxHttpServer。</returns>
    public static DrxHttpServer Create(string[] prefixes, string? staticFileRoot = null, int sessionTimeoutMinutes = 30)
    {
        if (prefixes == null || prefixes.Length == 0)
        {
            throw new ArgumentException("prefixes 不能为空。", nameof(prefixes));
        }

        return new DrxHttpServer(prefixes, staticFileRoot, sessionTimeoutMinutes);
    }

    /// <summary>
    /// 使用单前缀创建 DrxHttpServer 实例。
    /// </summary>
    /// <param name="prefix">监听前缀，例如 http://localhost:8080/。</param>
    /// <param name="staticFileRoot">静态文件根目录（可选）。</param>
    /// <param name="sessionTimeoutMinutes">会话超时分钟数，默认 30。</param>
    /// <returns>可在脚本中继续调用实例方法的 DrxHttpServer。</returns>
    public static DrxHttpServer CreateFromPrefix(string prefix, string? staticFileRoot = null, int sessionTimeoutMinutes = 30)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("prefix 不能为空。", nameof(prefix));
        }

        return new DrxHttpServer(new[] { prefix }, staticFileRoot, sessionTimeoutMinutes);
    }

    /// <summary>
    /// 启动服务器监听。
    /// </summary>
    public static Task StartAsync(DrxHttpServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.StartAsync();
    }

    /// <summary>
    /// 停止服务器。
    /// </summary>
    public static void Stop(DrxHttpServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        server.Stop();
    }

    /// <summary>
    /// 设置调试模式并返回当前服务器实例（链式调用）。
    /// </summary>
    public static DrxHttpServer SetDebugMode(DrxHttpServer server, bool enable)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.DebugMode(enable);
    }

    /// <summary>
    /// 启用或关闭静默模式（仅保留错误日志，大幅提升吞吐性能）。
    /// </summary>
    public static DrxHttpServer SetNoLog(DrxHttpServer server, bool enable)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.NoLog(enable);
    }

    /// <summary>
    /// 批量配置 FileRootPath / ViewRoot / NotFoundPagePath。
    /// </summary>
    public static DrxHttpServer ConfigurePaths(DrxHttpServer server, string? fileRootPath = null, string? viewRoot = null, string? notFoundPagePath = null)
    {
        ArgumentNullException.ThrowIfNull(server);
        server.FileRootPath = fileRootPath;
        server.ViewRoot = viewRoot;
        server.NotFoundPagePath = notFoundPagePath;
        return server;
    }

    /// <summary>
    /// 设置全局限流参数。
    /// </summary>
    public static void SetRateLimit(DrxHttpServer server, int maxRequests, int timeValue, string timeUnit)
    {
        ArgumentNullException.ThrowIfNull(server);
        server.SetRateLimit(maxRequests, timeValue, timeUnit);
    }

    /// <summary>
    /// 添加静态文件路由。
    /// </summary>
    public static void AddFileRoute(DrxHttpServer server, string urlPrefix, string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(server);
        server.AddFileRoute(urlPrefix, rootDirectory);
    }

    /// <summary>
    /// 解析文件绝对路径。
    /// </summary>
    public static string? ResolveFilePath(DrxHttpServer server, string pathOrIndicator)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.ResolveFilePath(pathOrIndicator);
    }

    /// <summary>
    /// 清空静态内容缓存。
    /// </summary>
    public static void ClearStaticContentCache(DrxHttpServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        server.ClearStaticContentCache();
    }

    /// <summary>
    /// 失效指定静态内容缓存。
    /// </summary>
    public static void InvalidateStaticContentCache(DrxHttpServer server, string filePath)
    {
        ArgumentNullException.ThrowIfNull(server);
        server.InvalidateStaticContentCache(filePath);
    }

    /// <summary>
    /// 获取 SSE 客户端数量。
    /// </summary>
    public static int GetSseClientCount(DrxHttpServer server, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.GetSseClientCount(path);
    }

    /// <summary>
    /// 向指定路径广播 SSE 消息。
    /// </summary>
    public static Task BroadcastSseAsync(DrxHttpServer server, string path, string? eventName, string data)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.BroadcastSseAsync(path, eventName, data);
    }

    /// <summary>
    /// 向全部 SSE 客户端广播消息。
    /// </summary>
    public static Task BroadcastSseToAllAsync(DrxHttpServer server, string? eventName, string data)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.BroadcastSseToAllAsync(eventName, data);
    }

    /// <summary>
    /// 断开指定 SSE 客户端。
    /// </summary>
    public static void DisconnectSseClient(DrxHttpServer server, string clientId)
    {
        ArgumentNullException.ThrowIfNull(server);
        server.DisconnectSseClient(clientId);
    }

    /// <summary>
    /// 断开全部 SSE 客户端。
    /// </summary>
    public static void DisconnectAllSseClients(DrxHttpServer server, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(server);
        server.DisconnectAllSseClients(path);
    }

    /// <summary>
    /// 异步释放服务器资源。
    /// </summary>
    public static Task DisposeAsync(DrxHttpServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        return server.DisposeAsync().AsTask();
    }

    /// <summary>
    /// 调用处理函数，自动适配参数个数：支持 (request, server) 或 (request) 或无参。
    /// </summary>
    private static object? InvokeHandler(object handler, HttpRequest request, DrxHttpServer server)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (handler is Delegate del)
        {
            var paramCount = del.Method.GetParameters().Length;
            return paramCount switch
            {
                0 => del.DynamicInvoke(),
                1 => del.DynamicInvoke(request),
                _ => del.DynamicInvoke(request, server)
            };
        }

        var candidates = new object?[][]
        {
            new object?[] { request, server },
            new object?[] { request },
            Array.Empty<object?>()
        };

        Exception? last = null;
        foreach (var args in candidates)
        {
            try
            {
                return args.Length switch
                {
                    0 => ((dynamic)handler)(),
                    1 => ((dynamic)handler)(args[0]),
                    _ => ((dynamic)handler)(args[0], args[1])
                };
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new InvalidOperationException($"调用处理函数失败: {last?.Message}", last);
    }

    /// <summary>
    /// 添加函数驱动的同步路由。
    /// 脚本函数可返回 HttpResponse / string / object / null。
    /// handler 签名支持 (request) 或 (request, server)。
    /// </summary>
    public static void Map(DrxHttpServer server, string method, string path, Func<HttpRequest, object?> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(handler);

        var httpMethod = ParseHttpMethod(method);
        server.AddRoute(
            httpMethod,
            path,
            req => ToHttpResponse(InvokeHandler(handler, req, server)),
            rateLimitMaxRequests,
            rateLimitWindowSeconds);
    }

    /// <summary>
    /// 添加函数驱动的异步路由。
    /// 脚本函数可返回 HttpResponse / string / object / null。
    /// handler 签名支持 (request) 或 (request, server)。
    /// </summary>
    public static void MapAsync(DrxHttpServer server, string method, string path, Func<HttpRequest, Task<object?>> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(handler);

        var httpMethod = ParseHttpMethod(method);
        server.AddRoute(
            httpMethod,
            path,
            async req => ToHttpResponse(await AwaitIfTaskAsync(InvokeHandler(handler, req, server)).ConfigureAwait(false)),
            rateLimitMaxRequests,
            rateLimitWindowSeconds);
    }

    /// <summary>
    /// 添加函数驱动同步路由，并可注册路由级限流回调。
    /// handler 签名支持 (request) 或 (request, server)。
    /// </summary>
    public static void MapWithRateCallback(DrxHttpServer server, string method, string path, Func<HttpRequest, object?> handler, int rateLimitMaxRequests, int rateLimitWindowSeconds, object? rateLimitCallback)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(handler);

        var httpMethod = ParseHttpMethod(method);
        server.AddRoute(
            httpMethod,
            path,
            req => Task.FromResult(ToHttpResponse(InvokeHandler(handler, req, server))),
            rateLimitMaxRequests,
            rateLimitWindowSeconds,
            BuildRateLimitCallback(rateLimitCallback));
    }

    /// <summary>
    /// 添加函数驱动异步路由，并可注册路由级限流回调。
    /// handler 签名支持 (request) 或 (request, server)。
    /// </summary>
    public static void MapAsyncWithRateCallback(DrxHttpServer server, string method, string path, Func<HttpRequest, Task<object?>> handler, int rateLimitMaxRequests, int rateLimitWindowSeconds, object? rateLimitCallback)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(handler);

        var httpMethod = ParseHttpMethod(method);
        server.AddRoute(
            httpMethod,
            path,
            async req => ToHttpResponse(await AwaitIfTaskAsync(InvokeHandler(handler, req, server)).ConfigureAwait(false)),
            rateLimitMaxRequests,
            rateLimitWindowSeconds,
            BuildRateLimitCallback(rateLimitCallback));
    }

    /// <summary>
    /// 添加函数驱动中间件。
    /// 返回 null 表示继续 next；返回非 null 表示短路并直接响应。
    /// middleware 签名支持 (request) 或 (request, server)。
    /// </summary>
    public static void Use(DrxHttpServer server, Func<HttpRequest, object?> middleware, string? path = null, int priority = -1, bool overrideGlobal = false)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(middleware);

        server.AddMiddleware(
            async (req, next, _) =>
            {
                var result = InvokeHandler(middleware, req, server);
                if (result is null)
                {
                    return await next(req).ConfigureAwait(false);
                }

                return ToHttpResponse(result);
            },
            path,
            priority,
            overrideGlobal);
    }

    /// <summary>
    /// 添加异步函数驱动中间件。
    /// 返回 null 表示继续 next；返回非 null 表示短路并直接响应。
    /// middleware 签名支持 (request) 或 (request, server)。
    /// </summary>
    public static void UseAsync(DrxHttpServer server, Func<HttpRequest, Task<object?>> middleware, string? path = null, int priority = -1, bool overrideGlobal = false)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(middleware);

        server.AddMiddleware(
            async (req, next, _) =>
            {
                var result = await AwaitIfTaskAsync(InvokeHandler(middleware, req, server)).ConfigureAwait(false);
                if (result is null)
                {
                    return await next(req).ConfigureAwait(false);
                }

                return ToHttpResponse(result);
            },
            path,
            priority,
            overrideGlobal);
    }

    /// <summary>
    /// 添加函数名驱动同步路由。
    /// 路由触发时调用全局函数：<c>globalThis[functionName](request, server)</c>。
    /// </summary>
    public static void MapByName(DrxHttpServer server, string method, string path, string functionName, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0)
    {
        ArgumentNullException.ThrowIfNull(server);

        var httpMethod = ParseHttpMethod(method);
        server.AddRoute(
            httpMethod,
            path,
            req => ToHttpResponse(InvokeScriptFunctionByName(functionName, req, server)),
            rateLimitMaxRequests,
            rateLimitWindowSeconds);
    }

    /// <summary>
    /// 添加函数名驱动同步路由，并可注册路由级限流回调。
    /// </summary>
    public static void MapByNameWithRateCallback(DrxHttpServer server, string method, string path, string functionName, int rateLimitMaxRequests, int rateLimitWindowSeconds, object? rateLimitCallback)
    {
        ArgumentNullException.ThrowIfNull(server);

        var httpMethod = ParseHttpMethod(method);
        server.AddRoute(
            httpMethod,
            path,
            req => Task.FromResult(ToHttpResponse(InvokeScriptFunctionByName(functionName, req, server))),
            rateLimitMaxRequests,
            rateLimitWindowSeconds,
            BuildRateLimitCallback(rateLimitCallback));
    }

    /// <summary>
    /// 添加函数名驱动中间件。
    /// 中间件函数返回 null 表示继续 next；返回非 null 则短路为响应。
    /// </summary>
    public static void UseByName(DrxHttpServer server, string functionName, string? path = null, int priority = -1, bool overrideGlobal = false)
    {
        ArgumentNullException.ThrowIfNull(server);

        server.AddMiddleware(
            async (req, next, _) =>
            {
                var result = InvokeScriptFunctionByName(functionName, req, server);
                if (result is null)
                {
                    return await next(req).ConfigureAwait(false);
                }

                return ToHttpResponse(result);
            },
            path,
            priority,
            overrideGlobal);
    }
}
