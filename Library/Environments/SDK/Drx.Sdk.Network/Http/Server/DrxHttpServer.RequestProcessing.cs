using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Diagnostics;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Serialization;
using Drx.Sdk.Network.Http.Performance;
using Drx.Sdk.Network.Http.Entry;
using Drx.Sdk.Network.Http.Commands;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpServer 请求处理部分：服务器启停、请求解析与响应发送
    /// </summary>
    public partial class DrxHttpServer
    {
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
                var processingTasks = new Task[Environment.ProcessorCount];
                for (int i = 0; i < processingTasks.Length; i++)
                {
                    processingTasks[i] = Task.Run(() => ProcessRequestsAsync(_cts.Token), _cts.Token);
                }

                var commandProcessingTask = Task.Run(() => ProcessCommandsAsync(_cts.Token), _cts.Token);

                try
                {
                    _interactiveConsole = new InteractiveCommandConsole(this);
                    _interactiveConsoleTask = Task.Run(() => _interactiveConsole.StartAsync(), _cts.Token);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"启动交互式控制台失败: {ex.Message}");
                }

                await Task.WhenAll(
                    ListenAsync(_cts.Token),
                    Task.WhenAll(processingTasks),
                    commandProcessingTask
                );
            }
            catch (Exception ex)
            {
                Logger.Error($"启动 HttpServer 时发生错误: {ex}");
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
                try
                {
                    _commandInputChannel?.Writer.TryComplete();
                }
                catch { }

                try
                {
                    _interactiveConsole?.Stop();
                    if (_interactiveConsoleTask != null)
                    {
                        try { _interactiveConsoleTask.Wait(500); } catch { }
                    }
                }
                catch { }

                _cts?.Cancel();
                _tickerWake?.Set();
                _listener.Stop();
                _semaphore.Dispose();
                try { _messageQueue?.Complete(); } catch { }
                try { _threadPool?.Dispose(); } catch { }
                try { _tokenBucketManager?.Dispose(); } catch { }
                try { _routeMatchCache?.Clear(); } catch { }
                Logger.Info("HttpServer 已停止");
            }
            catch (Exception ex)
            {
                Logger.Error($"停止 HttpServer 时发生错误: {ex}");
            }
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    await _requestChannel.Writer.WriteAsync(context);
                }
                catch (HttpListenerException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"接受请求时发生错误: {ex}");
                }
            }
            _requestChannel.Writer.Complete();
        }

        private ValueTask _message_queue_write(HttpListenerContext context) => _messageQueue.WriteAsync(context);

        private async Task ProcessRequestsAsync(CancellationToken token)
        {
            await foreach (var context in _requestChannel.Reader.ReadAllAsync(token))
            {
                await _semaphore.WaitAsync(token);
                _threadPool?.QueueWork(async () =>
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        await HandleRequestAsync(context).ConfigureAwait(false);
                    }
                    finally
                    {
                        try
                        {
                            var delayMs = _perMessageProcessingDelayMs;
                            if (delayMs > 0)
                            {
                                var elapsed = (int)sw.ElapsedMilliseconds;
                                if (elapsed < delayMs)
                                {
                                    var toWait = delayMs - elapsed;
                                    if (toWait > 0)
                                    {
                                        await Task.Delay(toWait).ConfigureAwait(false);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"应用每消息最小处理延迟时发生错误: {ex.Message}");
                        }

                        _semaphore.Release();
                    }
                });
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                var request = await ParseRequestAsync(context.Request);
                request.ListenerContext = context;

                Func<HttpRequest, Task<HttpResponse?>> finalHandler = async (req) =>
                {
                    if (TryServeFileStream(context))
                    {
                        return null;
                    }

                    var clientIP = req.ClientAddress.Ip ?? context.Request.RemoteEndPoint?.Address.ToString();

                    var rawPath = context.Request.Url?.AbsolutePath ?? "/";
                    foreach (var (Template, Handler, RateLimitMaxRequests, RateLimitWindowSeconds, RateLimitCallback) in _raw_routes_reader())
                    {
                        if (rawPath.StartsWith(Template, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                if (!string.IsNullOrEmpty(clientIP) && RateLimitMaxRequests > 0 && RateLimitWindowSeconds > 0)
                                {
                                    var baseKey = $"RAW:{Template}";
                                    var routeKey = GetOrCacheRateLimitKey(baseKey);
                                    var result = await CheckRateLimitForRouteAsync(clientIP, routeKey, RateLimitMaxRequests, RateLimitWindowSeconds, req, RateLimitCallback).ConfigureAwait(false);
                                    var isExceeded = result.Item1;
                                    var customResponse = result.Item2;

                                    if (isExceeded)
                                    {
                                        return customResponse ?? new HttpResponse(429, "Too Many Requests");
                                    }
                                }

                                if (!string.IsNullOrEmpty(clientIP) && RateLimitMaxRequests <= 0 && IsRateLimitExceeded(clientIP, req))
                                {
                                    return new HttpResponse(429, "Too Many Requests");
                                }

                                await Handler(context).ConfigureAwait(false);
                                return null;
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Raw handler 错误: {ex}");
                                return new HttpResponse(500, "Internal Server Error");
                            }
                        }
                    }

                    var method = ParseHttpMethod(req.Method);
                    if (method != null)
                    {
                        RouteEntry matchedRoute = null;
                        Dictionary<string, string> pathParameters = null;

                        if (_routeMatchCache!.TryGet(method.ToString(), req.Path, out var cacheResult) && cacheResult != null)
                        {
                            if (!cacheResult.IsNotFound)
                            {
                                lock (_routesLock)
                                {
                                    if (_routesByMethod.TryGetValue(method, out var routesForMethod) && cacheResult.RouteIndex >= 0 && cacheResult.RouteIndex < routesForMethod.Count)
                                    {
                                        matchedRoute = routesForMethod[cacheResult.RouteIndex];
                                        pathParameters = cacheResult.Parameters;
                                    }
                                }
                            }
                        }
                        else
                        {
                            List<RouteEntry> routesForMethod = null;
                            lock (_routesLock)
                            {
                                if (_routesByMethod.TryGetValue(method, out var routes))
                                {
                                    routesForMethod = routes;
                                }
                            }

                            if (routesForMethod != null)
                            {
                                for (int i = 0; i < routesForMethod.Count; i++)
                                {
                                    var route = routesForMethod[i];
                                    var parameters = route.ExtractParameters(req.Path);
                                    if (parameters != null)
                                    {
                                        matchedRoute = route;
                                        pathParameters = parameters;
                                        _routeMatchCache!.Set(method.ToString(), req.Path, i, parameters);
                                        break;
                                    }
                                }

                                if (matchedRoute == null)
                                {
                                    _routeMatchCache!.Set(method.ToString(), req.Path, -1, null);
                                }
                            }
                        }

                        if (matchedRoute != null)
                        {
                            req.PathParameters = pathParameters ?? new Dictionary<string, string>();

                            if (!string.IsNullOrEmpty(clientIP) && matchedRoute.RateLimitMaxRequests > 0 && matchedRoute.RateLimitWindowSeconds > 0)
                            {
                                var baseKey = $"ROUTE:{matchedRoute.Method}:{matchedRoute.Template}";
                                var routeKey = GetOrCacheRateLimitKey(baseKey);
                                var result = await CheckRateLimitForRouteAsync(clientIP, routeKey, matchedRoute.RateLimitMaxRequests, matchedRoute.RateLimitWindowSeconds, req, matchedRoute.RateLimitCallback).ConfigureAwait(false);
                                var isExceeded = result.Item1;
                                var customResponse = result.Item2;

                                if (isExceeded)
                                {
                                    return customResponse ?? new HttpResponse(429, "Too Many Requests");
                                }
                            }

                            if (!string.IsNullOrEmpty(clientIP) && (matchedRoute.RateLimitMaxRequests == 0) && IsRateLimitExceeded(clientIP, req))
                            {
                                return new HttpResponse(429, "Too Many Requests");
                            }

                            try
                            {
                                var resp = await matchedRoute.Handler(req).ConfigureAwait(false);
                                return resp;
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"执行路由处理器时发生错误: {ex}");
                                return new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                            }
                        }
                    }

                    // 静态资源服务（HTML/CSS/JS/图片等），支持 ETag 缓存与 304 响应
                    if (!string.IsNullOrEmpty(FileRootPath) || !string.IsNullOrEmpty(ViewRoot) || _staticFileRoot != null)
                    {
                        var staticResponse = TryServeStaticContent(req);
                        if (staticResponse != null)
                        {
                            return staticResponse;
                        }
                    }

                    if (!string.IsNullOrEmpty(NotFoundPagePath) && File.Exists(NotFoundPagePath))
                    {
                        try
                        {
                            var notFoundHtml = await File.ReadAllTextAsync(NotFoundPagePath).ConfigureAwait(false);
                            var notFoundResp = new HttpResponse(404, notFoundHtml);
                            notFoundResp.Headers["Content-Type"] = "text/html; charset=utf-8";
                            return notFoundResp;
                        }
                        catch { }
                    }
                    return new HttpResponse(404, "Not Found");
                };

                var response = await ExecuteMiddlewarePipelineAsync(context, request, finalHandler).ConfigureAwait(false);

                if (response == null)
                {
                    return;
                }

                SendResponse(context.Response, response);
            }
            catch (Exception ex)
            {
                Logger.Error($"处理请求时发生错误: {ex}");
                var errorResponse = new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                SendResponse(context.Response, errorResponse);
            }
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
                    Content = body,
                    RemoteEndPoint = request.RemoteEndPoint,
                    ClientAddress = HttpRequest.Address.FromEndPoint(request.RemoteEndPoint, request.Headers)
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"解析请求时发生错误: {ex}");
                throw;
            }
        }

        private async Task<HttpRequest> ParseRequestAsync(HttpListenerRequest request)
        {
            try
            {
                byte[] bodyBytes = null;
                string body = "";
                var httpRequest = new HttpRequest
                {
                    Method = request.HttpMethod,
                    Path = request.Url!.AbsolutePath,
                    Query = request.QueryString,
                    Headers = request.Headers,
                    RemoteEndPoint = request.RemoteEndPoint,
                    ClientAddress = HttpRequest.Address.FromEndPoint(request.RemoteEndPoint, request.Headers)
                };

                var contentType = request.Headers["Content-Type"] ?? request.Headers["content-type"];
                if (!string.IsNullOrEmpty(contentType) && (contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0 || contentType.IndexOf("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    try
                    {
                        if (request.HasEntityBody && request.InputStream != null)
                        {
                            await httpRequest.ParseFormAsync(contentType, request.InputStream, Encoding.UTF8).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"解析请求表单时发生错误: {ex}");
                        throw;
                    }

                    return httpRequest;
                }

                if (request.HasEntityBody)
                {
                    using var memoryStream = HttpObjectPool.CreatePooledMemoryStream();
                    var buffer = HttpObjectPool.BytePool.Rent(HttpObjectPool.DefaultBufferSize);
                    try
                    {
                        int bytesRead;
                        while ((bytesRead = request.InputStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            memoryStream.Write(buffer, 0, bytesRead);
                        }
                        bodyBytes = memoryStream.ToArray();
                        body = Encoding.UTF8.GetString(bodyBytes);
                    }
                    finally
                    {
                        HttpObjectPool.BytePool.Return(buffer);
                    }
                }

                httpRequest.Body = body;
                httpRequest.BodyBytes = bodyBytes;
                httpRequest.Content = new System.Dynamic.ExpandoObject();
                try { ((System.Collections.Generic.IDictionary<string, object>)httpRequest.Content)["Text"] = body; } catch { }
                return httpRequest;
            }
            catch (Exception ex)
            {
                Logger.Error($"解析请求时发生错误: {ex}");
                throw;
            }
        }

        private void SendResponse(HttpListenerResponse response, HttpResponse httpResponse)
        {
            try
            {
                response.StatusCode = httpResponse.StatusCode;
                response.StatusDescription = httpResponse.StatusDescription ?? GetDefaultStatusDescription(httpResponse.StatusCode);

                for (int i = 0; i < httpResponse.Headers.Count; i++)
                {
                    var key = httpResponse.Headers.GetKey(i);
                    var val = httpResponse.Headers.Get(i);
                    if (string.IsNullOrEmpty(key) || val == null)
                        continue;

                    if (string.Equals(key, "Content-Length", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        response.AddHeader(key, val);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"跳过无法添加的响应头 {key}: {ex}");
                    }
                }

                if (httpResponse.FileStream != null)
                {
                    var fs = httpResponse.FileStream;
                    try
                    {
                        if (fs.CanSeek)
                        {
                            long remaining = fs.Length - fs.Position;
                            try { response.ContentLength64 = remaining; }
                            catch (InvalidOperationException ioe)
                            {
                                Logger.Warn($"无法设置 ContentLength64（响应头可能已发送）: {ioe.Message}");
                                try { response.SendChunked = true; } catch { }
                            }
                        }
                        else
                        {
                            try { response.SendChunked = true; } catch { }
                        }
                    }
                    catch { try { response.SendChunked = true; } catch { } }

                    _ = Task.Run(async () =>
                    {
                        const int BufferSize = 256 * 1024;
                        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(BufferSize);
                        try
                        {

                            long localRemaining = -1;
                            try { localRemaining = response.ContentLength64; } catch { localRemaining = -1; }

                            long bytesPerSecond = 0;
                            try { bytesPerSecond = (long)httpResponse.BandwidthLimitKb * 1024L; } catch { bytesPerSecond = 0; }

                            var sw = Stopwatch.StartNew();
                            long totalBytesSent = 0;

                            while (true)
                            {
                                int toRead = (int)Math.Min(buffer.Length, (localRemaining >= 0) ? Math.Min(localRemaining, buffer.Length) : buffer.Length);
                                if (toRead <= 0) break;

                                int read = 0;
                                try { read = await fs.ReadAsync(buffer.AsMemory(0, toRead)).ConfigureAwait(false); } catch (Exception ex) { Logger.Warn($"读取文件流时发生错误: {ex}"); break; }
                                if (read <= 0) break;

                                try
                                {
                                    int writeCount = read;
                                    if (localRemaining >= 0)
                                    {
                                        writeCount = (int)Math.Min(read, localRemaining);
                                    }

                                    await response.OutputStream.WriteAsync(buffer.AsMemory(0, writeCount)).ConfigureAwait(false);
                                    totalBytesSent += writeCount;
                                    if (localRemaining >= 0) localRemaining -= writeCount;

                                    if (bytesPerSecond > 0)
                                    {
                                        double expectedMs = (double)totalBytesSent * 1000.0 / bytesPerSecond;
                                        var actualMs = sw.Elapsed.TotalMilliseconds;
                                        if (expectedMs > actualMs)
                                        {
                                            var waitMs = (int)Math.Ceiling(expectedMs - actualMs);
                                            if (waitMs > 0) await Task.Delay(waitMs).ConfigureAwait(false);
                                        }
                                    }

                                    if (writeCount < read)
                                    {
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    if (IsClientDisconnect(ex))
                                    {
                                        Logger.Warn($"客户端已断开连接，停止写入响应: {ex.Message}");
                                    }
                                    else
                                    {
                                        Logger.Warn($"写入响应输出流时发生错误（文件流）: {ex}");
                                    }
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (IsClientDisconnect(ex))
                            {
                                Logger.Warn($"客户端在传输中断开连接: {ex.Message}");
                            }
                            else
                            {
                                Logger.Error($"在传输文件到响应时发生错误: {ex}");
                            }
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                            try { httpResponse.FileStream?.Dispose(); } catch { }
                            try { response.OutputStream.Close(); } catch { }
                        }
                    });

                    return;
                }

                byte[] responseBytes = null;
                if (httpResponse.BodyBytes != null)
                {
                    responseBytes = httpResponse.BodyBytes;
                }
                else if (httpResponse.BodyObject != null)
                {
                    if (DrxJsonSerializerManager.TrySerialize(httpResponse.BodyObject, out var json) && !string.IsNullOrEmpty(json))
                    {
                        responseBytes = Encoding.UTF8.GetBytes(json);
                        if (string.IsNullOrEmpty(response.ContentType))
                            response.ContentType = "application/json";
                    }
                    else
                    {
                        Logger.Warn($"无法序列化对象（类型: {httpResponse.BodyObject.GetType().FullName}），使用错误响应");
                        var errorJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            error = "服务器内部错误：无法序列化响应对象",
                            type = httpResponse.BodyObject.GetType().FullName
                        });
                        responseBytes = Encoding.UTF8.GetBytes(errorJson);
                        response.StatusCode = 500;
                        if (string.IsNullOrEmpty(response.ContentType))
                            response.ContentType = "application/json";
                    }
                }
                else if (!string.IsNullOrEmpty(httpResponse.Body))
                {
                    responseBytes = Encoding.UTF8.GetBytes(httpResponse.Body);
                }

                if (responseBytes != null)
                {
                    try
                    {
                        response.ContentLength64 = responseBytes.Length;
                    }
                    catch (InvalidOperationException ioe)
                    {
                        Logger.Warn($"无法设置 ContentLength64（响应头可能已发送）: {ioe.Message}");
                    }
                    try
                    {
                        response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                    }
                    catch (Exception)
                    {
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
                    Logger.Warn($"关闭响应流时发生错误: {ex}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"发送响应时发生错误: {ex}");
            }
        }

        /// <summary>
        /// 允许处理器直接通过 server.Response(ctx, resp) 发送响应
        /// </summary>
        public void Response(HttpListenerContext ctx, HttpResponse resp)
        {
            try
            {
                SendResponse(ctx.Response, resp ?? new HttpResponse(204, ""));
            }
            catch (Exception ex)
            {
                Logger.Error($"通过 server.Response 发送响应时发生错误: {ex}");
            }
        }

        /// <summary>
        /// 允许处理器直接通过 server.Response(ctx, actionResult) 异步发送响应
        /// </summary>
        public async Task ResponseAsync(HttpListenerContext ctx, IActionResult action)
        {
            try
            {
                var req = await ParseRequestAsync(ctx.Request).ConfigureAwait(false);
                var resp = await action.ExecuteAsync(req, this).ConfigureAwait(false);
                SendResponse(ctx.Response, resp ?? new HttpResponse(500, "Internal Server Error"));
            }
            catch (Exception ex)
            {
                Logger.Error($"通过 server.ResponseAsync 发送响应时发生错误: {ex}");
                try { ctx.Response.StatusCode = 500; using var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8, leaveOpen: false); sw.Write("Internal Server Error"); } catch { }
            }
        }
    }
}
