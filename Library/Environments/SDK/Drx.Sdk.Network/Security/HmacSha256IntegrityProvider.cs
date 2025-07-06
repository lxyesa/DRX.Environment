using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Drx.Sdk.Network.Security
{
    public class HmacSha256IntegrityProvider : IPacketIntegrityProvider
    {
        private readonly byte[] _key;

        /// <summary>
        /// 使用默认的硬编码密钥初始化提供程序。
        /// 警告：这仅用于开发和测试目的。在生产环境中，切勿使用硬编码的密钥。
        /// </summary>
        public HmacSha256IntegrityProvider()
        {
            _key = KeyManager.Instance.HmacKey;
        }

        /// <summary>
        /// 使用指定的密钥初始化提供程序。
        /// </summary>
        /// <param name="key">用于HMAC-SHA256的密钥。建议长度至少为32字节。</param>
        public HmacSha256IntegrityProvider(byte[] key)
        {
            if (key == null || key.Length < 16)
                throw new ArgumentException("密钥不能为空且长度至少为16字节。", nameof(key));

            _key = key;
        }

        public byte[] Protect(byte[] originalData)
        {
            using (var hmac = new HMACSHA256(_key))
            {
                byte[] signature = hmac.ComputeHash(originalData);
                string base64Signature = Convert.ToBase64String(signature);
                string originalString = Encoding.UTF8.GetString(originalData);

                // Format as [signature][data]
                string protectedString = $"[{base64Signature}][{originalString}]";

                return Encoding.UTF8.GetBytes(protectedString);
            }
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            string protectedString;
            try
            {
                protectedString = Encoding.UTF8.GetString(protectedData);
            }
            catch
            {
                // Not a valid UTF-8 string, so it's invalid.
                return null;
            }

            // Use regex to parse the format: [signature][data]
            // This captures the base64 signature in group 1 and the original data in group 2.
            var match = Regex.Match(protectedString, @"^\[([^\]]+)\]\[(.*)\]$");

            if (!match.Success)
            {
                // The format is incorrect.
                return null;
            }

            string base64Signature = match.Groups[1].Value;
            string originalString = match.Groups[2].Value;

            byte[] receivedSignature;
            try
            {
                receivedSignature = Convert.FromBase64String(base64Signature);
            }
            catch (FormatException)
            {
                // The signature is not a valid Base64 string.
                return null;
            }

            // SHA256 signature is always 32 bytes long.
            if (receivedSignature.Length != 32)
            {
                return null;
            }

            byte[] originalData = Encoding.UTF8.GetBytes(originalString);

            using (var hmac = new HMACSHA256(_key))
            {
                byte[] computedSignature = hmac.ComputeHash(originalData);

                // Use a constant-time comparison to prevent timing attacks.
                if (CryptographicOperations.FixedTimeEquals(receivedSignature, computedSignature))
                {
                    return originalData;
                }
            }

            // Verification failed.
            return null;
        }
    }
} 