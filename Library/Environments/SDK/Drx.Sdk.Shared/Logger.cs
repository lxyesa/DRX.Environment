using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Reflection;
using System.Linq;

namespace Drx.Sdk.Shared;

/// <summary>
/// 提供面向控制台与 WPF 文本控件的高性能异步日志记录功能，支持分级着色与批量写入。
/// </summary>
public static class Logger
{
    private static readonly Channel<LogEntry> _channel = Channel.CreateBounded<LogEntry>(
        new BoundedChannelOptions(4096)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private static readonly CancellationTokenSource _cts = new();
    private static readonly Task _worker;

    private static readonly IReadOnlyDictionary<LogLevel, ConsoleColor> ConsoleColors = new Dictionary<LogLevel, ConsoleColor>
    {
        [LogLevel.Dbug] = ConsoleColor.Gray,
        [LogLevel.Info] = ConsoleColor.DarkGreen,
        [LogLevel.Warn] = ConsoleColor.Yellow,
        [LogLevel.Fail] = ConsoleColor.Red,
        [LogLevel.Fatal] = ConsoleColor.DarkRed
    };


    private const string DateTimeFormat = "yyyy/MM/dd HH:mm:ss";


    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    static Logger()
    {
#if DEBUG
        AllocConsole();
#endif
        _worker = Task.Run(ProcessQueueAsync);
        ConsoleInterceptor.Initialize();
        RegisterCrashHandlers();
    }





    /// <summary>
    /// 以信息级别记录一条日志。
    /// </summary>
    /// <param name="header">日志头部/来源。</param>
    /// <param name="message">日志正文。</param>
    /// <param name="lineNumber">调用处行号，自动填充。</param>
    /// <param name="filePath">调用处文件路径，自动填充。</param>
    public static void Log(string header, string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Info, header, message, null, className, lineNumber, filePath);
    }

    /// <summary>
    /// 以信息级别记录一条日志（支持Text对象）。
    /// </summary>
    /// <param name="header">日志头部/来源。</param>
    /// <param name="text">带样式的文本对象。</param>
    /// <param name="lineNumber">调用处行号，自动填充。</param>
    /// <param name="filePath">调用处文件路径，自动填充。</param>
    public static void Log(string header, Text text, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Info, header, null, text, className, lineNumber, filePath);
    }

    /// <summary>
    /// 以指定级别记录一条日志。
    /// </summary>
    /// <param name="level">日志级别。</param>
    /// <param name="header">日志头部/来源。</param>
    /// <param name="message">日志正文。</param>
    /// <param name="lineNumber">调用处行号，自动填充。</param>
    /// <param name="filePath">调用处文件路径，自动填充。</param>
    public static void Log(LogLevel level, string header, string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(level, header, message, null, className, lineNumber, filePath);
    }

    /// <summary>
    /// 以指定级别记录一条日志（支持Text对象）。
    /// </summary>
    /// <param name="level">日志级别。</param>
    /// <param name="header">日志头部/来源。</param>
    /// <param name="text">带样式的文本对象。</param>
    /// <param name="lineNumber">调用处行号，自动填充。</param>
    /// <param name="filePath">调用处文件路径，自动填充。</param>
    public static void Log(LogLevel level, string header, Text text, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(level, header, null, text, className, lineNumber, filePath);
    }

    /// <summary>
    /// 以错误级别记录一条日志。
    /// </summary>
    /// <param name="message">日志正文。</param>
    /// <param name="lineNumber">调用处行号，自动填充。</param>
    /// <param name="filePath">调用处文件路径，自动填充。</param>
    public static void Error(string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Fail, className, message, null, className, lineNumber, filePath);
    }

    /// <summary>
    /// 以错误级别记录一条日志（支持Text对象）。
    /// </summary>
    /// <param name="text">带样式的文本对象。</param>
    /// <param name="lineNumber">调用处行号，自动填充。</param>
    /// <param name="filePath">调用处文件路径，自动填充。</param>
    public static void Error(Text text, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Fail, className, null, text, className, lineNumber, filePath);
    }

    /// <summary>
    /// 以调试级别记录一条日志。
    /// </summary>
    /// <param name="message">日志正文。</param>
    /// <param name="lineNumber">调用处行号，自动填充。</param>
    /// <param name="filePath">调用处文件路径，自动填充。</param>
    public static void Debug(string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Dbug, className, message, null, className, lineNumber, filePath);
    }

    /// <summary>
    /// 以调试级别记录一条日志（支持Text对象）。
    /// </summary>
    /// <param name="text">带样式的文本对象。</param>
    /// <param name="lineNumber">调用处行号，自动填充。</param>
    /// <param name="filePath">调用处文件路径，自动填充。</param>
    public static void Debug(Text text, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Dbug, className, null, text, className, lineNumber, filePath);
    }

    /// <summary>
    /// 以警告级别记录一条日志。
    /// </summary>
    /// <param name="message">日志正文。</param>
    /// <param name="lineNumber">调用处行号，自动填充。</param>
    /// <param name="filePath">调用处文件路径，自动填充。</param>
    public static void Warn(string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Warn, className, message, null, className, lineNumber, filePath);
    }

    /// <summary>
    /// 以警告级别记录一条日志（支持Text对象）。
    /// </summary>
    /// <param name="text">带样式的文本对象。</param>
    /// <param name="lineNumber">调用处行号，自动填充。</param>
    /// <param name="filePath">调用处文件路径，自动填充。</param>
    public static void Warn(Text text, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Warn, className, null, text, className, lineNumber, filePath);
    }

    /// <summary>
    /// 以信息级别记录一条日志（简化重载）。
    /// </summary>
    /// <param name="message">日志正文。</param>
    /// <param name="lineNumber">调用处行号，自动填充。</param>
    /// <param name="filePath">调用处文件路径，自动填充。</param>
    public static void Info(string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Info, className, message, null, className, lineNumber, filePath);
    }

    /// <summary>
    /// 以信息级别记录一条日志（支持Text对象）。
    /// </summary>
    /// <param name="text">带样式的文本对象。</param>
    /// <param name="lineNumber">调用处行号，自动填充。</param>
    /// <param name="filePath">调用处文件路径，自动填充。</param>
    public static void Info(Text text, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Info, className, null, text, className, lineNumber, filePath);
    }

    /// <summary>
    /// 以 Trace（映射到调试级别）记录一条日志，兼容外部调用 Logger.Trace(...)。
    /// </summary>
    /// <param name="message">日志正文。</param>
    /// <param name="lineNumber">调用处行号，自动填充。</param>
    /// <param name="filePath">调用处文件路径，自动填充。</param>
    public static void Trace(string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        // 按需求将 Trace 映射为调试级别
        Enqueue(LogLevel.Dbug, className, message, null, className, lineNumber, filePath);
    }

    /// <summary>
    /// 以 Trace（映射到调试级别）记录一条日志（支持Text对象），兼容外部调用 Logger.Trace(...)。
    /// </summary>
    /// <param name="text">带样式的文本对象。</param>
    /// <param name="lineNumber">调用处行号，自动填充。</param>
    /// <param name="filePath">调用处文件路径，自动填充。</param>
    public static void Trace(Text text, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        // 按需求将 Trace 映射为调试级别
        Enqueue(LogLevel.Dbug, className, null, text, className, lineNumber, filePath);
    }

    private static string GetShortClassName(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return "UnknownClass";
        var name = System.IO.Path.GetFileNameWithoutExtension(filePath);
        return name;
    }

    private static void Enqueue(LogLevel level, string header, string? message, Text? text, string className, int lineNumber, string filePath)
    {
        if (string.IsNullOrEmpty(message) && text == null) return;
        // 检查是否为非托管模块调用
        string moduleInfo = "";
        bool isUnmanagedCall = false;
        try
        {
            var assembly = Assembly.GetCallingAssembly();
            if (assembly == null)
            {
                isUnmanagedCall = true;
            }
        }
        catch
        {
            isUnmanagedCall = true;
        }
        if (isUnmanagedCall)
        {
            string moduleName = "NonManaged";
            moduleInfo = $" [{moduleName}]";
            if (level == LogLevel.Dbug)
            {
                moduleInfo += " [Address: N/A]";
            }
        }
        header += moduleInfo;
        var entry = new LogEntry
        {
            Level = level,
            Header = header,
            Message = message,
            Text = text,
            ClassName = className,
            LineNumber = lineNumber,
            Time = DateTime.Now
        };
        _channel.Writer.TryWrite(entry);
    }

    private static async Task ProcessQueueAsync()
    {
        var token = _cts.Token;
        const int BatchMax = 256;
        const int TimeWindowMs = 3;

        var batch = new List<LogEntry>(BatchMax);
        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(token))
            {
                batch.Add(item);

                var sw = Stopwatch.StartNew();
                while (batch.Count < BatchMax && sw.ElapsedMilliseconds <= TimeWindowMs && _channel.Reader.TryRead(out var more))
                {
                    batch.Add(more);
                }

                try
                {
                    WriteBatch(batch);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [ERROR] 日志系统异常: {ex.Message}");
                }
                finally
                {
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [ERROR] 日志系统异常: {ex.Message}");
        }
    }

    private static string FormatLog(in LogEntry log, StringBuilder sb)
    {
        sb.Clear();
        sb.Append('[')
          .Append(log.Time.ToString(DateTimeFormat))
          .Append("][")
          .Append(log.ClassName)
          .Append(':')
          .Append(log.LineNumber)
          .Append("][")
          .Append(log.Level.ToString().ToUpper())
          .Append(']')
          .Append(log.Text != null ? log.Text.ToString() : log.Message);
        return sb.ToString();
    }

    private static void WriteBatch(IReadOnlyList<LogEntry> batch)
    {
        if (batch == null || batch.Count == 0) return;

        // 重用一个 StringBuilder 减少分配，按批次逐条格式化并输出
        var sb = new StringBuilder(256);
        var originalColor = Console.ForegroundColor;

        for (int i = 0; i < batch.Count; i++)
        {
            var log = batch[i];

            // 如果是Text对象，直接输出（已包含ANSI转义码和重置）
            if (log.Text != null)
            {
                // 在输出Text对象前，先重置到默认颜色，确保Text的颜色设置从干净的状态开始
                Console.ForegroundColor = originalColor;
                Console.WriteLine(FormatLog(log, sb));
                // Text对象已经包含了重置序列(\e[0m)，这里不再需要额外重置
                continue;
            }

            // 普通字符串消息，使用原有颜色逻辑
            var line = FormatLog(log, sb);
            var lvl = log.Level;
            var color = ConsoleColors.TryGetValue(lvl, out var cc) ? cc : ConsoleColor.White;

            Console.ForegroundColor = color;
            Console.WriteLine(line);
            Console.ForegroundColor = originalColor;
        }
    }


    private static void WriteBatchToConsole(string[] formatted, LogLevel[] levels)
    {
        if (formatted.Length == 0) return;

        var originalColor = Console.ForegroundColor;

        ConsoleColor? currentColor = null;
        LogLevel? currentLevel = null;

        for (int i = 0; i < formatted.Length; i++)
        {
            var lvl = levels[i];
            var color = ConsoleColors.TryGetValue(lvl, out var cc) ? cc : ConsoleColor.White;

            if (currentLevel != lvl)
            {
                if (currentColor != color)
                {
                    Console.ForegroundColor = color;
                    currentColor = color;
                }
                currentLevel = lvl;
            }

            Console.WriteLine(formatted[i]);
        }

        Console.ForegroundColor = originalColor;
    }

    /// <summary>
    /// 注册全局未处理异常与未观察任务异常的处理器，确保崩溃信息输出到控制台与日志队列。
    /// </summary>
    private static void RegisterCrashHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                if (e.ExceptionObject is Exception ex)
                    LogFatalException("UnhandledException", ex);
                else
                    Console.Error.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [FATAL] Unhandled non-Exception object: {e.ExceptionObject}");
            }
            catch
            {
                try { Console.Error.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [FATAL] Failed to handle UnhandledException."); } catch { }
            }
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            try
            {
                LogFatalException("UnobservedTaskException", e.Exception);
                e.SetObserved();
            }
            catch
            {
                try { Console.Error.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [FATAL] Failed to handle UnobservedTaskException."); } catch { }
            }
        };
    }

    private static void LogFatalException(string source, Exception ex)
    {
        try
        {
            var sb = new StringBuilder(512);
            var current = ex;
            int depth = 0;
            while (current != null && depth < 10)
            {
                sb.AppendLine($"{current.GetType().FullName}: {current.Message}");
                if (!string.IsNullOrEmpty(current.StackTrace))
                    sb.AppendLine(current.StackTrace);
                current = current.InnerException;
                depth++;
                if (current != null)
                    sb.AppendLine("---- Inner Exception ----");
            }

            var message = sb.ToString().TrimEnd();
            var header = source;

            // 直接输出到控制台错误流，便于崩溃时观察
            try
            {
                Console.Error.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [FATAL] {header} - {message}");
            }
            catch
            {
                // 若控制台不可用则忽略
            }

            // 同时入队到异步日志系统，以便持久化或统一处理
            Enqueue(LogLevel.Fatal, header, message, null, header, 0, "");
        }
        catch
        {
            try
            {
                Console.Error.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [FATAL] Exception while logging fatal exception.");
            }
            catch { }
        }
    }
}

/// <summary>
/// 控制台输出拦截器，负责将控制台输出同步写入日志文件。
/// </summary>
public sealed class ConsoleInterceptor : TextWriter
{
    private readonly TextWriter _originalWriter;
    private static readonly Channel<string> _channel = Channel.CreateBounded<string>(
        new BoundedChannelOptions(16384)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    private static StreamWriter? _logWriter;
    private static Task? _bgTask;
    private static readonly ThreadLocal<StringBuilder> _tls = new(() => new StringBuilder(256));
    /// <summary>
    /// 控制是否将控制台输出写入文件。默认启用。
    /// 注意：该开关应在调用 <see cref="Initialize"/> 或在 <see cref="Logger"/> 第一次被引用之前设置，
    /// 否则 <see cref="ConsoleInterceptor.Initialize"/> 可能已创建文件写入器并启动后台任务。
    /// </summary>
    public static bool EnableFileLogging { get; set; } = true;
    /// <inheritdoc />
    public override Encoding Encoding => Encoding.UTF8;

    /// <summary>
    /// 初始化 <see cref="ConsoleInterceptor"/> 的新实例。
    /// </summary>
    /// <param name="originalWriter">原始控制台写入器。</param>
    /// <param name="logFilePath">日志文件路径（构造函数保留参数，实际由 Initialize 管理）。</param>
    public ConsoleInterceptor(TextWriter originalWriter, string logFilePath)
    {
        _originalWriter = originalWriter;
    }

    /// <inheritdoc />
    public override void Write(char value)
    {
        // 按行缓冲，避免每字符同步写回导致大量系统调用和性能损失
        var sb = _tls.Value!;
        sb.Append(value);
        if (value == '\n')
        {
            // 完成一行，去除末尾可能的 '\r' 和 '\n'
            var line = sb.ToString().TrimEnd('\r', '\n');
            // 先写回原始控制台的一整行，保持输出可见性
            try
            {
                _originalWriter.WriteLine(line);
            }
            catch
            {
                // 忽略原始写入错误，保证不抛出
            }

            // 再异步入队写文件（根据开关决定）
            if (EnableFileLogging && _logWriter != null)
            {
                _channel.Writer.TryWrite(line);
            }
            sb.Clear();
        }
    }

    /// <inheritdoc />
    public override void Write(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        var sb = _tls.Value!;
        int start = 0;
        while (true)
        {
            int idx = value.IndexOf('\n', start);
            if (idx < 0)
            {
                // 没有更多换行，追加剩余部分到线程缓存
                sb.Append(value, start, value.Length - start);
                break;
            }

            // 有换行：追加该片段并组成完整行
            int len = idx - start;
            if (len > 0)
                sb.Append(value, start, len);

            var line = sb.ToString().TrimEnd('\r');

            // 先写回原始控制台的一整行
            try
            {
                _originalWriter.WriteLine(line);
            }
            catch
            {
            }

            // 再入队写入日志文件（根据开关决定）
            if (EnableFileLogging && _logWriter != null)
            {
                _channel.Writer.TryWrite(line);
            }
            sb.Clear();

            start = idx + 1;
            if (start >= value.Length) break;
        }
    }

    /// <inheritdoc />
    public override void WriteLine(string? value)
    {
        var line = value ?? string.Empty;

        // 使用线程缓存以兼容之前的 Write 累计逻辑
        var sb = _tls.Value!;
        sb.Append(line);

        var final = sb.ToString();

        try
        {
            _originalWriter.WriteLine(final);
        }
        catch
        {
        }

        if (EnableFileLogging && _logWriter != null)
        {
            _channel.Writer.TryWrite(final);
        }
        sb.Clear();
    }

    /// <inheritdoc />
    public override void Flush()
    {
        _originalWriter.Flush();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        // 不在 Dispose 中关闭共享的日志写入器；在程序退出时可能由宿主负责清理
        base.Dispose(disposing);
    }

    private static async Task ProcessLogFileAsync()
    {
        if (_logWriter == null) return;
        var reader = _channel.Reader;
        const int BatchMax = 1024;
        const int IdleMs = 2000; // 空闲 2 秒后写入
        var batch = new List<string>(256);
        try
        {
            // 等待并读取：当通道在 IdleMs 内无新数据时，视为空闲，执行写入并刷新
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                // 读取所有立即可用项（至少一项，因为 WaitToReadAsync 已返回 true）
                while (reader.TryRead(out var item))
                {
                    batch.Add(item);

                    // 尽可能先快速拉取已经可用的项
                    while (batch.Count < BatchMax && reader.TryRead(out var more))
                    {
                        batch.Add(more);
                    }

                    // 启动空闲延迟：在 IdleMs 内若有新数据到来则重置延迟
                    var idleDelay = Task.Delay(IdleMs);
                    while (batch.Count < BatchMax)
                    {
                        var waitReadTask = reader.WaitToReadAsync().AsTask();
                        var completed = await Task.WhenAny(waitReadTask, idleDelay).ConfigureAwait(false);

                        if (completed == waitReadTask)
                        {
                            // 有新数据可读
                            if (waitReadTask.Result)
                            {
                                // 立即拉取所有可用项并重置空闲计时
                                while (batch.Count < BatchMax && reader.TryRead(out var more2))
                                {
                                    batch.Add(more2);
                                }
                                // 重置 idleDelay
                                idleDelay = Task.Delay(IdleMs);
                                continue;
                            }
                            else
                            {
                                // 通道已完成，跳出
                                break;
                            }
                        }
                        else
                        {
                            // 空闲超时，准备写入
                            break;
                        }
                    }

                    // 将批次写入文件并刷新
                    foreach (var line in batch)
                    {
                        await _logWriter.WriteLineAsync(line).ConfigureAwait(false);
                    }

                    await _logWriter.FlushAsync().ConfigureAwait(false);
                    batch.Clear();
                }
            }

            // 通道完成后，若仍有残留数据则写入
            if (batch.Count > 0)
            {
                foreach (var line in batch)
                {
                    await _logWriter.WriteLineAsync(line).ConfigureAwait(false);
                }
                await _logWriter.FlushAsync().ConfigureAwait(false);
                batch.Clear();
            }
        }
        catch
        {
            // 忽略写入异常，避免阻塞应用关闭路径
        }
    }

    /// <summary>
    /// 初始化拦截器并重定向控制台标准输出与错误输出到日志文件。
    /// </summary>
    public static void Initialize()
    {
        var rootDir = AppDomain.CurrentDomain.BaseDirectory;
        var logsDir = Path.Combine(rootDir, "logs");
        Directory.CreateDirectory(logsDir);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var logFilePath = Path.Combine(logsDir, $"{timestamp}.txt");

        // 根据开关决定是否打开日志写入器并启动后台任务
        if (EnableFileLogging)
        {
            _logWriter = new StreamWriter(new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = false,
                NewLine = Environment.NewLine
            };

            _bgTask = Task.Run(ProcessLogFileAsync);
        }

        var interceptorOut = new ConsoleInterceptor(Console.Out, logFilePath);
        var interceptorErr = new ConsoleInterceptor(Console.Error, logFilePath);

        Console.SetOut(interceptorOut);
        Console.SetError(interceptorErr);
    }
}

/// <summary>
/// 日志级别定义。
/// </summary>
public enum LogLevel
{
    /// <summary>调试信息。</summary>
    Dbug,
    /// <summary>一般信息。</summary>
    Info,
    /// <summary>警告信息。</summary>
    Warn,
    /// <summary>错误信息。</summary>
    Fail,
    /// <summary>严重错误。</summary>
    Fatal
}

internal struct LogEntry
{
    public LogLevel Level;
    public string Header;
    public string? Message;
    public Text? Text;
    public string ClassName;
    public int LineNumber;
    public DateTime Time;
}