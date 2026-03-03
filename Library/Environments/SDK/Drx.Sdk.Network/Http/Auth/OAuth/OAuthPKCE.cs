using System;
using System.Security.Cryptography;
using System.Text;

namespace Drx.Sdk.Network.Http.Auth.OAuth
{
    /// <summary>
    /// PKCE（Proof Key for Code Exchange）工具类。
    /// 为 OAuth 2.0 公开客户端提供安全的授权码交换机制（RFC 7636）。
    /// </summary>
    public static class OAuthPKCE
    {
        /// <summary>
        /// Code Verifier 的最小长度
        /// </summary>
        public const int MinVerifierLength = 43;

        /// <summary>
        /// Code Verifier 的最大长度
        /// </summary>
        public const int MaxVerifierLength = 128;

        /// <summary>
        /// Code Verifier 的默认长度
        /// </summary>
        public const int DefaultVerifierLength = 64;

        /// <summary>
        /// 生成随机的 Code Verifier（URL 安全的 Base64 字符串）
        /// </summary>
        /// <param name="length">长度（43-128，默认 64）</param>
        /// <returns>Code Verifier 字符串</returns>
        public static string GenerateCodeVerifier(int length = DefaultVerifierLength)
        {
            if (length < MinVerifierLength || length > MaxVerifierLength)
                throw new ArgumentOutOfRangeException(nameof(length), $"长度必须在 {MinVerifierLength}-{MaxVerifierLength} 之间");

            // 使用加密安全随机数
            var bytes = new byte[length];
            RandomNumberGenerator.Fill(bytes);
            return Base64UrlEncode(bytes)[..length];
        }

        /// <summary>
        /// 使用 S256 方法从 Code Verifier 生成 Code Challenge
        /// </summary>
        /// <param name="codeVerifier">Code Verifier</param>
        /// <returns>Code Challenge（S256 编码）</returns>
        public static string GenerateCodeChallenge(string codeVerifier)
        {
            if (string.IsNullOrEmpty(codeVerifier))
                throw new ArgumentNullException(nameof(codeVerifier));

            var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            return Base64UrlEncode(bytes);
        }

        /// <summary>
        /// 一次性生成 Code Verifier 和 Code Challenge 对
        /// </summary>
        /// <param name="length">Verifier 长度（默认 64）</param>
        /// <returns>(CodeVerifier, CodeChallenge) 元组</returns>
        public static (string CodeVerifier, string CodeChallenge) GeneratePair(int length = DefaultVerifierLength)
        {
            var verifier = GenerateCodeVerifier(length);
            var challenge = GenerateCodeChallenge(verifier);
            return (verifier, challenge);
        }

        /// <summary>
        /// URL 安全的 Base64 编码（RFC 4648）
        /// </summary>
        private static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
