using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.Auth.OAuth
{
    /// <summary>
    /// OAuth 管理器，提供统一的 OAuth 2.0 认证流程管理。
    /// 支持多提供商注册、State 防 CSRF、PKCE 安全增强。
    /// </summary>
    /// <remarks>
    /// 使用示例：
    /// <code>
    /// // 注册提供商
    /// OAuthManager.AddProvider(new GitHubOAuthProvider("client_id", "client_secret", "https://myapp.com/callback"));
    /// OAuthManager.AddProvider(new GoogleOAuthProvider("client_id", "client_secret", "https://myapp.com/callback"));
    ///
    /// // 获取授权 URL（自动生成 State + PKCE）
    /// var url = OAuthManager.GetAuthorizationUrl("github");
    ///
    /// // 回调时完成认证
    /// var result = await OAuthManager.AuthenticateAsync("github", code, state);
    /// if (result.Success) {
    ///     Console.WriteLine($"欢迎 {result.User.Name}!");
    /// }
    /// </code>
    /// </remarks>
    public static class OAuthManager
    {
        /// <summary>
        /// 已注册的提供商
        /// </summary>
        private static readonly ConcurrentDictionary<string, OAuthProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// State 记录（防 CSRF）
        /// </summary>
        private static readonly ConcurrentDictionary<string, OAuthStateRecord> _states = new();

        /// <summary>
        /// 过期 State 清理计时器
        /// </summary>
        private static readonly Timer _cleanupTimer;

        /// <summary>
        /// State 默认过期时间（分钟）
        /// </summary>
        private static int _stateExpirationMinutes = 10;

        static OAuthManager()
        {
            // 每 2 分钟清理一次过期 State
            _cleanupTimer = new Timer(CleanupExpiredStates, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));
        }

        // ======================== 提供商管理 ========================

        /// <summary>
        /// 注册 OAuth 提供商
        /// </summary>
        /// <param name="provider">提供商实例</param>
        public static void AddProvider(OAuthProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            _providers[provider.Name] = provider;
            Logger.Info($"[OAuthManager] 注册提供商: {provider.DisplayName} ({provider.Name})");
        }

        /// <summary>
        /// 批量注册 OAuth 提供商
        /// </summary>
        /// <param name="providers">提供商列表</param>
        public static void AddProviders(params OAuthProvider[] providers)
        {
            foreach (var provider in providers)
            {
                AddProvider(provider);
            }
        }

        /// <summary>
        /// 移除 OAuth 提供商
        /// </summary>
        /// <param name="name">提供商标识名</param>
        /// <returns>是否成功移除</returns>
        public static bool RemoveProvider(string name)
        {
            if (_providers.TryRemove(name, out var provider))
            {
                provider.Dispose();
                Logger.Info($"[OAuthManager] 移除提供商: {name}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取已注册的提供商
        /// </summary>
        /// <param name="name">提供商标识名</param>
        /// <returns>提供商实例，未注册则返回 null</returns>
        public static OAuthProvider? GetProvider(string name)
        {
            _providers.TryGetValue(name, out var provider);
            return provider;
        }

        /// <summary>
        /// 获取所有已注册的提供商名称列表
        /// </summary>
        public static IReadOnlyList<string> GetRegisteredProviders()
        {
            return _providers.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// 检查提供商是否已注册
        /// </summary>
        public static bool HasProvider(string name)
        {
            return _providers.ContainsKey(name);
        }

        // ======================== 认证流程 ========================

        /// <summary>
        /// 获取授权 URL（自动处理 State 和 PKCE）
        /// </summary>
        /// <param name="providerName">提供商标识名</param>
        /// <param name="userData">用户自定义附加数据（如 returnUrl）</param>
        /// <returns>完整的授权重定向 URL</returns>
        public static string GetAuthorizationUrl(string providerName, string? userData = null)
        {
            var provider = GetProviderOrThrow(providerName);

            // 生成安全随机 State
            var state = Guid.NewGuid().ToString("N");
            string? codeVerifier = null;
            string? codeChallenge = null;

            // 如果启用 PKCE，生成 Code Verifier/Challenge 对
            if (provider.Config.UsePKCE)
            {
                (codeVerifier, codeChallenge) = OAuthPKCE.GeneratePair();
            }

            // 保存 State 记录
            var record = new OAuthStateRecord(state, providerName, codeVerifier, userData)
            {
                ExpirationMinutes = _stateExpirationMinutes
            };
            _states[state] = record;

            return provider.BuildAuthorizationUrl(state, codeChallenge);
        }

        /// <summary>
        /// 完成认证流程（回调时调用：验证 State → 交换 Token → 获取用户信息）
        /// </summary>
        /// <param name="providerName">提供商标识名</param>
        /// <param name="code">回调返回的授权码</param>
        /// <param name="state">回调返回的 State 参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>完整的认证结果</returns>
        public static async Task<OAuthResult> AuthenticateAsync(string providerName, string code, string state, CancellationToken cancellationToken = default)
        {
            // 验证 State（防 CSRF）
            if (!_states.TryRemove(state, out var stateRecord))
            {
                Logger.Warn($"[OAuthManager] State 验证失败（不存在或已使用）: {state}");
                return OAuthResult.Fail(providerName, "State 验证失败：无效或已使用的 State 参数");
            }

            if (stateRecord.IsExpired)
            {
                Logger.Warn($"[OAuthManager] State 已过期: {state}");
                return OAuthResult.Fail(providerName, "State 已过期，请重新发起授权");
            }

            if (!string.Equals(stateRecord.ProviderName, providerName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warn($"[OAuthManager] State 提供商不匹配: 期望 {stateRecord.ProviderName}，实际 {providerName}");
                return OAuthResult.Fail(providerName, "State 与提供商不匹配");
            }

            var provider = GetProviderOrThrow(providerName);
            return await provider.AuthenticateAsync(code, stateRecord.CodeVerifier, cancellationToken);
        }

        /// <summary>
        /// 简化认证：自动从 State 推断提供商（适用于统一回调 URL 场景）
        /// </summary>
        /// <param name="code">回调返回的授权码</param>
        /// <param name="state">回调返回的 State 参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>完整的认证结果</returns>
        public static async Task<OAuthResult> AuthenticateAsync(string code, string state, CancellationToken cancellationToken = default)
        {
            if (!_states.TryGetValue(state, out var stateRecord))
            {
                return OAuthResult.Fail("unknown", "State 验证失败：无效或已使用的 State 参数");
            }

            return await AuthenticateAsync(stateRecord.ProviderName, code, state, cancellationToken);
        }

        /// <summary>
        /// 使用 Refresh Token 刷新 Access Token
        /// </summary>
        /// <param name="providerName">提供商标识名</param>
        /// <param name="refreshToken">刷新令牌</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>新的 OAuth Token</returns>
        public static async Task<OAuthToken> RefreshTokenAsync(string providerName, string refreshToken, CancellationToken cancellationToken = default)
        {
            var provider = GetProviderOrThrow(providerName);
            return await provider.RefreshTokenAsync(refreshToken, cancellationToken);
        }

        /// <summary>
        /// 使用 Access Token 获取用户信息
        /// </summary>
        /// <param name="providerName">提供商标识名</param>
        /// <param name="accessToken">访问令牌</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>用户信息</returns>
        public static async Task<OAuthUserInfo> GetUserInfoAsync(string providerName, string accessToken, CancellationToken cancellationToken = default)
        {
            var provider = GetProviderOrThrow(providerName);
            return await provider.GetUserInfoAsync(accessToken, cancellationToken);
        }

        // ======================== 配置 ========================

        /// <summary>
        /// 设置 State 过期时间
        /// </summary>
        /// <param name="minutes">过期分钟数（默认 10）</param>
        public static void SetStateExpiration(int minutes)
        {
            if (minutes < 1) throw new ArgumentOutOfRangeException(nameof(minutes), "过期时间不能小于 1 分钟");
            _stateExpirationMinutes = minutes;
        }

        /// <summary>
        /// 清除所有注册的提供商和 State（通常在测试或重置时使用）
        /// </summary>
        public static void Reset()
        {
            foreach (var provider in _providers.Values)
            {
                provider.Dispose();
            }
            _providers.Clear();
            _states.Clear();
            Logger.Info("[OAuthManager] 已重置");
        }

        // ======================== 内部方法 ========================

        /// <summary>
        /// 获取提供商或抛出异常
        /// </summary>
        private static OAuthProvider GetProviderOrThrow(string name)
        {
            if (!_providers.TryGetValue(name, out var provider))
            {
                throw new InvalidOperationException($"OAuth 提供商 '{name}' 未注册。已注册: [{string.Join(", ", _providers.Keys)}]");
            }
            return provider;
        }

        /// <summary>
        /// 清理过期的 State 记录
        /// </summary>
        private static void CleanupExpiredStates(object? state)
        {
            var expiredKeys = _states.Where(kvp => kvp.Value.IsExpired)
                                     .Select(kvp => kvp.Key)
                                     .ToList();

            foreach (var key in expiredKeys)
            {
                _states.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                Logger.Info($"[OAuthManager] 清理了 {expiredKeys.Count} 个过期 State");
            }
        }
    }
}
