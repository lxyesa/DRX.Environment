using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System;
using System.IO;

namespace DRX.Framework;

public static class Logger
{
    private static readonly ConcurrentQueue<LogEntry> _queue = new();
    private static readonly CancellationTokenSource _cts = new();
    private static readonly Task _worker;
    private static readonly StringBuilder SharedBuilder = new(128);

    // 文件日志相关已移除

    private static readonly IReadOnlyDictionary<LogLevel, ConsoleColor> ConsoleColors = new Dictionary<LogLevel, ConsoleColor>
    {
        [LogLevel.Dbug] = ConsoleColor.Gray,
        [LogLevel.Info] = ConsoleColor.DarkGreen,
        [LogLevel.Warn] = ConsoleColor.Yellow,
        [LogLevel.Fail] = ConsoleColor.Red,
        [LogLevel.Fatal] = ConsoleColor.DarkRed
    };

    private static readonly IReadOnlyDictionary<LogLevel, SolidColorBrush> WpfBrushes = new Dictionary<LogLevel, SolidColorBrush>
    {
        [LogLevel.Dbug] = new SolidColorBrush(Colors.Gray),
        [LogLevel.Info] = new SolidColorBrush(Colors.Green),
        [LogLevel.Warn] = new SolidColorBrush(Colors.Yellow),
        [LogLevel.Fail] = new SolidColorBrush(Colors.Red),
        [LogLevel.Fatal] = new SolidColorBrush(Colors.DarkRed)
    };

    private const string DateTimeFormat = "yyyy/MM/dd HH:mm:ss";

    private static WeakReference<TextBox>? _boundTextBox;
    private static WeakReference<TextBlock>? _boundTextBlock;
    private static WeakReference<RichTextBox>? _boundRichTextBox;

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

    public static void Bind(TextBox textBox)
    {
        _boundTextBox = new WeakReference<TextBox>(textBox);
        _boundTextBlock = null;
        _boundRichTextBox = null;
    }

    public static void Bind(TextBlock textBlock)
    {
        _boundTextBox = null;
        _boundTextBlock = new WeakReference<TextBlock>(textBlock);
        _boundRichTextBox = null;
    }

    public static void Bind(RichTextBox richTextBox)
    {
        _boundTextBox = null;
        _boundTextBlock = null;
        _boundRichTextBox = new WeakReference<RichTextBox>(richTextBox);
    }

    public static void Unbind()
    {
        _boundTextBox = null;
        _boundTextBlock = null;
        _boundRichTextBox = null;
    }

    public static void Log(string header, string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Info, header, message, className, lineNumber);
    }

    public static void Log(LogLevel level, string header, string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(level, header, message, className, lineNumber);
    }

    public static void Error(string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Fail, className, message, className, lineNumber);
    }

    public static void Debug(string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Dbug, className, message, className, lineNumber);
    }

    public static void Warn(string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Warn, className, message, className, lineNumber);
    }

    public static void Info(string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        var className = GetShortClassName(filePath);
        Enqueue(LogLevel.Info, className, message, className, lineNumber);
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
        _queue.Enqueue(new LogEntry
        {
            Level = level,
            Header = header,
            Message = message,
            ClassName = className,
            LineNumber = lineNumber,
            Time = DateTime.Now
        });
    }

    private static async Task ProcessQueueAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            if (_queue.IsEmpty)
            {
                await Task.Delay(50);
                continue;
            }

            var batch = new List<LogEntry>(32);
            while (_queue.TryDequeue(out var entry))
            {
                batch.Add(entry);
                if (batch.Count >= 32) break;
            }

            foreach (var log in batch)
            {
                try
                {
                    WriteLog(log);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [ERROR] 日志系统异常: {ex.Message}");
                }
            }
        }
    }

    private static void WriteLog(LogEntry log)
    {
        SharedBuilder.Clear();
        SharedBuilder.Append('[')
            .Append(log.Time.ToString(DateTimeFormat))
            .Append("][")
            .Append(log.ClassName)
            .Append(':')
            .Append(log.LineNumber)
            .Append("][")
            .Append(log.Level.ToString().ToUpper())
            .Append(']')
            .Append(log.Message);

        var formatted = SharedBuilder.ToString();

        // 控件未绑定则输出到控制台
        if ((_boundTextBox == null || !_boundTextBox.TryGetTarget(out var tb)) &&
            (_boundTextBlock == null || !_boundTextBlock.TryGetTarget(out var tblock)) &&
            (_boundRichTextBox == null || !_boundRichTextBox.TryGetTarget(out var rtb)))
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColors.TryGetValue(log.Level, out var cc) ? cc : ConsoleColor.White;
            Console.WriteLine(formatted);
            Console.ForegroundColor = originalColor;
            return;
        }

        // WPF控件异步写入
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_boundTextBox != null && _boundTextBox.TryGetTarget(out var textBox))
            {
                var prevColor = textBox.Foreground;
                textBox.Foreground = WpfBrushes.TryGetValue(log.Level, out var brush) ? brush : Brushes.White;
                textBox.AppendText(formatted + Environment.NewLine);
                textBox.Foreground = prevColor;
                textBox.ScrollToEnd();
            }
            else if (_boundTextBlock != null && _boundTextBlock.TryGetTarget(out var textBlock))
            {
                var run = new Run(formatted + Environment.NewLine)
                {
                    Foreground = WpfBrushes.TryGetValue(log.Level, out var brush) ? brush : Brushes.White
                };
                textBlock.Inlines.Add(run);
            }
            else if (_boundRichTextBox != null && _boundRichTextBox.TryGetTarget(out var richTextBox))
            {
                var paragraph = new Paragraph();
                var run = new Run(formatted)
                {
                    Foreground = WpfBrushes.TryGetValue(log.Level, out var brush) ? brush : Brushes.White
                };
                paragraph.Inlines.Add(run);
                richTextBox.Document.Blocks.Add(paragraph);
                richTextBox.ScrollToEnd();
            }
        });
    }

        // LoggerFileTextWriter 已移除，日志文件写入由 WriteLog 控制
    }
    
    // 控制台输出拦截器：将所有 Console.Out/Error 输出同步写入日志文件
    public sealed class ConsoleInterceptor : TextWriter
    {
        private readonly TextWriter _originalWriter;
        private readonly StreamWriter _logWriter;
        private readonly object _lock = new();
        public override Encoding Encoding => Encoding.UTF8;
    
        public ConsoleInterceptor(TextWriter originalWriter, string logFilePath)
        {
            _originalWriter = originalWriter;
            _logWriter = new StreamWriter(new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = true
            };
        }
    
        public override void Write(char value)
        {
            lock (_lock)
            {
                _originalWriter.Write(value);
                _logWriter.Write(value);
            }
        }
    
        public override void Write(string? value)
        {
            lock (_lock)
            {
                _originalWriter.Write(value);
                _logWriter.Write(value);
            }
        }
    
        public override void WriteLine(string? value)
        {
            lock (_lock)
            {
                _originalWriter.WriteLine(value);
                _logWriter.WriteLine(value);
            }
        }
    
        public override void Flush()
        {
            lock (_lock)
            {
                _originalWriter.Flush();
                _logWriter.Flush();
            }
        }
    
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
    
        // 静态方法：初始化拦截器，重定向 Console.Out/Error
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
    
    public enum LogLevel
    {
        Dbug,
        Info,
        Warn,
        Fail,
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
