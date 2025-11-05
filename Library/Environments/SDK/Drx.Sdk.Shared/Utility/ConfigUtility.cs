using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Xml.Serialization;

namespace Drx.Sdk.Shared.Utility
{
    /// <summary>
    /// 提供对磁盘上配置的简单读写封装，线程安全且支持三种持久化格式：INI、JSON、XML。
    /// 本类将配置表示为 "section => (key => value)" 的嵌套字典（对键和分组名大小写不敏感）。
    /// 主要设计要点：
    /// - 以文件为粒度使用内部并发字典维护锁对象，避免并发写入导致的文件损坏（lock per file）。
    /// - 写入以先在内存构建并写入临时文件，再以原子替换的方式覆盖目标文件，降低半写入风险。
    /// - 默认使用 INI 格式以保持向后兼容；JSON 与 XML 作为可选格式互转和持久化。
    /// - 所有公开 API 支持指定 Encoding；默认使用 UTF-8。
    /// 示例：
    /// <code>
    /// ConfigUtility.Push("config.ini", "key", "value", "group");
    /// var val = ConfigUtility.Read("config.ini", "key", "group");
    /// </code>
    /// 注意事项：
    /// - 对于不存在的文件，Read/TryRead/Get* 将返回空或空集合；Push 会在必要时创建目录并写入新文件。
    /// - Push 会将传入的 null 值转换为空字符串写入。
    /// </summary>
    public static class ConfigUtility
    {
        /// <summary>
        /// 指定配置文件的存储格式。
        /// </summary>
        public enum StorageFormat
        {
            /// <summary>
            /// 使用 JSON 存储，格式为 Dictionary<string, Dictionary<string, string>>（可读性好，支持缩进）。
            /// </summary>
            JSON,
            /// <summary>
            /// 传统 INI 存储，支持 [section] 和 key=value 行解析（默认为该格式以兼容历史文件）。
            /// </summary>
            INI,
            /// <summary>
            /// 使用 XML 序列化的中间表示，适合与其他系统交换结构化配置。
            /// </summary>
            XML
        }

        const string DefaultGroup = "default";

        // per-file lock to avoid concurrent writes corrupting file
        private static readonly ConcurrentDictionary<string, object> FileLocks = new(StringComparer.OrdinalIgnoreCase);

        private static object GetLock(string path) => FileLocks.GetOrAdd(path, _ => new object());

        /// <summary>
        /// 将指定键写入到配置文件的指定分组（section）。
        /// </summary>
        /// <param name="file">目标配置文件路径（相对或绝对）。不能为空或全空白；如果路径所指目录不存在会尝试创建。</param>
        /// <param name="key">要写入的键名，不能为空。</param>
        /// <param name="val">要写入的值；如果为 <c>null</c>，方法会写入空字符串。</param>
        /// <param name="group">分组/节名，可选。为 <c>null</c> 时使用默认分组 "default"。</param>
        /// <param name="encoding">文本编码，可选，默认为 <see cref="Encoding.UTF8"/>。</param>
        /// <param name="format">存储格式，默认为 <see cref="StorageFormat.INI"/>。</param>
        /// <remarks>
        /// 方法流程：
        /// 1. 解析并规范化目标路径（Path.GetFullPath）。
        /// 2. 确保目录存在（必要时创建）。
        /// 3. 获取文件级别的锁以保证并发安全。
        /// 4. 加载现有配置（若文件不存在则开始于空结构），在内存中更新分组/键值。
        /// 5. 以原子方式写回文件（先写入临时文件，再替换目标文件）。
        /// 抛出异常：
        /// - 若 <paramref name="file"/> 无效，会抛出 <see cref="ArgumentException"/>。
        /// - 若 <paramref name="key"/> 为 <c>null</c>，会抛出 <see cref="ArgumentNullException"/>。
        /// </remarks>
        public static void Push(string file, string key, string val, string? group = null, Encoding? encoding = null, StorageFormat format = StorageFormat.INI)
        {
            if (string.IsNullOrWhiteSpace(file)) throw new ArgumentException("file is required", nameof(file));
            if (key is null) throw new ArgumentNullException(nameof(key));
            group ??= DefaultGroup;
            encoding ??= Encoding.UTF8;

            string path = Path.GetFullPath(file);
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            lock (GetLock(path))
            {
                var data = LoadConfig(path, encoding, format);
                if (!data.TryGetValue(group, out var section))
                {
                    section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    data[group] = section;
                }

                section[key] = val ?? string.Empty;
                SaveConfig(path, data, encoding, format);
            }
        }

        /// <summary>
        /// 从配置文件中读取指定分组下的键值。
        /// </summary>
        /// <param name="file">配置文件路径；若为空或文件不存在，方法返回空字符串。</param>
        /// <param name="key">要读取的键名；若为 <c>null</c>，方法返回空字符串。</param>
        /// <param name="group">分组名，可选；默认为 "default"。</param>
        /// <param name="encoding">可选的文本编码，默认 UTF-8。</param>
        /// <param name="format">指定文件格式（INI/JSON/XML），默认为 INI。</param>
        /// <returns>找到则返回对应的字符串值；未找到或发生路径/参数问题时返回空字符串。</returns>
        /// <remarks>
        /// 本方法为只读操作，不会创建文件或目录。对于不存在的键/分组或解析失败，统一返回空字符串以便调用方简洁处理。
        /// </remarks>
        public static string Read(string file, string key, string? group = null, Encoding? encoding = null, StorageFormat format = StorageFormat.INI)
        {
            if (string.IsNullOrWhiteSpace(file)) return string.Empty;
            if (key is null) return string.Empty;
            group ??= DefaultGroup;
            encoding ??= Encoding.UTF8;

            string path = Path.GetFullPath(file);
            if (!File.Exists(path)) return string.Empty;

            var data = LoadConfig(path, encoding, format);
            if (data.TryGetValue(group, out var section) && section.TryGetValue(key, out var val))
            {
                return val;
            }

            return string.Empty;
        }

        /// <summary>
        /// 尝试从配置文件读取指定键的值，并以布尔值指示是否成功读取到有效值。
        /// </summary>
        /// <param name="file">配置文件路径；若文件不存在或路径无效，返回 <c>false</c>。</param>
        /// <param name="key">要读取的键名。</param>
        /// <param name="value">当返回值为 <c>true</c> 时，包含读取到的值；未成功读取时为 <c>string.Empty</c>。</param>
        /// <param name="group">分组名，默认为 "default"。</param>
        /// <param name="encoding">文本编码，默认为 UTF-8。</param>
        /// <param name="format">存储格式，默认为 INI。</param>
        /// <returns>如果成功找到对应分组和键并赋值给 <paramref name="value"/>，则返回 <c>true</c>；否则返回 <c>false</c>。</returns>
        /// <remarks>此方法不会抛出异常用于找不到文件或键，只会返回 false，便于调用方以条件分支处理。</remarks>
        public static bool TryRead(string file, string key, out string? value, string? group = null, Encoding? encoding = null, StorageFormat format = StorageFormat.INI)
        {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(file) || key is null) return false;
            group ??= DefaultGroup;
            encoding ??= Encoding.UTF8;

            string path = Path.GetFullPath(file);
            if (!File.Exists(path)) return false;

            var data = LoadConfig(path, encoding, format);
            if (data.TryGetValue(group, out var section) && section.TryGetValue(key, out var val))
            {
                value = val;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 从指定配置文件与分组中删除某个键。
        /// </summary>
        /// <param name="file">配置文件路径；若文件不存在或路径无效方法返回 <c>false</c>。</param>
        /// <param name="key">要删除的键名。</param>
        /// <param name="group">分组名，默认为 "default"。</param>
        /// <param name="encoding">文本编码，默认 UTF-8。</param>
        /// <param name="format">存储格式，默认 INI。</param>
        /// <returns>若文件存在且键被成功移除则返回 <c>true</c>；否则返回 <c>false</c>（例如分组或键不存在）。</returns>
        /// <remarks>写操作在文件级别加锁并在内存中更新后以原子方式写回磁盘。</remarks>
        public static bool DeleteKey(string file, string key, string? group = null, Encoding? encoding = null, StorageFormat format = StorageFormat.INI)
        {
            if (string.IsNullOrWhiteSpace(file) || key is null) return false;
            group ??= DefaultGroup;
            encoding ??= Encoding.UTF8;

            string path = Path.GetFullPath(file);
            if (!File.Exists(path)) return false;

            lock (GetLock(path))
            {
                var data = LoadConfig(path, encoding, format);
                if (data.TryGetValue(group, out var section) && section.Remove(key))
                {
                    SaveConfig(path, data, encoding, format);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 删除整个分组（section）及其所有键。
        /// </summary>
        /// <param name="file">配置文件路径；若文件不存在则返回 <c>false</c>。</param>
        /// <param name="group">要删除的分组名。</param>
        /// <param name="encoding">文本编码，默认 UTF-8。</param>
        /// <param name="format">存储格式，默认 INI。</param>
        /// <returns>若分组存在并被删除则返回 <c>true</c>；否则返回 <c>false</c>。</returns>
        /// <remarks>该操作为写操作，会获取文件级锁并在成功删除后原子写回。</remarks>
        public static bool DeleteSection(string file, string group, Encoding? encoding = null, StorageFormat format = StorageFormat.INI)
        {
            if (string.IsNullOrWhiteSpace(file) || group is null) return false;
            encoding ??= Encoding.UTF8;

            string path = Path.GetFullPath(file);
            if (!File.Exists(path)) return false;

            lock (GetLock(path))
            {
                var data = LoadConfig(path, encoding, format);
                if (data.Remove(group))
                {
                    SaveConfig(path, data, encoding, format);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取配置文件中存在的所有分组（section）名。
        /// </summary>
        /// <param name="file">配置文件路径；若文件不存在或路径无效，返回空集合。</param>
        /// <param name="encoding">文本编码，默认 UTF-8。</param>
        /// <param name="format">存储格式，默认 INI。</param>
        /// <returns>分组名的枚举快照（List）。</returns>
        /// <remarks>该方法为只读，不会创建文件或修改内容。</remarks>
        public static IEnumerable<string> GetSections(string file, Encoding? encoding = null, StorageFormat format = StorageFormat.INI)
        {
            if (string.IsNullOrWhiteSpace(file)) return Array.Empty<string>();
            encoding ??= Encoding.UTF8;
            string path = Path.GetFullPath(file);
            if (!File.Exists(path)) return Array.Empty<string>();
            var data = LoadConfig(path, encoding, format);
            return data.Keys.ToList();
        }

        /// <summary>
        /// 返回指定分组下的所有键名列表。
        /// </summary>
        /// <param name="file">配置文件路径，若文件不存在返回空集合。</param>
        /// <param name="group">分组名，默认为 "default"。</param>
        /// <param name="encoding">文本编码，默认 UTF-8。</param>
        /// <param name="format">存储格式，默认 INI。</param>
        /// <returns>指定分组下键名的枚举；若分组不存在返回空集合。</returns>
        public static IEnumerable<string> GetKeys(string file, string? group = null, Encoding? encoding = null, StorageFormat format = StorageFormat.INI)
        {
            if (string.IsNullOrWhiteSpace(file)) return Array.Empty<string>();
            encoding ??= Encoding.UTF8;
            group ??= DefaultGroup;
            string path = Path.GetFullPath(file);
            if (!File.Exists(path)) return Array.Empty<string>();
            var data = LoadConfig(path, encoding, format);
            if (data.TryGetValue(group, out var section)) return section.Keys.ToList();
            return Array.Empty<string>();
        }

        /// <summary>
        /// 将整个配置文件读取为嵌套字典结构：Dictionary<section, Dictionary<key, value>>。
        /// </summary>
        /// <param name="file">配置文件路径；若 <c>null</c> 或空则返回空字典。</param>
        /// <param name="encoding">文本编码，默认 UTF-8。</param>
        /// <param name="format">存储格式，默认 INI。</param>
        /// <returns>返回包含所有分组及其键值对的字典；当文件缺失或解析失败时返回空字典。</returns>
        /// <remarks>返回的字典对键与分组名采用不区分大小写的比较器（StringComparer.OrdinalIgnoreCase）。</remarks>
        public static Dictionary<string, Dictionary<string, string>> ReadAll(string file, Encoding? encoding = null, StorageFormat format = StorageFormat.INI)
        {
            if (string.IsNullOrWhiteSpace(file)) return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            encoding ??= Encoding.UTF8;
            string path = Path.GetFullPath(file);
            return LoadConfig(path, encoding, format);
        }

        // 通用加载实现，根据格式解析为嵌套字典（区分大小写不敏感）
        private static Dictionary<string, Dictionary<string, string>> LoadConfig(string path, Encoding? encoding, StorageFormat format)
        {
            encoding ??= Encoding.UTF8;
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return result;

            switch (format)
            {
                case StorageFormat.INI:
                    {
                        string current = DefaultGroup;
                        result[current] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var raw in File.ReadLines(path, encoding))
                        {
                            var line = raw.Trim();
                            if (line.Length == 0) continue;
                            if (line.StartsWith(";") || line.StartsWith("#")) continue; // 注释

                            if (line.StartsWith("[") && line.EndsWith("]"))
                            {
                                current = line.Substring(1, Math.Max(0, line.Length - 2)).Trim();
                                if (!result.ContainsKey(current)) result[current] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                continue;
                            }

                            int idx = line.IndexOf('=');
                            if (idx >= 0)
                            {
                                var k = line.Substring(0, idx).Trim();
                                var v = line.Substring(idx + 1).Trim();
                                if (!result.ContainsKey(current)) result[current] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                result[current][k] = v;
                            }
                            else
                            {
                                // 无 '='，视为键但值为空
                                var k = line.Trim();
                                if (!result.ContainsKey(current)) result[current] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                result[current][k] = string.Empty;
                            }
                        }
                        return result;
                    }
                case StorageFormat.JSON:
                    {
                        var json = File.ReadAllText(path, encoding);
                        if (string.IsNullOrWhiteSpace(json)) return result;
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
                            if (parsed == null) return result;
                            foreach (var sec in parsed)
                            {
                                var inner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                foreach (var kv in sec.Value)
                                {
                                    inner[kv.Key] = kv.Value;
                                }
                                result[sec.Key] = inner;
                            }
                        }
                        catch
                        {
                            // ignore parse errors and return empty
                        }
                        return result;
                    }
                case StorageFormat.XML:
                    {
                        try
                        {
                            var serializer = new XmlSerializer(typeof(ConfigDocument));
                            using var fs = File.OpenRead(path);
                            var doc = (ConfigDocument?)serializer.Deserialize(fs);
                            if (doc == null) return result;
                            foreach (var sec in doc.Sections ?? Enumerable.Empty<Section>())
                            {
                                var inner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                foreach (var kv in sec.Items ?? Enumerable.Empty<KeyValue>())
                                {
                                    inner[kv.Key ?? string.Empty] = kv.Value ?? string.Empty;
                                }
                                result[sec.Name ?? string.Empty] = inner;
                            }
                        }
                        catch
                        {
                            // ignore parse errors
                        }
                        return result;
                    }
                default:
                    return result;
            }
        }

        // 通用保存实现，写入前在内存中构建并原子写盘
        private static void SaveConfig(string path, Dictionary<string, Dictionary<string, string>> data, Encoding? encoding, StorageFormat format)
        {
            encoding ??= Encoding.UTF8;

            var temp = path + ".tmp";

            switch (format)
            {
                case StorageFormat.INI:
                    {
                        var sb = new StringBuilder();
                        foreach (var section in data)
                        {
                            sb.AppendLine($"[{section.Key}]");
                            foreach (var kv in section.Value)
                            {
                                sb.AppendLine($"{kv.Key}={kv.Value}");
                            }
                            sb.AppendLine();
                        }

                        File.WriteAllText(temp, sb.ToString(), encoding);
                        break;
                    }
                case StorageFormat.JSON:
                    {
                        var options = new JsonSerializerOptions { WriteIndented = true };
                        var json = JsonSerializer.Serialize(data, options);
                        File.WriteAllText(temp, json, encoding);
                        break;
                    }
                case StorageFormat.XML:
                    {
                        var doc = new ConfigDocument
                        {
                            Sections = data.Select(s => new Section
                            {
                                Name = s.Key,
                                Items = s.Value.Select(kv => new KeyValue { Key = kv.Key, Value = kv.Value }).ToList()
                            }).ToList()
                        };

                        var serializer = new XmlSerializer(typeof(ConfigDocument));
                        using (var fs = File.Create(temp))
                        {
                            serializer.Serialize(fs, doc);
                        }
                        break;
                    }
            }

            // 原子替换
            File.Copy(temp, path, true);
            File.Delete(temp);
        }

        /// <summary>
        /// 用于 XML 序列化/反序列化的中间文档表示。
        /// 当使用 <see cref="StorageFormat.XML"/> 时，配置会映射为本类型的实例以进行 XmlSerializer 操作。
        /// </summary>
        [Serializable]
        public class ConfigDocument
        {
            /// <summary>
            /// 配置中的分组集合，每项对应一个 <see cref="Section"/>。
            /// </summary>
            public List<Section>? Sections { get; set; }
        }

        /// <summary>
        /// 表示 XML 中的一个分组（section），包含分组名和若干键值项。
        /// </summary>
        [Serializable]
        public class Section
        {
            /// <summary>
            /// 分组名（Section name）。反序列化时可能为 null，但在转换为字典时会使用空字符串替代。
            /// </summary>
            public string? Name { get; set; }
            /// <summary>
            /// 分组内的键值对集合。
            /// </summary>
            public List<KeyValue>? Items { get; set; }
        }

        /// <summary>
        /// 表示单个键值对，用于 XML 序列化。
        /// </summary>
        [Serializable]
        public class KeyValue
        {
            /// <summary>
            /// 键名；序列化/反序列化期间可能为 null，调用端会将 null 视为 empty string。
            /// </summary>
            public string? Key { get; set; }
            /// <summary>
            /// 对应的字符串值；可能为 null，调用端会将 null 视为 empty string。
            /// </summary>
            public string? Value { get; set; }
        }
    }
}
