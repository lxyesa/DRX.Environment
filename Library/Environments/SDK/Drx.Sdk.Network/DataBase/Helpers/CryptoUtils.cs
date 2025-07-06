using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Drx.Sdk.Network.DataBase.Helpers
{
    /// <summary>
    /// 提供加密相关的功能
    /// </summary>
    public static class CryptoUtils
    {
        /// <summary>
        /// 计算字符串的SHA256哈希值
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <returns>SHA256哈希值</returns>
        public static string GetSha256Hash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                var builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// 计算文件的SHA256哈希值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>SHA256哈希值</returns>
        public static string GetFileSha256Hash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return ToHexString(hash);
                }
            }
        }

        private static string ToHexString(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }
} 