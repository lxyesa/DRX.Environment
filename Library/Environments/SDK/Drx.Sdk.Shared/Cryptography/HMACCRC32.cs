using System;
using System.Text;

namespace Drx.Sdk.Shared.Cryptography
{
    /// <summary>
    /// 自定义HMAC-CRC32实现
    /// 注意：CRC32主要用于错误检测，不是密码学安全的哈希函数
    /// 此HMAC实现仅作为演示，不建议用于安全敏感场景
    /// </summary>
    public class HMACCRC32
    {
        private readonly byte[] _key;
        private const byte IPAD = 0x36;
        private const byte OPAD = 0x5C;
        private const int BLOCK_SIZE = 64; // 使用与HMAC-SHA256相同的块大小

        public HMACCRC32(byte[] key)
        {
            // 如果密钥长度超过块大小，先计算其CRC32值
            if (key.Length > BLOCK_SIZE)
            {
                uint crc = CRC32.Compute(key);
                _key = BitConverter.GetBytes(crc);
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

            // 4. 计算CRC32((K XOR ipad) || data)
            byte[] innerData = new byte[iKeyPad.Length + data.Length];
            Array.Copy(iKeyPad, innerData, iKeyPad.Length);
            Array.Copy(data, 0, innerData, iKeyPad.Length, data.Length);
            uint innerCRC = CRC32.Compute(innerData);
            byte[] innerHash = BitConverter.GetBytes(innerCRC);

            // 5. 计算CRC32((K XOR opad) || innerHash)
            byte[] outerData = new byte[oKeyPad.Length + innerHash.Length];
            Array.Copy(oKeyPad, outerData, oKeyPad.Length);
            Array.Copy(innerHash, 0, outerData, oKeyPad.Length, innerHash.Length);
            uint outerCRC = CRC32.Compute(outerData);
            
            return BitConverter.GetBytes(outerCRC);
        }
        
        /// <summary>
        /// 计算HMAC-CRC32哈希并返回十六进制字符串
        /// </summary>
        /// <param name="data">要计算哈希的数据</param>
        /// <returns>小写十六进制字符串</returns>
        public string ComputeHashString(byte[] data)
        {
            byte[] hash = ComputeHash(data);
            return SHA256.BinaryToHexString(hash);
        }
        
        /// <summary>
        /// 计算字符串的HMAC-CRC32哈希并返回十六进制字符串
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