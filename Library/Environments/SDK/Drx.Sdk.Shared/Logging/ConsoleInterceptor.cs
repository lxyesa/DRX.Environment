using System.Text;
using System.Threading.Channels;

namespace Drx.Sdk.Shared;

/// <summary>
/// 控制台输出拦截器，负责将控制台输出同步写入日志文件。
/// </summary>
public sealed class ConsoleInterceptor : TextWriter
{
    private readonly TextWriter _originalWriter;
    private static readonly AsyncLocal<int> _realtimeBypassDepth = new();
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
    /// 注意：该开关应在调用 <see cref="Initialize"/> 或在 <see cref="Logger"/> 第一次被引用之前设置。
    /// </summary>
    public static bool EnableFileLogging { get; set; } = true;

    /// <summary>
    /// 开启实时控制台直写作用域。作用域内 Console.Write 将绕过按行缓冲，
    /// 用于流式输出（如 LLM token/chunk 实时显示）。
    /// </summary>
    public static IDisposable BeginRealtimeConsoleWriteScope()
    {
        _realtimeBypassDepth.Value = _realtimeBypassDepth.Value + 1;
        return new RealtimeConsoleWriteScope();
    }

    private static bool IsRealtimeConsoleWriteEnabled => _realtimeBypassDepth.Value > 0;

    private sealed class RealtimeConsoleWriteScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_realtimeBypassDepth.Value > 0)
                _realtimeBypassDepth.Value = _realtimeBypassDepth.Value - 1;
        }
    }

    /// <inheritdoc />
    public override Encoding Encoding => Encoding.UTF8;

    /// <summary>
    /// 初始化 <see cref="ConsoleInterceptor"/> 的新实例。
    /// </summary>
    /// <param name="originalWriter">原始控制台写入器。</param>
    /// <param name="logFilePath">日志文件路径（保留参数，实际由 Initialize 管理）。</param>
    public ConsoleInterceptor(TextWriter originalWriter, string logFilePath)
    {
        _originalWriter = originalWriter;
    }

    /// <inheritdoc />
    public override void Write(char value)
    {
        try { _originalWriter.Write(value); } catch { }

        if (!EnableFileLogging || _logWriter == null) return;
        var sb = _tls.Value!;
        sb.Append(value);
        if (value == '\n')
        {
            var line = sb.ToString().TrimEnd('\r', '\n');
            TryEnqueueLine(line);
            sb.Clear();
        }
    }

    /// <inheritdoc />
    public override void Write(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        try { _originalWriter.Write(value); } catch { }

        if (!EnableFileLogging || _logWriter == null) return;
        var sb = _tls.Value!;
        int start = 0;
        while (true)
        {
            int idx = value.IndexOf('\n', start);
            if (idx < 0)
            {
                sb.Append(value, start, value.Length - start);
                break;
            }

            int len = idx - start;
            if (len > 0) sb.Append(value, start, len);

            var line = sb.ToString().TrimEnd('\r');
            TryEnqueueLine(line);
            sb.Clear();

            start = idx + 1;
            if (start >= value.Length) break;
        }
    }

    /// <inheritdoc />
    public override void WriteLine(string? value)
    {
        var line = value ?? string.Empty;

        try { _originalWriter.WriteLine(line); } catch { }

        if (!EnableFileLogging || _logWriter == null) return;
        var sb = _tls.Value!;
        if (sb.Length > 0)
        {
            sb.Append(line);
            TryEnqueueLine(sb.ToString());
            sb.Clear();
        }
        else
        {
            TryEnqueueLine(line);
        }
    }

    /// <inheritdoc />
    public override void Flush()
    {
        _originalWriter.Flush();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    private static async Task ProcessLogFileAsync()
    {
        if (_logWriter == null) return;
        var reader = _channel.Reader;
        const int BatchMax = 1024;
        const int FlushIntervalMs = 500;
        var batch = new List<string>(BatchMax);
        long lastFlushTick = Environment.TickCount64;

        try
        {
            await foreach (var item in reader.ReadAllAsync().ConfigureAwait(false))
            {
                batch.Add(item);

                while (batch.Count < BatchMax && reader.TryRead(out var more))
                    batch.Add(more);

                var nowTick = Environment.TickCount64;
                var shouldFlush = batch.Count >= BatchMax || (nowTick - lastFlushTick) >= FlushIntervalMs;
                if (!shouldFlush) continue;

                for (int i = 0; i < batch.Count; i++)
                    await _logWriter.WriteLineAsync(batch[i]).ConfigureAwait(false);

                await _logWriter.FlushAsync().ConfigureAwait(false);
                batch.Clear();
                lastFlushTick = nowTick;
            }

            if (batch.Count > 0)
            {
                for (int i = 0; i < batch.Count; i++)
                    await _logWriter.WriteLineAsync(batch[i]).ConfigureAwait(false);

                await _logWriter.FlushAsync().ConfigureAwait(false);
                batch.Clear();
            }
        }
        catch { }
    }

    private static void TryEnqueueLine(string line)
    {
        if (!EnableFileLogging || _logWriter == null) return;
        _channel.Writer.TryWrite(line);
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

        if (EnableFileLogging)
        {
            _logWriter = new StreamWriter(
                new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
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
