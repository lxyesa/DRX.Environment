namespace Drx.Sdk.Shared;

/// <summary>
/// 日志输出目标接口。实现此接口以将日志写入自定义后端（文件、数据库、远程服务等）。
/// </summary>
public interface ILogSink
{
    /// <summary>
    /// 处理单条日志。
    /// </summary>
    /// <param name="formattedMessage">已格式化的日志字符串。</param>
    /// <param name="level">日志级别。</param>
    /// <param name="timestamp">日志产生的时间戳。</param>
    void Emit(string formattedMessage, LogLevel level, DateTime timestamp);

    /// <summary>
    /// 批量处理日志（可选覆盖以优化吞吐）。
    /// 默认实现逐条调用 <see cref="Emit"/>。
    /// </summary>
    /// <param name="entries">日志条目集合，每项为 (格式化字符串, 级别, 时间戳)。</param>
    void EmitBatch(ReadOnlySpan<(string Message, LogLevel Level, DateTime Time)> entries)
    {
        foreach (var (msg, level, time) in entries)
            Emit(msg, level, time);
    }

    /// <summary>
    /// 释放此 Sink 持有的资源（可选）。
    /// </summary>
    void Flush() { }
}
