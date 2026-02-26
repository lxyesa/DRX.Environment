using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Configs;

namespace Drx.Sdk.Network.Http.Authorization
{
    /// <summary>
    /// 授权管理器
    /// </summary>
    public class AuthorizationManager
    {
        private readonly ConcurrentDictionary<string, AuthorizationRecord> _authorizations = new();
        private readonly Timer _cleanupTimer;
        private readonly int _defaultExpirationMinutes;

        public AuthorizationManager(int defaultExpirationMinutes = 5)
        {
            _defaultExpirationMinutes = defaultExpirationMinutes;
            // 每分钟清理一次过期授权码
            _cleanupTimer = new Timer(CleanupExpiredAuthorizations, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// 生成新的授权码
        /// </summary>
        public string GenerateAuthorizationCode(string userName, string applicationName, string applicationDescription = "", string scopes = "")
        {
            var code = Guid.NewGuid().ToString("N");
            var record = new AuthorizationRecord(code, userName, applicationName, applicationDescription, _defaultExpirationMinutes, scopes);
            _authorizations[code] = record;
            Logger.Info($"生成授权码: {code} for user {userName} on app {applicationName}");
            return code;
        }

        /// <summary>
        /// 获取授权记录
        /// </summary>
        public AuthorizationRecord? GetAuthorizationRecord(string code)
        {
            if (_authorizations.TryGetValue(code, out var record))
            {
                if (!record.IsExpired)
                {
                    return record;
                }
                else
                {
                    _authorizations.TryRemove(code, out _);
                    Logger.Info($"授权码已过期: {code}");
                }
            }
            return null;
        }

        /// <summary>
        /// 完成授权
        /// </summary>
        public bool CompleteAuthorization(string code)
        {
            if (_authorizations.TryGetValue(code, out var record))
            {
                if (!record.IsExpired && !record.IsAuthorized)
                {
                    record.IsAuthorized = true;
                    record.AuthorizedAt = DateTime.UtcNow;
                    Logger.Info($"授权完成: {code} for user {record.UserName}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 清理过期授权码
        /// </summary>
        private void CleanupExpiredAuthorizations(object? state)
        {
            var expiredKeys = _authorizations.Where(kvp => kvp.Value.IsExpired)
                                            .Select(kvp => kvp.Key)
                                            .ToList();

            foreach (var key in expiredKeys)
            {
                _authorizations.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                Logger.Info($"清理了 {expiredKeys.Count} 个过期授权码");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }
}
