using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.Collections.Specialized;

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

            var token = new JwtSecurityToken(
                issuer: _config.Issuer,
                audience: _config.Audience,
                claims: claims,
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
                return new ClaimsPrincipal(new ClaimsIdentity(((JwtSecurityToken)validatedToken).Claims));
            }
            catch
            {
                return null;
            }
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