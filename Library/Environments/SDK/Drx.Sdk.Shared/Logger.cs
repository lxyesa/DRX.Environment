using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DRX.Framework;

public static class Logger
{
    private static readonly object _lockObj = new();
    private static readonly StringBuilder SharedBuilder = new(128);
    private const string DateTimeFormat = "yyyy/MM/dd HH:mm:ss";

    private static TextBox? _boundTextBox;
    private static TextBlock? _boundTextBlock;
    private static RichTextBox? _boundRichTextBox;

    /* P/Invoke 声明 */
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    /* 静态构造函数 */
    static Logger()
    {
#if DEBUG
        AllocConsole();
#endif
    }

    // 绑定方法
    public static void Bind(TextBox textBox)
    {
        _boundTextBox = textBox;
        _boundTextBlock = null;
        _boundRichTextBox = null;
    }

    public static void Bind(TextBlock textBlock)
    {
        _boundTextBox = null;
        _boundTextBlock = textBlock;
        _boundRichTextBox = null;
    }

    public static void Bind(RichTextBox richTextBox)
    {
        _boundTextBox = null;
        _boundTextBlock = null;
        _boundRichTextBox = richTextBox;
    }

    // 保持原有方法签名以兼容现有代码
    public static void Log(string header, string message)
    {
        LogInternal(LogLevel.Info, header, message);
    }

    // 新增重载方法
    public static void Log(LogLevel level, string header, string message)
    {
        LogInternal(level, header, message);
    }

    // 新增 Error 方法
    public static void Error(string message)
    {
        var callerInfo = GetCallerInfo();
        LogInternal(LogLevel.Error, callerInfo, message);
    }

    // 新增 Debug 方法
    public static void Debug(string message)
    {
        var callerInfo = GetCallerInfo();
        LogInternal(LogLevel.Debug, callerInfo, message);
    }

    public static void Warring(string message)
    {
        var callerInfo = GetCallerInfo();
        LogInternal(LogLevel.Warning, callerInfo, message);
    }

    public static void Info(string message)
    {
        var callerInfo = GetCallerInfo();
        LogInternal(LogLevel.Info, callerInfo, message);
    }

    private static string GetCallerInfo()
    {
        var stackTrace = new StackTrace();
        // 跳过 GetCallerInfo 和 Error/Debug/Warning 等包装方法
        var frame = stackTrace.GetFrame(2);
        var method = frame?.GetMethod();
        var className = method?.DeclaringType?.FullName ?? "UnknownClass";
        return className;
    }

    private static void LogInternal(LogLevel level, string header, string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        lock (_lockObj)
        {
            try
            {
                var levelString = level.ToString().ToLower();
                var firstLine = $"{levelString}: {header}[0]";
                var indentedMessage = $"      {message}";

                // 控制台输出
                if (_boundTextBox == null && _boundTextBlock == null && _boundRichTextBox == null)
                {
                    Console.OutputEncoding = Encoding.UTF8;
                    var originalColor = Console.ForegroundColor;

                    Console.ForegroundColor = GetLogLevelColor(level);
                    Console.Write($"{levelString}:");

                    Console.ForegroundColor = originalColor;
                    Console.WriteLine($" {header}[0]");
                    Console.WriteLine(indentedMessage);
                    return;
                }

                // WPF控件输出
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_boundTextBox != null)
                    {
                        _boundTextBox.AppendText(firstLine + Environment.NewLine);
                        _boundTextBox.AppendText(indentedMessage + Environment.NewLine);
                        _boundTextBox.ScrollToEnd();
                    }
                    else if (_boundTextBlock != null)
                    {
                        _boundTextBlock.Text += firstLine + Environment.NewLine;
                        _boundTextBlock.Text += indentedMessage + Environment.NewLine;
                    }
                    else if (_boundRichTextBox != null)
                    {
                        var paragraph = new Paragraph();
                        
                        var levelRun = new Run($"{levelString}:")
                        {
                            Foreground = GetWpfLogLevelBrush(level)
                        };
                        
                        var restOfLineRun = new Run($" {header}[0]{Environment.NewLine}{indentedMessage}")
                        {
                            Foreground = new SolidColorBrush(Colors.White)
                        };

                        paragraph.Inlines.Add(levelRun);
                        paragraph.Inlines.Add(restOfLineRun);
                        _boundRichTextBox.Document.Blocks.Add(paragraph);
                        _boundRichTextBox.ScrollToEnd();
                    }
                });
            }
            catch (Exception)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [ERROR] 日志系统异常: {header} - {message}");
            }
        }
    }

    private static ConsoleColor GetLogLevelColor(LogLevel level) => level switch
    {
        LogLevel.Debug => ConsoleColor.Gray,
        LogLevel.Info => ConsoleColor.Green,
        LogLevel.Warning => ConsoleColor.Yellow,
        LogLevel.Error => ConsoleColor.Red,
        LogLevel.Fatal => ConsoleColor.DarkRed,
        _ => ConsoleColor.White
    };

    private static SolidColorBrush GetWpfLogLevelBrush(LogLevel level) => level switch
    {
        LogLevel.Debug => new SolidColorBrush(Colors.Gray),
        LogLevel.Info => new SolidColorBrush(Colors.Green),
        LogLevel.Warning => new SolidColorBrush(Colors.Yellow),
        LogLevel.Error => new SolidColorBrush(Colors.Red),
        LogLevel.Fatal => new SolidColorBrush(Colors.DarkRed),
        _ => new SolidColorBrush(Colors.White)
    };
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}
