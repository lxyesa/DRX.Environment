using System;
using System.Text;

namespace Drx.Sdk.Shared.Cryptography
{
    /// <summary>
    /// 自定义SHA256实现
    /// </summary>
    public class SHA256
    {
        // SHA256 初始哈希值（前8个素数的平方根小数部分）
        private static readonly uint[] H = new uint[8]
        {
            0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
            0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19
        };

        // SHA256 常量（前64个素数的立方根小数部分）
        private static readonly uint[] K = new uint[64]
        {
            0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
            0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
            0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
            0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
            0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
            0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
            0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
            0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
        };

        // 位运算函数
        private static uint ROTR(uint x, int n) => (x >> n) | (x << (32 - n));
        private static uint Ch(uint x, uint y, uint z) => (x & y) ^ (~x & z);
        private static uint Maj(uint x, uint y, uint z) => (x & y) ^ (x & z) ^ (y & z);
        private static uint Sigma0(uint x) => ROTR(x, 2) ^ ROTR(x, 13) ^ ROTR(x, 22);
        private static uint Sigma1(uint x) => ROTR(x, 6) ^ ROTR(x, 11) ^ ROTR(x, 25);
        private static uint sigma0(uint x) => ROTR(x, 7) ^ ROTR(x, 18) ^ (x >> 3);
        private static uint sigma1(uint x) => ROTR(x, 17) ^ ROTR(x, 19) ^ (x >> 10);

        /// <summary>
        /// 计算SHA256哈希值
        /// </summary>
        public static byte[] ComputeHash(byte[] data)
        {
            // 预处理 - 填充消息
            byte[] paddedData = PadMessage(data);

            // 解析消息到块
            int blockCount = paddedData.Length / 64;
            
            // 初始化哈希值
            uint[] hash = new uint[8];
            Array.Copy(H, hash, 8);

            // 处理每个512位块
            for (int i = 0; i < blockCount; i++)
            {
                ProcessBlock(paddedData, i * 64, hash);
            }

            // 转换最终哈希值为字节数组
            byte[] result = new byte[32];
            for (int i = 0; i < 8; i++)
            {
                BitConverter.GetBytes(hash[i]).CopyTo(result, i * 4);
                // 转换为大端格式
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(result, i * 4, 4);
                }
            }

            return result;
        }

        /// <summary>
        /// 计算SHA256哈希并返回十六进制字符串
        /// </summary>
        /// <param name="data">要计算哈希的数据</param>
        /// <returns>小写十六进制字符串</returns>
        public static string ComputeHashString(byte[] data)
        {
            byte[] hash = ComputeHash(data);
            return BinaryToHexString(hash);
        }
        
        /// <summary>
        /// 计算字符串的SHA256哈希并返回十六进制字符串
        /// </summary>
        /// <param name="text">要计算哈希的字符串</param>
        /// <param name="encoding">使用的编码，默认为UTF8</param>
        /// <returns>小写十六进制字符串</returns>
        public static string ComputeHashString(string text, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;
            byte[] data = encoding.GetBytes(text);
            return ComputeHashString(data);
        }
        
        /// <summary>
        /// 将二进制数据转换为十六进制字符串
        /// </summary>
        /// <param name="data">二进制数据</param>
        /// <returns>小写十六进制字符串</returns>
        public static string BinaryToHexString(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;
                
            // 使用与KernelCrypto.cpp相同的手动实现方式
            char[] hexChars = "0123456789abcdef".ToCharArray();
            char[] result = new char[data.Length * 2];
            
            for (int i = 0; i < data.Length; i++)
            {
                result[i * 2] = hexChars[(data[i] >> 4) & 0xF];
                result[i * 2 + 1] = hexChars[data[i] & 0xF];
            }
            
            return new string(result);
        }

        /// <summary>
        /// 填充消息以符合SHA256要求
        /// </summary>
        private static byte[] PadMessage(byte[] data)
        {
            // 原始长度（比特）
            long bitLength = data.Length * 8L;
            
            // 计算填充后的长度 (原始长度 + 1位"1" + k位"0" + 64位长度)
            int paddedLength = data.Length + 1 + 8; // 至少需要添加1字节"10000000"和8字节长度
            int k = 64 - (paddedLength % 64); // 需要多少字节使总长度是64的倍数
            if (k == 64) k = 0;
            
            paddedLength += k;
            
            byte[] paddedData = new byte[paddedLength];
            
            // 1. 复制原始数据
            Array.Copy(data, paddedData, data.Length);
            
            // 2. 添加一个"1"位后跟k个"0"位
            paddedData[data.Length] = 0x80; // 10000000
            
            // 3. 添加原始长度作为64位大端整数
            byte[] lengthBytes = BitConverter.GetBytes(bitLength);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);
                
            // 将长度放在最后8字节
            Array.Copy(lengthBytes, 0, paddedData, paddedLength - 8, 8);
            
            return paddedData;
        }

        /// <summary>
        /// 处理一个512位的块
        /// </summary>
        private static void ProcessBlock(byte[] data, int offset, uint[] hash)
        {
            // 1. 准备消息调度表W
            uint[] W = new uint[64];
            
            // 将块的16个字转换为调度表的前16个字
            for (int i = 0; i < 16; i++)
            {
                if (BitConverter.IsLittleEndian)
                {
                    W[i] = (uint)((data[offset + i * 4] << 24) |
                                 (data[offset + i * 4 + 1] << 16) |
                                 (data[offset + i * 4 + 2] << 8) |
                                 (data[offset + i * 4 + 3]));
                }
                else
                {
                    W[i] = BitConverter.ToUInt32(data, offset + i * 4);
                }
            }
            
            // 扩展其余的调度表
            for (int i = 16; i < 64; i++)
            {
                W[i] = sigma1(W[i-2]) + W[i-7] + sigma0(W[i-15]) + W[i-16];
            }
            
            // 2. 初始化工作变量
            uint a = hash[0];
            uint b = hash[1];
            uint c = hash[2];
            uint d = hash[3];
            uint e = hash[4];
            uint f = hash[5];
            uint g = hash[6];
            uint h = hash[7];
            
            // 3. 主循环
            for (int i = 0; i < 64; i++)
            {
                uint T1 = h + Sigma1(e) + Ch(e, f, g) + K[i] + W[i];
                uint T2 = Sigma0(a) + Maj(a, b, c);
                
                h = g;
                g = f;
                f = e;
                e = d + T1;
                d = c;
                c = b;
                b = a;
                a = T1 + T2;
            }
            
            // 4. 计算中间哈希值
            hash[0] += a;
            hash[1] += b;
            hash[2] += c;
            hash[3] += d;
            hash[4] += e;
            hash[5] += f;
            hash[6] += g;
            hash[7] += h;
        }
    }
} 