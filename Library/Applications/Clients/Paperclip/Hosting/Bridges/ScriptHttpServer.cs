using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Shared;
using DrxPaperclip.Hosting.Watch;
using System.Net.Http;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 脚本友好型 HTTP 服务器包装器。
/// 支持 <c>new ScriptHttpServer(prefix)</c> 构造，以及实例方法直接传入 JS 函数引用。
/// </summary>
/// <remarks>
/// <para>依赖：Drx.Sdk.Network.Http.DrxHttpServer</para>
/// <para>
/// 路由与中间件回调支持直接传入 JS 函数对象，无需字符串函数名。
/// 回调签名：<c>(request: HttpRequest) => any</c>
/// </para>
/// </remarks>
public sealed class ScriptHttpServer
{
    private readonly DrxHttpServer _server;
    private readonly object _eventHooksLock = new();
    private readonly Dictionary<string, List<object>> _eventHooks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 使用单前缀创建 HTTP 服务器。
    /// </summary>
    /// <param name="prefix">监听前缀，例如 http://localhost:8080/。</param>
    public ScriptHttpServer(string prefix)
        : this(prefix, null, 30)
    {
    }

    /// <summary>
    /// 使用单前缀和可选参数创建 HTTP 服务器。
    /// </summary>
    /// <param name="prefix">监听前缀，例如 http://localhost:8080/。</param>
    /// <param name="staticFileRoot">静态文件根目录（可选）。</param>
    /// <param name="sessionTimeoutMinutes">会话超时分钟数，默认 30。</param>
    public ScriptHttpServer(string prefix, string? staticFileRoot, int sessionTimeoutMinutes = 30)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("prefix 不能为空。", nameof(prefix));
        }

        _server = new DrxHttpServer(new[] { prefix }, staticFileRoot, sessionTimeoutMinutes);
        ScriptHttpResponse.BoundServer = _server;
        ActiveServerTracker.Register(_server);

        _server.AddMiddleware(async (req, next, _) =>
        {
            await InvokeEventHooksAsync("request", req, _server).ConfigureAwait(false);

            try
            {
                var response = await next(req).ConfigureAwait(false);
                await InvokeEventHooksAsync("response", req, response, _server).ConfigureAwait(false);
                return response;
            }
            catch (Exception ex)
            {
                await InvokeEventHooksAsync("error", req, ex, _server).ConfigureAwait(false);
                throw;
            }
        }, priority: int.MinValue);
    }

    /// <summary>
    /// 获取底层 DrxHttpServer 实例（高级用法）。
    /// </summary>
    public DrxHttpServer Server => _server;

    // ───────────────────────────── 生命周期 ─────────────────────────────

    /// <summary>启动服务器监听。</summary>
    public Task startAsync()
    {
        ScriptHttpResponse.BoundServer = _server;

        return StartAndNotifyAsync();
    }

    private async Task StartAndNotifyAsync()
    {
        try
        {
            await _server.StartAsync().ConfigureAwait(false);
            await InvokeEventHooksAsync("start", _server).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await InvokeEventHooksAsync("error", null, ex, _server).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>停止服务器。</summary>
    public void stop()
    {
        try
        {
            _server.Stop();
            InvokeEventHooksBlocking("stop", _server);
        }
        catch (Exception ex)
        {
            InvokeEventHooksBlocking("error", null, ex, _server);
            throw;
        }
    }

    /// <summary>异步释放服务器资源。</summary>
    public Task disposeAsync() => _server.DisposeAsync().AsTask();

    // ───────────────────────────── 事件钩子 ─────────────────────────────

    /// <summary>
    /// 注册服务器启动成功事件钩子。
    /// </summary>
    public ScriptHttpServer onStart(object handler) => RegisterEventHook("start", handler);

    /// <summary>
    /// 注册服务器停止事件钩子。
    /// </summary>
    public ScriptHttpServer onStop(object handler) => RegisterEventHook("stop", handler);

    /// <summary>
    /// 注册请求进入事件钩子。
    /// </summary>
    public ScriptHttpServer onRequest(object handler) => RegisterEventHook("request", handler);

    /// <summary>
    /// 注册响应输出事件钩子。
    /// </summary>
    public ScriptHttpServer onResponse(object handler) => RegisterEventHook("response", handler);

    /// <summary>
    /// 注册错误事件钩子。
    /// </summary>
    public ScriptHttpServer onError(object handler) => RegisterEventHook("error", handler);

    /// <summary>
    /// 通用事件注册。
    /// 支持：start/stop/request/response/error。
    /// </summary>
    public ScriptHttpServer on(string eventName, object handler)
    {
        var key = NormalizeEventName(eventName);
        return RegisterEventHook(key, handler);
    }

    private ScriptHttpServer RegisterEventHook(string eventName, object handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_eventHooksLock)
        {
            if (!_eventHooks.TryGetValue(eventName, out var handlers))
            {
                handlers = new List<object>();
                _eventHooks[eventName] = handlers;
            }

            handlers.Add(handler);
        }

        return this;
    }

    private static string NormalizeEventName(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException("eventName 不能为空。", nameof(eventName));
        }

        var normalized = eventName.Trim().ToLowerInvariant();
        return normalized switch
        {
            "start" or "started" => "start",
            "stop" or "stopped" => "stop",
            "request" or "req" => "request",
            "response" or "resp" => "response",
            "error" or "err" or "exception" => "error",
            _ => throw new ArgumentOutOfRangeException(nameof(eventName), eventName, "仅支持 start/stop/request/response/error 事件。")
        };
    }

    // ───────────────────────────── 配置 ─────────────────────────────

    /// <summary>设置调试模式。</summary>
    public ScriptHttpServer debugMode(bool enable)
    {
        _server.DebugMode(enable);
        return this;
    }

    public ScriptHttpServer noLog(bool enable)
    {
        _server.NoLog(enable);
        return this;
    }

    /// <summary>设置静态文件根目录。</summary>
    public ScriptHttpServer setFileRoot(string? path)
    {
        _server.FileRootPath = path;
        return this;
    }

    /// <summary>设置视图根目录。</summary>
    public ScriptHttpServer setViewRoot(string? path)
    {
        _server.ViewRoot = path;
        return this;
    }

    /// <summary>设置 404 页面路径。</summary>
    public ScriptHttpServer setNotFoundPage(string? path)
    {
        _server.NotFoundPagePath = path;
        return this;
    }

    /// <summary>设置全局限流。</summary>
    public ScriptHttpServer setRateLimit(int maxRequests, int timeValue, string timeUnit)
    {
        _server.SetRateLimit(maxRequests, timeValue, timeUnit);
        return this;
    }

    /// <summary>添加静态文件路由。</summary>
    public ScriptHttpServer addFileRoute(string urlPrefix, string rootDirectory)
    {
        _server.AddFileRoute(urlPrefix, rootDirectory);
        return this;
    }

    // ───────────────────────────── 路由 ─────────────────────────────

    /// <summary>
    /// 添加路由（直接传入 JS 函数）。
    /// </summary>
    /// <param name="method">HTTP 方法：GET, POST, PUT, DELETE 等。</param>
    /// <param name="path">路由路径，例如 /api/users。</param>
    /// <param name="handler">处理函数：<c>(request) => response</c>。</param>
    /// <returns>当前实例（链式调用）。</returns>
    public ScriptHttpServer map(string method, string path, object handler)
    {
        return map(method, path, handler, 0, 0);
    }

    /// <summary>
    /// 添加路由（带限流参数）。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="path">路由路径。</param>
    /// <param name="handler">处理函数。</param>
    /// <param name="rateLimitMaxRequests">限流最大请求数（0=不限流）。</param>
    /// <param name="rateLimitWindowSeconds">限流时间窗口秒数。</param>
    /// <returns>当前实例。</returns>
    public ScriptHttpServer map(string method, string path, object handler, int rateLimitMaxRequests, int rateLimitWindowSeconds)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var httpMethod = ParseHttpMethod(method);
        _server.AddRoute(
            httpMethod,
            path,
            req => ToHttpResponse(InvokeHandler(handler, req, _server)),
            rateLimitMaxRequests,
            rateLimitWindowSeconds);
        return this;
    }

    /// <summary>
    /// 添加路由（带限流参数与超限回调）。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="path">路由路径。</param>
    /// <param name="handler">处理函数。</param>
    /// <param name="rateLimitMaxRequests">限流最大请求数（0=不限流）。</param>
    /// <param name="rateLimitWindowSeconds">限流时间窗口秒数。</param>
    /// <param name="rateLimitCallback">超限回调，参数支持：(count, request, context) 或其前缀子集，返回 null 表示使用默认限流响应。</param>
    /// <returns>当前实例。</returns>
    public ScriptHttpServer mapWithRateCallback(string method, string path, object handler, int rateLimitMaxRequests, int rateLimitWindowSeconds, object? rateLimitCallback)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var httpMethod = ParseHttpMethod(method);
        _server.AddRoute(
            httpMethod,
            path,
            req => Task.FromResult(ToHttpResponse(InvokeHandler(handler, req, _server))),
            rateLimitMaxRequests,
            rateLimitWindowSeconds,
            BuildRateLimitCallback(rateLimitCallback));
        return this;
    }

    /// <summary>
    /// 添加 GET 路由。
    /// </summary>
    public ScriptHttpServer get(string path, object handler)
    {
        return map("GET", path, handler);
    }

    /// <summary>
    /// 添加 POST 路由。
    /// </summary>
    public ScriptHttpServer post(string path, object handler)
    {
        return map("POST", path, handler);
    }

    /// <summary>
    /// 添加 PUT 路由。
    /// </summary>
    public ScriptHttpServer put(string path, object handler)
    {
        return map("PUT", path, handler);
    }

    /// <summary>
    /// 添加 DELETE 路由。
    /// </summary>
    public ScriptHttpServer delete(string path, object handler)
    {
        return map("DELETE", path, handler);
    }

    /// <summary>
    /// 添加 PATCH 路由。
    /// </summary>
    public ScriptHttpServer patch(string path, object handler)
    {
        return map("PATCH", path, handler);
    }

    /// <summary>
    /// 添加 HEAD 路由。
    /// </summary>
    public ScriptHttpServer head(string path, object handler)
    {
        return map("HEAD", path, handler);
    }

    /// <summary>
    /// 添加 OPTIONS 路由。
    /// </summary>
    public ScriptHttpServer options(string path, object handler)
    {
        return map("OPTIONS", path, handler);
    }

    // ───────────────────────────── 中间件 ─────────────────────────────

    /// <summary>
    /// 添加中间件（直接传入 JS 函数）。
    /// </summary>
    /// <param name="middleware">中间件函数：<c>(request) => response | null</c>。返回 null 表示继续执行下一个中间件/路由。</param>
    /// <returns>当前实例。</returns>
    public ScriptHttpServer use(object middleware)
    {
        return use(middleware, null, -1, false);
    }

    /// <summary>
    /// 添加中间件（带路径过滤）。
    /// </summary>
    /// <param name="middleware">中间件函数。</param>
    /// <param name="path">仅匹配此路径前缀的请求触发。</param>
    /// <returns>当前实例。</returns>
    public ScriptHttpServer use(object middleware, string? path)
    {
        return use(middleware, path, -1, false);
    }

    /// <summary>
    /// 添加中间件（完整参数）。
    /// </summary>
    /// <param name="middleware">中间件函数。</param>
    /// <param name="path">路径过滤（null = 所有路径）。</param>
    /// <param name="priority">优先级（越小越先执行）。</param>
    /// <param name="overrideGlobal">是否覆盖全局中间件。</param>
    /// <returns>当前实例。</returns>
    public ScriptHttpServer use(object middleware, string? path, int priority, bool overrideGlobal)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        _server.AddMiddleware(
            async (req, next, _) =>
            {
                var result = InvokeHandler(middleware, req, _server);
                if (result is null)
                {
                    return await next(req).ConfigureAwait(false);
                }

                return ToHttpResponse(result);
            },
            path,
            priority,
            overrideGlobal);
        return this;
    }

    // ───────────────────────────── SSE (Server-Sent Events) ─────────────────────────────

    /// <summary>获取 SSE 客户端数量。</summary>
    public int getSseClientCount(string? path = null) => _server.GetSseClientCount(path);

    /// <summary>向指定路径广播 SSE 消息。</summary>
    public Task broadcastSse(string path, string? eventName, string data) =>
        _server.BroadcastSseAsync(path, eventName, data);

    /// <summary>向所有 SSE 客户端广播消息。</summary>
    public Task broadcastSseToAll(string? eventName, string data) =>
        _server.BroadcastSseToAllAsync(eventName, data);

    /// <summary>断开指定 SSE 客户端。</summary>
    public void disconnectSseClient(string clientId) =>
        _server.DisconnectSseClient(clientId);

    /// <summary>断开所有 SSE 客户端。</summary>
    public void disconnectAllSseClients(string? path = null) =>
        _server.DisconnectAllSseClients(path);

    // ───────────────────────────── 缓存 ─────────────────────────────

    /// <summary>清空静态内容缓存。</summary>
    public void clearCache() => _server.ClearStaticContentCache();

    /// <summary>失效指定静态内容缓存。</summary>
    public void invalidateCache(string filePath) => _server.InvalidateStaticContentCache(filePath);

    // ───────────────────────────── 私有方法 ─────────────────────────────

    private static HttpMethod ParseHttpMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("method 不能为空。", nameof(method));
        }

        return new HttpMethod(method.Trim().ToUpperInvariant());
    }

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
    /// 调用 JS 函数对象。
    /// 利用 ClearScript 的 dynamic 调用能力来执行传入的 JS 函数。
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

    private static Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? BuildRateLimitCallback(object? callback)
    {
        if (callback is null)
        {
            return null;
        }

        return async (count, request, context) =>
        {
            var result = InvokeRateLimitCallback(callback, count, request, context);
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                result = task.GetType().IsGenericType
                    ? task.GetType().GetProperty("Result")?.GetValue(task)
                    : null;
            }

            return ToOptionalHttpResponse(result);
        };
    }

    private static object? InvokeRateLimitCallback(object callback, int count, HttpRequest request, OverrideContext context)
    {
        if (callback is Delegate del)
        {
            var paramCount = del.Method.GetParameters().Length;
            return paramCount switch
            {
                0 => del.DynamicInvoke(),
                1 => del.DynamicInvoke(count),
                2 => del.DynamicInvoke(count, request),
                _ => del.DynamicInvoke(count, request, context)
            };
        }

        var candidates = new object?[][]
        {
            new object?[] { count, request, context },
            new object?[] { count, request },
            new object?[] { count },
            Array.Empty<object?>()
        };

        Exception? last = null;
        foreach (var args in candidates)
        {
            try
            {
                return args.Length switch
                {
                    0 => ((dynamic)callback)(),
                    1 => ((dynamic)callback)(args[0]),
                    2 => ((dynamic)callback)(args[0], args[1]),
                    _ => ((dynamic)callback)(args[0], args[1], args[2])
                };
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new InvalidOperationException($"调用速率限制回调失败: {last?.Message}", last);
    }

    private async Task InvokeEventHooksAsync(string eventName, params object?[] args)
    {
        List<object>? snapshot;
        lock (_eventHooksLock)
        {
            if (!_eventHooks.TryGetValue(eventName, out var handlers) || handlers.Count == 0)
            {
                return;
            }

            snapshot = new List<object>(handlers);
        }

        foreach (var handler in snapshot)
        {
            try
            {
                var result = InvokeFlexibleCallback(handler, args);
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"执行事件钩子 {eventName} 失败: {ex.Message}");
            }
        }
    }

    private void InvokeEventHooksBlocking(string eventName, params object?[] args)
    {
        try
        {
            InvokeEventHooksAsync(eventName, args).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.Warn($"执行事件钩子 {eventName} 失败: {ex.Message}");
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

        throw new InvalidOperationException($"调用事件钩子失败: {last?.Message}", last);
    }
}
