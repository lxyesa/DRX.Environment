using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Text.Encodings.Web;
using System.Buffers;

namespace NetworkCoreStandard.IO;

public class File : IDisposable
{
    public void Dispose()
    {
        // Implement your cleanup code here if needed
    }
    
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static async Task SaveToJsonAsync<T>(string path, T data)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("路径不能为空", nameof(path));

        using FileStream fs = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(fs, data, _jsonOptions);
    }

    public static async Task<T?> LoadFromJsonAsync<T>(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("路径不能为空", nameof(path));
        
        if (!System.IO.File.Exists(path))
            throw new FileNotFoundException($"找不到文件: {path}");

        using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<T>(fs, _jsonOptions);
    }

    public static async Task<T?> ReadJsonKeyAsync<T>(string path, string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("键名不能为空", nameof(key));

        using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var doc = await JsonNode.ParseAsync(fs);
        var value = doc?[key];
        
        return value != null ? value.GetValue<T>() : default;
    }

    public static async Task WriteJsonKeyAsync<T>(string path, string key, T value)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("键名不能为空", nameof(key));

        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("路径不能为空", nameof(path));

        JsonObject jsonObj;
        if (System.IO.File.Exists(path))
        {
            using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            jsonObj = (await JsonNode.ParseAsync(fs))?.AsObject() ?? new JsonObject();
        }
        else
        {
            jsonObj = new JsonObject();
        }

        jsonObj[key] = JsonValue.Create(value);
        
        using FileStream writeFs = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(writeFs, new JsonWriterOptions 
        { 
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        
        jsonObj.WriteTo(writer);
    }

    // 同步方法包装保持不变
    public static void SaveToJson<T>(string path, T data) => 
        SaveToJsonAsync(path, data).GetAwaiter().GetResult();

    public static T? LoadFromJson<T>(string path) => 
        LoadFromJsonAsync<T>(path).GetAwaiter().GetResult();

    public static T? ReadJsonKey<T>(string path, string key) => 
        ReadJsonKeyAsync<T>(path, key).GetAwaiter().GetResult();

    public static void WriteJsonKey<T>(string path, string key, T value) => 
        WriteJsonKeyAsync(path, key, value).GetAwaiter().GetResult();
}