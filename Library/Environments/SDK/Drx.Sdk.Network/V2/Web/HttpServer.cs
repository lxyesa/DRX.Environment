using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Collections.Specialized;
using Drx.Sdk.Shared;
using System.Net.Http;
using System.Threading.Channels;
using System.Threading;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Drx.Sdk.Network.V2.Web
{
    public class HttpServer
    {
        private HttpListener _listener;
        // 文件路由映射：url 前缀 -> 本地根目录
        private readonly List<(string Prefix, string RootDir)> _fileRoutes = new();
        private readonly List<RouteEntry> _routes = new();
        private readonly List<(string Template, Func<HttpListenerContext, Task> Handler)> _rawRoutes = new();
        private readonly string _staticFileRoot;
        private CancellationTokenSource _cts;
        private readonly Channel<HttpListenerContext> _requestChannel;
        private readonly SemaphoreSlim _semaphore;
        private const int MaxConcurrentRequests = 100; // 最大并发请求数

        private class RouteEntry
        {
            public string Template { get; set; }
            public HttpMethod Method { get; set; }
            public Func<HttpRequest, Task<HttpResponse>> Handler { get; set; }
            public Func<string, Dictionary<string, string>> ExtractParameters { get; set; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="prefixes">监听前缀，如 "http://localhost:8080/"</param>
        /// <param name="staticFileRoot">静态文件根目录</param>
        public HttpServer(IEnumerable<string> prefixes, string staticFileRoot = null)
        {
            _listener = new HttpListener();
            foreach (var prefix in prefixes)
            {
                _listener.Prefixes.Add(prefix);
            }
            _staticFileRoot = staticFileRoot;
            _fileRoutes = new List<(string Prefix, string RootDir)>();
            _rawRoutes = new List<(string Template, Func<HttpListenerContext, Task> Handler)>();
            _requestChannel = Channel.CreateBounded<HttpListenerContext>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });
            _semaphore = new SemaphoreSlim(MaxConcurrentRequests);
        }

        /// <summary>
        /// 添加原始路由（raw handler），处理方法直接接收 HttpListenerContext，适合流式上传/下载场景。
        /// </summary>
        /// <param name="path">路径前缀</param>
        /// <param name="handler">处理委托，接收 HttpListenerContext</param>
        public void AddRawRoute(string path, Func<HttpListenerContext, Task> handler)
        {
            if (string.IsNullOrEmpty(path) || handler == null) return;
            //规范化
            if (!path.StartsWith("/")) path = "/" + path;
            _rawRoutes.Add((path, handler));
            Logger.Info($"添加原始路由: {path}");
        }

        /// <summary>
        /// 添加流式上传路由。处理方法接收 HttpRequest 并可通过 HttpRequest.UploadFile.Stream读取上传数据流。
        /// 与 Raw 不同，处理方法无需声明 HttpListenerContext；只需声明 (HttpRequest) -> HttpResponse 或 Task<HttpResponse> 即可。
        /// </summary>
        /// <param name="path">路径前缀</param>
        /// <param name="handler">处理委托</param>
        public void AddStreamUploadRoute(string path, Func<HttpRequest, Task<HttpResponse>> handler)
        {
            if (string.IsNullOrEmpty(path) || handler == null) return;
            if (!path.StartsWith("/")) path = "/" + path;

            // 将其包装为 raw handler，内部构造 HttpRequest 并将 HttpListenerRequest.InputStream作为 UploadFile.Stream
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
                        ListenerContext = ctx
                    };

                    // 不在此处读取整个请求体，直接将流暴露给处理方法
                    req.UploadFile = new HttpRequest.UploadFileDescriptor
                    {
                        Stream = listenerReq.InputStream,
                        FileName = listenerReq.Headers["X-File-Name"] ?? Path.GetFileName(req.Path),
                        FieldName = "file",
                        Progress = null,
                        CancellationToken = CancellationToken.None
                    };

                    HttpResponse resp;
                    try
                    {
                        resp = await handler(req).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"执行 StreamUpload处理方法时发生错误: {ex.Message}");
                        resp = new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                    }

                    SendResponse(ctx.Response, resp);
                }
                catch (Exception ex)
                {
                    Logger.Error($"StreamUpload raw handler 错误: {ex.Message}");
                    try { ctx.Response.StatusCode =500; using var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8, leaveOpen: false); sw.Write("Internal Server Error"); } catch { }
                }
            };

            _rawRoutes.Add((path, rawHandler));
            Logger.Info($"添加流式上传路由: {path}");
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                _cts = new CancellationTokenSource();
                _listener.Start();
                Logger.Info("HttpServer 已启动");

                // 启动请求处理任务
                var processingTasks = new Task[Environment.ProcessorCount];
                for (int i = 0; i < processingTasks.Length; i++)
                {
                    processingTasks[i] = Task.Run(() => ProcessRequestsAsync(_cts.Token), _cts.Token);
                }

                await Task.WhenAll(
                    ListenAsync(_cts.Token),
                    Task.WhenAll(processingTasks)
                );
            }
            catch (Exception ex)
            {
                Logger.Error($"启动 HttpServer 时发生错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener.Stop();
                _semaphore.Dispose();
                Logger.Info("HttpServer 已停止");
            }
            catch (Exception ex)
            {
                Logger.Error($"停止 HttpServer 时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加路由
        /// </summary>
        /// <param name="method">HTTP 方法</param>
        /// <param name="path">路径</param>
        /// <param name="handler">处理委托</param>
        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, HttpResponse> handler)
        {
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = async (request) => await Task.FromResult(handler(request)),
                    ExtractParameters = CreateParameterExtractor(path)
                };
                _routes.Add(route);
                Logger.Info($"添加路由: {method} {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加路由 {method} {path} 时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加异步路由
        /// </summary>
        /// <param name="method">HTTP 方法</param>
        /// <param name="path">路径</param>
        /// <param name="handler">异步处理委托</param>
        public void AddRoute(HttpMethod method, string path, Func<HttpRequest, Task<HttpResponse>> handler)
        {
            try
            {
                var route = new RouteEntry
                {
                    Template = path,
                    Method = method,
                    Handler = handler,
                    ExtractParameters = CreateParameterExtractor(path)
                };
                _routes.Add(route);
                Logger.Info($"添加异步路由: {method} {path}");
            }
            catch (Exception ex)
            {
                Logger.Error($"添加异步路由 {method} {path} 时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 从程序集中注册带有 HttpHandle 特性的方法
        /// </summary>
        /// <param name="assembly">要扫描的程序集</param>
        /// <param name="server">HttpServer 实例</param>
        public static void RegisterHandlersFromAssembly(Assembly assembly, HttpServer server)
        {
            try
            {
                var methods = assembly.GetTypes()
                    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    .Where(m => m.GetCustomAttributes(typeof(HttpHandleAttribute), false).Length > 0)
                    .ToList();

                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes<HttpHandleAttribute>();
                    foreach (var attr in attributes)
                    {
                        // 如果标注为 Raw / Stream 下载, 注册为原始处理器（接收 HttpListenerContext）
                        // 注意：StreamUpload 将使用专门的装饰器，允许处理方法签名为 (HttpRequest) -> HttpResponse 或 Task<HttpResponse>
                        if (attr.Raw || attr.StreamDownload)
                        {
                            // 方法签名需接受 HttpListenerContext
                            var parameters = method.GetParameters();
                            if (parameters.Length ==1 && parameters[0].ParameterType == typeof(HttpListenerContext))
                            {
                                var returnType = method.ReturnType;
                                if (returnType == typeof(void))
                                {
                                    Func<HttpListenerContext, Task> handler = ctx => { method.Invoke(null, new object[] { ctx }); return Task.CompletedTask; };
                                    server.AddRawRoute(attr.Path, handler);
                                }
                                else if (returnType == typeof(Task))
                                {
                                    Func<HttpListenerContext, Task> handler = ctx => (Task)method.Invoke(null, new object[] { ctx });
                                    server.AddRawRoute(attr.Path, handler);
                                }
                                else if (returnType == typeof(HttpResponse))
                                {
                                    Func<HttpListenerContext, Task> handler = async ctx =>
                                    {
                                        var resp = (HttpResponse)method.Invoke(null, new object[] { ctx });
                                        server.SendResponse(ctx.Response, resp);
                                        await Task.CompletedTask;
                                    };
                                    server.AddRawRoute(attr.Path, handler);
                                }
                                else if (returnType == typeof(Task<HttpResponse>))
                                {
                                    Func<HttpListenerContext, Task> handler = async ctx =>
                                    {
                                        var task = (Task<HttpResponse>)method.Invoke(null, new object[] { ctx });
                                        var resp = await task.ConfigureAwait(false);
                                        server.SendResponse(ctx.Response, resp);
                                    };
                                    server.AddRawRoute(attr.Path, handler);
                                }
                                else
                                {
                                    Logger.Warn($"不能注册原始路由: 方法 {method.Name} 返回类型不受支持: {returnType}");
                                }
                            }
                            else
                            {
                                Logger.Warn($"标注为 Raw/Stream 的方法 {method.Name} 必须接受一个 HttpListenerContext 参数");
                            }
                        }
                        else if (attr.StreamUpload)
                        {
                            // StreamUpload: 自动将 HttpListenerContext 的请求流传递给处理方法，方法可声明为 (HttpRequest) -> HttpResponse/Task<HttpResponse>
                            var handler = CreateHandlerDelegate(method);
                            if (handler != null)
                            {
                                // 使用专用注册器将 handler 包装为 raw handler，传入 HttpRequest.Stream 和 ListenerContext
                                server.AddStreamUploadRoute(attr.Path, handler);
                            }
                            else
                            {
                                Logger.Warn($"不能注册 StreamUpload 路由: 方法 {method.Name} 的签名或返回类型不受支持");
                            }
                        }
                        else
                        {
                            var httpMethod = ParseHttpMethod(attr.Method);
                            if (httpMethod != null)
                            {
                                var handler = CreateHandlerDelegate(method);
                                if (handler != null)
                                {
                                    server.AddRoute(httpMethod, attr.Path, handler);
                                }
                            }
                            else
                            {
                                Logger.Warn($"无效的 HTTP 方法: {attr.Method}");
                            }
                        }
                    }
                }

                Logger.Info($"从程序集 {assembly.FullName} 注册了 {methods.Count} 个 HTTP处理方法");
            }
            catch (Exception ex)
            {
                Logger.Error($"注册 HTTP处理方法时发生错误: {ex.Message}");
            }
        }

        private static Func<HttpRequest, Task<HttpResponse>> CreateHandlerDelegate(MethodInfo method)
        {
            try
            {
                var parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(HttpRequest))
                {
                    Logger.Warn($"方法 {method.Name} 的签名不正确，应为 (HttpRequest) -> HttpResponse");
                    return null;
                }

                var returnType = method.ReturnType;
                if (returnType != typeof(HttpResponse) && returnType != typeof(Task<HttpResponse>))
                {
                    Logger.Warn($"方法 {method.Name} 的返回类型不正确，应为 HttpResponse 或 Task<HttpResponse>");
                    return null;
                }

                return async (HttpRequest request) =>
                {
                    try
                    {
                        if (returnType == typeof(HttpResponse))
                        {
                            return (HttpResponse)method.Invoke(null, new object[] { request });
                        }
                        else
                        {
                            return await (Task<HttpResponse>)method.Invoke(null, new object[] { request });
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"执行 HTTP处理方法 {method.Name} 时发生错误: {ex.Message}");
                        return new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                    }
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"创建处理委托时发生错误: {ex.Message}");
                return null;
            }
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    await _requestChannel.Writer.WriteAsync(context, token);
                }
                catch (HttpListenerException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"接受请求时发生错误: {ex.Message}");
                }
            }
            _requestChannel.Writer.Complete();
        }

        private async Task ProcessRequestsAsync(CancellationToken token)
        {
            await foreach (var context in _requestChannel.Reader.ReadAllAsync(token))
            {
                await _semaphore.WaitAsync(token);
                _ = Task.Run(() => HandleRequestAsync(context), token).ContinueWith(t => _semaphore.Release());
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                // 优先尝试以流方式服务文件下载（支持大文件与 Range）
                if (TryServeFileStream(context))
                {
                    return; // 已直接响应（流异步在后台执行，不阻塞本线程）
                }

                // 尝试原始路由（raw handlers），这些处理器可以直接操作 HttpListenerContext 用于流式上传/下载
                var rawPath = context.Request.Url?.AbsolutePath ?? "/";
                foreach (var (Template, Handler) in _rawRoutes)
                {
                    if (rawPath.StartsWith(Template, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            await Handler(context).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Raw handler 错误: {ex.Message}");
                            try { context.Response.StatusCode = 500; using var sw = new StreamWriter(context.Response.OutputStream, Encoding.UTF8, leaveOpen: false); sw.Write("Internal Server Error"); } catch { }
                        }
                        return;
                    }
                }

                var request = ParseRequest(context.Request);
                HttpResponse response;

                var method = ParseHttpMethod(request.Method);
                if (method != null)
                {
                    foreach (var route in _routes)
                    {
                        if (route.Method == method)
                        {
                            var parameters = route.ExtractParameters(request.Path);
                            if (parameters != null)
                            {
                                request.PathParameters = parameters;
                                response = await route.Handler(request);
                                goto respond;
                            }
                        }
                    }
                }

                // 如果没有匹配的路由，尝试静态文件
                if (_staticFileRoot != null && TryServeStaticFile(request.Path, out var fileResponse))
                {
                    response = fileResponse;
                }
                else
                {
                    response = new HttpResponse(404, "Not Found");
                }

            respond:
                SendResponse(context.Response, response);
            }
            catch (Exception ex)
            {
                Logger.Error($"处理请求时发生错误: {ex.Message}");
                var errorResponse = new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                SendResponse(context.Response, errorResponse);
            }
        }

        /// <summary>
        /// 添加文件路由，将 URL 前缀映射到本地目录。例如 AddFileRoute("/download/", "C:\\wwwroot")
        /// 当请求以该前缀开头时，后续路径会被映射到本地目录并尝试以流方式返回文件。
        /// </summary>
        public void AddFileRoute(string urlPrefix, string rootDirectory)
        {
            if (string.IsNullOrEmpty(urlPrefix) || string.IsNullOrEmpty(rootDirectory)) return;
            //规范化前缀，确保以 '/' 开头并以 '/'结尾
            if (!urlPrefix.StartsWith("/")) urlPrefix = "/" + urlPrefix;
            if (!urlPrefix.EndsWith("/")) urlPrefix += "/";
            _fileRoutes.Add((urlPrefix, rootDirectory));
            Logger.Info($"添加文件路由: {urlPrefix} -> {rootDirectory}");
        }

        /// <summary>
        /// 尝试以流方式服务文件（支持 Range），如果处理则直接写入 context.Response 并返回 true。
        /// 注意：实际的流写入将在后台异步执行，不会阻塞请求处理线程。
        /// </summary>
        private bool TryServeFileStream(HttpListenerContext context)
        {
            try
            {
                var req = context.Request;
                var path = req.Url?.AbsolutePath ?? "/";

                foreach (var (Prefix, RootDir) in _fileRoutes)
                {
                    if (!path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) continue;

                    var rel = path.Substring(Prefix.Length);
                    // 防止路径穿越
                    rel = rel.Replace('/', Path.DirectorySeparatorChar);
                    if (rel.Contains(".."))
                    {
                        context.Response.StatusCode = 400;
                        context.Response.StatusDescription = "Bad Request";
                        context.Response.OutputStream.Close();
                        return true;
                    }

                    var filePath = Path.Combine(RootDir, rel);
                    if (!File.Exists(filePath))
                    {
                        context.Response.StatusCode = 404;
                        context.Response.StatusDescription = "Not Found";
                        context.Response.OutputStream.Close();
                        return true;
                    }

                    var fileInfo = new FileInfo(filePath);
                    long totalLength = fileInfo.Length;
                    var rangeHeader = req.Headers["Range"];
                    long start = 0, end = totalLength - 1;
                    bool isPartial = false;

                    if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                    {
                        // 支持单个范围，例如: bytes=123-
                        var rng = rangeHeader.Substring("bytes=".Length);
                        var parts = rng.Split('-');
                        if (long.TryParse(parts[0], out var s)) start = s;
                        if (parts.Length > 1 && long.TryParse(parts[1], out var e)) end = e;
                        if (start < 0) start = 0;
                        if (end >= totalLength) end = totalLength - 1;
                        if (start <= end) isPartial = true;
                    }

                    var resp = context.Response;
                    resp.AddHeader("Accept-Ranges", "bytes");
                    resp.ContentType = GetMimeType(Path.GetExtension(filePath));
                    resp.SendChunked = false;

                    if (isPartial)
                    {
                        resp.StatusCode = 206;
                        resp.StatusDescription = "Partial Content";
                        resp.AddHeader("Content-Range", $"bytes {start}-{end}/{totalLength}");
                        resp.ContentLength64 = end - start + 1;
                    }
                    else
                    {
                        resp.StatusCode = 200;
                        resp.StatusDescription = "OK";
                        resp.ContentLength64 = totalLength;
                    }

                    // 设置下载时的文件名提示（attachment）
                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        resp.AddHeader("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                    }
                    catch { }

                    // 启动后台任务进行异步流式传输，避免阻塞请求处理线程
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await StreamFileToResponseAsync(context, filePath, start, end, isPartial, totalLength).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"后台流式传输文件时发生错误: {ex.Message}");
                            try { context.Response.StatusCode = 500; context.Response.OutputStream.Close(); } catch { }
                        }
                    });

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"TryServeFileStream发生错误: {ex.Message}");
                try { context.Response.StatusCode = 500; context.Response.OutputStream.Close(); } catch { }
                return true;
            }
        }

        /// <summary>
        /// 将指定文件的指定范围异步写入到响应输出流，完成后关闭输出流。
        /// </summary>
        private async Task StreamFileToResponseAsync(HttpListenerContext context, string filePath, long start, long end, bool isPartial, long totalLength)
        {
            const int BufferSize = 64 * 1024;
            var resp = context.Response;

            // 使用异步文件流
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous))
            {
                fs.Seek(start, SeekOrigin.Begin);
                var remaining = (isPartial ? (end - start + 1) : totalLength);
                var buffer = new byte[BufferSize];
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(buffer.Length, remaining);
                    var read = await fs.ReadAsync(buffer.AsMemory(0, toRead)).ConfigureAwait(false);
                    if (read <= 0) break;
                    try
                    {
                        await resp.OutputStream.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"写入响应输出流时发生错误（文件流）: {ex.Message}");
                        break;
                    }
                    remaining -= read;
                }
            }

            try { resp.OutputStream.Close(); } catch { }
        }

        private static HttpMethod? ParseHttpMethod(string methodString)
        {
            return methodString.ToUpper() switch
            {
                "GET" => HttpMethod.Get,
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                "PATCH" => HttpMethod.Patch,
                "HEAD" => HttpMethod.Head,
                "OPTIONS" => HttpMethod.Options,
                _ => null
            };
        }

        private HttpRequest ParseRequest(HttpListenerRequest request)
        {
            try
            {
                byte[] bodyBytes = null;
                string body = "";
                if (request.HasEntityBody)
                {
                    using var memoryStream = new MemoryStream();
                    request.InputStream.CopyTo(memoryStream);
                    bodyBytes = memoryStream.ToArray();
                    body = Encoding.UTF8.GetString(bodyBytes);
                }

                return new HttpRequest
                {
                    Method = request.HttpMethod,
                    Path = request.Url!.AbsolutePath,
                    Query = request.QueryString,
                    Headers = request.Headers,
                    Body = body,
                    BodyBytes = bodyBytes,
                    RemoteEndPoint = request.RemoteEndPoint
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"解析请求时发生错误: {ex.Message}");
                throw;
            }
        }

        private void SendResponse(HttpListenerResponse response, HttpResponse httpResponse)
        {
            try
            {
                response.StatusCode = httpResponse.StatusCode;
                // Ensure StatusDescription is not null (HttpListenerResponse may throw on null)
                response.StatusDescription = httpResponse.StatusDescription ?? GetDefaultStatusDescription(httpResponse.StatusCode);

                // Safely add headers, skipping null keys/values
                for (int i = 0; i < httpResponse.Headers.Count; i++)
                {
                    var key = httpResponse.Headers.GetKey(i);
                    var val = httpResponse.Headers.Get(i);
                    if (string.IsNullOrEmpty(key) || val == null)
                        continue;

                    try
                    {
                        response.AddHeader(key, val);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"跳过无法添加的响应头 {key}: {ex.Message}");
                    }
                }

                byte[] responseBytes = null;
                if (httpResponse.BodyBytes != null)
                {
                    responseBytes = httpResponse.BodyBytes;
                }
                else if (httpResponse.BodyObject != null)
                {
                    // 假设 BodyObject 是可序列化的对象，序列化为 JSON
                    var json = System.Text.Json.JsonSerializer.Serialize(httpResponse.BodyObject);
                    responseBytes = Encoding.UTF8.GetBytes(json);
                    // 设置 Content-Type，仅当未由调用方指定时设置
                    if (string.IsNullOrEmpty(response.ContentType))
                        response.ContentType = "application/json";
                }
                else if (!string.IsNullOrEmpty(httpResponse.Body))
                {
                    responseBytes = Encoding.UTF8.GetBytes(httpResponse.Body);
                }

                // Ensure ContentLength64 is set to0 if no body
                if (responseBytes != null)
                {
                    response.ContentLength64 = responseBytes.Length;
                    try
                    {
                        response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"写入响应输出流时发生错误: {ex.Message}");
                    }
                }
                else
                {
                    response.ContentLength64 = 0;
                }

                try
                {
                    response.OutputStream.Close();
                }
                catch (Exception ex)
                {
                    Logger.Warn($"关闭响应流时发生错误: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"发送响应时发生错误: {ex.Message}");
            }
        }

        // 提供一个本地的默认状态描述，以防 HttpResponse 没有提供
        private string GetDefaultStatusDescription(int statusCode)
        {
            return statusCode switch
            {
                200 => "OK",
                201 => "Created",
                204 => "No Content",
                400 => "Bad Request",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Not Found",
                500 => "Internal Server Error",
                _ => ""
            };
        }

        private bool TryServeStaticFile(string path, out HttpResponse response)
        {
            response = null;
            if (string.IsNullOrEmpty(_staticFileRoot) || !path.StartsWith("/static/"))
                return false;

            var filePath = Path.Combine(_staticFileRoot, path.Substring("/static/".Length));
            if (!File.Exists(filePath))
                return false;

            try
            {
                var content = File.ReadAllText(filePath);
                var mimeType = GetMimeType(Path.GetExtension(filePath));
                response = new HttpResponse(200, content);
                response.Headers.Add("Content-Type", mimeType);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"服务静态文件 {filePath} 时发生错误: {ex.Message}");
                return false;
            }
        }

        private string GetMimeType(string extension)
        {
            return extension.ToLower() switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                _ => "text/plain"
            };
        }

        private bool MatchRoute(string path, string template, out Dictionary<string, string> parameters)
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
                    // 路由参数
                    var paramName = templateParts[i][1..^1];
                    parameters[paramName] = pathParts[i];
                }
                else if (!string.Equals(templateParts[i], pathParts[i], StringComparison.OrdinalIgnoreCase))
                {
                    // 确定不匹配的部分
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

            // 提取参数名
            var paramNames = new List<string>();
            var regexPattern = "^" + Regex.Escape(template).Replace("\\{[^}]+\\}", "([^/]+)") + "$";
            var match = Regex.Match(template, @"\{([^}]+)\}");
            while (match.Success)
            {
                paramNames.Add(match.Groups[1].Value);
                match = match.NextMatch();
            }

            var regex = new Regex(regexPattern);
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
    }

    /// <summary>
    /// HTTP处理方法特性
    /// 支持通过属性标注该方法为原始（Raw）处理，或用于流式上传/下载场景。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HttpHandleAttribute : Attribute
    {
        /// <summary>
        /// 请求路径
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// HTTP 方法字符串 (e.g. "GET", "POST")
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// 标记该处理器为原始处理（Raw），方法可以直接接收 HttpListenerContext进行流式处理
        /// </summary>
        public bool Raw { get; set; }

        /// <summary>
        /// 标记该处理器用于流式上传（服务器接收大文件流）
        /// 相当于 Raw 的语义扩展，用于可读性和过滤
        /// </summary>
        public bool StreamUpload { get; set; }

        /// <summary>
        /// 标记该处理器用于流式下载（服务器直接写入响应流）
        /// 相当于 Raw 的语义扩展
        /// </summary>
        public bool StreamDownload { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="path">请求路径</param>
        /// <param name="method">HTTP 方法字符串</param>
        public HttpHandleAttribute(string path, string method)
        {
            Path = path;
            Method = method;
        }
    }
}
