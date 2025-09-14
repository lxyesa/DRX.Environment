using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Drx.Sdk.Network.V2.Socket.Packet;

/// <summary>
/// 数据包打包/解包器。
/// 提供将原始字节数据打包为带长度前缀的传输格式以及可选的 AES 加密支持，
/// 同时支持将打包数据解包与解密回原始字节。
/// </summary>
public static class Packetizer
{
    /// <summary>
    /// 将原始字节数据打包为单个包，格式为 <c>(length:payload)</c>。
    /// </summary>
    /// <param name="data">要打包的原始字节数据。</param>
    /// <param name="packetSize">保留参数：建议为分片大小（当前实现不做分片处理）。</param>
    /// <param name="maxPacketSize">保留参数：最大包大小（当前实现不做限制）。</param>
    /// <returns>返回包含长度头的 UTF-8 字节数组。</returns>
    public static byte[] Pack(byte[] data, int packetSize = 0, int maxPacketSize = 0)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        var payload = data;
        return WrapWithLength(payload);
    }

    /// <summary>
    /// 从打包格式中解析并返回原始 payload 字节（不做解密）。
    /// </summary>
    /// <param name="packet">符合 <c>(length:payload)</c> 格式的字节数组。</param>
    /// <returns>返回解析出的 payload 字节数组。</returns>
    public static byte[] Unpack(byte[] packet)
    {
        if (packet == null) throw new ArgumentNullException(nameof(packet));
        return UnwrapLength(packet);
    }

    /// <summary>
    /// 使用 AES-CBC 对数据进行加密后，再按打包格式包装并返回。
    /// </summary>
    /// <param name="data">要加密并打包的原始字节数据。</param>
    /// <param name="aesKey">AES 对称密钥（字节数组）。</param>
    /// <param name="aesIV">AES 初始化向量（字节数组）。</param>
    /// <param name="packetSize">保留参数：分片大小（未使用）。</param>
    /// <param name="maxPacketSize">保留参数：最大包大小（未使用）。</param>
    /// <returns>返回加密后并带长度头的字节数组。</returns>
    public static byte[] Pack(byte[] data, byte[] aesKey, byte[] aesIV, int packetSize = 0, int maxPacketSize = 0)
    {
        if (aesKey == null) throw new ArgumentNullException(nameof(aesKey));
        if (aesIV == null) throw new ArgumentNullException(nameof(aesIV));
        var encrypted = AesEncrypt(data, aesKey, aesIV);
        return WrapWithLength(encrypted);
    }

    /// <summary>
    /// 解包并使用 AES-CBC 进行解密，返回原始明文字节。
    /// </summary>
    /// <param name="packet">符合打包格式且包含密文的字节数组。</param>
    /// <param name="aesKey">用于解密的 AES 密钥。</param>
    /// <param name="aesIV">用于解密的 AES 初始化向量。</param>
    /// <returns>返回解密后的明文字节数组。</returns>
    public static byte[] Unpack(byte[] packet, byte[] aesKey, byte[] aesIV)
    {
        if (aesKey == null) throw new ArgumentNullException(nameof(aesKey));
        if (aesIV == null) throw new ArgumentNullException(nameof(aesIV));
        var payload = UnwrapLength(packet);
        return AesDecrypt(payload, aesKey, aesIV);
    }

    private static byte[] WrapWithLength(byte[] payload)
    {
        var header = Encoding.UTF8.GetBytes("(" + payload.Length + ":");
        var footer = new byte[] { (byte)')' };
        var result = new byte[header.Length + payload.Length + footer.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(payload, 0, result, header.Length, payload.Length);
        Buffer.BlockCopy(footer, 0, result, header.Length + payload.Length, footer.Length);
        return result;
    }

    private static byte[] UnwrapLength(byte[] packet)
    {
        var str = Encoding.UTF8.GetString(packet);
        if (!str.StartsWith("(") || !str.EndsWith(")")) throw new FormatException("Invalid packet format");
        var colon = str.IndexOf(':');
        if (colon <= 1) throw new FormatException("Invalid packet header");
        var lenStr = str.Substring(1, colon - 1);
        if (!int.TryParse(lenStr, out var len)) throw new FormatException("Invalid length in packet header");
        var headerBytes = Encoding.UTF8.GetBytes("(" + lenStr + ":");
        if (packet.Length < headerBytes.Length + len + 1) throw new FormatException("Packet length mismatch");
        var payload = new byte[len];
        Buffer.BlockCopy(packet, headerBytes.Length, payload, 0, len);
        return payload;
    }

    /// <summary>
    /// 生成随机 AES 密钥与 IV，并将其写入运行目录下的 data 目录（文件名分别为 aes_key.bin 与 aes_iv.bin）。
    /// </summary>
    /// <param name="keySize">密钥长度（字节），默认为 32（256 位）。</param>
    /// <param name="ivSize">IV 长度（字节），默认为 16（128 位）。</param>
    public static void GenerateAesKeyFiles(int keySize = 32, int ivSize = 16)
    {
        var key = RandomNumberGenerator.GetBytes(keySize);
        var iv = RandomNumberGenerator.GetBytes(ivSize);
        var dir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dir);
        var keyPath = Path.Combine(dir, "aes_key.bin");
        var ivPath = Path.Combine(dir, "aes_iv.bin");
        File.WriteAllBytes(keyPath, key);
        File.WriteAllBytes(ivPath, iv);
        Console.WriteLine($"Generated AES key -> {keyPath} (len={key.Length})");
        Console.WriteLine($"Generated AES iv  -> {ivPath} (len={iv.Length})");
        Console.WriteLine($"Key (base64): {Convert.ToBase64String(key)}");
        Console.WriteLine($"IV  (base64): {Convert.ToBase64String(iv)}");
    }

    private static byte[] AesEncrypt(byte[] plain, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
        cs.Write(plain, 0, plain.Length);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }

    private static byte[] AesDecrypt(byte[] cipher, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write);
        cs.Write(cipher, 0, cipher.Length);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }
}
