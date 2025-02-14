using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DRX.Framework.Common.Systems;

public class FileSystem : IDisposable
{
    public static readonly string ConfigDirectory = Path.Combine(AppContext.BaseDirectory, "data", "configs");
    public static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "data", "configs", "config.json");
    public static readonly string DataPath = Path.Combine(AppContext.BaseDirectory, "data");
    public static readonly string UserPath = Path.Combine(AppContext.BaseDirectory, "data", "users");
    public static readonly string BanPath = Path.Combine(AppContext.BaseDirectory, "data", "banned_list.json");
    public static readonly string I18Path = Path.Combine(AppContext.BaseDirectory, "data", "i18n");
    public static readonly string I18DictPath = Path.Combine(AppContext.BaseDirectory, "data", "i18n", "dicts");
    public static readonly string I18LangPath = Path.Combine(AppContext.BaseDirectory, "data", "i18n", "langs");
    public static readonly string PluginPath = Path.Combine(AppContext.BaseDirectory, "data", "plugins");

    public void Dispose()
    {
        // Implement your cleanup code here if needed
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
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

        await using FileStream fs = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(fs, data, JsonOptions);
    }

    public static async Task<T?> LoadFromJsonAsync<T>(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("路径不能为空", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException($"找不到文件: {path}");

        await using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<T>(fs, JsonOptions);
    }

    public static async Task<(bool success, T? value)> ReadJsonKeyAsync<T>(string path, string key)
    {
        if (string.IsNullOrEmpty(key))
            return (false, default);

        if (!File.Exists(path))
            return (false, default);

        try
        {
            using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var doc = await JsonNode.ParseAsync(fs);
            var value = doc?[key];

            if (value == null)
                return (false, default);

            return (true, value.GetValue<T>());
        }
        catch (Exception ex)
        {
            Logger.Log("File", $"读取JSON键值时发生错误: {ex.Message}");
            return (false, default);
        }
    }

    public static async Task WriteJsonKeyAsync<T>(string path, string key, T value, bool debugging = false)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("键名不能为空", nameof(key));

        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("路径不能为空", nameof(path));

        try
        {
            JsonObject jsonObj;
            if (File.Exists(path))
            {
                using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                jsonObj = (await JsonNode.ParseAsync(fs))?.AsObject() ?? new JsonObject();

                // 检查键是否已存在
                if (jsonObj.ContainsKey(key))
                {
                    if (debugging)
                    {
                        Logger.Log("File", $"键 '{key}' 已存在，将被覆盖");
                    }
                }
            }
            else
            {
                jsonObj = new JsonObject();
            }

            // 更新或添加键值对
            jsonObj[key] = JsonValue.Create(value);

            // 写入文件
            using FileStream writeFs = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new Utf8JsonWriter(writeFs, new JsonWriterOptions
            {
                Indented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            jsonObj.WriteTo(writer);
        }
        catch (Exception ex)
        {
            Logger.Log("File", $"写入JSON键值时发生错误: {ex.Message}");
            throw;
        }
    }

    public static async Task RemoveJsonKeyAsync(string path, string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("键名不能为空", nameof(key));

        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("路径不能为空", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException($"找不到文件: {path}");

        try
        {
            await using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var doc = await JsonNode.ParseAsync(fs);
            if (doc is not JsonObject jsonObj)
                throw new InvalidOperationException("JSON 结构无效，无法移除键值对。");

            if (jsonObj.Remove(key))
            {
                // 写回文件
                fs.SetLength(0); // 清空文件
                fs.Seek(0, SeekOrigin.Begin);
                await JsonSerializer.SerializeAsync(fs, jsonObj, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            Logger.Log("File", $"移除JSON键值时发生错误: {ex.Message}");
            throw;
        }
    }

    public static async Task<bool> CreateFileAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("文件路径不能为空", nameof(filePath));

        try
        {
            if (File.Exists(filePath))
                return false;

            await using FileStream fs = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await fs.FlushAsync();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log("File", $"创建文件时发生错误: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> CreateDirectoryAsync(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath))
            throw new ArgumentException("目录路径不能为空", nameof(directoryPath));

        try
        {
            if (Directory.Exists(directoryPath))
                return false;

            Directory.CreateDirectory(directoryPath);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log("File", $"创建目录时发生错误: {ex.Message}");
            return false;
        }
    }

    // 同步方法包装保持不变
    public static void SaveToJson<T>(string path, T data) =>
        SaveToJsonAsync(path, data).GetAwaiter().GetResult();

    public static T? LoadFromJson<T>(string path) =>
        LoadFromJsonAsync<T>(path).GetAwaiter().GetResult();

    public static T? ReadJsonKey<T>(string path, string key)
    {
        var result = ReadJsonKeyAsync<T>(path, key).GetAwaiter().GetResult();
        return result.success ? result.value : default;
    }

    public static void WriteJsonKey<T>(string path, string key, T value) =>
        WriteJsonKeyAsync(path, key, value).GetAwaiter().GetResult();

    public static void RemoveJsonKey(string path, string key) =>
        RemoveJsonKeyAsync(path, key).GetAwaiter().GetResult();
}
