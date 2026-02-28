using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Entry;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpServer 路由部分：路由添加与匹配
    /// </summary>
    public partial class DrxHttpServer
    {
        #region 原始路由（Raw Route）

        /// <summary>
        /// 添加原始路由（raw handler），处理方法直接接收 HttpListenerContext，适合流式上传/下载场景。
        /// </summary>
        /// <param name="path">路径前缀</param>
        /// <param name="handler">处理委托，接收 HttpListenerContext</param>
        public void AddRawRoute(string path, Func<HttpListenerContext, Task> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0)
        {
            if (string.IsNullOrEmpty(path) || handler == null) return;
            if (!path.StartsWith("/")) path = "/" + path;
            _raw_routes_add(path, handler, rateLimitMaxRequests, rateLimitWindowSeconds);
            Logger.Info($"添加原始路由: {path}");
        }

        /// <summary>
        /// 添加原始路由（raw handler），处理方法可选接收 server 参数
        /// </summary>
        public void AddRawRoute(string path, Func<HttpListenerContext, DrxHttpServer, Task> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0)
        {
            if (string.IsNullOrEmpty(path) || handler == null) return;
            if (!path.StartsWith("/")) path = "/" + path;
            Func<HttpListenerContext, Task> wrapped = ctx => handler(ctx, this);
            _raw_routes_add(path, wrapped, rateLimitMaxRequests, rateLimitWindowSeconds);
            Logger.Info($"添加原始路由: {path}");
        }

        private void _raw_routes_add(string path, Func<HttpListenerContext, Task> handler, int rateLimitMaxRequests, int rateLimitWindowSeconds)
        {
            _raw_routes_internal_add(path, handler, rateLimitMaxRequests, rateLimitWindowSeconds);
        }

        private void _raw_routes_internal_add(string path, Func<HttpListenerContext, Task> handler, int rateLimitMaxRequests, int rateLimitWindowSeconds)
        {
            _rawRoutes.Add((path, handler, rateLimitMaxRequests, rateLimitWindowSeconds, null));
        }

        private IEnumerable<(string Template, Func<HttpListenerContext, Task> Handler, int RateLimitMaxRequests, int RateLimitWindowSeconds, Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? RateLimitCallback)> _raw_routes_reader() => _rawRoutes;

        #endregion

        #region 流式上传路由

        /// <summary>
        /// 添加流式上传路由。处理方法接收 HttpRequest 并可通过 HttpRequest.UploadFile.Stream读取上传数据流。
        /// </summary>
        /// <param name="path">路径前缀</param>
        /// <param name="handler">处理委托</param>
        public void AddStreamUploadRoute(string path, Func<HttpRequest, Task<HttpResponse>> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0)
        {
            if (string.IsNullOrEmpty(path) || handler == null) return;
            if (!path.StartsWith("/")) path = "/" + path;

            Func<HttpListenerContext, Task> rawHandler = async ctx =>
            {
                try
                {
                    var listenerReq = ctx.Request;
                    var req = new HttpRequest
                    {
                        Method = listenerReq.HttpMethod,
                        Path = listenerReq.Url?.AbsolutePath ?? "/",
                        Url = listenerReq.Url?.ToString(),
                        Query = listenerReq.QueryString,
                        Headers = listenerReq.Headers,
                        RemoteEndPoint = listenerReq.RemoteEndPoint,
                        ClientAddress = HttpRequest.Address.FromEndPoint(listenerReq.RemoteEndPoint, listenerReq.Headers),
                        ListenerContext = ctx
                    };

                    req.UploadFile = new HttpRequest.UploadFileDescriptor
                    {
                        Stream = listenerReq.InputStream,
                        FileName = listenerReq.Headers["X-File-Name"] ?? Path.GetFileName(req.Path),
                        FieldName = "file",
                        Progress = null,
                        CancellationToken = System.Threading.CancellationToken.None
                    };

                    HttpResponse? resp;
                    try
                    {
                        resp = await handler(req).ConfigureAwait(false) ?? new HttpResponse(500, "Internal Server Error");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"执行 StreamUpload处理方法时发生错误: {ex.InnerException?.Message ?? ex.Message}\n{ex.InnerException?.StackTrace ?? ex.StackTrace}");
                        resp = new HttpResponse(500, $"Internal Server Error: {ex.InnerException?.Message ?? ex.Message}");
                    }

                    SendResponse(ctx.Response, resp ?? new HttpResponse(500, "Internal Server Error"));
                }
                catch (Exception ex)
                {
                    Logger.Error($"StreamUpload raw handler 错误: {ex}");
                    try { ctx.Response.StatusCode = 500; using var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8, leaveOpen: false); sw.Write("Internal Server Error"); } catch { }
                }
            };

            _raw_routes_add(path, rawHandler, rateLimitMaxRequests, rateLimitWindowSeconds);
            Logger.Info($"添加流式上传路由: {path}");
        }

        /// <summary>
        /// 添加流式上传路由，handler 支持可选的 server 参数
        /// </summary>
        public void AddStreamUploadRoute(string path, Func<HttpRequest, DrxHttpServer, Task<HttpResponse>> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0)
        {
            if (handler == null) return;
            Func<HttpListenerContext, Task> rawHandler = async ctx =>
            {
                try
                {
                    var listenerReq = ctx.Request;
                    var req = new HttpRequest
                    {
                        Method = listenerReq.HttpMethod,
                        Path = listenerReq.Url?.AbsolutePath ?? "/",
                        Url = listenerReq.Url?.ToString(),
                        Query = listenerReq.QueryString,
                        Headers = listenerReq.Headers,
                        RemoteEndPoint = listenerReq.RemoteEndPoint,
                        ClientAddress = HttpRequest.Address.FromEndPoint(listenerReq.RemoteEndPoint, listenerReq.Headers),
                        ListenerContext = ctx
                    };

                    req.UploadFile = new HttpRequest.UploadFileDescriptor
                    {
                        Stream = listenerReq.InputStream,
                        FileName = listenerReq.Headers["X-File-Name"] ?? Path.GetFileName(req.Path),
                        FieldName = "file",
                        Progress = null,
                        CancellationToken = System.Threading.CancellationToken.None
                    };

                    HttpResponse? resp;
                    try
                    {
                        resp = await handler(req, this).ConfigureAwait(false) ?? new HttpResponse(500, "Internal Server Error");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"执行 StreamUpload处理方法时发生错误: {ex.InnerException?.Message ?? ex.Message}\n{ex.InnerException?.StackTrace ?? ex.StackTrace}");
                        resp = new HttpResponse(500, $"Internal Server Error: {ex.InnerException?.Message ?? ex.Message}");
                    }

                    SendResponse(ctx.Response, resp ?? new HttpResponse(500, "Internal Server Error"));
                }
                catch (Exception ex)
                {
                    Logger.Error($"StreamUpload raw handler 错误: {ex}");
                    try { ctx.Response.StatusCode = 500; using var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8, leaveOpen: false); sw.Write("Internal Server Error"); } catch { }
                }
            };

            _raw_routes_add(path, rawHandler, rateLimitMaxRequests, rateLimitWindowSeconds);
            Logger.Info($"添加流式上传路由: {path}");
        }

        #endregion

        #region 常规路由（AddRoute）

        /// <summary>
        /// 添加路由
        /// </summary>
        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, HttpResponse> handler)
        {
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = (request) => Task.FromResult(handler(request)),
                    ExtractParameters = CreateParameterExtractor(path)
                };
                lock (_routesLock)
                {
                    _routes.Add(route);
                    if (!_routesByMethod.ContainsKey(method))
                        _routesByMethod[method] = new();
                    _routesByMethod[method].Add(route);
                    _routeMatchCache?.Invalidate();
                }
                Logger.Info($"添加同步路由: {method} {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加同步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, DrxHttpServer, HttpResponse> handler)
        {
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = (request) => Task.FromResult(handler(request, this)),
                    ExtractParameters = CreateParameterExtractor(path)
                };
                lock (_routesLock)
                {
                    _routes.Add(route);
                    if (!_routesByMethod.ContainsKey(method))
                        _routesByMethod[method] = new();
                    _routesByMethod[method].Add(route);
                    _routeMatchCache?.Invalidate();
                }
                Logger.Info($"添加同步路由: {method} {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加同步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, HttpResponse> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0)
        {
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = (request) => Task.FromResult(handler(request)),
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = rateLimitMaxRequests,
                    RateLimitWindowSeconds = rateLimitWindowSeconds
                };
                lock (_routesLock)
                {
                    _routes.Add(route);
                    if (!_routesByMethod.ContainsKey(method))
                        _routesByMethod[method] = new();
                    _routesByMethod[method].Add(route);
                    _routeMatchCache?.Invalidate();
                }
                Logger.Info($"添加同步路由: {method} {path} (rate={rateLimitMaxRequests}/{rateLimitWindowSeconds}s)");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加同步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, DrxHttpServer, HttpResponse> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0)
        {
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = (request) => Task.FromResult(handler(request, this)),
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = rateLimitMaxRequests,
                    RateLimitWindowSeconds = rateLimitWindowSeconds
                };
                lock (_routesLock)
                {
                    _routes.Add(route);
                    if (!_routesByMethod.ContainsKey(method))
                        _routesByMethod[method] = new();
                    _routesByMethod[method].Add(route);
                    _routeMatchCache?.Invalidate();
                }
                Logger.Info($"添加同步路由: {method} {path} (rate={rateLimitMaxRequests}/{rateLimitWindowSeconds}s)");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加同步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        /// <summary>
        /// 添加异步路由
        /// </summary>
        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, Task<HttpResponse>> handler)
        {
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = handler,
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = 0,
                    RateLimitWindowSeconds = 0
                };
                lock (_routesLock)
                {
                    _routes.Add(route);
                    if (!_routesByMethod.ContainsKey(method))
                        _routesByMethod[method] = new();
                    _routesByMethod[method].Add(route);
                }
                Logger.Info($"添加异步路由: {method} {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加异步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, DrxHttpServer, Task<HttpResponse>> handler)
        {
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = (request) => handler(request, this),
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = 0,
                    RateLimitWindowSeconds = 0
                };
                lock (_routesLock)
                {
                    _routes.Add(route);
                    if (!_routesByMethod.ContainsKey(method))
                        _routesByMethod[method] = new();
                    _routesByMethod[method].Add(route);
                }
                Logger.Info($"添加异步路由: {method} {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加异步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, Task<HttpResponse>> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0, Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? rateLimitCallback = null)
        {
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = handler,
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = rateLimitMaxRequests,
                    RateLimitWindowSeconds = rateLimitWindowSeconds,
                    RateLimitCallback = rateLimitCallback
                };
                lock (_routesLock)
                {
                    _routes.Add(route);
                    if (!_routesByMethod.ContainsKey(method))
                        _routesByMethod[method] = new();
                    _routesByMethod[method].Add(route);
                }
                Logger.Info($"添加异步路由: {method} {path} (rate={rateLimitMaxRequests}/{rateLimitWindowSeconds}s)");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加异步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, DrxHttpServer, Task<HttpResponse>> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0, Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? rateLimitCallback = null)
        {
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = (request) => handler(request, this),
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = rateLimitMaxRequests,
                    RateLimitWindowSeconds = rateLimitWindowSeconds,
                    RateLimitCallback = rateLimitCallback
                };
                lock (_routesLock)
                {
                    _routes.Add(route);
                    if (!_routesByMethod.ContainsKey(method))
                        _routesByMethod[method] = new();
                    _routesByMethod[method].Add(route);
                }
                Logger.Info($"添加异步路由: {method} {path} (rate={rateLimitMaxRequests}/{rateLimitWindowSeconds}s)");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加异步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        #endregion

        #region IActionResult 路由

        /// <summary>
        /// 添加返回 IActionResult 的同步路由
        /// </summary>
        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, IActionResult> handler)
        {
            if (handler == null) return;
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = async (request) =>
                    {
                        var action = handler(request);
                        if (action == null)
                            return await Task.FromResult(new HttpResponse(204, string.Empty));
                        return await action.ExecuteAsync(request, this).ConfigureAwait(false);
                    },
                    ExtractParameters = CreateParameterExtractor(path)
                };
                lock (_routesLock)
                {
                    _routes.Add(route);
                    if (!_routesByMethod.ContainsKey(method))
                        _routesByMethod[method] = new();
                    _routesByMethod[method].Add(route);
                    _routeMatchCache?.Invalidate();
                }
                Logger.Info($"添加同步路由: {method} {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加同步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, DrxHttpServer, IActionResult> handler)
        {
            if (handler == null) return;
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = async (request) =>
                    {
                        var action = handler(request, this);
                        if (action == null)
                            return await Task.FromResult(new HttpResponse(204, string.Empty));
                        return await action.ExecuteAsync(request, this).ConfigureAwait(false);
                    },
                    ExtractParameters = CreateParameterExtractor(path)
                };
                lock (_routesLock)
                {
                    _routes.Add(route);
                    if (!_routesByMethod.ContainsKey(method))
                        _routesByMethod[method] = new();
                    _routesByMethod[method].Add(route);
                    _routeMatchCache?.Invalidate();
                }
                Logger.Info($"添加同步路由: {method} {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加同步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        /// <summary>
        /// 添加返回 IActionResult 的异步路由
        /// </summary>
        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, Task<IActionResult>> handler)
        {
            if (handler == null) return;
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = async (request) =>
                    {
                        var action = await handler(request).ConfigureAwait(false);
                        if (action == null)
                            return new HttpResponse(204, string.Empty);
                        return await action.ExecuteAsync(request, this).ConfigureAwait(false);
                    },
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = 0,
                    RateLimitWindowSeconds = 0
                };
                lock (_routesLock)
                {
                    _routes.Add(route);
                    if (!_routesByMethod.ContainsKey(method))
                        _routesByMethod[method] = new();
                    _routesByMethod[method].Add(route);
                }
                Logger.Info($"添加异步路由: {method} {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加异步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, DrxHttpServer, Task<IActionResult>> handler)
        {
            if (handler == null) return;
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = async (request) =>
                    {
                        var action = await handler(request, this).ConfigureAwait(false);
                        if (action == null)
                            return new HttpResponse(204, string.Empty);
                        return await action.ExecuteAsync(request, this).ConfigureAwait(false);
                    },
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = 0,
                    RateLimitWindowSeconds = 0
                };
                lock (_routesLock)
                {
                    _routes.Add(route);
                    if (!_routesByMethod.ContainsKey(method))
                        _routesByMethod[method] = new();
                    _routesByMethod[method].Add(route);
                }
                Logger.Info($"添加异步路由: {method} {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加异步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, Task<IActionResult>> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0, Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? rateLimitCallback = null)
        {
            if (handler == null) return;
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = async (request) =>
                    {
                        var action = await handler(request).ConfigureAwait(false);
                        if (action == null)
                            return new HttpResponse(204, string.Empty);
                        return await action.ExecuteAsync(request, this).ConfigureAwait(false);
                    },
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = rateLimitMaxRequests,
                    RateLimitWindowSeconds = rateLimitWindowSeconds,
                    RateLimitCallback = rateLimitCallback
                };
                lock (_routesLock)
                {
                    _routes.Add(route);
                    if (!_routesByMethod.ContainsKey(method))
                        _routesByMethod[method] = new();
                    _routesByMethod[method].Add(route);
                }
                Logger.Info($"添加异步路由: {method} {path} (rate={rateLimitMaxRequests}/{rateLimitWindowSeconds}s)");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加异步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, DrxHttpServer, Task<IActionResult>> handler, int rateLimitMaxRequests = 0, int rateLimitWindowSeconds = 0, Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? rateLimitCallback = null)
        {
            if (handler == null) return;
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = async (request) =>
                    {
                        var action = await handler(request, this).ConfigureAwait(false);
                        if (action == null)
                            return new HttpResponse(204, string.Empty);
                        return await action.ExecuteAsync(request, this).ConfigureAwait(false);
                    },
                    ExtractParameters = CreateParameterExtractor(path),
                    RateLimitMaxRequests = rateLimitMaxRequests,
                    RateLimitWindowSeconds = rateLimitWindowSeconds,
                    RateLimitCallback = rateLimitCallback
                };
                lock (_routesLock)
                {
                    _routes.Add(route);
                    if (!_routesByMethod.ContainsKey(method))
                        _routesByMethod[method] = new();
                    _routesByMethod[method].Add(route);
                }
                Logger.Info($"添加异步路由: {method} {path} (rate={rateLimitMaxRequests}/{rateLimitWindowSeconds}s)");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加异步路由 {method} {path} 时发生错误: {ex}");
            }
        }

        #endregion

        #region 文件路由

        /// <summary>
        /// 添加文件路由，将 URL 前缀映射到本地目录
        /// </summary>
        public void AddFileRoute(string urlPrefix, string rootDirectory)
        {
            if (string.IsNullOrEmpty(urlPrefix) || string.IsNullOrEmpty(rootDirectory)) return;
            if (!urlPrefix.StartsWith("/")) urlPrefix = "/" + urlPrefix;
            if (!urlPrefix.EndsWith("/")) urlPrefix += "/";
            _file_routes_add(urlPrefix, rootDirectory);
            Logger.Info($"添加文件路由: {urlPrefix} -> {rootDirectory}");
        }

        private void _file_routes_add(string urlPrefix, string rootDirectory) => _fileRoutes.Add((urlPrefix, rootDirectory));

        #endregion

        #region 路由匹配

        private bool MatchRoute(string path, string template, out Dictionary<string, string>? parameters)
        {
            parameters = null;
            var templateParts = template.Split('/');
            var pathParts = path.Split('/');

            if (templateParts.Length != pathParts.Length)
                return false;

            parameters = new Dictionary<string, string>();
            for (int i = 0; i < templateParts.Length; i++)
            {
                if (string.IsNullOrEmpty(templateParts[i]))
                    continue;

                if (templateParts[i].StartsWith("{") && templateParts[i].EndsWith("}"))
                {
                    var paramName = templateParts[i][1..^1];
                    parameters[paramName] = pathParts[i];
                }
                else if (!string.Equals(templateParts[i], pathParts[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static Func<string, Dictionary<string, string>> CreateParameterExtractor(string template)
        {
            if (!template.Contains('{'))
            {
                return path => path == template ? new Dictionary<string, string>() : null;
            }

            var paramNames = new List<string>();
            var sb = new System.Text.StringBuilder();
            int lastIndex = 0;
            var matches = Regex.Matches(template, "\\{([^}]+)\\}");
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (m.Index > lastIndex)
                {
                    sb.Append(Regex.Escape(template.Substring(lastIndex, m.Index - lastIndex)));
                }

                sb.Append("([^/]+)");
                paramNames.Add(m.Groups[1].Value);
                lastIndex = m.Index + m.Length;
            }

            if (lastIndex < template.Length)
            {
                sb.Append(Regex.Escape(template.Substring(lastIndex)));
            }

            var regexPattern = "^" + sb.ToString() + "$";
            var regex = new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            return path =>
            {
                var m = regex.Match(path);
                if (!m.Success) return null;
                var dict = new Dictionary<string, string>();
                for (int i = 0; i < paramNames.Count; i++)
                {
                    dict[paramNames[i]] = m.Groups[i + 1].Value;
                }
                return dict;
            };
        }

        #endregion
    }
}
