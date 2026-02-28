using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Sse;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpServer SSE 部分：客户端追踪、广播、生命周期管理
    /// </summary>
    public partial class DrxHttpServer
    {
        #region SSE 客户端追踪

        private readonly ConcurrentDictionary<string, SseClientInfo> _sseClients = new();

        /// <summary>
        /// SSE 客户端连接时触发
        /// </summary>
        public event Func<SseClientInfo, Task>? OnSseClientConnected;

        /// <summary>
        /// SSE 客户端断开时触发
        /// </summary>
        public event Func<SseClientInfo, Task>? OnSseClientDisconnected;

        /// <summary>
        /// 获取指定路径（或全部）的活跃 SSE 客户端列表
        /// </summary>
        public IReadOnlyList<SseClientInfo> GetSseClients(string? path = null)
        {
            var all = _sseClients.Values;
            if (string.IsNullOrEmpty(path))
                return all.ToList().AsReadOnly();
            return all.Where(c => c.Path == path).ToList().AsReadOnly();
        }

        /// <summary>
        /// 获取指定路径（或全部）的活跃 SSE 客户端数量
        /// </summary>
        public int GetSseClientCount(string? path = null)
        {
            if (string.IsNullOrEmpty(path))
                return _sseClients.Count;
            return _sseClients.Values.Count(c => c.Path == path);
        }

        #endregion

        #region SSE 广播

        /// <summary>
        /// 向指定路径的所有 SSE 客户端广播一条事件
        /// </summary>
        public async Task BroadcastSseAsync(string path, string? eventName, string data)
        {
            var clients = _sseClients.Values.Where(c => c.Path == path && c.Writer.IsConnected).ToList();
            var tasks = new List<Task>(clients.Count);

            foreach (var client in clients)
            {
                tasks.Add(SafeSendAsync(client, eventName, data));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// 向指定路径的所有 SSE 客户端广播一条 JSON 事件
        /// </summary>
        public async Task BroadcastSseJsonAsync<T>(string path, string? eventName, T data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await BroadcastSseAsync(path, eventName, json).ConfigureAwait(false);
        }

        /// <summary>
        /// 向所有 SSE 客户端广播一条事件（不限路径）
        /// </summary>
        public async Task BroadcastSseToAllAsync(string? eventName, string data)
        {
            var clients = _sseClients.Values.Where(c => c.Writer.IsConnected).ToList();
            var tasks = new List<Task>(clients.Count);

            foreach (var client in clients)
            {
                tasks.Add(SafeSendAsync(client, eventName, data));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// 向所有 SSE 客户端广播一条 JSON 事件（不限路径）
        /// </summary>
        public async Task BroadcastSseJsonToAllAsync<T>(string? eventName, T data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await BroadcastSseToAllAsync(eventName, json).ConfigureAwait(false);
        }

        /// <summary>
        /// 向指定客户端发送事件
        /// </summary>
        public async Task SendSseToClientAsync(string clientId, string? eventName, string data)
        {
            if (_sseClients.TryGetValue(clientId, out var client) && client.Writer.IsConnected)
            {
                await SafeSendAsync(client, eventName, data).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 断开指定 SSE 客户端
        /// </summary>
        public void DisconnectSseClient(string clientId)
        {
            if (_sseClients.TryGetValue(clientId, out var client))
            {
                try { client.Cts.Cancel(); } catch { }
            }
        }

        /// <summary>
        /// 断开指定路径的所有 SSE 客户端
        /// </summary>
        public void DisconnectAllSseClients(string? path = null)
        {
            var targets = string.IsNullOrEmpty(path)
                ? _sseClients.Values.ToList()
                : _sseClients.Values.Where(c => c.Path == path).ToList();

            foreach (var client in targets)
            {
                try { client.Cts.Cancel(); } catch { }
            }
        }

        private static async Task SafeSendAsync(SseClientInfo client, string? eventName, string data)
        {
            try
            {
                await client.Writer.SendAsync(eventName, data).ConfigureAwait(false);
            }
            catch
            {
                try { client.Cts.Cancel(); } catch { }
            }
        }

        #endregion

        #region SSE 处理器注册（内部）

        /// <summary>
        /// 注册 SSE 处理器为原始路由，供 HandlerRegistration 调用
        /// </summary>
        internal void RegisterSseRoute(
            string path,
            Func<ISseWriter, Protocol.HttpRequest, CancellationToken, DrxHttpServer, Task> handler,
            int heartbeatSeconds,
            int rateLimitMaxRequests,
            int rateLimitWindowSeconds)
        {
            Func<System.Net.HttpListenerContext, Task> rawHandler = async ctx =>
            {
                var sseWriter = new SseWriter(ctx);
                var cts = new CancellationTokenSource();
                var clientInfo = new SseClientInfo
                {
                    ClientId = sseWriter.ClientId,
                    Path = path,
                    Writer = sseWriter,
                    RemoteAddress = ctx.Request.RemoteEndPoint?.ToString(),
                    ConnectedAt = DateTime.Now,
                    Cts = cts
                };

                var request = new Protocol.HttpRequest
                {
                    Method = ctx.Request.HttpMethod,
                    Path = ctx.Request.Url?.AbsolutePath ?? "/",
                    Url = ctx.Request.Url?.ToString(),
                    Query = ctx.Request.QueryString,
                    Headers = ctx.Request.Headers,
                    RemoteEndPoint = ctx.Request.RemoteEndPoint!,
                    ListenerContext = ctx
                };
                request.ClientAddress = Protocol.HttpRequest.Address.FromEndPoint(
                    ctx.Request.RemoteEndPoint, ctx.Request.Headers);

                Task? heartbeatTask = null;
                Task? handlerTask = null;

                try
                {
                    _sseClients.TryAdd(clientInfo.ClientId, clientInfo);

                    if (OnSseClientConnected != null)
                    {
                        try { await OnSseClientConnected(clientInfo).ConfigureAwait(false); }
                        catch (Exception ex) { Logger.Warn($"[SSE] OnSseClientConnected 回调异常: {ex.Message}"); }
                    }

                    if (heartbeatSeconds > 0)
                    {
                        heartbeatTask = RunHeartbeatAsync(sseWriter, heartbeatSeconds, cts.Token);
                    }

                    handlerTask = handler(sseWriter, request, cts.Token, this);
                    await handlerTask.ConfigureAwait(false);

                    if (sseWriter.WasRejected)
                        return;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Logger.Debug($"[SSE] 客户端 {clientInfo.ClientId} 处理异常: {ex.Message}");
                }
                finally
                {
                    sseWriter.MarkDisconnected();
                    _sseClients.TryRemove(clientInfo.ClientId, out _);

                    try { cts.Cancel(); } catch { }

                    if (heartbeatTask != null)
                    {
                        try { await heartbeatTask.ConfigureAwait(false); } catch { }
                    }
                    if (handlerTask != null && !handlerTask.IsCompleted)
                    {
                        try { await handlerTask.ConfigureAwait(false); } catch { }
                    }

                    if (OnSseClientDisconnected != null)
                    {
                        try { await OnSseClientDisconnected(clientInfo).ConfigureAwait(false); }
                        catch (Exception ex) { Logger.Warn($"[SSE] OnSseClientDisconnected 回调异常: {ex.Message}"); }
                    }

                    try { cts.Dispose(); } catch { }
                    try { ctx.Response.Close(); } catch { }

                    Logger.Debug($"[SSE] 客户端已断开: {clientInfo.ClientId} ({path})");
                }
            };

            _raw_routes_add(path, rawHandler, rateLimitMaxRequests, rateLimitWindowSeconds);
            Logger.Info($"注册 SSE 端点: {path} (心跳={heartbeatSeconds}s)");
        }

        private static async Task RunHeartbeatAsync(SseWriter writer, int intervalSeconds, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && writer.IsConnected)
                {
                    await Task.Delay(intervalSeconds * 1000, ct).ConfigureAwait(false);
                    if (!ct.IsCancellationRequested && writer.IsConnected && !writer.WasRejected)
                    {
                        await writer.PingAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        #endregion
    }
}
