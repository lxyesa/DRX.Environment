using System.Runtime.CompilerServices;

namespace Drx.Sdk.Shared;

/// <summary>
/// 旧版 API 兼容层 — 保留所有原始公共方法签名，确保现有调用方零改动迁移。
/// </summary>
public static partial class Logger
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Info
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>以信息级别记录一条日志（同步，header + message）。</summary>
    public static void Info(string header, string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Info, header, message, null, cn, lineNumber);
    }

    /// <summary>以信息级别记录一条日志（异步，header + message）。</summary>
    public static Task InfoAsync(string header, string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Info, header, message, null, cn, lineNumber);
        return Task.CompletedTask;
    }

    /// <summary>以信息级别记录一条日志（同步，header + Text）。</summary>
    public static void Info(string header, Text text,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Info, header, null, text, cn, lineNumber);
    }

    /// <summary>以信息级别记录一条日志（异步，header + Text）。</summary>
    public static Task InfoAsync(string header, Text text,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Info, header, null, text, cn, lineNumber);
        return Task.CompletedTask;
    }

    /// <summary>以信息级别记录一条日志（同步，简化重载）。</summary>
    public static void Info(string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Info, cn, message, null, cn, lineNumber);
    }

    /// <summary>以信息级别记录一条日志（异步，简化重载）。</summary>
    public static Task InfoAsync(string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Info, cn, message, null, cn, lineNumber);
        return Task.CompletedTask;
    }

    /// <summary>以信息级别记录一条日志（同步，Text 简化重载）。</summary>
    public static void Info(Text text,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Info, cn, null, text, cn, lineNumber);
    }

    /// <summary>以信息级别记录一条日志（异步，Text 简化重载）。</summary>
    public static Task InfoAsync(Text text,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Info, cn, null, text, cn, lineNumber);
        return Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Error
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>以错误级别记录一条日志（同步）。</summary>
    public static void Error(string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Fail, cn, message, null, cn, lineNumber);
    }

    /// <summary>以错误级别记录一条日志（异步）。</summary>
    public static Task ErrorAsync(string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Fail, cn, message, null, cn, lineNumber);
        return Task.CompletedTask;
    }

    /// <summary>以错误级别记录一条日志（同步，Text）。</summary>
    public static void Error(Text text,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Fail, cn, null, text, cn, lineNumber);
    }

    /// <summary>以错误级别记录一条日志（异步，Text）。</summary>
    public static Task ErrorAsync(Text text,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Fail, cn, null, text, cn, lineNumber);
        return Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Warn
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>以警告级别记录一条日志（同步）。</summary>
    public static void Warn(string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Warn, cn, message, null, cn, lineNumber);
    }

    /// <summary>以警告级别记录一条日志（异步）。</summary>
    public static Task WarnAsync(string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Warn, cn, message, null, cn, lineNumber);
        return Task.CompletedTask;
    }

    /// <summary>以警告级别记录一条日志（同步，Text）。</summary>
    public static void Warn(Text text,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Warn, cn, null, text, cn, lineNumber);
    }

    /// <summary>以警告级别记录一条日志（异步，Text）。</summary>
    public static Task WarnAsync(Text text,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Warn, cn, null, text, cn, lineNumber);
        return Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Debug
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>以调试级别记录一条日志（同步）。</summary>
    public static void Debug(string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Dbug, cn, message, null, cn, lineNumber);
    }

    /// <summary>以调试级别记录一条日志（异步）。</summary>
    public static Task DebugAsync(string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Dbug, cn, message, null, cn, lineNumber);
        return Task.CompletedTask;
    }

    /// <summary>以调试级别记录一条日志（同步，Text）。</summary>
    public static void Debug(Text text,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Dbug, cn, null, text, cn, lineNumber);
    }

    /// <summary>以调试级别记录一条日志（异步，Text）。</summary>
    public static Task DebugAsync(Text text,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Dbug, cn, null, text, cn, lineNumber);
        return Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Trace（映射到 Dbug）
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>以 Trace（映射到调试级别）记录一条日志（同步）。</summary>
    public static void Trace(string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Dbug, cn, message, null, cn, lineNumber);
    }

    /// <summary>以 Trace（映射到调试级别）记录一条日志（异步）。</summary>
    public static Task TraceAsync(string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Dbug, cn, message, null, cn, lineNumber);
        return Task.CompletedTask;
    }

    /// <summary>以 Trace（映射到调试级别）记录一条日志（同步，Text）。</summary>
    public static void Trace(Text text,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Dbug, cn, null, text, cn, lineNumber);
    }

    /// <summary>以 Trace（映射到调试级别）记录一条日志（异步，Text）。</summary>
    public static Task TraceAsync(Text text,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(LogLevel.Dbug, cn, null, text, cn, lineNumber);
        return Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Log（指定级别）
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>以指定级别记录一条日志（同步）。</summary>
    public static void Log(LogLevel level, string header, string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(level, header, message, null, cn, lineNumber);
    }

    /// <summary>以指定级别记录一条日志（异步）。</summary>
    public static Task LogAsync(LogLevel level, string header, string message,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(level, header, message, null, cn, lineNumber);
        return Task.CompletedTask;
    }

    /// <summary>以指定级别记录一条日志（同步，Text）。</summary>
    public static void Log(LogLevel level, string header, Text text,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(level, header, null, text, cn, lineNumber);
    }

    /// <summary>以指定级别记录一条日志（异步，Text）。</summary>
    public static Task LogAsync(LogLevel level, string header, Text text,
        [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var cn = GetShortClassName(filePath);
        Enqueue(level, header, null, text, cn, lineNumber);
        return Task.CompletedTask;
    }
}
