

using System.IO;
using System.Text.Json;

namespace NetworkCoreStandard.Utils.Common.Config;

public abstract class ConfigItem
{
    public virtual async Task<bool> SaveToFileAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentNullException(nameof(path), "文件路径不能为空");
        }

        if (System.IO.File.Exists(path))
        {
            Console.WriteLine($"文件已存在: {path}");
            return false;
        }

        try
        {
            // 使用UTF8JsonWriter提升序列化性能
            using var memoryStream = new MemoryStream();
            using var jsonWriter = new Utf8JsonWriter(memoryStream, new JsonWriterOptions
            {
                Indented = true,
                SkipValidation = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            // 使用运行时类型进行序列化
            JsonSerializer.Serialize(jsonWriter, this, this.GetType());
            await jsonWriter.FlushAsync();

            using var fileStream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(fileStream);
            await fileStream.FlushAsync();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存配置时发生错误: {ex.Message}");
            return false;
        }
    }

    public virtual async Task<bool> LoadFromFileAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentNullException(nameof(path), "文件路径不能为空");
        }

        if (!System.IO.File.Exists(path))
        {
            Console.WriteLine($"找不到配置文件: {path}");
            return false;
        }

        try
        {
            using var fileStream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            // 使用运行时类型进行反序列化
            var config = await JsonSerializer.DeserializeAsync(
                memoryStream,
                this.GetType(),
                new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });

            if (config != null)
            {
                // 使用运行时类型复制属性
                var properties = this.GetType().GetProperties();
                foreach (var prop in properties)
                {
                    if (prop.CanWrite)
                    {
                        prop.SetValue(this, prop.GetValue(config));
                    }
                }
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载配置时发生错误: {ex.Message}");
            return false;
        }
    }
}

public static class ConfigPath
{
    public static readonly string ServerConfigPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "config.json");
}