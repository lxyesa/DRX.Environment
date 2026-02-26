using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

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
            lock (_rateLimitKeyCacheLock)
            {
                if (_rateLimitKeyCache.TryGetValue(baseKey, out var cached))
                {
                    return cached;
                }
                _rateLimitKeyCache[baseKey] = baseKey;
                return baseKey;
            }
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

        public ValueTask DisposeAsync()
        {
            Stop();
            _sessionManager?.Dispose();
            _authorizationManager?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
