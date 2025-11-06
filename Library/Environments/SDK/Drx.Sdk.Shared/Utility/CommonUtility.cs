using System;

namespace Drx.Sdk.Shared.Utility;

public static class CommonUtility
{
    public static long ToLong(this string str)
    {
        if (long.TryParse(str, out var result))
        {
            return result;
        }
        throw new FormatException($"无法将字符串转换为长整型: {str}");
    }

    public static int ToInt(this string str)
    {
        if (int.TryParse(str, out var result))
        {
            return result;
        }
        throw new FormatException($"无法将字符串转换为整型: {str}");
    }

    public static bool ToBool(this string str)
    {
        if (bool.TryParse(str, out var result))
        {
            return result;
        }
        throw new FormatException($"无法将字符串转换为布尔型: {str}");
    }

    public static double ToDouble(this string str)
    {
        if (double.TryParse(str, out var result))
        {
            return result;
        }
        throw new FormatException($"无法将字符串转换为双精度浮点型: {str}");
    }

    public static float ToFloat(this string str)
    {
        if (float.TryParse(str, out var result))
        {
            return result;
        }
        throw new FormatException($"无法将字符串转换为单精度浮点型: {str}");
    }

    public static decimal ToDecimal(this string str)
    {
        if (decimal.TryParse(str, out var result))
        {
            return result;
        }
        throw new FormatException($"无法将字符串转换为十进制型: {str}");
    }

    public static DateTime ToDateTime(this string str)
    {
        if (DateTime.TryParse(str, out var result))
        {
            return result;
        }
        throw new FormatException($"无法将字符串转换为日期时间型: {str}");
    }

    public static Guid ToGuid(this string str)
    {
        if (Guid.TryParse(str, out var result))
        {
            return result;
        }
        throw new FormatException($"无法将字符串转换为 GUID 型: {str}");
    }

    public static byte ToByte(this string str)
    {
        if (byte.TryParse(str, out var result))
        {
            return result;
        }
        throw new FormatException($"无法将字符串转换为字节型: {str}");
    }

    public static short ToShort(this string str)
    {
        if (short.TryParse(str, out var result))
        {
            return result;
        }
        throw new FormatException($"无法将字符串转换为短整型: {str}");
    }

    /// <summary>
    /// 通用码生成器，如 UPL-xxxx-xxxx-xxxx-xxxx-时间戳hash
    /// </summary>
    /// <param name=\"prefix\">令牌前缀</param>
    /// <param name=\"segmentCount\">段落数</param>
    /// <param name=\"segmentLength\">每段长度</param>
    /// <param name=\"includeTimestampHash\">是否附加时间戳hash</param>
    /// <returns>生成的令牌字符串</returns>
    public static string GenerateGeneralCode(string prefix, int segmentCount, int segmentLength, bool includeTimestampHash = true)
    {
        // 新增参数：是否全部大写
        return GenerateGeneralCode(prefix, segmentCount, segmentLength, includeTimestampHash, false);
    }

    /// <summary>
    /// 通用码生成器（重载），支持自定义结构和大小写控制、增强随机性
    /// </summary>
    /// <param name="prefix">令牌前缀</param>
    /// <param name="segmentCount">段落数</param>
    /// <param name="segmentLength">每段长度</param>
    /// <param name="includeTimestampHash">是否附加时间戳hash</param>
    /// <param name="upperCaseOnly">是否全部大写（true则只用大写字母，false则大小写混用）</param>
    /// <returns>生成的令牌字符串</returns>
    public static string GenerateGeneralCode(string prefix, int segmentCount, int segmentLength, bool includeTimestampHash, bool upperCaseOnly)
    {
        // 字符集
        string chars = upperCaseOnly ? "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789" : "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        // 使用更强的伪随机算法（柏林噪声模拟，实际为种子扰动+SHA1混合）
        var segments = new string[segmentCount];
        long baseSeed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() ^ Guid.NewGuid().GetHashCode();
        for (int i = 0; i < segmentCount; i++)
        {
            var sb = new System.Text.StringBuilder();
            for (int j = 0; j < segmentLength; j++)
            {
                // 伪柏林噪声：用SHA1(种子+段号+位号)扰动
                long noiseSeed = baseSeed + i * 9973 + j * 7919;
                byte[] hashBytes;
                using (var sha1 = System.Security.Cryptography.SHA1.Create())
                {
                    var bytes = BitConverter.GetBytes(noiseSeed);
                    hashBytes = sha1.ComputeHash(bytes);
                }
                int idx = hashBytes[j % hashBytes.Length] % chars.Length;
                sb.Append(chars[idx]);
            }
            segments[i] = sb.ToString();
        }
        string code = string.IsNullOrWhiteSpace(prefix) ? string.Join("-", segments) : prefix + "-" + string.Join("-", segments);
        if (includeTimestampHash)
        {
            long ticks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string hash;
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(ticks.ToString());
                var hashBytes = sha1.ComputeHash(bytes);
                hash = BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8);
            }
            code += "-" + hash;
        }
        return code;
    }

    /// <summary>
    /// 计算字符串的 SHA256 哈希值
    /// </summary>
    /// <param name="input">待计算哈希的字符串</param>
    /// <returns>SHA256 哈希值</returns>
    public static string ComputeSHA256Hash(string input)
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// 验证电子邮件地址格式是否有效
    /// </summary>
    /// <param name="email">电子邮件地址字符串</param>
    /// <returns>如果格式有效则返回 true，否则返回 false</returns>
    public static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
