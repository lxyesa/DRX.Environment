using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Drx.Sdk.Shared;

/// <summary>
/// 流式日志输出扩展，支持将任意异步数据流的内容实时打印到控制台，
/// 并在流结束后将完整内容写入日志系统。
/// <para>
/// 支持的流类型：
/// <list type="bullet">
///   <item><description><see cref="IAsyncEnumerable{T}"/> + 文本选择器（通用）</description></item>
///   <item><description><see cref="IAsyncEnumerable{T}"/> where T is string?（纯字符串流）</description></item>
///   <item><description><see cref="System.IO.Stream"/>（字节/文本流，逐字符读取）</description></item>
///   <item><description><see cref="TextReader"/>（行读取流）</description></item>
/// </list>
/// </para>
/// </summary>
public static partial class Logger
{
    private static readonly SemaphoreSlim _streamConsoleLock = new(1, 1);

    // ─────────────────────────────────────────────────────────────────────────
    // 通用 IAsyncEnumerable<T> 重载
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 以流式方式日志记录 <see cref="IAsyncEnumerable{T}"/> 的每一个 chunk，
    /// 实时追加输出到控制台，流结束后将完整内容一次性写入日志文件。
    /// </summary>
    /// <typeparam name="T">流元素类型。</typeparam>
    /// <param name="header">日志来源标签。</param>
    /// <param name="stream">异步数据流。</param>
    /// <param name="textSelector">
    ///   从 chunk 提取要输出的文本片段；返回 <c>null</c> 或空字符串表示跳过该 chunk。
    /// </param>
    /// <param name="level">日志级别，默认 <see cref="LogLevel.Info"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="lineNumber">调用处行号（自动填充）。</param>
    /// <param name="filePath">调用处文件路径（自动填充）。</param>
    /// <returns>流输出的完整文本内容。</returns>
    public static async Task<string> StreamAsync<T>(
        string header,
        IAsyncEnumerable<T> stream,
        Func<T, string?> textSelector,
        LogLevel level = LogLevel.Info,
        CancellationToken cancellationToken = default,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        var sw = Stopwatch.StartNew();
        var contentBuffer = new StringBuilder(512);
        using var _ = ConsoleInterceptor.BeginRealtimeConsoleWriteScope();

        await WriteStreamHeaderAsync(header, className, lineNumber, level).ConfigureAwait(false);

        try
        {
            await foreach (var chunk in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                var text = textSelector(chunk);
                if (!string.IsNullOrEmpty(text))
                {
                    WriteStreamChunkToConsole(text, level);
                    contentBuffer.Append(text);
                }
            }
        }
        catch (OperationCanceledException)
        {
            WriteStreamChunkToConsole(" [cancelled]", LogLevel.Warn);
        }
        catch (Exception ex)
        {
            WriteStreamChunkToConsole($" [error: {ex.Message}]", LogLevel.Fail);
        }

        sw.Stop();
        var fullContent = contentBuffer.ToString();
        await WriteStreamFooterAsync(header, className, lineNumber, level, fullContent, sw.Elapsed).ConfigureAwait(false);

        return fullContent;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IAsyncEnumerable<string?> 纯字符串流重载
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 以流式方式日志记录字符串异步数据流。
    /// </summary>
    public static Task<string> StreamAsync(
        string header,
        IAsyncEnumerable<string?> stream,
        LogLevel level = LogLevel.Info,
        CancellationToken cancellationToken = default,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "")
        => StreamAsync(
            header, stream, static s => s,
            level, cancellationToken, lineNumber, filePath);

    // ─────────────────────────────────────────────────────────────────────────
    // System.IO.Stream 重载
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 以流式方式日志记录 <see cref="System.IO.Stream"/> 的内容，
    /// 逐字符实时追加输出到控制台，流结束后将完整内容一次性写入日志文件。
    /// </summary>
    public static async Task<string> StreamFromAsync(
        string header,
        System.IO.Stream stream,
        Encoding? encoding = null,
        int bufferSize = 4096,
        LogLevel level = LogLevel.Info,
        CancellationToken cancellationToken = default,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        var sw = Stopwatch.StartNew();
        var contentBuffer = new StringBuilder(512);
        var enc = encoding ?? Encoding.UTF8;
        using var _ = ConsoleInterceptor.BeginRealtimeConsoleWriteScope();

        await WriteStreamHeaderAsync(header, className, lineNumber, level).ConfigureAwait(false);

        try
        {
            var byteBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(bufferSize);
            var charBuffer = new char[enc.GetMaxCharCount(bufferSize)];
            var decoder = enc.GetDecoder();
            try
            {
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(byteBuffer.AsMemory(0, bufferSize), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    int charsDecoded = decoder.GetChars(byteBuffer, 0, bytesRead, charBuffer, 0, flush: false);
                    if (charsDecoded > 0)
                    {
                        var text = new string(charBuffer, 0, charsDecoded);
                        WriteStreamChunkToConsole(text, level);
                        contentBuffer.Append(text);
                    }
                }

                int finalChars = decoder.GetChars(Array.Empty<byte>(), 0, 0, charBuffer, 0, flush: true);
                if (finalChars > 0)
                {
                    var tail = new string(charBuffer, 0, finalChars);
                    WriteStreamChunkToConsole(tail, level);
                    contentBuffer.Append(tail);
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(byteBuffer);
            }
        }
        catch (OperationCanceledException)
        {
            WriteStreamChunkToConsole(" [cancelled]", LogLevel.Warn);
        }
        catch (Exception ex)
        {
            WriteStreamChunkToConsole($" [error: {ex.Message}]", LogLevel.Fail);
        }

        sw.Stop();
        var fullContent = contentBuffer.ToString();
        await WriteStreamFooterAsync(header, className, lineNumber, level, fullContent, sw.Elapsed).ConfigureAwait(false);

        return fullContent;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TextReader 重载
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 以流式方式日志记录 <see cref="TextReader"/> 的内容，
    /// 逐行实时输出到控制台，流结束后将完整内容一次性写入日志文件。
    /// </summary>
    public static async Task<string> StreamFromAsync(
        string header,
        TextReader reader,
        LogLevel level = LogLevel.Info,
        CancellationToken cancellationToken = default,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        var sw = Stopwatch.StartNew();
        var contentBuffer = new StringBuilder(512);
        using var _ = ConsoleInterceptor.BeginRealtimeConsoleWriteScope();

        await WriteStreamHeaderAsync(header, className, lineNumber, level).ConfigureAwait(false);

        try
        {
            string? line;
            bool first = true;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = first ? line : Environment.NewLine + line;
                WriteStreamChunkToConsole(text, level);
                contentBuffer.Append(text);
                first = false;
            }
        }
        catch (OperationCanceledException)
        {
            WriteStreamChunkToConsole(" [cancelled]", LogLevel.Warn);
        }
        catch (Exception ex)
        {
            WriteStreamChunkToConsole($" [error: {ex.Message}]", LogLevel.Fail);
        }

        sw.Stop();
        var fullContent = contentBuffer.ToString();
        await WriteStreamFooterAsync(header, className, lineNumber, level, fullContent, sw.Elapsed).ConfigureAwait(false);

        return fullContent;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 流式日志内部辅助
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task WriteStreamHeaderAsync(
        string header, string className, int lineNumber, LogLevel level)
    {
        await _streamConsoleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var color = ConsoleSink.Colors.TryGetValue(level, out var c) ? c : ConsoleColor.White;
            var origColor = Console.ForegroundColor;

            var prefix = BuildStreamPrefix(header, className, lineNumber, level, "▶");
            if (color != origColor) Console.ForegroundColor = color;
            Console.Write(prefix);
            Console.Out.Flush();
            if (color != origColor) Console.ForegroundColor = origColor;
        }
        finally
        {
            _streamConsoleLock.Release();
        }
    }

    private static void WriteStreamChunkToConsole(string text, LogLevel level)
    {
        if (string.IsNullOrEmpty(text)) return;
        var color = ConsoleSink.Colors.TryGetValue(level, out var c) ? c : ConsoleColor.White;
        var origColor = Console.ForegroundColor;
        if (color != origColor) Console.ForegroundColor = color;
        Console.Write(text);
        Console.Out.Flush();
        if (color != origColor) Console.ForegroundColor = origColor;
    }

    private static async Task WriteStreamFooterAsync(
        string header, string className, int lineNumber, LogLevel level,
        string fullContent, TimeSpan elapsed)
    {
        await _streamConsoleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var color = ConsoleSink.Colors.TryGetValue(level, out var c) ? c : ConsoleColor.White;
            var origColor = Console.ForegroundColor;

            var summary = BuildStreamPrefix(header, className, lineNumber, level, "◀")
                + $"elapsed={elapsed.TotalMilliseconds:F0}ms chars={fullContent.Length}";

            if (color != origColor) Console.ForegroundColor = color;
            Console.WriteLine();
            Console.WriteLine(summary);
            Console.Out.Flush();
            if (color != origColor) Console.ForegroundColor = origColor;
        }
        finally
        {
            _streamConsoleLock.Release();
        }

        if (!string.IsNullOrEmpty(fullContent))
        {
            Enqueue(level, header, fullContent, null, className, lineNumber);
        }
    }

    private static string BuildStreamPrefix(
        string header, string className, int lineNumber, LogLevel level, string marker)
    {
        var sb = new StringBuilder(128);
        sb.Append('[')
          .Append(DateTime.Now.ToString(DateTimeFormat))
          .Append("][")
          .Append(className)
          .Append(':')
          .Append(lineNumber)
          .Append("][")
          .Append(LevelNames.TryGetValue(level, out var lvl) ? lvl : level.ToString().ToUpper())
          .Append(']');

        if (!string.IsNullOrEmpty(header) && header != className)
            sb.Append('[').Append(header).Append(']');

        sb.Append(' ').Append(marker).Append(' ');
        return sb.ToString();
    }
}
