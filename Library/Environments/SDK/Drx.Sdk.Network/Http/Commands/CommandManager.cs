using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Configs;

namespace Drx.Sdk.Network.Http.Commands
{
    /// <summary>
    /// 命令管理器，负责注册、解析和执行命令。
    /// 支持通过 CommandAttribute 特性注册命令或手动注册。
    /// </summary>
    public class CommandManager
    {
        /// <summary>
        /// 内部命令条目结构
        /// </summary>
        private class CommandEntry
        {
            public string Name { get; set; }
            public CommandAttribute Attribute { get; set; }
            public CommandParser Parser { get; set; }
            public MethodInfo Method { get; set; }
            public Type DeclaringType { get; set; }
        }

        private readonly List<CommandEntry> _commands = new();
        private readonly object _commandLock = new object();
        private static readonly ConcurrentDictionary<Type, MethodInfo[]> _methodCache = new();

        /// <summary>
        /// 从指定类型扫描所有带 CommandAttribute 的方法，自动注册命令。
        /// 使用缓存避免重复反射调用。
        /// </summary>
        public void RegisterCommandsFromType(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            try
            {
                // 使用缓存的反射结果
                if (!_methodCache.TryGetValue(type, out var methods))
                {
                    methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                    _methodCache.TryAdd(type, methods);
                }

                foreach (var method in methods)
                {
                    var commandAttrs = method.GetCustomAttributes(typeof(CommandAttribute), false).Cast<CommandAttribute>();
                    foreach (var attr in commandAttrs)
                    {
                        try
                        {
                            var parser = new CommandParser(attr.Format);
                            lock (_commandLock)
                            {
                                _commands.Add(new CommandEntry
                                {
                                    Name = parser.CommandName,
                                    Attribute = attr,
                                    Parser = parser,
                                    Method = method,
                                    DeclaringType = type
                                });
                            }
                            Logger.Info($"已注册命令: {parser.CommandName} - {attr.Description}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"注册命令 {attr.Format} 失败: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"从类型 {type.FullName} 扫描命令失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 手动注册一个命令
        /// </summary>
        public void RegisterCommand(string format, string category, string description, Delegate handler)
        {
            if (string.IsNullOrEmpty(format)) throw new ArgumentNullException(nameof(format));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            try
            {
                var parser = new CommandParser(format);
                var method = handler.Method;
                var type = handler.Target?.GetType() ?? method.DeclaringType;

                lock (_commandLock)
                {
                    _commands.Add(new CommandEntry
                    {
                        Name = parser.CommandName,
                        Attribute = new CommandAttribute(format, category, description),
                        Parser = parser,
                        Method = method,
                        DeclaringType = type!
                    });
                }
                Logger.Info($"已注册命令: {parser.CommandName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"手动注册命令 {format} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行命令，返回执行结果（字符串形式）。
        /// 若命令不存在或执行出错，返回错误消息。
        /// </summary>
        public string ExecuteCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "错误：命令不能为空";

            try
            {
                // 分解输入为命令名和参数
                var tokens = TokenizeInput(input);
                if (tokens.Count == 0)
                    return "错误：无效的命令格式";

                var commandName = tokens[0].ToLower();
                var args = tokens.Skip(1).ToList();

                // 查找匹配的命令
                CommandEntry entry;
                lock (_commandLock)
                {
                    entry = _commands.FirstOrDefault(c => string.Equals(c.Name, commandName, StringComparison.OrdinalIgnoreCase));
                }

                if (entry == null)
                    return $"错误：命令 '{commandName}' 不存在。输入 'help' 获取帮助。";

                // 验证参数
                var parseResult = entry.Parser.Parse(args);
                if (!parseResult.IsValid)
                    return $"错误: {parseResult.ErrorMessage}\n用法: {entry.Parser.GetUsage()}";

                // 构建方法调用参数
                var methodParams = entry.Method.GetParameters();
                var invokeArgs = new object[methodParams.Length];

                for (int i = 0; i < methodParams.Length; i++)
                {
                    var paramType = methodParams[i].ParameterType;
                    var paramName = methodParams[i].Name ?? "";

                    if (!parseResult.Parameters.TryGetValue(paramName, out var value))
                    {
                        // 尝试忽略大小写查找
                        var key = parseResult.Parameters.Keys.FirstOrDefault(k => 
                            string.Equals(k, paramName, StringComparison.OrdinalIgnoreCase));
                        if (key != null)
                            value = parseResult.Parameters[key];
                        else
                            value = null;
                    }

                    // 类型转换
                    if (value != null)
                    {
                        invokeArgs[i] = Convert.ChangeType(value, paramType);
                    }
                    else if (paramType.IsValueType && Nullable.GetUnderlyingType(paramType) == null)
                    {
                        // 非可空值类型，使用默认值
                        invokeArgs[i] = Activator.CreateInstance(paramType)!;
                    }
                    else
                    {
                        invokeArgs[i] = null!;
                    }
                }

                // 执行方法
                try
                {
                    object instance = null;
                    if (!entry.Method.IsStatic && entry.DeclaringType != null)
                    {
                        instance = Activator.CreateInstance(entry.DeclaringType);
                    }

                    var result = entry.Method.Invoke(instance, invokeArgs);

                    // 处理异步方法
                    if (result is Task task)
                    {
                        task.Wait();
                        var resultProp = task.GetType().GetProperty("Result");
                        result = resultProp?.GetValue(task);
                    }

                    return result?.ToString() ?? $"命令 '{commandName}' 执行成功。";
                }
                catch (TargetInvocationException tie)
                {
                    Logger.Error($"执行命令 '{commandName}' 时发生异常: {tie.InnerException?.Message}");
                    return $"错误：命令执行失败：{tie.InnerException?.Message}";
                }
                catch (Exception ex)
                {
                    Logger.Error($"执行命令 '{commandName}' 时发生异常: {ex.Message}");
                    return $"错误：命令执行失败：{ex.Message}";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"处理命令输入时发生异常: {ex.Message}");
                return $"错误：处理命令失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 获取所有已注册的命令列表（用于帮助显示）
        /// </summary>
        public List<(string CommandName, string Format, string Category, string Description)> GetAllCommands()
        {
            lock (_commandLock)
            {
                return _commands.Select(c => (c.Name, c.Attribute.Format, c.Attribute.Category, c.Attribute.Description)).ToList();
            }
        }

        /// <summary>
        /// 获取帮助文本
        /// </summary>
        public string GetHelpText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("可用命令：");
            sb.AppendLine();

            lock (_commandLock)
            {
                var groupedByCategory = _commands.GroupBy(c => c.Attribute.Category);
                foreach (var group in groupedByCategory)
                {
                    sb.AppendLine($"【{group.Key}】");
                    foreach (var cmd in group)
                    {
                        sb.AppendLine($"  {cmd.Attribute.Format,-40} {cmd.Attribute.Description}");
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 将输入字符串分解为令牌，支持带引号的参数
        /// </summary>
        private List<string> TokenizeInput(string input)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            foreach (var ch in input)
            {
                if (ch == '"' && (current.Length == 0 || current[current.Length - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                }
                else if (char.IsWhiteSpace(ch) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }

            if (current.Length > 0)
                tokens.Add(current.ToString());

            return tokens;
        }
    }
}
