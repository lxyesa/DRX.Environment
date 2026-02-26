using System;
using System.Threading.Tasks;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Performance;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpServer 速率限制部分：全局与路由级限流、令牌桶管理、性能监控
    /// </summary>
    public partial class DrxHttpServer
    {
        /// <summary>
        /// 设置每条消息的最小处理延迟（毫秒）。
        /// </summary>
        public void SetPerMessageProcessingDelay(int ms)
        {
            try
            {
                if (ms <= 0) ms = 0;
                _perMessageProcessingDelayMs = ms;
                Logger.Info($"设置每消息最小处理延迟: {ms} ms");
            }
            catch (Exception ex)
            {
                Logger.Error($"SetPerMessageProcessingDelay 失败: {ex}");
            }
        }

        /// <summary>
        /// 获取当前每条消息的最小处理延迟（毫秒）。
        /// </summary>
        public int GetPerMessageProcessingDelay()
        {
            return _perMessageProcessingDelayMs;
        }

        /// <summary>
        /// 设置基于IP的请求速率限制。
        /// </summary>
        public void SetRateLimit(int maxRequests, int timeValue, string timeUnit)
        {
            if (maxRequests < 0) maxRequests = 0;
            if (timeValue < 0) timeValue = 0;
            TimeSpan window;
            switch (timeUnit.ToLower())
            {
                case "seconds":
                    window = TimeSpan.FromSeconds(timeValue);
                    break;
                case "minutes":
                    window = TimeSpan.FromMinutes(timeValue);
                    break;
                case "hours":
                    window = TimeSpan.FromHours(timeValue);
                    break;
                case "days":
                    window = TimeSpan.FromDays(timeValue);
                    break;
                default:
                    throw new ArgumentException("无效的时间单位。支持: seconds, minutes, hours, days", nameof(timeUnit));
            }
            lock (_rateLimitLock)
            {
                _rateLimitMaxRequests = maxRequests;
                _rateLimitWindow = window;
            }
            Logger.Info($"设置速率限制: 每{timeValue} {timeUnit} 最多 {maxRequests} 个请求");
        }

        /// <summary>
        /// 获取当前速率限制设置。
        /// </summary>
        public (int maxRequests, TimeSpan window) GetRateLimit()
        {
            lock (_rateLimitLock)
            {
                return (_rateLimitMaxRequests, _rateLimitWindow);
            }
        }

        /// <summary>
        /// 检查指定IP是否超出全局速率限制（使用令牌桶算法）。
        /// </summary>
        private bool IsRateLimitExceeded(string ip, HttpRequest? request = null)
        {
            int maxRequests;
            TimeSpan window;
            lock (_rateLimitLock)
            {
                maxRequests = _rateLimitMaxRequests;
                window = _rateLimitWindow;
            }
            if (maxRequests <= 0 || window == TimeSpan.Zero) return false;

            var windowMs = window.TotalMilliseconds;
            var hasToken = _tokenBucketManager!.TryConsume(ip, maxRequests, windowMs);

            if (!hasToken)
            {
                if (request != null && OnGlobalRateLimitExceeded != null)
                {
                    var availableTokens = _tokenBucketManager!.GetAvailableTokens(ip);
                    var triggeredCount = Math.Max(1, Math.Abs(availableTokens));
                    var cb = OnGlobalRateLimitExceeded;
                    _ = FireAndForgetAsync(async () =>
                    {
                        try
                        {
                            await cb(triggeredCount, request).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"执行全局速率限制回调时发生错误: {ex.Message}");
                        }
                    });
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查路由级速率限制，如果超限则优先调用路由级回调并返回其响应。
        /// </summary>
        private async Task<(bool isExceeded, HttpResponse? customResponse)> CheckRateLimitForRouteAsync(string ip, string routeKey, int maxRequests, int windowSeconds, HttpRequest request, Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? rateLimitCallback)
        {
            if (maxRequests <= 0 || windowSeconds <= 0) return (false, null);

            var dictKey = string.Concat(ip, "#", routeKey);
            var windowMs = windowSeconds * 1000.0;
            var hasToken = _tokenBucketManager!.TryConsume(dictKey, maxRequests, windowMs);

            if (!hasToken)
            {
                var availableTokens = _tokenBucketManager!.GetAvailableTokens(dictKey);
                var triggeredCount = Math.Max(1, Math.Abs(availableTokens));

                if (rateLimitCallback != null)
                {
                    try
                    {
                        var ctx = new OverrideContext(routeKey, maxRequests, windowSeconds);
                        var customResponse = await rateLimitCallback(triggeredCount, request, ctx).ConfigureAwait(false);

                        if (OnRouteRateLimitExceeded != null)
                        {
                            var cb = OnRouteRateLimitExceeded;
                            _ = FireAndForgetAsync(async () =>
                            {
                                try { await cb(triggeredCount, request, routeKey).ConfigureAwait(false); }
                                catch (Exception ex) { Logger.Warn($"执行路由速率限制通知回调时发生错误: {ex.Message}"); }
                            });
                        }

                        return (true, customResponse);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"执行路由级速率限制回调时发生错误: {ex.Message}，回退到默认行为");
                    }
                }

                if (OnRouteRateLimitExceeded != null)
                {
                    var cb = OnRouteRateLimitExceeded;
                    _ = FireAndForgetAsync(async () =>
                    {
                        try { await cb(triggeredCount, request, routeKey).ConfigureAwait(false); }
                        catch (Exception ex) { Logger.Warn($"执行路由速率限制通知回调时发生错误: {ex.Message}"); }
                    });
                }

                return (true, null);
            }

            return (false, null);
        }

        /// <summary>
        /// 获取路由匹配缓存的性能统计信息
        /// </summary>
        public (long hits, long misses, int currentCacheSize, double hitRate) GetRouteMatchCacheStats()
        {
            if (_routeMatchCache == null)
                return (0, 0, 0, 0.0);

            var hits = _routeMatchCache.Hits;
            var misses = _routeMatchCache.Misses;
            var total = hits + misses;
            var hitRate = total > 0 ? (double)hits / total : 0.0;
            return (hits, misses, _routeMatchCache.Count, hitRate);
        }

        /// <summary>
        /// 清空路由匹配缓存
        /// </summary>
        public void ClearRouteMatchCache()
        {
            _routeMatchCache?.Clear();
            Logger.Info("路由匹配缓存已清空");
        }

        /// <summary>
        /// 获取令牌桶管理器中指定客户端/路由的可用令牌数
        /// </summary>
        public int GetAvailableTokens(string ip, string? routeKey = null)
        {
            if (_tokenBucketManager == null) return -1;

            var key = string.IsNullOrEmpty(routeKey) ? ip : ip + "#" + routeKey;
            return _tokenBucketManager.GetAvailableTokens(key);
        }
    }
}
