using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Entry;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpServer 中间件部分：中间件管理
    /// </summary>
    public partial class DrxHttpServer
    {
        /// <summary>
        /// 添加中间件
        /// </summary>
        /// <param name="middleware">中间件处理委托</param>
        /// <param name="path">路径前缀，为 null 表示全局中间件</param>
        /// <param name="priority">优先级，-1 表示使用默认</param>
        /// <param name="overrideGlobal">是否覆盖全局优先级</param>
        public void AddMiddleware(Func<HttpListenerContext, Task> middleware, string? path = null, int priority = -1, bool overrideGlobal = false)
        {
            if (middleware == null) return;
            if (path != null && !path.StartsWith("/")) path = "/" + path;

            if (priority == -1)
            {
                priority = (path == null) ? 0 : 100;
            }

            if (overrideGlobal)
            {
                priority = -1;
            }

            var entry = new MiddlewareEntry
            {
                Handler = middleware,
                Path = path,
                Priority = priority,
                OverrideGlobal = overrideGlobal,
                AddOrder = _middlewareCounter++
            };

            _middlewares.Add(entry);
            lock (_middlewareCacheLock)
            {
                _cachedSortedMiddlewares = null;
            }
            Logger.Info($"添加中间件: {path ?? "全局"} (优先级: {priority})");
        }

        /// <summary>
        /// 添加中间件，支持注入 server 参数
        /// </summary>
        public void AddMiddleware(Func<HttpListenerContext, DrxHttpServer, Task> middleware, string? path = null, int priority = -1, bool overrideGlobal = false)
        {
            if (middleware == null) return;
            Func<HttpListenerContext, Task> wrapped = ctx => middleware(ctx, this);
            AddMiddleware(wrapped, path, priority, overrideGlobal);
        }

        /// <summary>
        /// 添加基于 HttpRequest 的中间件，支持注入 server 参数
        /// </summary>
        public void AddMiddleware(Func<HttpRequest, Func<HttpRequest, Task<HttpResponse?>>, DrxHttpServer, Task<HttpResponse?>> requestMiddleware, string? path = null, int priority = -1, bool overrideGlobal = false)
        {
            if (requestMiddleware == null) return;
            var entry = new MiddlewareEntry
            {
                RequestMiddleware = (req, next) => requestMiddleware(req, next, this),
                Path = path,
                Priority = priority == -1 ? (path == null ? 0 : 100) : priority,
                OverrideGlobal = overrideGlobal,
                AddOrder = _middlewareCounter++
            };

            _middlewares.Add(entry);
            lock (_middlewareCacheLock)
            {
                _cachedSortedMiddlewares = null;
            }
            Logger.Info($"添加中间件: {path ?? "全局"} (优先级: {entry.Priority})");
        }

        /// <summary>
        /// 路径级中间件列表缓存，避免每次请求都重新过滤和分配列表
        /// </summary>
        private readonly ConcurrentDictionary<string, List<MiddlewareEntry>> _pathMiddlewareCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 执行中间件管道
        /// </summary>
        private async Task<HttpResponse?> ExecuteMiddlewarePipelineAsync(HttpListenerContext context, HttpRequest request, Func<HttpRequest, Task<HttpResponse?>> finalHandler)
        {
            var rawPath = context.Request.Url?.AbsolutePath ?? "/";

            List<MiddlewareEntry> sortedMiddlewares;
            lock (_middlewareCacheLock)
            {
                if (_cachedSortedMiddlewares == null)
                {
                    var applicableMiddlewares = new List<MiddlewareEntry>();
                    applicableMiddlewares.AddRange(_middlewares.Where(m => m.Path == null));
                    applicableMiddlewares.AddRange(_middlewares.Where(m => m.Path != null));

                    applicableMiddlewares.Sort((a, b) =>
                    {
                        int aPriority = a.OverrideGlobal ? -1 : a.Priority;
                        int bPriority = b.OverrideGlobal ? -1 : b.Priority;
                        int priorityCompare = aPriority.CompareTo(bPriority);
                        if (priorityCompare != 0) return priorityCompare;
                        return a.AddOrder.CompareTo(b.AddOrder);
                    });
                    _cachedSortedMiddlewares = applicableMiddlewares;
                    _pathMiddlewareCache.Clear();
                }
                sortedMiddlewares = _cachedSortedMiddlewares;
            }

            if (!_pathMiddlewareCache.TryGetValue(rawPath, out var applicableForPath))
            {
                applicableForPath = new List<MiddlewareEntry>(sortedMiddlewares.Count);
                foreach (var m in sortedMiddlewares)
                {
                    if (m.Path == null || rawPath.StartsWith(m.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        applicableForPath.Add(m);
                    }
                }
                _pathMiddlewareCache.TryAdd(rawPath, applicableForPath);
            }

            Func<HttpRequest, Task<HttpResponse?>> pipeline = finalHandler;
            for (int i = applicableForPath.Count - 1; i >= 0; i--)
            {
                var mw = applicableForPath[i];
                var next = pipeline;
                pipeline = async (req) =>
                {
                    if (mw.RequestMiddleware != null)
                    {
                        try
                        {
                            return await mw.RequestMiddleware(req, next).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"执行基于请求的中间件时发生错误: {ex}");
                            return new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                        }
                    }
                    else
                    {
                        try
                        {
                            await mw.Handler(context).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"执行基于上下文的中间件时发生错误: {ex}");
                        }
                        return await next(req).ConfigureAwait(false);
                    }
                };
            }

            return await pipeline(request).ConfigureAwait(false);
        }
    }
}
