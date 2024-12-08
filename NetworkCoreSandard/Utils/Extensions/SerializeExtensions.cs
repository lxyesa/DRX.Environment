using System;
using System.Text.Json;

namespace NetworkCoreStandard.Utils.Extensions;

public static class SerializeExtensions
{
    public static T? GetObject<T>(this string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON string cannot be null or empty.", nameof(json));
        }

        return JsonSerializer.Deserialize<T>(json);
    }

    public static string GetJson<T>(this T obj)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj), "Object cannot be null.");
        }

        return JsonSerializer.Serialize(obj);
    }
}
