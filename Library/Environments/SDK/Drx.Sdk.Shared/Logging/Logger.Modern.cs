using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Drx.Sdk.Shared;

/// <summary>
/// 现代化 API 扩展 — 提供插值字符串零分配日志、作用域、异常日志等便捷方法。
/// </summary>
public static partial class Logger
{
    // ═══════════════════════════════════════════════════════════════════════════
    // 作用域 API
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 创建一个日志作用域。在 <c>using</c> 块内的所有日志将自动附加上下文标签。
    /// <example>
    /// <code>
    /// using (Logger.BeginScope("RequestId=abc123"))
    /// {
    ///     Logger.Info("处理中");  // → [...][INFO][RequestId=abc123] 处理中
    /// }
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="state">作用域上下文描述。</param>
    /// <returns>可释放的作用域对象。</returns>
    public static IDisposable BeginScope(string state) => new LogScope(state);

    // ═══════════════════════════════════════════════════════════════════════════
    // 插值字符串 Handler — 低于 MinimumLevel 时完全跳过字符串格式化
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 以指定级别记录一条日志，使用插值字符串 handler 实现零分配跳过。
    /// 当 <paramref name="level"/> 低于 <see cref="MinimumLevel"/> 时，
    /// 插值字符串内的表达式 **不会被求值**。
    /// <example>
    /// <code>
    /// Logger.Write(LogLevel.Dbug, $"计算结果: {ExpensiveCalculation()}");
    /// // 若 MinimumLevel > Dbug，ExpensiveCalculation() 不会被调用
    /// </code>
    /// </example>
    /// </summary>
    public static void Write(LogLevel level, [InterpolatedStringHandlerArgument("level")] LogInterpolatedStringHandler handler,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        if (!handler.IsEnabled) return;
        var cn = GetShortClassName(filePath);
        Enqueue(level, cn, handler.ToStringAndClear(), null, cn, lineNumber);
    }

    /// <summary>
    /// 以指定级别记录一条日志（带 header），使用插值字符串 handler。
    /// </summary>
    public static void Write(LogLevel level, string header, [InterpolatedStringHandlerArgument("level")] LogInterpolatedStringHandler handler,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        if (!handler.IsEnabled) return;
        var cn = GetShortClassName(filePath);
        Enqueue(level, header, handler.ToStringAndClear(), null, cn, lineNumber);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 异常日志
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 记录一个异常（自动展开 InnerException 链）。
    /// </summary>
    /// <param name="ex">要记录的异常。</param>
    /// <param name="message">附加消息（可选）。</param>
    /// <param name="level">日志级别，默认 <see cref="LogLevel.Fail"/>。</param>
    /// <param name="lineNumber">调用处行号（自动填充）。</param>
    /// <param name="filePath">调用处文件路径（自动填充）。</param>
    public static void Exception(Exception ex, string? message = null, LogLevel level = LogLevel.Fail,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        if (!IsEnabled(level)) return;
        var cn = GetShortClassName(filePath);
        var sb = new StringBuilder(256);

        if (!string.IsNullOrEmpty(message))
            sb.AppendLine(message);

        var current = ex;
        int depth = 0;
        while (current != null && depth < 10)
        {
            if (depth > 0) sb.AppendLine("---- Inner Exception ----");
            sb.AppendLine($"{current.GetType().FullName}: {current.Message}");
            if (!string.IsNullOrEmpty(current.StackTrace))
                sb.AppendLine(current.StackTrace);
            current = current.InnerException;
            depth++;
        }

        Enqueue(level, cn, sb.ToString().TrimEnd(), null, cn, lineNumber);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 计时器
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 创建一个计时日志作用域。<c>Dispose</c> 时自动输出耗时。
    /// <example>
    /// <code>
    /// using (Logger.BeginTimedScope("数据库查询"))
    /// {
    ///     await db.QueryAsync(...);
    /// }
    /// // → [INFO] 数据库查询 completed in 42ms
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="operationName">操作名称。</param>
    /// <param name="level">日志级别，默认 <see cref="LogLevel.Info"/>。</param>
    /// <param name="lineNumber">调用处行号（自动填充）。</param>
    /// <param name="filePath">调用处文件路径（自动填充）。</param>
    /// <returns>可释放的计时作用域。</returns>
    public static IDisposable BeginTimedScope(string operationName, LogLevel level = LogLevel.Info,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        return new TimedScope(operationName, level, lineNumber, filePath);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 条件日志
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 仅当条件为 true 时才记录日志（避免在调用处写 if 判断）。
    /// </summary>
    public static void InfoIf(bool condition, string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        if (!condition) return;
        Info(message, lineNumber, filePath);
    }

    /// <summary>
    /// 仅当条件为 true 时才记录警告。
    /// </summary>
    public static void WarnIf(bool condition, string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        if (!condition) return;
        Warn(message, lineNumber, filePath);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 内部类型
    // ═══════════════════════════════════════════════════════════════════════════

    private sealed class TimedScope : IDisposable
    {
        private readonly string _name;
        private readonly LogLevel _level;
        private readonly int _lineNumber;
        private readonly string _filePath;
        private readonly Stopwatch _sw;
        private bool _disposed;

        public TimedScope(string name, LogLevel level, int lineNumber, string filePath)
        {
            _name = name;
            _level = level;
            _lineNumber = lineNumber;
            _filePath = filePath;
            _sw = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _sw.Stop();
            var cn = GetShortClassName(_filePath);
            Enqueue(_level, cn, $"{_name} completed in {_sw.ElapsedMilliseconds}ms", null, cn, _lineNumber);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// 插值字符串 Handler
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 自定义插值字符串 handler，当日志级别低于最小级别时完全跳过格式化，
/// 实现零开销的条件日志。
/// </summary>
[InterpolatedStringHandler]
public ref struct LogInterpolatedStringHandler
{
    private StringBuilder? _builder;

    internal bool IsEnabled { get; }

    public LogInterpolatedStringHandler(int literalLength, int formattedCount, LogLevel level, out bool isEnabled)
    {
        IsEnabled = isEnabled = Logger.IsEnabled(level);
        if (isEnabled)
            _builder = new StringBuilder(literalLength);
    }

    public void AppendLiteral(string s)
    {
        if (!IsEnabled) return;
        _builder!.Append(s);
    }

    public void AppendFormatted<T>(T value)
    {
        if (!IsEnabled) return;
        _builder!.Append(value?.ToString());
    }

    public void AppendFormatted<T>(T value, string? format) where T : IFormattable
    {
        if (!IsEnabled) return;
        _builder!.Append(value?.ToString(format, null));
    }

    public void AppendFormatted(ReadOnlySpan<char> value)
    {
        if (!IsEnabled) return;
        _builder!.Append(value);
    }

    internal string ToStringAndClear()
    {
        if (_builder == null) return string.Empty;
        var result = _builder.ToString();
        _builder.Clear();
        return result;
    }
}
