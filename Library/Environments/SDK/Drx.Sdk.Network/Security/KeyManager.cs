using System;
using System.IO;
using System.Security.Cryptography;
using System.Xml.Linq;
using System.Linq;

namespace Drx.Sdk.Network.Security
{
    /// <summary>
    /// 负责加载、生成和管理加密和签名密钥。
    /// </summary>
    public sealed class KeyManager
    {
        private static readonly Lazy<KeyManager> lazy = new Lazy<KeyManager>(() => new KeyManager());
        public static KeyManager Instance => lazy.Value;

        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "hashcode", "key.xml");

        public byte[] AesKey { get; private set; }
        public byte[] AesIV { get; private set; }
        public byte[] HmacKey { get; private set; }

        private KeyManager()
        {
            LoadOrGenerateKeys();
        }

        private void LoadOrGenerateKeys()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    LoadKeysFromXml();
                }
                else
                {
                    GenerateNewKeys();
                    SaveKeysToXml();
                }
            }
            catch (Exception ex)
            {
                // 如果加载或生成密钥时发生严重错误，可以考虑抛出异常或记录日志。
                // 为确保系统能启动，这里我们退回到生成新密钥。
                Console.WriteLine($"Error during key management: {ex.Message}. Falling back to generating new keys.");
                GenerateNewKeys();
            }
        }

        private void GenerateNewKeys()
        {
            AesKey = RandomNumberGenerator.GetBytes(32); // AES-256
            AesIV = RandomNumberGenerator.GetBytes(16);  // AES IV
            HmacKey = RandomNumberGenerator.GetBytes(32); // HMAC-SHA256
        }

        private void SaveKeysToXml()
        {
            var doc = new XDocument(
                new XElement("Keys",
                    new XElement("Aes",
                        new XElement("Key", Convert.ToBase64String(AesKey)),
                        new XElement("IV", Convert.ToBase64String(AesIV)),
                        new XElement("KeyHex", ToHexString(AesKey)),
                        new XElement("IVHex", ToHexString(AesIV))
                    ),
                    new XElement("Hmac",
                        new XElement("Key", Convert.ToBase64String(HmacKey)),
                        new XElement("KeyHex", ToHexString(HmacKey))
                    )
                )
            );

            Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
            doc.Save(_configPath);
        }

        private void LoadKeysFromXml()
        {
            var doc = XDocument.Load(_configPath);
            AesKey = Convert.FromBase64String(doc.Root.Element("Aes").Element("Key").Value);
            AesIV = Convert.FromBase64String(doc.Root.Element("Aes").Element("IV").Value);
            HmacKey = Convert.FromBase64String(doc.Root.Element("Hmac").Element("Key").Value);
        }

        /// <summary>
        /// 将字节数组转换为逗号分隔的十六进制字符串（例如 "0xAB,0xCD"）。
        /// </summary>
        private string ToHexString(byte[] bytes)
        {
            return string.Join(",", bytes.Select(b => $"0x{b:X2}"));
        }

        /// <summary>
        /// 生成一个指定长度的、符合密码学安全的随机字节数组。
        /// </summary>
        /// <param name="length">字节数组的长度。必须是介于2和1024之间，且为2的倍数。</param>
        /// <returns>随机字节数组。</returns>
        public static byte[] GenerateRandomBytes(int length)
        {
            if (length < 2 || length > 1024 || length % 2 != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be between 2 and 1024, and a multiple of 2.");
            }
            return RandomNumberGenerator.GetBytes(length);
        }
    }
} 