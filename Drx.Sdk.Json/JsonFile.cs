using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace Drx.Sdk.Json
{
    /// <summary>
    /// JSON 文件操作类
    /// </summary>
    public static class JsonFile
    {
        /// <summary>
        /// 默认 JSON 序列化选项
        /// </summary>
        private static readonly JsonSerializerOptions DefaultOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver() // 添加此行启用反射序列化
        };

        /// <summary>
        /// 将对象序列化为 JSON 并写入文件
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">要序列化的对象</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="options">序列化选项</param>
        /// <exception cref="ArgumentNullException">当 obj 或 filePath 为 null 时抛出</exception>
        /// <exception cref="JsonException">当序列化失败时抛出</exception>
        /// <exception cref="IOException">当文件操作失败时抛出</exception>
        public static void WriteToFile<T>(T obj, string filePath, bool append = false, JsonSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(obj);
            ArgumentNullException.ThrowIfNull(filePath);

            try
            {
                string directory = Path.GetDirectoryName(filePath)!;
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string jsonString = JsonSerializer.Serialize(obj, options ?? DefaultOptions);

                // 根据 append 参数决定是追加还是覆盖
                if (append && File.Exists(filePath))
                {
                    // 读取现有内容
                    string existingJson = File.ReadAllText(filePath).Trim();
                    if (existingJson.StartsWith("["))
                    {
                        // 如果是数组，在末尾添加新元素
                        existingJson = existingJson.TrimEnd(']');
                        jsonString = jsonString.TrimStart('[');
                        jsonString = $"{existingJson},{jsonString}";
                    }
                    else
                    {
                        // 如果不是数组，将现有内容和新内容包装在数组中
                        jsonString = $"[{existingJson},{jsonString.TrimStart('[').TrimEnd(']')}]";
                    }
                }

                File.WriteAllText(filePath, jsonString);
            }
            catch (JsonException ex)
            {
                throw new JsonException($"序列化对象失败: {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new IOException($"写入文件失败: {filePath}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"序列化对象到文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 异步将对象序列化为 JSON 并写入文件
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">要序列化的对象</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="options">序列化选项</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        public static async Task WriteToFileAsync<T>(T obj, string filePath,
    bool append = false,
    JsonSerializerOptions? options = null,
    CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(obj);
            ArgumentNullException.ThrowIfNull(filePath);

            try
            {
                string directory = Path.GetDirectoryName(filePath)!;
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (append && File.Exists(filePath))
                {
                    // 读取现有内容
                    string existingJson = await File.ReadAllTextAsync(filePath, cancellationToken);
                    using var ms = new MemoryStream();

                    if (existingJson.Trim().StartsWith("["))
                    {
                        // 如果是数组，追加新元素
                        existingJson = existingJson.TrimEnd(']');
                        await File.WriteAllTextAsync(filePath, existingJson + ",", cancellationToken);

                        using (var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None))
                        {
                            await JsonSerializer.SerializeAsync(fs, obj, options ?? DefaultOptions, cancellationToken);
                            await fs.WriteAsync(System.Text.Encoding.UTF8.GetBytes("]"), cancellationToken);
                        }
                    }
                    else
                    {
                        // 如果不是数组，将内容包装在数组中
                        await File.WriteAllTextAsync(filePath, $"[{existingJson},", cancellationToken);

                        using (var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None))
                        {
                            await JsonSerializer.SerializeAsync(fs, obj, options ?? DefaultOptions, cancellationToken);
                            await fs.WriteAsync(System.Text.Encoding.UTF8.GetBytes("]"), cancellationToken);
                        }
                    }
                }
                else
                {
                    // 覆盖模式
                    using FileStream fs = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await JsonSerializer.SerializeAsync(fs, obj, options ?? DefaultOptions, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"异步序列化对象到文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 从 JSON 文件反序列化为对象
        /// </summary>
        /// <typeparam name="T">目标对象类型</typeparam>
        /// <param name="filePath">JSON 文件路径</param>
        /// <param name="options">反序列化选项</param>
        /// <returns>反序列化后的对象</returns>
        /// <exception cref="FileNotFoundException">当文件不存在时抛出</exception>
        /// <exception cref="JsonException">当反序列化失败时抛出</exception>
        public static T? ReadFromFile<T>(string filePath, JsonSerializerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("指定的 JSON 文件不存在", filePath);
            }

            try
            {
                string jsonString = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<T>(jsonString, options ?? DefaultOptions);
            }
            catch (JsonException ex)
            {
                throw new JsonException($"反序列化失败: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"从文件反序列化对象失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 异步从 JSON 文件反序列化为对象
        /// </summary>
        /// <typeparam name="T">目标对象类型</typeparam>
        /// <param name="filePath">JSON 文件路径</param>
        /// <param name="options">反序列化选项</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>反序列化后的对象</returns>
        public static async Task<T?> ReadFromFileAsync<T>(string filePath,
            JsonSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("指定的 JSON 文件不存在", filePath);
            }

            try
            {
                using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return await JsonSerializer.DeserializeAsync<T>(fs, options ?? DefaultOptions, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"异步从文件反序列化对象失败: {ex.Message}", ex);
            }
        }
    }
}
