using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.Concurrent;
using System.Linq;

namespace Drx.Sdk.Network.V2.Web
{
    /// <summary>
    /// JWT 帮助类，提供快速生成和验证 JWT 令牌的功能
    /// </summary>
    public static class JwtHelper
    {
        /// <summary>
        /// JWT 配置类
        /// </summary>
        public class JwtConfig
        {
            public string SecretKey { get; set; } = "A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6"; // 生产环境应使用强密钥
            public string Issuer { get; set; } = "DrxHttpServer";
            public string Audience { get; set; } = "DrxUsers";
            public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(1);
        }

        private static JwtConfig _config = new();
        // 被撤销的 token 列表：键 = jti, 值 = 到期时间（Unix 秒）
        private static readonly ConcurrentDictionary<string, long> _revokedTokens = new();

        /// <summary>
        /// 设置全局 JWT 配置
        /// </summary>
        public static void Configure(JwtConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// 生成 JWT 令牌
        /// </summary>
        /// <param name="claims">用户声明列表</param>
        /// <returns>JWT 字符串</returns>
        public static string GenerateToken(IEnumerable<Claim> claims)
        {
            var securityKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_config.SecretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            // 确保包含 jti（用于可撤销）和 iat
            var claimsList = (claims ?? Array.Empty<Claim>()).ToList();
            if (!claimsList.Any(c => c.Type == JwtRegisteredClaimNames.Jti))
            {
                claimsList.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
            }
            if (!claimsList.Any(c => c.Type == JwtRegisteredClaimNames.Iat))
            {
                claimsList.Add(new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));
            }

            var token = new JwtSecurityToken(
                issuer: _config.Issuer,
                audience: _config.Audience,
                claims: claimsList,
                expires: DateTime.UtcNow.Add(_config.Expiration),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// 生成无声明的 JWT 令牌
        /// </summary>
        /// <returns>JWT 字符串</returns>
        /// <exception cref="Exception">若配置无效则抛出异常</exception>
        public static string GenerateToken()
        {
            // 生成不包含任何声明的 JWT 令牌
            return GenerateToken(Array.Empty<Claim>());
        }

        /// <summary>
        /// 生成 JWT 令牌（简化版本）
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="userName">用户名</param>
        /// <param name="email">邮箱</param>
        /// <returns>JWT 字符串</returns>
        public static string GenerateToken(string userId, string userName, string? email = null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userName),
            };

            if (!string.IsNullOrEmpty(email))
            {
                claims.Add(new Claim(ClaimTypes.Email, email));
            }

            claims.Add(new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));

            return GenerateToken(claims);
        }

        /// <summary>
        /// 验证 JWT 令牌
        /// </summary>
        /// <param name="token">JWT 字符串</param>
        /// <returns>ClaimsPrincipal，如果验证失败返回 null</returns>
        public static ClaimsPrincipal ValidateToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            var tokenHandler = new JwtSecurityTokenHandler();
            var securityKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_config.SecretKey));

            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _config.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _config.Audience,
                    ValidateLifetime = true,
                    IssuerSigningKey = securityKey,
                    ClockSkew = TimeSpan.Zero
                };

                tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
                // 验证后检查是否被撤销（通过 jti）
                if (validatedToken is JwtSecurityToken jwt)
                {
                    var jti = jwt.Id ?? jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                    if (!string.IsNullOrEmpty(jti) && IsTokenRevoked(jti))
                    {
                        return null;
                    }
                    return new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims));
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 将给定的 JWT 字符串标记为撤销（可在后续验证时拒绝该 token）。
        /// 方法会尝试解析 token，读取 jti 与 exp，若解析失败则忽略。
        /// </summary>
        public static void RevokeToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);
                var jti = jwt.Id ?? jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                long expiresAt = 0;
                if (jwt.Payload.Expiration.HasValue)
                {
                    expiresAt = jwt.Payload.Expiration.Value;
                }
                else
                {
                    expiresAt = DateTimeOffset.UtcNow.Add(_config.Expiration).ToUnixTimeSeconds();
                }

                if (!string.IsNullOrEmpty(jti))
                {
                    _revokedTokens[jti] = expiresAt;
                }
            }
            catch
            {
                // 忽略解析错误
            }
        }

        /// <summary>
        /// 检查指定 jti 的 token 是否已被撤销。
        /// 如果记录已过期会自动清理并返回 false。
        /// </summary>
        public static bool IsTokenRevoked(string jti)
        {
            if (string.IsNullOrEmpty(jti)) return false;
            if (_revokedTokens.TryGetValue(jti, out var expiresAt))
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now >= expiresAt)
                {
                    _revokedTokens.TryRemove(jti, out _);
                    return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 从请求头中提取并验证 JWT 令牌
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>ClaimsPrincipal，如果无令牌或验证失败返回 null</returns>
        public static ClaimsPrincipal ValidateTokenFromRequest(HttpRequest request)
        {
            var authHeader = request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return null;

            var token = authHeader.Substring("Bearer ".Length);
            return ValidateToken(token);
        }

        /// <summary>
        /// 创建未授权响应
        /// </summary>
        /// <returns>401 响应</returns>
        public static HttpResponse CreateUnauthorizedResponse(string message = "无效或缺失的令牌。")
        {
            return new HttpResponse
            {
                StatusCode = 401,
                Headers = new NameValueCollection { { "WWW-Authenticate", "Bearer" } },
                Body = message
            };
        }
    }
}