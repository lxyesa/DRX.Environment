using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DRX.Framework;

//// <summary>
//// 轻量级高性能异步日志，面向 Win32 控制台，尽量贴近原生 C 的实现风格：
//// - 使用固定缓冲与 minimal 分配
//// - P/Invoke 调用 WriteConsoleW/WriteFile
//// - 控制台颜色用 SetConsoleTextAttribute
//// - 批量写入、降级到文件
//// </summary>
public static class Logger
{
    // 高性能：使用无锁 MPSC 环形缓冲区，生产者 TryEnqueue，单消费者阻塞读取
    private const int RingSize = 1 << 12; // 4096（2 的幂方便位运算）
    private static readonly LogEntry[] _ring = new LogEntry[RingSize];
    private static int _writeIndex; // 原子递增
    private static int _readIndex;  // 仅消费者线程读写
    private static readonly AutoResetEvent _evt = new(false);

    private static readonly CancellationTokenSource _cts = new();
    private static readonly Task _worker;

    // Win32 颜色常量（与 FOREGROUND_* 常量组合）
    private const ushort FOREGROUND_BLUE = 0x0001;
    private const ushort FOREGROUND_GREEN = 0x0002;
    private const ushort FOREGROUND_RED = 0x0004;
    private const ushort FOREGROUND_INTENSITY = 0x0008;
    private const uint STD_OUTPUT_HANDLE = unchecked((uint)-11);
    private const uint STD_ERROR_HANDLE = unchecked((uint)-12);
    private static readonly IntPtr StdOut = GetStdHandle(STD_OUTPUT_HANDLE);
    private static readonly IntPtr StdErr = GetStdHandle(STD_ERROR_HANDLE);
    private static readonly ushort DefaultAttr = GetCurrentConsoleAttributes(StdOut);

    // 级别到颜色属性映射（尽量接近原生命令行）
    private static readonly IReadOnlyDictionary<LogLevel, ushort> ConsoleAttrs = new Dictionary<LogLevel, ushort>
    {
        [LogLevel.Dbug] = FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE, // Gray/White, 不加亮
        [LogLevel.Info] = FOREGROUND_GREEN | FOREGROUND_INTENSITY,
        [LogLevel.Warn] = FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_INTENSITY, // Yellow
        [LogLevel.Fail] = FOREGROUND_RED | FOREGROUND_INTENSITY,
        [LogLevel.Fatal] = FOREGROUND_RED // 深红（不加亮）
    };


    private const string DateTimeFormat = "yyyy/MM/dd HH:mm:ss";
    // 预分配缓冲，减少分配
    [ThreadStatic] private static StringBuilder? _sbTls;


    [DllImport("kernel32.dll")] private static extern bool AllocConsole();
    [DllImport("kernel32.dll")] private static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GetStdHandle(uint nStdHandle);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteConsoleW(IntPtr hConsoleOutput, char[] lpBuffer, uint nNumberOfCharsToWrite, out uint lpNumberOfCharsWritten, IntPtr lpReserved);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetConsoleTextAttribute(IntPtr hConsoleOutput, ushort wAttributes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD { public short X; public short Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct SMALL_RECT { public short Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public COORD dwSize;
        public COORD dwCursorPosition;
        public ushort wAttributes;
        public SMALL_RECT srWindow;
        public COORD dwMaximumWindowSize;
    }

    private static ushort GetCurrentConsoleAttributes(IntPtr handle)
    {
        if (handle != IntPtr.Zero && GetConsoleScreenBufferInfo(handle, out var info))
            return info.wAttributes;
        // 兜底为常用白色
        return (ushort)(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Enqueue(LogLevel level, string header, string message, string className, int lineNumber)
    {
        if (string.IsNullOrEmpty(message)) return;

        // 预判：如果环形缓冲满则丢弃最旧（覆盖），以极致吞吐优先
        var idx = Interlocked.Increment(ref _writeIndex) - 1;
        var slot = idx & (RingSize - 1);

        // 组装 LogEntry（最小化分配：字符串直接引用）
        _ring[slot] = new LogEntry
        {
            Level = level,
            Header = header,
            Message = message,
            ClassName = className,
            LineNumber = lineNumber,
            Time = DateTime.Now
        };

        // 唤醒消费者（轻量事件，避免每次写都唤醒的惊群：尝试仅在队列从空->非空时唤醒）
        // 简化：直接 Set，事件自身会 coalesce
        _evt.Set();
    }

    private static async Task ProcessQueueAsync()
    {
        var token = _cts.Token;
        const int BatchMax = 512;  // 更大批量，减少系统调用
        const int IdleWaitMs = 2;  // 空闲自旋后短暂等待，降低 CPU
        var batch = new List<LogEntry>(BatchMax);

        try
        {
            while (!token.IsCancellationRequested)
            {
                // 快速搬运，尽量批量取满
                int localWrite = Volatile.Read(ref _writeIndex);
                while (_readIndex < localWrite && batch.Count < BatchMax)
                {
                    var slot = _readIndex & (RingSize - 1);
                    batch.Add(_ring[slot]);
                    _readIndex++;
                }

                if (batch.Count == 0)
                {
                    // 轻量等待：先 WaitOne 超时，减少空转
                    _evt.WaitOne(IdleWaitMs);
                    continue;
                }

                try
                {
                    WriteBatch(batch);
                }
                finally
                {
                    batch.Clear();
                }
            }
        }
        catch (Exception ex)
        {
            // 极端情况下直接写控制台，避免再进入队列
            try { Console.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [ERROR] 日志系统异常: {ex.Message}"); } catch { }
        }
        await Task.CompletedTask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatLog(in LogEntry log)
    {
        var sb = _sbTls ??= new StringBuilder(256);
        sb.Clear();
        sb.Append('[');
        sb.Append(log.Time.ToString(DateTimeFormat));
        sb.Append("][");
        sb.Append(log.ClassName);
        sb.Append(':');
        sb.Append(log.LineNumber);
        sb.Append("][");
        // 避免 ToUpperInvariant 分配，手写映射
        switch (log.Level)
        {
            case LogLevel.Dbug: sb.Append("DBUG"); break;
            case LogLevel.Info: sb.Append("INFO"); break;
            case LogLevel.Warn: sb.Append("WARN"); break;
            case LogLevel.Fail: sb.Append("FAIL"); break;
            case LogLevel.Fatal: sb.Append("FATAL"); break;
        }
        sb.Append(']');
        sb.Append(log.Message);
        return sb.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteBatch(IReadOnlyList<LogEntry> batch)
    {
        if (batch == null || batch.Count == 0) return;

        // 避免多次分配数组：逐条格式化并写出
        var handle = StdOut != IntPtr.Zero ? StdOut : GetStdHandle(STD_OUTPUT_HANDLE);
        var isConsole = handle != IntPtr.Zero && GetConsoleScreenBufferInfo(handle, out _);
        ushort currentAttr = DefaultAttr;
        LogLevel? currentLevel = null;

        for (int i = 0; i < batch.Count; i++)
        {
            var item = batch[i];
            var text = FormatLog(in item);

            if (isConsole)
            {
                if (currentLevel != item.Level && ConsoleAttrs.TryGetValue(item.Level, out var attr))
                {
                    if (currentAttr != attr)
                    {
                        SetConsoleTextAttribute(handle, attr);
                        currentAttr = attr;
                    }
                    currentLevel = item.Level;
                }

                // 直接写 UTF-16
                var withNlLen = text.Length + 2;
                var buffer = new char[withNlLen];
                text.CopyTo(0, buffer, 0, text.Length);
                buffer[withNlLen - 2] = '\n'; // 使用 \n，避免额外开销；Windows 控制台可兼容
                buffer[withNlLen - 1] = '\0'; // 提前填充占位，仍按 withNlLen 写入
                WriteConsoleW(handle, buffer, (uint)(withNlLen - 1), out _, IntPtr.Zero);
            }
            else
            {
                // 重定向：UTF-8
                var withNewLine = string.Concat(text, "\n");
                var bytes = Encoding.UTF8.GetBytes(withNewLine);
                WriteFile(handle, bytes, (uint)bytes.Length, out _, IntPtr.Zero);
            }
        }

        if (isConsole && currentAttr != DefaultAttr)
        {
            SetConsoleTextAttribute(handle, DefaultAttr);
        }
    }


    private static void WriteBatchToConsoleWin32(string[] formatted, LogLevel[] levels)
    {
        if (formatted.Length == 0) return;

        var handle = StdOut != IntPtr.Zero ? StdOut : GetStdHandle(STD_OUTPUT_HANDLE);
        var isConsole = handle != IntPtr.Zero && GetConsoleScreenBufferInfo(handle, out _);

        // 预先获取默认颜色，避免频繁查询
        ushort currentAttr = DefaultAttr;
        LogLevel? currentLevel = null;

        for (int i = 0; i < formatted.Length; i++)
        {
            var text = formatted[i];
            var lvl = levels[i];

            if (isConsole)
            {
                if (currentLevel != lvl && ConsoleAttrs.TryGetValue(lvl, out var attr))
                {
                    if (currentAttr != attr)
                    {
                        SetConsoleTextAttribute(handle, attr);
                        currentAttr = attr;
                    }
                    currentLevel = lvl;
                }

                // WriteConsoleW 以 UTF-16 写入
                var withNewLine = text + Environment.NewLine;
                var chars = withNewLine.ToCharArray();
                WriteConsoleW(handle, chars, (uint)chars.Length, out _, IntPtr.Zero);
            }
            else
            {
                // 当输出被重定向到文件/管道，使用 WriteFile 写入 UTF-8
                var withNewLine = text + Environment.NewLine;
                var bytes = Encoding.UTF8.GetBytes(withNewLine);
                WriteFile(handle, bytes, (uint)bytes.Length, out _, IntPtr.Zero);
            }
        }

        if (isConsole && currentAttr != DefaultAttr)
        {
            SetConsoleTextAttribute(handle, DefaultAttr);
        }
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
