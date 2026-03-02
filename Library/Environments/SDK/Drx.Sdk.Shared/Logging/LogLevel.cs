namespace Drx.Sdk.Shared;

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
    Fatal,
    /// <summary>关闭所有日志输出。</summary>
    None = 99
}
