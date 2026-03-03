using System.Net;
using System.Net.Http;
using Drx.Sdk.Network.Http.Models;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.ResourceManagement;
using Drx.Sdk.Network.Http.Sse;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpServer 开发态运行时集成（生命周期与事件桥接）。
    /// </summary>
    public partial class DrxHttpServer
    {
        private DevFileChangeService? _devFileChangeService;
        private readonly object _devRuntimeRouteLock = new();
        private bool _devRuntimeRoutesRegistered;

        private void InitializeDevRuntime()
        {
            var config = _options.DevRuntime;
            if (config is null || !config.Enabled)
            {
                return;
            }

            var watchDirectories = ResolveDevWatchDirectories(config.WatchDirectories);
            if (watchDirectories.Count == 0)
            {
                Logger.Warn("[DevRuntime] 已启用但没有可用监听目录，跳过启动");
                return;
            }

            _devFileChangeService = new DevFileChangeService(
                watchDirectories,
                config.DebounceMilliseconds,
                NormalizeDevAssetPath);

            _devFileChangeService.ChangesAggregated += OnDevFileChangesAggregated;
            _devFileChangeService.Start();

            RegisterDevRuntimeCoreRoutes();

            Logger.Info($"[DevRuntime] 已启动，监听目录: {string.Join(", ", watchDirectories)}");
        }

        private void ShutdownDevRuntime()
        {
            if (_devFileChangeService is null)
            {
                return;
            }

            try
            {
                _devFileChangeService.ChangesAggregated -= OnDevFileChangesAggregated;
                _devFileChangeService.Dispose();
                Logger.Info("[DevRuntime] 已停止");
            }
            catch (Exception ex)
            {
                Logger.Warn($"[DevRuntime] 停止时出现异常: {ex.Message}");
            }
            finally
            {
                _devFileChangeService = null;
            }
        }

        private void OnDevFileChangesAggregated(object? sender, DevAssetChangedEvent evt)
        {
            if (!_options.DevRuntime.Enabled)
            {
                return;
            }

            var endpoint = _options.DevRuntime.DevEventsEndpoint;
            _ = Task.Run(async () =>
            {
                try
                {
                    await BroadcastSseJsonAsync(endpoint, "asset-changed", evt).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[DevRuntime] 广播变更事件失败: {ex.Message}");
                }
            });
        }

        private IReadOnlyList<string> ResolveDevWatchDirectories(IReadOnlyList<string> configuredDirectories)
        {
            var list = new List<string>();

            if (configuredDirectories is { Count: > 0 })
            {
                foreach (var directory in configuredDirectories)
                {
                    if (string.IsNullOrWhiteSpace(directory))
                    {
                        continue;
                    }

                    try
                    {
                        list.Add(Path.GetFullPath(directory));
                    }
                    catch
                    {
                        Logger.Warn($"[DevRuntime] 忽略非法监听目录: {directory}");
                    }
                }
            }

            if (list.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(ViewRoot))
                {
                    list.Add(Path.GetFullPath(ViewRoot));
                }

                if (!string.IsNullOrWhiteSpace(FileRootPath))
                {
                    list.Add(Path.GetFullPath(FileRootPath));
                }

                if (!string.IsNullOrWhiteSpace(_staticFileRoot))
                {
                    list.Add(Path.GetFullPath(_staticFileRoot));
                }
            }

            return list
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string NormalizeDevAssetPath(string fullPath)
        {
            var normalized = fullPath.Replace('\\', '/');

            var roots = new[] { ViewRoot, FileRootPath, _staticFileRoot }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => Path.GetFullPath(x!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var root in roots)
            {
                if (!normalized.StartsWith(root.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var relative = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
                    return relative.StartsWith('/') ? relative : "/" + relative;
                }
                catch
                {
                    return normalized;
                }
            }

            return normalized;
        }

        private void RegisterDevRuntimeCoreRoutes()
        {
            lock (_devRuntimeRouteLock)
            {
                if (_devRuntimeRoutesRegistered)
                {
                    return;
                }

                var config = _options.DevRuntime;

                RegisterSseRoute(
                    config.DevEventsEndpoint,
                    async (ISseWriter writer, Protocol.HttpRequest request, CancellationToken ct, DrxHttpServer server) =>
                    {
                        if (!IsDevAccessAllowed(request.ClientAddress.Ip))
                        {
                            await writer.RejectAsync(403, "Forbidden").ConfigureAwait(false);
                            return;
                        }

                        await writer.SendAsync("connected", "{\"ok\":true}").ConfigureAwait(false);
                        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                    },
                    heartbeatSeconds: 15,
                    rateLimitMaxRequests: 0,
                    rateLimitWindowSeconds: 0);

                AddRoute(HttpMethod.Get, config.RuntimeScriptEndpoint, request =>
                {
                    if (!IsDevAccessAllowed(request.ClientAddress.Ip))
                    {
                        return new HttpResponse(403, "Forbidden");
                    }

                    var resp = new HttpResponse(200, GetDevRuntimeBootstrapScript());
                    resp.Headers["Content-Type"] = "application/javascript; charset=utf-8";
                    resp.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                    resp.Headers["Pragma"] = "no-cache";
                    return resp;
                });

                AddRoute(HttpMethod.Get, config.ComponentsManifestEndpoint, request =>
                {
                    if (!IsDevAccessAllowed(request.ClientAddress.Ip))
                    {
                        return new HttpResponse(403, "Forbidden");
                    }

                    var body = "{\"version\":1,\"components\":[]}";
                    var resp = new HttpResponse(200, body);
                    resp.Headers["Content-Type"] = "application/json; charset=utf-8";
                    resp.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                    resp.Headers["Pragma"] = "no-cache";
                    return resp;
                });

                _devRuntimeRoutesRegistered = true;
                Logger.Info("[DevRuntime] 已注册开发态端点（dev-events/runtime.js/components-manifest）");
            }
        }

        private static bool IsDevAccessAllowed(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return true;
            }

            if (!IPAddress.TryParse(ipAddress, out var ip))
            {
                return false;
            }

            return IPAddress.IsLoopback(ip);
        }

        private static string GetDevRuntimeBootstrapScript()
        {
            return """
                (() => {
                  if (window.__drxRuntimeInjected) return;
                  window.__drxRuntimeInjected = true;

                  window.__drxDevConnect = function (endpoint = '/__drx/dev-events') {
                    try {
                      const source = new EventSource(endpoint);
                      source.addEventListener('asset-changed', () => window.location.reload());
                      source.onerror = () => { /* ignore transient network errors */ };
                      return source;
                    } catch (e) {
                      console.warn('[drx runtime] connect failed:', e);
                      return null;
                    }
                  };
                })();
                """;
        }
    }
}
