using System;
using System.IO;
using System.Security.Cryptography;

namespace Drx.Sdk.Network.Security
{
    public class AesEncryptor : IPacketEncryptor
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        /// <summary>
        /// 使用默认的硬编码密钥和IV初始化AesEncryptor。
        /// 警告：这仅用于开发和测试目的。在生产环境中，切勿使用硬编码的密钥。
        /// </summary>
        public AesEncryptor()
        {
            var keyManager = KeyManager.Instance;
            _key = keyManager.AesKey;
            _iv = keyManager.AesIV;
        }

        /// <summary>
        /// 使用指定的密钥和IV初始化AesEncryptor。
        /// </summary>
        /// <param name="key">用于加密的32字节密钥。</param>
        /// <param name="iv">用于加密的16字节初始化向量。</param>
        public AesEncryptor(byte[] key, byte[] iv)
        {
            if (key == null || key.Length != 32)
                throw new ArgumentException("密钥必须是32字节。", nameof(key));
            if (iv == null || iv.Length != 16)
                throw new ArgumentException("初始化向量必须是16字节。", nameof(iv));

            _key = key;
            _iv = iv;
        }

        public byte[] Encrypt(byte[] data)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var memoryStream = new MemoryStream())
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(data, 0, data.Length);
                        cryptoStream.FlushFinalBlock();
                        return memoryStream.ToArray();
                    }
                }
            }
        }

        public byte[] Decrypt(byte[] data)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = _key;
                    aes.IV = _iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var memoryStream = new MemoryStream())
                    {
                        using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(data, 0, data.Length);
                            cryptoStream.FlushFinalBlock();
                            return memoryStream.ToArray();
                        }
                    }
                }
            }
            catch (CryptographicException)
            {
                // 解密失败（例如，错误的密钥，损坏的数据，填充错误）
                // 根据您的安全策略，您可以选择返回null，抛出特定异常，或记录错误。
                // 返回null可以防止信息泄露，但会使调用方难以区分空数据和解密失败。
                return null;
            }
        }
    }
} 