using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace Drx.Sdk.Shared;

/// <summary>
/// 提供面向控制台与 WPF 文本控件的高性能异步日志记录功能，支持分级着色、批量写入、
/// 可插拔 Sink 与日志作用域。
/// <para>此类为 partial class，各功能分布在以下文件中：</para>
/// <list type="bullet">
///   <item><description>Logger.cs — 核心引擎（Channel 队列、格式化、Sink 分发）</description></item>
///   <item><description>Logger.Legacy.cs — 旧版 API 兼容层（Info/Warn/Error/Debug/Trace/Log 及其 Async 变体）</description></item>
///   <item><description>Logger.Modern.cs — 现代化 API（插值字符串 handler、BeginScope、IsEnabled）</description></item>
///   <item><description>Logger.Stream.cs — 流式日志扩展（StreamAsync / StreamFromAsync）</description></item>
///   <item><description>Logger.CrashHandler.cs — 全局未处理异常捕获</description></item>
/// </list>
/// </summary>
public static partial class Logger
{
    // ─────────────────────────────────────────────────────────────────────────
    // Channel 与后台消费
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly Channel<LogEntry> _channel = Channel.CreateBounded<LogEntry>(
        new BoundedChannelOptions(4096)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private static readonly CancellationTokenSource _cts = new();
    private static readonly Task _worker;

    // ─────────────────────────────────────────────────────────────────────────
    // Sink 管理
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly List<ILogSink> _sinks = [];
    private static readonly object _sinkLock = new();
    private static ILogSink[] _sinkSnapshot = [];  // 无锁快照，供热路径使用

    // ─────────────────────────────────────────────────────────────────────────
    // 配置
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 最小日志级别。低于此级别的日志将被静默丢弃（默认 <see cref="LogLevel.Dbug"/>，即全部输出）。
    /// </summary>
    public static LogLevel MinimumLevel { get; set; } = LogLevel.Dbug;

    /// <summary>
    /// 是否在日志中显示调用方代码行信息（ClassName:LineNumber）。
    /// 默认关闭以节省性能（避免 CallerFilePath/CallerLineNumber 的字符串处理开销）。
    /// </summary>
    public static bool ShowCallerInfo { get; set; } = false;

    /// <summary>
    /// 日志输出回调钩子，每条日志格式化后会调用此委托（若不为 null）。
    /// 参数：(格式化后的日志字符串, 日志级别, 时间戳)
    /// </summary>
    public static Action<string, LogLevel, DateTime>? OnLogEntry { get; set; }

    // ─────────────────────────────────────────────────────────────────────────
    // 缓存与常量
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly ConcurrentDictionary<string, string> _classNameCache = new(StringComparer.Ordinal);

    [ThreadStatic]
    private static StringBuilder? _tlsStringBuilder;

    internal const string DateTimeFormat = "yyyy/MM/dd HH:mm:ss";

    /// <summary>级别名称预计算，避免 ToString().ToUpper() 的重复分配。</summary>
    internal static readonly IReadOnlyDictionary<LogLevel, string> LevelNames =
        new Dictionary<LogLevel, string>
        {
            [LogLevel.Dbug]  = "DBUG",
            [LogLevel.Info]  = "INFO",
            [LogLevel.Warn]  = "WARN",
            [LogLevel.Fail]  = "FAIL",
            [LogLevel.Fatal] = "FATAL"
        };

    // ─────────────────────────────────────────────────────────────────────────
    // P/Invoke
    // ─────────────────────────────────────────────────────────────────────────

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    // ─────────────────────────────────────────────────────────────────────────
    // 静态构造函数
    // ─────────────────────────────────────────────────────────────────────────

    static Logger()
    {
#if DEBUG
        AllocConsole();
#endif
        // 注册内置控制台 Sink
        AddSink(new ConsoleSink());

        _worker = Task.Run(ProcessQueueAsync);
        ConsoleInterceptor.Initialize();
        RegisterCrashHandlers();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sink 注册 API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 注册一个日志输出目标。
    /// </summary>
    /// <param name="sink">要注册的 <see cref="ILogSink"/> 实例。</param>
    public static void AddSink(ILogSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        lock (_sinkLock)
        {
            _sinks.Add(sink);
            _sinkSnapshot = [.. _sinks];
        }
    }

    /// <summary>
    /// 移除一个已注册的日志输出目标。
    /// </summary>
    /// <param name="sink">要移除的 <see cref="ILogSink"/> 实例。</param>
    /// <returns>是否移除成功。</returns>
    public static bool RemoveSink(ILogSink sink)
    {
        lock (_sinkLock)
        {
            var removed = _sinks.Remove(sink);
            if (removed)
                _sinkSnapshot = [.. _sinks];
            return removed;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 核心入队
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 检查给定日志级别是否启用。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEnabled(LogLevel level) => level >= MinimumLevel;

    /// <summary>
    /// 从文件路径提取短类名（带缓存）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetShortClassName(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return "UnknownClass";
        return _classNameCache.GetOrAdd(filePath, static p => Path.GetFileNameWithoutExtension(p));
    }

    /// <summary>
    /// 将日志条目非阻塞写入 Channel（所有正常日志路径使用此方法）。
    /// </summary>
    internal static void Enqueue(LogLevel level, string header, string? message, Text? text, string className, int lineNumber)
    {
        if (level < MinimumLevel) return;
        if (string.IsNullOrEmpty(message) && text == null) return;

        _channel.Writer.TryWrite(new LogEntry
        {
            Level = level,
            Header = header,
            Message = message,
            Text = text,
            ClassName = className,
            LineNumber = lineNumber,
            Time = DateTime.Now,
            ScopeContext = LogScope.GetScopeString()
        });
    }

    /// <summary>
    /// 同步立即写入日志（仅供崩溃/Fatal 路径使用，会阻塞调用线程）。
    /// </summary>
    internal static void WriteImmediately(LogLevel level, string header, string? message, Text? text, string className, int lineNumber)
    {
        if (string.IsNullOrEmpty(message) && text == null) return;
        var entry = new LogEntry
        {
            Level = level,
            Header = header,
            Message = message,
            Text = text,
            ClassName = className,
            LineNumber = lineNumber,
            Time = DateTime.Now,
            ScopeContext = LogScope.GetScopeString()
        };
        WriteSingle(in entry);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 格式化
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 格式化单条日志为字符串。格式：[时间][类:行][级别][header?][scope?] 消息
    /// </summary>
    internal static string FormatLog(in LogEntry log, StringBuilder sb)
    {
        sb.Clear();
        sb.Append('[')
          .Append(log.Time.ToString(DateTimeFormat))
          .Append(']');

        if (ShowCallerInfo)
        {
            sb.Append('[')
              .Append(log.ClassName)
              .Append(':')
              .Append(log.LineNumber)
              .Append(']');
        }

        sb.Append('[')
          .Append(LevelNames.TryGetValue(log.Level, out var lvlName) ? lvlName : log.Level.ToString().ToUpper())
          .Append(']');

        // 若 Header 与 ClassName 不同（调用方显式指定了来源标签），则输出 Header
        if (!string.IsNullOrEmpty(log.Header) && log.Header != log.ClassName)
        {
            sb.Append('[').Append(log.Header).Append(']');
        }

        // 附加 Scope 上下文
        if (!string.IsNullOrEmpty(log.ScopeContext))
        {
            sb.Append('[').Append(log.ScopeContext).Append(']');
        }

        sb.Append(' ').Append(log.Text != null ? log.Text.ToString() : log.Message);
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 写入实现
    // ─────────────────────────────────────────────────────────────────────────

    private static void WriteSingle(in LogEntry log)
    {
        var sb = _tlsStringBuilder ??= new StringBuilder(256);
        var line = FormatLog(log, sb);
        var sinks = _sinkSnapshot;
        var hook = OnLogEntry;

        for (int i = 0; i < sinks.Length; i++)
        {
            try { sinks[i].Emit(line, log.Level, log.Time); }
            catch { /* Sink 异常不应中断日志管线 */ }
        }

        try { hook?.Invoke(line, log.Level, log.Time); }
        catch { }
    }

    private static void WriteBatch(IReadOnlyList<LogEntry> batch)
    {
        if (batch == null || batch.Count == 0) return;

        var sb = _tlsStringBuilder ??= new StringBuilder(256);
        var sinks = _sinkSnapshot;
        var hook = OnLogEntry;

        // 预格式化所有条目
        var formatted = new (string Message, LogLevel Level, DateTime Time)[batch.Count];
        for (int i = 0; i < batch.Count; i++)
        {
            var log = batch[i];
            formatted[i] = (FormatLog(log, sb), log.Level, log.Time);
        }

        // 分发到所有 Sink
        var span = formatted.AsSpan();
        for (int s = 0; s < sinks.Length; s++)
        {
            try { sinks[s].EmitBatch(span); }
            catch { }
        }

        // OnLogEntry 钩子（向后兼容）
        if (hook != null)
        {
            for (int i = 0; i < formatted.Length; i++)
            {
                try { hook(formatted[i].Message, formatted[i].Level, formatted[i].Time); }
                catch { }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 后台消费循环
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 等待 Channel 中所有已入队日志全部写出后返回。
    /// 适用于控制台程序退出前确保日志不丢失。
    /// </summary>
    public static async Task FlushAsync()
    {
        _channel.Writer.TryComplete();
        await _worker.ConfigureAwait(false);
        var sinks = _sinkSnapshot;
        for (int i = 0; i < sinks.Length; i++)
        {
            try { sinks[i].Flush(); }
            catch { }
        }
    }

    /// <summary>
    /// 同步版 Flush，阻塞调用线程直到所有日志写出。
    /// </summary>
    public static void Flush() => FlushAsync().GetAwaiter().GetResult();

    private static async Task ProcessQueueAsync()
    {
        var token = _cts.Token;
        const int BatchMax = 256;
        var batch = new List<LogEntry>(BatchMax);

        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(token))
            {
                batch.Add(item);

                while (batch.Count < BatchMax && _channel.Reader.TryRead(out var more))
                    batch.Add(more);

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
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [ERROR] 日志系统异常: {ex.Message}");
        }
    }
}
