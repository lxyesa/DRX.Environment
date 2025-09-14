using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System;
using System.IO;

namespace DRX.Framework;

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
        Enqueue(LogLevel.Info, header, message, className, lineNumber);
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
        Enqueue(level, header, message, className, lineNumber);
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
        Enqueue(LogLevel.Fail, className, message, className, lineNumber);
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
        Enqueue(LogLevel.Dbug, className, message, className, lineNumber);
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
        Enqueue(LogLevel.Warn, className, message, className, lineNumber);
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
        Enqueue(LogLevel.Info, className, message, className, lineNumber);
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
        Enqueue(LogLevel.Dbug, className, message, className, lineNumber);
    }

    private static string GetShortClassName(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return "UnknownClass";
        var name = System.IO.Path.GetFileNameWithoutExtension(filePath);
        return name;
    }

    private static void Enqueue(LogLevel level, string header, string message, string className, int lineNumber)
    {
        if (string.IsNullOrEmpty(message)) return;
        var entry = new LogEntry
        {
            Level = level,
            Header = header,
            Message = message,
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

    private static string FormatLog(in LogEntry log)
    {
        var sb = new StringBuilder(256);
        sb.Append('[')
          .Append(log.Time.ToString(DateTimeFormat))
          .Append("][")
          .Append(log.ClassName)
          .Append(':')
          .Append(log.LineNumber)
          .Append("][")
          .Append(log.Level.ToString().ToUpper())
          .Append(']')
          .Append(log.Message);
        return sb.ToString();
    }

    private static void WriteBatch(IReadOnlyList<LogEntry> batch)
    {
        if (batch == null || batch.Count == 0) return;

        var formatted = new string[batch.Count];
        var levels = new LogLevel[batch.Count];
        for (int i = 0; i < batch.Count; i++)
        {
            formatted[i] = FormatLog(batch[i]);
            levels[i] = batch[i].Level;
        }

        // 精简：移除全部 WPF 分支，统一走控制台输出
        WriteBatchToConsole(formatted, levels);
        return;
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
}

/// <summary>
/// 控制台输出拦截器，负责将控制台输出同步写入日志文件。
/// </summary>
public sealed class ConsoleInterceptor : TextWriter
{
    private readonly TextWriter _originalWriter;
    private readonly StreamWriter _logWriter;
    private readonly object _lock = new();
    /// <inheritdoc />
    public override Encoding Encoding => Encoding.UTF8;

    /// <summary>
    /// 初始化 <see cref="ConsoleInterceptor"/> 的新实例。
    /// </summary>
    /// <param name="originalWriter">原始控制台写入器。</param>
    /// <param name="logFilePath">日志文件路径。</param>
    public ConsoleInterceptor(TextWriter originalWriter, string logFilePath)
    {
        _originalWriter = originalWriter;
        _logWriter = new StreamWriter(new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = false
        };
    }

    /// <inheritdoc />
    public override void Write(char value)
    {
        lock (_lock)
        {
            _originalWriter.Write(value);
            _logWriter.Write(value);
        }
    }

    /// <inheritdoc />
    public override void Write(string? value)
    {
        lock (_lock)
        {
            _originalWriter.Write(value);
            _logWriter.Write(value);
        }
    }

    /// <inheritdoc />
    public override void WriteLine(string? value)
    {
        lock (_lock)
        {
            _originalWriter.WriteLine(value);
            _logWriter.WriteLine(value);
            _logWriter.Flush();
        }
    }

    /// <inheritdoc />
    public override void Flush()
    {
        lock (_lock)
        {
            _originalWriter.Flush();
            _logWriter.Flush();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_lock)
            {
                _logWriter.Dispose();
            }
        }
        base.Dispose(disposing);
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
    public string Message;
    public string ClassName;
    public int LineNumber;
    public DateTime Time;
}