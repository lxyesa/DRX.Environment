using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http.Performance;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpServer 工具方法部分：HTTP 方法解析、状态描述、客户端断连检测、文件名清理、异步辅助、资源释放
    /// </summary>
    public partial class DrxHttpServer
    {
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

        private static bool IsClientDisconnect(Exception ex)
        {
            if (ex == null) return false;
            try
            {
                if (ex is HttpListenerException hle)
                {
                    return hle.ErrorCode == 64 || hle.ErrorCode == 995 || hle.ErrorCode == 10054;
                }

                if (ex is IOException ioe && ioe.InnerException is System.Net.Sockets.SocketException se)
                {
                    return se.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset || se.SocketErrorCode == System.Net.Sockets.SocketError.Shutdown;
                }

                if (ex is System.Net.Sockets.SocketException socketEx)
                {
                    return socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset || socketEx.SocketErrorCode == System.Net.Sockets.SocketError.Shutdown;
                }
            }
            catch { }
            return false;
        }

        private static string SanitizeFileNameForHeader(string name)
        {
            if (string.IsNullOrEmpty(name)) return "file";
            var sb = new StringBuilder();
            foreach (var ch in name)
            {
                if (char.IsControl(ch) || ch == '"' || ch == '\\') continue;
                sb.Append(ch);
            }
            var result = sb.ToString().Trim();
            if (string.IsNullOrEmpty(result)) result = "file";
            return result;
        }

        /// <summary>
        /// 获取或缓存速率限制键以避免频繁的字符串拼接
        /// </summary>
        private string GetOrCacheRateLimitKey(string baseKey)
        {
            return _cacheProvider.RateLimitKey.GetOrSet<string>(
                baseKey,
                (_, _) => baseKey
            );
        }

        /// <summary>
        /// Fire-and-forget 异步操作辅助方法
        /// </summary>
        private static async Task FireAndForgetAsync(Func<Task> operation)
        {
            try
            {
                await operation().ConfigureAwait(false);
            }
            catch
            {
            }
        }

        // ── 压缩策略灰度 / 回滚控制（任务 3.3 / R7）────────────────────────────

        /// <summary>
        /// 检查当前响应压缩是否处于活跃状态。
        /// CPU 守护触发降级后此值为 false。
        /// </summary>
        public bool IsCompressionActive => _compressionStrategy?.IsCompressionActive ?? false;

        /// <summary>
        /// 获取当前压缩统计摘要（灰度验证用）。
        /// </summary>
        public CompressionStats GetCompressionStats() => _compressionStrategy?.GetStats()
            ?? new CompressionStats { IsActive = false };

        /// <summary>
        /// 强制禁用响应压缩（灰度回滚场景）。
        /// 覆盖 CPU 守护状态，立即生效；调用 <see cref="ResumeCompression"/> 可重新启用。
        /// </summary>
        public void DisableCompression() => _compressionStrategy?.ForceDisable();

        /// <summary>
        /// 恢复响应压缩（在 <see cref="DisableCompression"/> 或 CPU 降级后手动恢复）。
        /// 需确保 <see cref="DrxHttpServerOptions.EnableCompression"/> 为 true。
        /// </summary>
        public void ResumeCompression() => _compressionStrategy?.ForceEnable();

        public async ValueTask DisposeAsync()
        {
            Stop();
            await _cacheProvider.DisposeAsync().ConfigureAwait(false);
            _sessionManager?.Dispose();
            _authorizationManager?.Dispose();

            try { _cts?.Dispose(); } catch { }
            try { _tickerWake?.Dispose(); } catch { }
            try { ((IDisposable)_listener)?.Dispose(); } catch { }
            try { if (_dataPersistentManager is IDisposable dpDisposable) dpDisposable.Dispose(); } catch { }
            try { _compressionStrategy?.Dispose(); } catch { }

            _ipRequestHistory.Clear();
            _ipRouteRequestHistory.Clear();

            return;
        }
    }
}
