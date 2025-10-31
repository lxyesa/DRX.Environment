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
    /// 简单且线程安全的配置工具，支持 INI/JSON/XML 三种存储格式。
    /// 用法示例：
    /// ConfigUtility.Push("config.ini", "key", "value", "group");
    /// var val = ConfigUtility.Read("config.ini", "key", "group");
    /// 默认使用 INI 格式以保证向后兼容。
    /// </summary>
    public static class ConfigUtility
    {
        public enum StorageFormat
        {
            JSON,
            INI,
            XML
        }

        const string DefaultGroup = "default";

        // per-file lock to avoid concurrent writes corrupting file
        private static readonly ConcurrentDictionary<string, object> FileLocks = new(StringComparer.OrdinalIgnoreCase);

        private static object GetLock(string path) => FileLocks.GetOrAdd(path, _ => new object());

        /// <summary>
        /// 将键值写入配置（支持 INI/JSON/XML）。val 为 null 则写入空字符串。
        /// </summary>
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
        ///读取键的值；不存在时返回空字符串。
        /// </summary>
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
        /// 尝试读取键的值，返回是否成功。
        /// </summary>
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
        /// 删除指定键，存在并删除成功返回 true，否则 false。
        /// </summary>
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
        /// 删除整个分组(section)，成功返回 true。
        /// </summary>
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
        /// 获取所有分组名
        /// </summary>
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
        /// 获取指定分组下的所有键
        /// </summary>
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
        ///读取整个配置为嵌套字典
        /// </summary>
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

        // 用于 XML 序列化的中间类
        [Serializable]
        public class ConfigDocument
        {
            public List<Section>? Sections { get; set; }
        }

        [Serializable]
        public class Section
        {
            public string? Name { get; set; }
            public List<KeyValue>? Items { get; set; }
        }

        [Serializable]
        public class KeyValue
        {
            public string? Key { get; set; }
            public string? Value { get; set; }
        }
    }
}
