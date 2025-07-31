using System;
using System.Collections.Concurrent;
using System.Threading;
using DRX.Framework;

namespace Drx.Sdk.Network.Extensions
{
    /// <summary>
    /// 控制台命令处理器，支持注册命令及其处理逻辑，并在后台线程监听控制台输入。
    /// </summary>
    public class ConsoleCommandProcessor
    {
        private readonly ConcurrentDictionary<string, Func<string[], object>> _commands = new();
        // 命令用法注册表
        private readonly Dictionary<string, Func<CommandInfoLine, bool>> _usages = new();
        /// <summary>
        /// 注册命令用法说明
        /// </summary>
        /// <param name="command">命令名</param>
        /// <param name="usageHandler">用法描述回调</param>
        public void RegisterUsage(string command, Func<CommandInfoLine, bool> usageHandler)
        {
            if (!string.IsNullOrWhiteSpace(command) && usageHandler != null)
            {
                _usages[command] = usageHandler;
            }
        }

        /// <summary>
        /// 获取命令用法说明
        /// </summary>
        /// <param name="command">命令名</param>
        /// <returns>用法描述字符串列表</returns>
        public List<string> GetUsage(string command)
        {
            var result = new List<string>();
            if (_usages.TryGetValue(command, out var handler))
            {
                var infoLine = new CommandInfoLine();
                handler(infoLine);
                result.AddRange(infoLine.Lines);
            }
            return result;
        }

        /// <summary>
        /// 获取所有命令的用法说明
        /// </summary>
        /// <returns>命令-用法描述映射</returns>
        public Dictionary<string, List<string>> GetAllUsages()
        {
            var dict = new Dictionary<string, List<string>>();
            foreach (var kv in _usages)
            {
                var infoLine = new CommandInfoLine();
                kv.Value(infoLine);
                dict[kv.Key] = new List<string>(infoLine.Lines);
            }
            return dict;
        }

        /// <summary>
        /// 用于收集命令用法描述的辅助类
        /// </summary>
        public class CommandInfoLine
        {
            public List<string> Lines { get; } = new List<string>();
            public void Add(string line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    Lines.Add(line);
            }
        }
        // 变量名 -> 指针/对象映射
        private readonly Dictionary<string, WeakReference<object>> _variablePointers = new Dictionary<string, WeakReference<object>>();
        private volatile bool _running = true;
        // 命令历史支持
        private readonly List<string> _history = new();
        private int _historyIndex = -1;


        /// <summary>
        /// 构造函数，自动注册script命令
        /// </summary>
        public ConsoleCommandProcessor()
        {
            RegisterCommand("script", ScriptCommandHandler);
            RegisterCommand("help", HelpCommandHandler);
        }

        /// <summary>
        /// help命令处理器，输出所有命令的usage信息
        /// </summary>
        private object HelpCommandHandler(string[] args)
        {
            var usages = GetAllUsages();
            if (usages.Count == 0)
            {
                Console.WriteLine("No command usage registered.");
                return true;
            }
            Console.WriteLine("Available commands:");
            foreach (var kv in usages)
            {
                Console.WriteLine($"\n[{kv.Key}]");
                foreach (var line in kv.Value)
                {
                    Console.WriteLine($"  {line}");
                }
            }
            return true;
        }

        /// <summary>
        /// 注册命令及其处理逻辑
        /// </summary>
        public void RegisterCommand(string command, Func<string[], object> handler)
        {
            if (!string.IsNullOrWhiteSpace(command) && handler != null)
            {
                _commands[command] = handler;
            }
        }

        /// <summary>
        /// script命令处理器，执行@script{...}脚本
        /// </summary>
        private object ScriptCommandHandler(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                Logger.Error("script命令缺少参数: 需要脚本文件路径");
                return false;
            }
            var filePath = args[0];
            if (!File.Exists(filePath))
            {
                Logger.Error($"脚本文件不存在: {filePath}");
                return false;
            }
            string content = File.ReadAllText(filePath);
            // 1. 解析所有 @变量名{...} 和 @变量名(参数...){...} 块
            var varDict = new Dictionary<string, string>(); // 静态文本变量
            int idx = 0;
            while (idx < content.Length)
            {
                if (content[idx] == '@')
                {
                    int nameStart = idx + 1;
                    int parenIdx = content.IndexOf('(', nameStart);
                    int braceIdx = content.IndexOf('{', nameStart);
                    // 动态变量: @varName(args...){...}
                    if (parenIdx > nameStart && braceIdx > parenIdx)
                    {
                        string varName = content.Substring(nameStart, parenIdx - nameStart).Trim();
                        int argsEnd = content.IndexOf(')', parenIdx);
                        if (!string.IsNullOrEmpty(varName) && argsEnd > parenIdx)
                        {
                            string argStr = content.Substring(parenIdx + 1, argsEnd - parenIdx - 1).Trim();
                            string[] dynArgs = string.IsNullOrWhiteSpace(argStr) ? Array.Empty<string>() : argStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            int blockStart = content.IndexOf('{', argsEnd);
                            int blockEnd = FindMatchingBrace(content, blockStart);
                            if (blockStart > 0 && blockEnd > blockStart)
                            {
                                string cmd = content.Substring(blockStart + 1, blockEnd - blockStart - 1).Trim();
                                // 执行命令，保存指针
                                object resultPtr = null;
                                if (!string.IsNullOrEmpty(cmd))
                                {
                                    // 只支持单命令（可扩展为多命令）
                                    var cmdParts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                    if (cmdParts.Length > 0)
                                    {
                                        var command = cmdParts[0];
                                        var cmdArgs = cmdParts.Length > 1 ? cmdParts[1..] : Array.Empty<string>();
                                        if (_commands.TryGetValue(command, out var handler))
                                        {
                                            resultPtr = handler(dynArgs.Length > 0 ? dynArgs : cmdArgs);
                                        }
                                    }
                                }
                                _variablePointers[varName] = new WeakReference<object>(resultPtr!);
                                idx = blockEnd + 1;
                                continue;
                            }
                        }
                    }
                    // 静态变量: @varName{...}
                    else if (braceIdx > nameStart)
                    {
                        string varName = content.Substring(nameStart, braceIdx - nameStart).Trim();
                        int blockStart = braceIdx + 1;
                        int blockEnd = FindMatchingBrace(content, braceIdx);
                        if (!string.IsNullOrEmpty(varName) && blockEnd > blockStart)
                        {
                            string varValue = content.Substring(blockStart, blockEnd - blockStart).Trim();
                            varDict[varName] = varValue;
                            _variablePointers[varName] = new WeakReference<object>(varValue!); // 静态变量也存指针表
                            idx = blockEnd + 1;
                            continue;
                        }
                    }
                }
                idx++;
            }

            // 2. 查找@script{...}块
            var start = content.IndexOf("@script{", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                int line = 1;
                for (int i = 0; i < content.Length; i++)
                {
                    if (content[i] == '\n') line++;
                }
                Logger.Error($"脚本内容格式错误，缺少@script{{...}} (大致在第1行)");
                return false;
            }
            start += "@script{".Length;
            var end = FindMatchingBrace(content, start - "@script{".Length);
            if (end < 0 || end <= start)
            {
                int line = 1;
                for (int i = 0; i < start; i++)
                {
                    if (content[i] == '\n') line++;
                }
                Logger.Error($"脚本内容格式错误，缺少结尾 '}}' (大致在第{line}行)");
                return false;
            }
            var scriptBlock = content.Substring(start, end - start).Trim();
            if (string.IsNullOrWhiteSpace(scriptBlock))
            {
                int line = 1;
                for (int i = 0; i < start; i++)
                {
                    if (content[i] == '\n') line++;
                }
                Logger.Error($"@script{{}}块内容为空 (大致在第{line}行)");
                return false;
            }
            // 3. 替换@script{}中的@变量名为变量内容（多行变量内容替换为单行，防止命令被拆分）
            foreach (var kv in _variablePointers)
            {
                string valueStr = kv.Value?.ToString() ?? string.Empty;
                valueStr = valueStr.Replace("\r", "").Replace("\n", " ");
                scriptBlock = scriptBlock.Replace($"@{kv.Key}", valueStr);
            }

            // 行号辅助
            var commands = scriptBlock.Split(new char[] { '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries);
            int baseLine = 1;
            for (int i = 0; i < start; i++)
            {
                if (content[i] == '\n') baseLine++;
            }
            for (int i = 0; i < commands.Length; i++)
            {
                var line = commands[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                {
                    Logger.Error($"脚本第{baseLine + i}行语法错误: 空命令");
                    continue;
                }
                var cmd = parts[0];
                var cmdArgs = parts.Length > 1 ? parts[1..] : Array.Empty<string>();
                if (_commands.TryGetValue(cmd, out var handler))
                {
                    try
                    {
                        var result = handler(cmdArgs);
                        if (result is bool b && !b)
                        {
                            Logger.Warn($"脚本命令执行失败: {line} (第{baseLine + i}行)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"脚本命令异常: {line} (第{baseLine + i}行) => {ex.Message}");
                    }
                }
                else
                {
                    Logger.Warn($"脚本中未知命令: {cmd} (第{baseLine + i}行)");
                }
            }
            return true;
        }

        /// <summary>
        /// 获取变量指针（对象引用），用于命令间高效传递
        /// </summary>
        public object GetVariablePointer(string varName)
        {
            if (_variablePointers.TryGetValue(varName, out var weakRef))
            {
                if (weakRef.TryGetTarget(out var target))
                    return target;
            }
            return null;
        }

        /// <summary>
        /// 清理已被GC回收的变量引用
        /// </summary>
        public void CleanupVariablePointers()
        {
            var keysToRemove = new List<string>();
            foreach (var kv in _variablePointers)
            {
                if (!kv.Value.TryGetTarget(out _))
                    keysToRemove.Add(kv.Key);
            }
            foreach (var key in keysToRemove)
                _variablePointers.Remove(key);
        }
        /// <summary>
        /// 查找匹配的 '}'，支持嵌套
        /// </summary>
        private int FindMatchingBrace(string text, int openBraceIdx)
        {
            int depth = 0;
            for (int i = openBraceIdx; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 提取@script{...}块内容
        /// </summary>
        private string ExtractScriptBlock(string content)
        {
            var start = content.IndexOf("@script{", StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;
            start += "@script{".Length;
            var end = content.LastIndexOf('}');
            if (end < 0 || end <= start) return null;
            return content.Substring(start, end - start).Trim();
        }

        /// <summary>
        /// 启动后台线程监听控制台输入
        /// </summary>
        public void Start()
        {
            var thread = new Thread(() =>
            {
                bool supportAdvancedInput = true;
                try
                {
                    // 检查是否支持高级输入（ReadKey/SetCursorPosition）
                    var origLeft = Console.CursorLeft;
                    var origTop = Console.CursorTop;
                    Console.SetCursorPosition(origLeft, origTop);
                    var _ = Console.KeyAvailable;
                }
                catch
                {
                    supportAdvancedInput = false;
                }
                if (!supportAdvancedInput)
                {
                    Logger.Warn("当前终端不支持高级命令行编辑，已切换为兼容模式（不支持历史/光标移动）");
                }
                while (_running)
                {
                    try
                    {
                        string input = supportAdvancedInput ? ReadLineWithHistory() : Console.ReadLine();
                        if (string.IsNullOrWhiteSpace(input)) continue;
                        // 记录历史
                        _history.Add(input);
                        _historyIndex = _history.Count;
                        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 0) continue;
                        var cmd = parts[0];
                        var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();
                        if (_commands.TryGetValue(cmd, out var handler))
                        {
                            var result = handler(args);
                            // 可根据 result 输出信息
                        }
                        else
                        {
                            Logger.Warn($"未知命令: {cmd}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"命令处理异常: {ex.Message}");
                    }
                }
            })
            {
                IsBackground = true
            };
            thread.Start();
        }

        /// <summary>
        /// 支持命令历史的自定义输入行，方向键上下切换历史
        /// </summary>
        private string ReadLineWithHistory()
        {
            var buffer = new List<char>();
            int cursor = 0;
            int localHistoryIndex = _history.Count;
            string currentLine = string.Empty;
            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                else if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (cursor > 0)
                    {
                        buffer.RemoveAt(cursor - 1);
                        cursor--;
                        RedrawInput(buffer, cursor);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.LeftArrow)
                {
                    if (cursor > 0)
                    {
                        cursor--;
                        Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.RightArrow)
                {
                    if (cursor < buffer.Count)
                    {
                        cursor++;
                        Console.SetCursorPosition(Console.CursorLeft + 1, Console.CursorTop);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.UpArrow)
                {
                    if (_history.Count > 0 && localHistoryIndex > 0)
                    {
                        localHistoryIndex--;
                        buffer.Clear();
                        buffer.AddRange(_history[localHistoryIndex]);
                        cursor = buffer.Count;
                        RedrawInput(buffer, cursor);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.DownArrow)
                {
                    if (_history.Count > 0 && localHistoryIndex < _history.Count - 1)
                    {
                        localHistoryIndex++;
                        buffer.Clear();
                        buffer.AddRange(_history[localHistoryIndex]);
                        cursor = buffer.Count;
                        RedrawInput(buffer, cursor);
                    }
                    else if (localHistoryIndex == _history.Count - 1)
                    {
                        localHistoryIndex = _history.Count;
                        buffer.Clear();
                        cursor = 0;
                        RedrawInput(buffer, cursor);
                    }
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    buffer.Insert(cursor, keyInfo.KeyChar);
                    cursor++;
                    RedrawInput(buffer, cursor);
                }
            }
            return new string(buffer.ToArray());
        }

        /// <summary>
        /// 重绘输入行
        /// </summary>
        private void RedrawInput(List<char> buffer, int cursor)
        {
            // 清除当前行
            int left = Console.CursorLeft;
            int top = Console.CursorTop;
            Console.SetCursorPosition(0, top);
            Console.Write(new string(' ', Console.BufferWidth - 1));
            Console.SetCursorPosition(0, top);
            Console.Write(new string(buffer.ToArray()));
            Console.SetCursorPosition(cursor, top);
        }

        /// <summary>
        /// 异步触发指定命令（用于代码调用，非控制台输入）。
        /// 兼容 RegisterCommand 注册的所有命令，自动将 object[] 参数转换为 string[]，确保与控制台输入一致。
        /// 若命令不存在或参数异常，将抛出详细异常并输出提示，便于调试和维护。
        /// 推荐用于程序内部自动化、脚本调用等场景。
        /// 示例用法：
        ///   await TriggerCommand("reload", new object[] { "config.json" });
        /// </summary>
        /// <param name="command">命令名称，需与注册时一致</param>
        /// <param name="args">参数列表（object[]，自动转换为 string[]，支持任意类型）</param>
        /// <returns>异步任务，执行命令处理逻辑</returns>
        public object TriggerCommand(string command, object[] args)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                Logger.Error("命令不能为空");
                throw new ArgumentException("命令不能为空", nameof(command));
            }

            if (!_commands.TryGetValue(command, out var handler))
            {
                Logger.Error($"未知命令: {command}");
                throw new InvalidOperationException($"未知命令: {command}");
            }

            // 将 object[] 转换为 string[]
            string[] strArgs = args?.Select(a => a?.ToString() ?? string.Empty).ToArray() ?? Array.Empty<string>();

            try
            {
                return handler(strArgs);
            }
            catch (Exception ex)
            {
                Logger.Error($"命令执行异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 停止命令处理器
        /// </summary>
        public void Stop()
        {
            _running = false;
        }
    }
}