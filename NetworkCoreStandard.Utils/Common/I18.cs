using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// 提供国际化（i18n）支持，通过管理和检索字典中的本地化文本。
/// </summary>
public class I18
{
    private static readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> dictionaries
        = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

    private const string DEFAULT_DICT = "default";
    private static readonly string basePath = NetworkCoreStandard.Utils.File.I18DictPath;

    /// <summary>
    /// 静态构造函数，用于初始化默认字典。
    /// </summary>
    static I18()
    {
        // 确保字典路径存在
        EnsurePath(basePath);

        // 加载默认字典
        if (File.Exists(Path.Combine(basePath, "default.dict")))
        {
            Register("default", DEFAULT_DICT);
        }
    }

    /// <summary>
    /// 从文件中注册字典。
    /// </summary>
    /// <param name="fileName">包含字典的文件名。</param>
    /// <param name="dictionaryName">要分配给字典的名称。</param>
    /// <exception cref="FileNotFoundException">当找不到字典文件时抛出。</exception>
    public static void Register(string fileName, string dictionaryName)
    {
        var filePath = Path.Combine(basePath, $"{fileName}.dict");
        EnsurePathAndFile(basePath, $"{fileName}.dict");

        if (!File.Exists(filePath))
        {
            // 创建一个空的 .dict 文件
            File.Create(filePath).Dispose();
            throw new FileNotFoundException($"未找到字典文件: {filePath}，已创建空的字典文件。");
        }

        var dict = new Dictionary<string, Dictionary<string, string>>();
        var lines = File.ReadAllLines(filePath);

        Parallel.ForEach(lines, line =>
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) return;

            var parts = line.Split('=');
            if (parts.Length != 2) return;

            var key = parts[0].Trim();
            var value = parts[1].Trim().Trim('"');

            var groupKey = key.Split('.');
            if (groupKey.Length != 2) return;

            lock (dict)
            {
                if (!dict.ContainsKey(groupKey[0]))
                {
                    dict[groupKey[0]] = new Dictionary<string, string>();
                }

                dict[groupKey[0]][groupKey[1]] = value;
            }
        });

        dictionaries[dictionaryName] = dict;
    }

    /// <summary>
    /// 从指定字典中检索指定组和键的本地化文本。
    /// </summary>
    /// <param name="group">字典中的组名。</param>
    /// <param name="key">组中的键名。</param>
    /// <param name="dictionaryName">字典的名称。默认为 "default"。</param>
    /// <param name="variables">可选的变量数组，用于替换文本中的占位符。</param>
    /// <returns>本地化文本。</returns>
    /// <exception cref="KeyNotFoundException">当找不到字典、组或键时抛出。</exception>
    public static string GetText(string group, string key, string dictionaryName = DEFAULT_DICT, params Variable[] variables)
    {
        if (!dictionaries.ContainsKey(dictionaryName))
        {
            throw new KeyNotFoundException($"未找到字典: {dictionaryName}");
        }

        var dict = dictionaries[dictionaryName];
        if (!dict.ContainsKey(group))
        {
            throw new KeyNotFoundException($"未找到组: {group}");
        }

        if (!dict[group].ContainsKey(key))
        {
            throw new KeyNotFoundException($"未找到键: {key}");
        }

        var text = dict[group][key];

        // 替换文本中的占位符
        foreach (var variable in variables)
        {
            text = text.Replace($"{{{variable.Name}}}", variable.Value);
        }

        return text;
    }

    /// <summary>
    /// 更新指定字典中指定组和键的本地化文本。
    /// </summary>
    /// <param name="group">字典中的组名。</param>
    /// <param name="key">组中的键名。</param>
    /// <param name="value">新的本地化文本。</param>
    /// <param name="dictionaryName">字典的名称。默认为 "default"。</param>
    public static void UpdateText(string group, string key, string value, string dictionaryName = DEFAULT_DICT)
    {
        if (!dictionaries.ContainsKey(dictionaryName))
        {
            dictionaries[dictionaryName] = new Dictionary<string, Dictionary<string, string>>();
        }

        var dict = dictionaries[dictionaryName];
        if (!dict.ContainsKey(group))
        {
            dict[group] = new Dictionary<string, string>();
        }

        dict[group][key] = value;
    }

    /// <summary>
    /// 删除指定字典中指定组和键的本地化文本。
    /// </summary>
    /// <param name="group">字典中的组名。</param>
    /// <param name="key">组中的键名。</param>
    /// <param name="dictionaryName">字典的名称。默认为 "default"。</param>
    public static void RemoveText(string group, string key, string dictionaryName = DEFAULT_DICT)
    {
        if (dictionaries.ContainsKey(dictionaryName) && dictionaries[dictionaryName].ContainsKey(group))
        {
            dictionaries[dictionaryName][group].Remove(key);
        }
    }

    /// <summary>
    /// 检查指定字典中是否存在指定组和键的本地化文本。
    /// </summary>
    /// <param name="group">字典中的组名。</param>
    /// <param name="key">组中的键名。</param>
    /// <param name="dictionaryName">字典的名称。默认为 "default"。</param>
    /// <returns>如果存在则返回 true，否则返回 false。</returns>
    public static bool ContainsText(string group, string key, string dictionaryName = DEFAULT_DICT)
    {
        return dictionaries.ContainsKey(dictionaryName) &&
               dictionaries[dictionaryName].ContainsKey(group) &&
               dictionaries[dictionaryName][group].ContainsKey(key);
    }

    /// <summary>
    /// 获取所有字典的名称。
    /// </summary>
    /// <returns>字典名称的列表。</returns>
    public static List<string> GetAllDictionaryNames()
    {
        return dictionaries.Keys.ToList();
    }

    /// <summary>
    /// 获取指定字典中所有组和键的本地化文本。
    /// </summary>
    /// <param name="dictionaryName">字典的名称。默认为 "default"。</param>
    /// <returns>包含所有组和键的本地化文本的字典。</returns>
    public static Dictionary<string, Dictionary<string, string>> GetAllTexts(string dictionaryName = DEFAULT_DICT)
    {
        if (!dictionaries.ContainsKey(dictionaryName))
        {
            throw new KeyNotFoundException($"未找到字典: {dictionaryName}");
        }

        return dictionaries[dictionaryName];
    }

    /// <summary>
    /// 检查路径是否存在，如果不存在则创建路径。
    /// </summary>
    /// <param name="path">要检查的路径。</param>
    private static void EnsurePath(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    /// <summary>
    /// 检查路径是否存在，如果不存在则创建路径和文件。
    /// </summary>
    /// <param name="path">要检查的路径。</param>
    /// <param name="fileName">要创建的文件名。</param>
    private static void EnsurePathAndFile(string path, string fileName)
    {
        EnsurePath(path);

        var filePath = Path.Combine(path, fileName);
        if (!File.Exists(filePath))
        {
            File.Create(filePath).Dispose();
        }
    }
}

/// <summary>
/// 表示一个变量，用于替换文本中的占位符。
/// </summary>
public class Variable
{
    /// <summary>
    /// 获取或设置变量的名称。
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 获取或设置变量的值。
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// 初始化 <see cref="Variable"/> 类的新实例。
    /// </summary>
    /// <param name="name">变量的名称。</param>
    /// <param name="value">变量的值。</param>
    public Variable(string name, string value)
    {
        Name = name;
        Value = value;
    }
}

public static class I18Extensions
{
    public static string I18n(this string key, string group, params Variable[] variables)
        => I18.GetText(group, key, variables: variables);
}
