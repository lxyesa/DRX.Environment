using System.Collections.Concurrent;

namespace Drx.Sdk.Shared;

/// <summary>
/// 内置控制台日志 Sink，支持按级别着色输出。
/// </summary>
public sealed class ConsoleSink : ILogSink
{
    /// <summary>
    /// 级别 → 控制台颜色映射。
    /// </summary>
    internal static readonly IReadOnlyDictionary<LogLevel, ConsoleColor> Colors =
        new Dictionary<LogLevel, ConsoleColor>
        {
            [LogLevel.Dbug]  = ConsoleColor.Gray,
            [LogLevel.Info]  = ConsoleColor.DarkGreen,
            [LogLevel.Warn]  = ConsoleColor.Yellow,
            [LogLevel.Fail]  = ConsoleColor.Red,
            [LogLevel.Fatal] = ConsoleColor.DarkRed
        };

    /// <inheritdoc />
    public void Emit(string formattedMessage, LogLevel level, DateTime timestamp)
    {
        var origColor = Console.ForegroundColor;
        var targetColor = Colors.TryGetValue(level, out var cc) ? cc : ConsoleColor.White;

        if (targetColor != origColor) Console.ForegroundColor = targetColor;
        Console.WriteLine(formattedMessage);
        if (targetColor != origColor) Console.ForegroundColor = origColor;
    }

    /// <inheritdoc />
    public void EmitBatch(ReadOnlySpan<(string Message, LogLevel Level, DateTime Time)> entries)
    {
        if (entries.Length == 0) return;

        var originalColor = Console.ForegroundColor;
        var currentColor = originalColor;

        foreach (var (msg, level, _) in entries)
        {
            var targetColor = Colors.TryGetValue(level, out var cc) ? cc : ConsoleColor.White;

            if (currentColor != targetColor)
            {
                Console.ForegroundColor = targetColor;
                currentColor = targetColor;
            }

            Console.WriteLine(msg);
        }

        if (currentColor != originalColor)
            Console.ForegroundColor = originalColor;
    }
}
