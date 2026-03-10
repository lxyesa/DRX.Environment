// Copyright (c) DRX SDK — Paperclip 加密脚本桥接层
// 职责：将 AES 加密/解密 与哈希能力导出到 JS/TS 脚本
// 关键依赖：Drx.Sdk.Network.Security.AesEncryptor, System.Security.Cryptography

using System;
using System.Security.Cryptography;
using System.Text;
using Drx.Sdk.Network.Security;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 加密与哈希脚本桥接层。提供 AES 加解密、SHA256/MD5 哈希、Base64 编解码等。
/// </summary>
public static class CryptoBridge
{
    #region AES 加密/解密

    /// <summary>
    /// 使用默认密钥 AES 加密文本，返回 Base64 字符串。
    /// </summary>
    public static string aesEncrypt(string plainText)
    {
        var encryptor = new AesEncryptor();
        var data = Encoding.UTF8.GetBytes(plainText);
        var encrypted = encryptor.Encrypt(data);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// 使用默认密钥 AES 解密 Base64 字符串。
    /// </summary>
    public static string aesDecrypt(string base64Cipher)
    {
        var encryptor = new AesEncryptor();
        var data = Convert.FromBase64String(base64Cipher);
        var decrypted = encryptor.Decrypt(data);
        return decrypted != null ? Encoding.UTF8.GetString(decrypted) : string.Empty;
    }

    /// <summary>
    /// 使用自定义密钥 AES 加密文本。key 为 Base64 格式的 32 字节密钥，iv 为 Base64 格式的 16 字节 IV。
    /// </summary>
    public static string aesEncryptWithKey(string plainText, string keyBase64, string ivBase64)
    {
        var key = Convert.FromBase64String(keyBase64);
        var iv = Convert.FromBase64String(ivBase64);
        var encryptor = new AesEncryptor(key, iv);
        var data = Encoding.UTF8.GetBytes(plainText);
        var encrypted = encryptor.Encrypt(data);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// 使用自定义密钥 AES 解密。
    /// </summary>
    public static string aesDecryptWithKey(string base64Cipher, string keyBase64, string ivBase64)
    {
        var key = Convert.FromBase64String(keyBase64);
        var iv = Convert.FromBase64String(ivBase64);
        var encryptor = new AesEncryptor(key, iv);
        var data = Convert.FromBase64String(base64Cipher);
        var decrypted = encryptor.Decrypt(data);
        return decrypted != null ? Encoding.UTF8.GetString(decrypted) : string.Empty;
    }

    /// <summary>
    /// 生成 AES 密钥对（Base64）：{ key, iv }。
    /// </summary>
    public static object generateAesKey()
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.GenerateKey();
        aes.GenerateIV();
        return new { key = Convert.ToBase64String(aes.Key), iv = Convert.ToBase64String(aes.IV) };
    }

    #endregion

    #region 哈希

    /// <summary>
    /// SHA256 哈希（返回十六进制字符串）。
    /// </summary>
    public static string sha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// MD5 哈希（返回十六进制字符串）。
    /// </summary>
    public static string md5(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// HMAC-SHA256 签名（返回 Base64 字符串）。
    /// </summary>
    public static string hmacSha256(string input, string keyBase64)
    {
        var key = Convert.FromBase64String(keyBase64);
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = HMACSHA256.HashData(key, bytes);
        return Convert.ToBase64String(hash);
    }

    #endregion

    #region Base64

    /// <summary>
    /// Base64 编码。
    /// </summary>
    public static string base64Encode(string input)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(input));

    /// <summary>
    /// Base64 解码。
    /// </summary>
    public static string base64Decode(string base64)
        => Encoding.UTF8.GetString(Convert.FromBase64String(base64));

    #endregion

    #region 随机

    /// <summary>
    /// 生成指定字节数的随机字节，返回 Base64 字符串。
    /// </summary>
    public static string randomBytes(int length = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// 生成 UUID v4。
    /// </summary>
    public static string uuid()
        => Guid.NewGuid().ToString();

    #endregion
}
