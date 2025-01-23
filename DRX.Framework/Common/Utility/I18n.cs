using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace DRX.Framework.Common.Utility;

/// <summary>
/// 提供国际化（i18n）支持，通过管理和检索字典中的本地化文本。
/// </summary>
public class I18N
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, string>>> dictionaries
        = new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, string>>>();

    private const string DefaultDict = "default";
    private static readonly string BasePath = DrxFile.I18DictPath;

    /// <summary>
    /// 静态构造函数，用于初始化默认字典。
    /// </summary>
    static I18N()
    {
        // 确保字典路径存在
        EnsurePath(BasePath);

        // 加载默认字典
        var defaultDictPath = Path.Combine(BasePath, "default.dict");
        if (System.IO.File.Exists(defaultDictPath))
        {
            try
            {
                Register("default", DefaultDict);
            }
            catch (FileNotFoundException)
            {
                // 已在 Register 中创建空的字典文件
            }
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
        var filePath = Path.Combine(BasePath, $"{fileName}.dict");
        EnsurePathAndFile(BasePath, $"{fileName}.dict");

        if (!System.IO.File.Exists(filePath))
        {
            // 创建一个空的 .dict 文件
            using (var fs = System.IO.File.Create(filePath))
            {
                // 定义默认内容
                var defaultEntries = new List<string>
                {
                    "## 请按照以下格式添加本地化文本",
                    "message.dis=\"与服务器断开连接\"",
                    "register.permission_not_enough_msg=\"权限不足\"",
                    "register.args_count_error_msg=\"应该存在 {a_count} 个参数，但是传入了 {a_count2} 个参数。\"",
                };

                // 将默认内容写入文件
                var content = string.Join(Environment.NewLine, defaultEntries);
                var bytes = Encoding.UTF8.GetBytes(content);
                fs.Write(bytes, 0, bytes.Length);
            }

            throw new FileNotFoundException($"未找到字典文件: {filePath}，已创建并写入默认内容。");
        }

        var dict = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();
        var lines = System.IO.File.ReadAllLines(filePath);

        Parallel.ForEach(lines, line =>
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || line.StartsWith("##")) return;

            var parts = line.Split('=');
            if (parts.Length != 2) return;

            var key = parts[0].Trim();
            var value = parts[1].Trim().Trim('"');

            var groupKey = key.Split('.');
            if (groupKey.Length != 2) return;

            var group = groupKey[0];
            var subKey = groupKey[1];

            // 使用线程安全的方式初始化组
            var groupDict = dict.GetOrAdd(group, _ => new ConcurrentDictionary<string, string>());
            // 直接添加键值对
            groupDict[subKey] = value;
        });

        // 添加到全局字典
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
    public static string GetText(string group, string key, string dictionaryName = DefaultDict, params Variable[] variables)
    {
        if (!dictionaries.TryGetValue(dictionaryName, out var dict) ||
            !dict.TryGetValue(group, out var groupDict) ||
            !groupDict.TryGetValue(key, out var text))
        {
            throw new KeyNotFoundException($"未找到字典: {dictionaryName}, 组: {group}, 键: {key}");
        }

        if (variables == null || variables.Length == 0)
        {
            return text;
        }

        var sb = new StringBuilder(text);
        foreach (var variable in variables)
        {
            sb.Replace($"{{{variable.Name}}}", variable.Value.ToString());
        }

        return sb.ToString();
    }

    /// <summary>
    /// 更新指定字典中指定组和键的本地化文本。
    /// </summary>
    /// <param name="group">字典中的组名。</param>
    /// <param name="key">组中的键名。</param>
    /// <param name="value">新的本地化文本。</param>
    /// <param name="dictionaryName">字典的名称。默认为 "default"。</param>
    public static void UpdateText(string group, string key, string value, string dictionaryName = DefaultDict)
    {
        var dict = dictionaries.GetOrAdd(dictionaryName, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>());
        var groupDict = dict.GetOrAdd(group, _ => new ConcurrentDictionary<string, string>());
        groupDict[key] = value;
    }

    /// <summary>
    /// 删除指定字典中指定组和键的本地化文本。
    /// </summary>
    /// <param name="group">字典中的组名。</param>
    /// <param name="key">组中的键名。</param>
    /// <param name="dictionaryName">字典的名称。默认为 "default"。</param>
    public static void RemoveText(string group, string key, string dictionaryName = DefaultDict)
    {
        if (dictionaries.TryGetValue(dictionaryName, out var dict) &&
            dict.TryGetValue(group, out var groupDict))
        {
            groupDict.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// 检查指定字典中是否存在指定组和键的本地化文本。
    /// </summary>
    /// <param name="group">字典中的组名。</param>
    /// <param name="key">组中的键名。</param>
    /// <param name="dictionaryName">字典的名称。默认为 "default"。</param>
    /// <returns>如果存在则返回 true，否则返回 false。</returns>
    public static bool ContainsText(string group, string key, string dictionaryName = DefaultDict)
    {
        return dictionaries.TryGetValue(dictionaryName, out var dict) &&
               dict.TryGetValue(group, out var groupDict) &&
               groupDict.ContainsKey(key);
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
    public static Dictionary<string, Dictionary<string, string>> GetAllTexts(string dictionaryName = DefaultDict)
    {
        if (!dictionaries.TryGetValue(dictionaryName, out var dict))
        {
            throw new KeyNotFoundException($"未找到字典: {dictionaryName}");
        }

        // 将 ConcurrentDictionary 转换为普通 Dictionary 以供返回
        return dict.ToDictionary(
            group => group.Key,
            group => group.Value.ToDictionary(
                item => item.Key,
                item => item.Value
            )
        );
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
        if (!System.IO.File.Exists(filePath))
        {
            System.IO.File.Create(filePath).Dispose();
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
    public object Value { get; set; }

    /// <summary>
    /// 初始化 <see cref="Variable"/> 类的新实例。
    /// </summary>
    /// <param name="name">变量的名称。</param>
    /// <param name="value">变量的值。</param>
    public Variable(string name, object value)
    {
        Name = name;
        Value = value;
    }
}

public static class I18Extensions
{
    public static string GetGroup(this string key, string group, params Variable[] variables)
        => I18N.GetText(group, key, variables: variables);

    public static string GetKey(this string group, string key, params Variable[] variables)
        => I18N.GetText(group, key, variables: variables);
}
