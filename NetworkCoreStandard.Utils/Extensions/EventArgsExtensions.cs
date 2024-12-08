using System;
using System.Text.Json;

namespace NetworkCoreStandard.Utils.Extensions;

public static class EventArgsExtensions
{
    /// <summary>
    /// 将字节数组反序列化为指定类型的对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="bytes">要反序列化的字节数组</param>
    /// <returns>反序列化后的对象</returns>
    public static T GetObject<T>(this byte[] bytes) where T : class
    {
        try
        {
            // 创建JSON序列化选项
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            // 反序列化
            var result = JsonSerializer.Deserialize<T>(bytes, options);

            if (result == null)
            {
                throw new InvalidOperationException("反序列化结果为空");
            }

            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"反序列化时发生错误: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 将对象序列化为字节数组
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <returns>序列化后的字节数组</returns>
    public static byte[] GetBytes<T>(this T obj) where T : class
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            return JsonSerializer.SerializeToUtf8Bytes(obj, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"序列化时发生错误: {ex.Message}", ex);
        }
    }
}
