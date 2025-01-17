using System.IO;
using System.Text.Json;

namespace DRX.Framework.Common.Base;

public abstract class BaseConfig
{
    /// <summary>
    /// 将配置保存到指定文件路径。
    /// 如果文件不存在，则创建文件并保存配置；如果文件已存在，则不执行任何操作。
    /// </summary>
    /// <param name="path">配置文件的路径。</param>
    /// <returns>保存成功返回 true，文件已存在返回 false。</returns>
    public virtual async Task<bool> SaveToFileAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentNullException(nameof(path), "文件路径不能为空");
        }

        try
        {
            // 获取目录路径
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                // 创建目录
                Directory.CreateDirectory(directory);
                Console.WriteLine($"目录已创建: {directory}");
            }

            if (File.Exists(path))
            {
                Console.WriteLine($"文件已存在: {path}，不执行保存操作。");
                return false;
            }

            // 使用UTF8JsonWriter提升序列化性能
            using var memoryStream = new MemoryStream();
            using var jsonWriter = new Utf8JsonWriter(memoryStream, new JsonWriterOptions
            {
                Indented = true,
                SkipValidation = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            // 使用运行时类型进行序列化
            JsonSerializer.Serialize(jsonWriter, this, GetType());
            await jsonWriter.FlushAsync();

            using var fileStream = new FileStream(
                path,
                FileMode.CreateNew, // 仅在文件不存在时创建新文件
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(fileStream);
            await fileStream.FlushAsync();

            Console.WriteLine($"配置已保存到: {path}");
            return true;
        }
        catch (IOException ioEx) when (ioEx is IOException && File.Exists(path))
        {
            // 文件已存在，忽略创建操作
            Console.WriteLine($"文件已存在: {path}，无法创建新文件。");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存配置时发生错误: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从指定文件路径加载配置。
    /// 如果文件不存在，则创建一个默认配置文件；如果文件存在，则加载配置。
    /// </summary>
    /// <param name="path">配置文件的路径。</param>
    /// <returns>加载成功返回 true，创建默认配置返回 false。</returns>
    public virtual async Task<bool> LoadFromFileAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentNullException(nameof(path), "文件路径不能为空");
        }

        try
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"找不到配置文件: {path}");

                // 尝试保存默认配置
                bool saveResult = await SaveToFileAsync(path);
                if (saveResult)
                {
                    Console.WriteLine($"默认配置文件已创建: {path}");
                }
                else
                {
                    Console.WriteLine($"未创建配置文件: {path}");
                }
                return false; // 返回 false 表示加载失败，需要用户进行后续操作
            }

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
                GetType(),
                new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });

            if (config != null)
            {
                // 使用运行时类型复制属性
                var properties = GetType().GetProperties();
                foreach (var prop in properties)
                {
                    if (prop.CanWrite)
                    {
                        prop.SetValue(this, prop.GetValue(config));
                    }
                }
                Console.WriteLine($"配置已加载: {path}");
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