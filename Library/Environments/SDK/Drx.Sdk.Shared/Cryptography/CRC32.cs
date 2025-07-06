using System;
using System.Text;

namespace Drx.Sdk.Shared.Cryptography;

/// <summary>
/// 自定义CRC32实现
/// </summary>
public class CRC32
{
    // CRC32多项式常量 (IEEE 802.3标准)
    private const uint Polynomial = 0xEDB88320;
    
    // CRC32查找表
    private static readonly uint[] Table = new uint[256];
    
    // 静态构造函数，初始化查找表
    static CRC32()
    {
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) == 1)
                    crc = (crc >> 1) ^ Polynomial;
                else
                    crc >>= 1;
            }
            Table[i] = crc;
        }
    }
    
    /// <summary>
    /// 计算CRC32校验值
    /// </summary>
    /// <param name="data">要校验的数据</param>
    /// <returns>CRC32校验值</returns>
    public static uint Compute(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
            
        uint crc = 0xFFFFFFFF;
        
        for (int i = 0; i < data.Length; i++)
        {
            byte index = (byte)((crc & 0xFF) ^ data[i]);
            crc = (crc >> 8) ^ Table[index];
        }
        
        return ~crc; // 最终结果取反
    }
    
    /// <summary>
    /// 计算CRC32校验值并返回十六进制字符串
    /// </summary>
    /// <param name="data">要校验的数据</param>
    /// <returns>CRC32校验值的十六进制字符串</returns>
    public static string ComputeHashString(byte[] data)
    {
        uint crc = Compute(data);
        byte[] crcBytes = BitConverter.GetBytes(crc);
        return SHA256.BinaryToHexString(crcBytes);
    }
    
    /// <summary>
    /// 计算字符串的CRC32校验值
    /// </summary>
    /// <param name="text">要校验的字符串</param>
    /// <param name="encoding">使用的编码，默认为UTF8</param>
    /// <returns>CRC32校验值的十六进制字符串</returns>
    public static string ComputeHashString(string text, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        byte[] data = encoding.GetBytes(text);
        return ComputeHashString(data);
    }
    
    /// <summary>
    /// 计算CRC32校验值并返回字节数组
    /// </summary>
    /// <param name="data">要校验的数据</param>
    /// <returns>表示CRC32的4字节数组</returns>
    public static byte[] ComputeHash(byte[] data)
    {
        uint crc = Compute(data);
        return BitConverter.GetBytes(crc);
    }
}
