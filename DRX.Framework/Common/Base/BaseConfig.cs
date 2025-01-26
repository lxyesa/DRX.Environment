using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DRX.Framework.Common.Systems;

namespace DRX.Framework.Common.Base;

public abstract class BaseConfig
{
    /// <summary>
    /// 将当前配置实例保存到默认路径的 JSON 文件中。
    /// 如果文件已存在，则尝试更新现有配置。
    /// </summary>
    /// <returns>保存成功返回 true。如果发生错误，将抛出异常。</returns>
    /// <exception cref="InvalidOperationException">配置文件路径未正确设置时抛出。</exception>
    /// <exception cref="IOException">写入过程中发生 IO 错误时抛出。</exception>
    public virtual async Task<bool> CreateFileAsync()
    {
        var path = GetSavePath();
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException("配置文件路径未正确设置");
        }

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 如果文件存在，执行更新操作
            if (File.Exists(path))
            {
                // return await SaveConfigAsync();
                // 掷出异常，避免直接覆盖现有文件
                throw new IOException($"配置文件已存在: {path}");
            }

            // 以下是原有的新文件创建逻辑
            using var memoryStream = new MemoryStream();
            await using var jsonWriter = new Utf8JsonWriter(memoryStream, new JsonWriterOptions
            {
                Indented = true,
                SkipValidation = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                IncludeFields = true,
                IgnoreReadOnlyProperties = false,
                IgnoreReadOnlyFields = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            JsonSerializer.Serialize(jsonWriter, this, GetType(), serializerOptions);
            await jsonWriter.FlushAsync();

            await using var fileStream = new FileStream(
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
            throw new IOException($"保存配置文件失败: {path}", ex);
        }
    }

    /// <summary>
    /// 从默认路径的 JSON 文件中加载配置并返回新的实例。
    /// </summary>
    /// <typeparam name="T">配置类型，必须继承自 BaseConfig</typeparam>
    /// <returns>返回加载的配置实例。如果发生错误，将抛出异常。</returns>
    /// <exception cref="InvalidOperationException">配置文件路径未正确设置时抛出。</exception>
    /// <exception cref="FileNotFoundException">配置文件不存在时抛出。</exception>
    /// <exception cref="InvalidDataException">
    /// 在以下情况下抛出：
    /// - JSON 文件格式无效
    /// - 反序列化过程失败
    /// </exception>
    /// <exception cref="IOException">读取文件过程中发生 IO 错误时抛出。</exception>
    protected async Task<T> LoadFromFileAsync<T>() where T : BaseConfig
    {
        var instance = Activator.CreateInstance<T>();
        var path = instance.GetSavePath();

        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException("配置文件路径未正确设置");
        }

        try
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"配置文件不存在: {path}");
            }

            await using var fileStream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                IncludeFields = true,
                IgnoreReadOnlyProperties = false,
                IgnoreReadOnlyFields = false,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var config = await JsonSerializer.DeserializeAsync<T>(fileStream, options);

            if (config == null)
            {
                throw new InvalidDataException($"配置文件格式无效: {path}");
            }

            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"配置文件解析失败: {path}", ex);
        }
        catch (Exception ex) when (ex is not FileNotFoundException && ex is not InvalidDataException)
        {
            throw new IOException($"加载配置文件失败: {path}", ex);
        }
    }

    /// <summary>
    /// 更新现有的配置文件。
    /// 如果文件不存在，则抛出异常。
    /// </summary>
    /// <returns>更新成功返回 true。如果发生错误，将抛出异常。</returns>
    /// <exception cref="FileNotFoundException">配置文件不存在时抛出。</exception>
    /// <exception cref="IOException">写入过程中发生 IO 错误时抛出。</exception>
    public virtual async Task<bool> SaveConfigAsync()
    {
        var path = GetSavePath();
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException("配置文件路径未正确设置");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"配置文件不存在: {path}");
        }

        try
        {
            using var memoryStream = new MemoryStream();
            await using var jsonWriter = new Utf8JsonWriter(memoryStream, new JsonWriterOptions
            {
                Indented = true,
                SkipValidation = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                IncludeFields = true,
                IgnoreReadOnlyProperties = false,
                IgnoreReadOnlyFields = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            JsonSerializer.Serialize(jsonWriter, this, GetType(), serializerOptions);
            await jsonWriter.FlushAsync();

            await using var fileStream = new FileStream(
                path,
                FileMode.Create, // 使用Create模式覆盖现有文件
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
            throw new IOException($"更新配置文件失败: {path}", ex);
        }
    }

    /// <summary>
    /// 检查配置文件是否存在
    /// </summary>
    /// <returns>
    /// 如果配置文件存在且可访问返回 true，否则返回 false。
    /// 在以下情况下返回 false：
    /// - 配置文件路径未正确设置（为空或无效）
    /// - 配置文件不存在
    /// - 没有足够的权限访问配置文件
    /// </returns>
    /// <remarks>
    /// 此方法仅检查文件是否存在，不验证文件内容的有效性。
    /// 为确保配置文件可用，建议配合 LoadFromFileAsync 方法使用。
    /// </remarks>
    public virtual bool HasConfig()
    {
        try
        {
            var path = GetSavePath();
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            // 检查文件是否存在，以及是否有读取权限
            return File.Exists(path) && HasFileAccess(path);
        }
        catch (Exception ex)
        {
            // 记录异常但不抛出，因为这是一个查询方法
            Logger.Error($"检查配置文件时发生错误: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 检查是否有文件的访问权限
    /// </summary>
    /// <param name="filePath">要检查的文件路径</param>
    /// <returns>如果有访问权限返回 true，否则返回 false</returns>
    private static bool HasFileAccess(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }


    /// <summary>
    /// 获取配置文件的保存路径。
    /// 默认实现根据类名生成配置文件路径，规则如下：
    /// 1. 如果类名以 "Config" 结尾，直接使用类名作为文件名
    /// 2. 如果类名不以 "Config" 结尾，添加 "Config" 后缀
    /// 最终生成的文件名将添加 ".json" 扩展名
    /// </summary>
    /// <returns>
    /// 返回完整的配置文件路径。
    /// 例如：对于类 UserConfig，返回 "{ConfigPath}/UserConfig.json"
    /// 对于类 System，返回 "{ConfigPath}/SystemConfig.json"
    /// </returns>
    /// <remarks>
    /// 派生类可以重写此方法以自定义配置文件的存储位置。
    /// 建议在重写时遵循以下原则：
    /// - 返回绝对路径
    /// - 确保路径对应的目录具有写入权限
    /// - 使用 .json 作为文件扩展名
    /// </remarks>
    protected virtual string GetSavePath()
    {
        // 获取实际类型名称（移除可能的代理类后缀）
        var typeName = GetType().Name;
        // 如果类名已经以Config结尾，直接使用
        return Path.Combine(FileSystem.ConfigDirectory, typeName.EndsWith("Config", StringComparison.OrdinalIgnoreCase) ? $"{typeName}.json" :
            // 如果类名不以Config结尾，添加Config后缀
            $"{typeName}Config.json");
    }


    /// <summary>
    /// 从配置文件加载配置。派生类必须实现此方法来定义具体的配置加载逻辑。
    /// </summary>
    /// <returns>表示异步操作的 Task</returns>
    /// <remarks>
    /// 实现建议：
    /// 1. 使用 LoadFromFileAsync&lt;T&gt; 方法加载配置文件
    /// 2. 处理可能出现的异常情况：
    ///    - FileNotFoundException: 文件不存在时使用默认值
    ///    - InvalidDataException: 配置格式无效时进行错误恢复
    ///    - IOException: 处理文件访问错误
    /// 3. 确保在出现错误时系统仍能正常运行
    /// 
    /// 示例实现:
    /// <code>
    /// public override async Task LoadAsync()
    /// {
    ///     try 
    ///     {
    ///         var config = await LoadFromFileAsync&lt;MyConfig&gt;();
    ///         // 应用加载的配置
    ///         this.Property1 = config.Property1;
    ///         this.Property2 = config.Property2;
    ///     }
    ///     catch (FileNotFoundException)
    ///     {
    ///         // 使用默认值
    ///         Logger.Debug("使用默认配置值");
    ///     }
    ///     catch (Exception ex)
    ///     {
    ///         Logger.Error($"加载配置失败: {ex.Message}");
    ///         throw;
    ///     }
    /// }
    /// </code>
    /// </remarks>
    /// <seealso cref="LoadFromFileAsync{T}"/>
    /// <seealso cref="HasConfig"/>
    public abstract Task LoadAsync();
}
