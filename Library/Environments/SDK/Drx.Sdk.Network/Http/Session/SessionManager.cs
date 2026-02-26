using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Configs;

namespace Drx.Sdk.Network.Http.Session
{
    /// <summary>
    /// 标准 HTTP/S 会话管理器。
    /// 遵循 RFC 6265 (Cookies) 规范，完全基于 Cookie 进行会话标识传输。
    /// 服务器端存储会话数据，仅通过 HttpOnly Secure Cookie 传输会话 ID。
    /// </summary>
    public class SessionManager
    {
        private readonly ConcurrentDictionary<string, Configs.Session> _sessions = new();
        private readonly TimeSpan _timeout;
        private readonly Timer _cleanupTimer;
        private readonly object _disposeLock = new();
        private volatile bool _disposed = false;

        /// <summary>
        /// 创建会话管理器
        /// </summary>
        /// <param name="timeoutMinutes">会话超时时间（分钟，默认30分钟）</param>
        public SessionManager(int timeoutMinutes = 30)
        {
            if (timeoutMinutes < 1) timeoutMinutes = 30;
            _timeout = TimeSpan.FromMinutes(timeoutMinutes);
            // 定期清理过期会话（每5分钟检查一次）
            _cleanupTimer = new Timer(CleanupExpiredSessions, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// 创建新会话
        /// </summary>
        /// <returns>新建的会话对象</returns>
        public Configs.Session CreateSession()
        {
            var id = GenerateSecureSessionId();
            var session = new Configs.Session(id);
            _sessions[id] = session;
            return session;
        }

        /// <summary>
        /// 获取指定 ID 的会话，并更新其最后访问时间。
        /// 如果会话不存在或已过期，返回 null。
        /// </summary>
        /// <param name="id">会话标识符</param>
        /// <returns>会话对象或 null</returns>
        public Configs.Session? GetSession(string? id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (_sessions.TryGetValue(id, out var session))
            {
                // 验证会话是否已过期
                if (IsExpired(session))
                {
                    _sessions.TryRemove(id, out _);
                    return null;
                }
                // 更新最后访问时间
                session.UpdateLastAccess();
                return session;
            }
            return null;
        }

        /// <summary>
        /// 获取或创建会话。如果提供的会话 ID 有效，则返回该会话；否则创建新会话。
        /// </summary>
        /// <param name="id">现有会话 ID（可为 null）</param>
        /// <returns>会话对象</returns>
        public Configs.Session GetOrCreateSession(string? id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                var session = GetSession(id);  // GetSession 已处理过期检查
                if (session != null) return session;
            }
            return CreateSession();
        }

        /// <summary>
        /// 删除指定 ID 的会话（用于显式注销）
        /// </summary>
        /// <param name="id">会话标识符</param>
        public void RemoveSession(string? id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                _sessions.TryRemove(id, out _);
            }
        }

        /// <summary>
        /// 检查会话是否已过期
        /// </summary>
        private bool IsExpired(Configs.Session session)
        {
            return (DateTime.UtcNow - session.LastAccessAt) > _timeout;
        }

        /// <summary>
        /// 清理所有已过期的会话（由内部定时器自动调用）
        /// </summary>
        private void CleanupExpiredSessions(object? state)
        {
            if (_disposed) return;

            try
            {
                var now = DateTime.UtcNow;
                var expiredKeys = _sessions
                    .Where(kvp => (now - kvp.Value.LastAccessAt) > _timeout)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _sessions.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    Logger.Info($"已清理 {expiredKeys.Count} 个过期会话");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"清理过期会话时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成安全的会话 ID（使用 GUID + 时间戳组合）
        /// </summary>
        private static string GenerateSecureSessionId()
        {
            // 使用加密强度 GUID 确保唯一性和不可预测性
            return Guid.NewGuid().ToString("N") + DateTime.UtcNow.Ticks.ToString("X");
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed) return;
                _disposed = true;

                try { _cleanupTimer?.Dispose(); } catch { }
                _sessions.Clear();
            }
        }
    }
}
