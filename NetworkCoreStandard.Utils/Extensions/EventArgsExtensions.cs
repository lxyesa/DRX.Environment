using System;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace NetworkCoreStandard.Utils.Extensions;

public static class EventArgsExtensions
{
    // 缓存 JsonSerializerOptions 实例，避免重复创建
    private static readonly JsonSerializerOptions _deserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions _serializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 将字节数组反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="bytes">要反序列化的字节数组</param>
    /// <returns>反序列化后的对象</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetObject<T>(this byte[] bytes) where T : class
    {
        try
        {
            // 直接使用缓存的选项进行反序列化
            var result = JsonSerializer.Deserialize<T>(bytes, _deserializeOptions);

            return result ?? throw new InvalidOperationException("反序列化结果为空");
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] GetBytes<T>(this T obj) where T : class
    {
        try
        {
            // 直接使用缓存的选项进行序列化
            return JsonSerializer.SerializeToUtf8Bytes(obj, _serializeOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"序列化时发生错误: {ex.Message}", ex);
        }
    }
}