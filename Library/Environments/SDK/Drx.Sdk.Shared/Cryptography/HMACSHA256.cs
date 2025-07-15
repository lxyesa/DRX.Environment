using System;
using System.Text;

namespace Drx.Sdk.Shared.Cryptography
{
    /// <summary>
    /// 自定义HMAC-SHA256实现
    /// </summary>
    public class HMACSHA256
    {
        private readonly byte[] _key;
        private const byte IPAD = 0x36;
        private const byte OPAD = 0x5C;
        private const int BLOCK_SIZE = 64; // SHA256的块大小为64字节

        public HMACSHA256(byte[] key)
        {
            // 如果密钥长度超过块大小，先计算其哈希值
            if (key.Length > BLOCK_SIZE)
            {
                _key = SHA256.ComputeHash(key);
            }
            else
            {
                _key = new byte[key.Length];
                Array.Copy(key, _key, key.Length);
            }
        }

        public byte[] ComputeHash(byte[] data)
        {
            // 1. 准备密钥（如果需要，填充到块大小）
            byte[] normalizedKey = new byte[BLOCK_SIZE];
            Array.Copy(_key, normalizedKey, _key.Length);

            // 2. 计算(K XOR ipad)
            byte[] iKeyPad = new byte[BLOCK_SIZE];
            for (int i = 0; i < BLOCK_SIZE; i++)
            {
                iKeyPad[i] = (byte)(normalizedKey[i] ^ IPAD);
            }

            // 3. 计算(K XOR opad)
            byte[] oKeyPad = new byte[BLOCK_SIZE];
            for (int i = 0; i < BLOCK_SIZE; i++)
            {
                oKeyPad[i] = (byte)(normalizedKey[i] ^ OPAD);
            }

            // 4. 计算H((K XOR ipad) || data)
            byte[] innerData = new byte[iKeyPad.Length + data.Length];
            Array.Copy(iKeyPad, innerData, iKeyPad.Length);
            Array.Copy(data, 0, innerData, iKeyPad.Length, data.Length);
            byte[] innerHash = SHA256.ComputeHash(innerData);

            // 5. 计算H((K XOR opad) || innerHash)
            byte[] outerData = new byte[oKeyPad.Length + innerHash.Length];
            Array.Copy(oKeyPad, outerData, oKeyPad.Length);
            Array.Copy(innerHash, 0, outerData, oKeyPad.Length, innerHash.Length);
            return SHA256.ComputeHash(outerData);
        }
        
        /// <summary>
        /// 计算HMAC-SHA256哈希并返回十六进制字符串
        /// </summary>
        /// <param name="data">要计算哈希的数据</param>
        /// <returns>小写十六进制字符串</returns>
        public string ComputeHashString(byte[] data)
        {
            byte[] hash = ComputeHash(data);
            return SHA256.BinaryToHexString(hash);
        }
        
        /// <summary>
        /// 计算字符串的HMAC-SHA256哈希并返回十六进制字符串
        /// </summary>
        /// <param name="text">要计算哈希的字符串</param>
        /// <param name="encoding">使用的编码，默认为UTF8</param>
        /// <returns>小写十六进制字符串</returns>
        public string ComputeHashString(string text, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            byte[] data = encoding.GetBytes(text);
            return ComputeHashString(data);
        }
    }
} 