using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DRX.Framework;

public static class Logger
{
    private static readonly Lock Lock = new();
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
        Debug("debug模式启动");
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

    private static string GetCallerInfo()
    {
        var stackTrace = new StackTrace();
        // 跳过当前方法和 Debug/Error 方法
        var frame = stackTrace.GetFrame(3);
        var method = frame?.GetMethod();
        var className = method?.DeclaringType?.Name ?? "UnknownClass";
        var methodName = method?.Name ?? "UnknownMethod";
        return $"{className}.{methodName}";
    }

    private static void LogInternal(LogLevel level, string header, string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        lock (Lock)
        {
            try
            {
                SharedBuilder.Clear();
                SharedBuilder.Append('[')
                             .Append(DateTime.Now.ToString(DateTimeFormat))
                             .Append(']');

                if (!string.IsNullOrEmpty(header))
                {
                    SharedBuilder.Append(" [")
                                 .Append(header)
                                 .Append(']');
                }

                SharedBuilder.Append(" [")
                             .Append(level.ToString().ToUpper())
                             .Append("] ")
                             .Append(message);

                var logText = SharedBuilder.ToString();

                // 控制台输出
                if (_boundTextBox == null && _boundTextBlock == null && _boundRichTextBox == null)
                {
                    Console.OutputEncoding = Encoding.UTF8;
                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = GetLogLevelColor(level);
                    Console.Write(logText);
                    Console.ForegroundColor = originalColor;
                    Console.WriteLine();
                    return;
                }

                // WPF控件输出
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_boundTextBox != null)
                    {
                        _boundTextBox.AppendText(logText + Environment.NewLine);
                        _boundTextBox.ScrollToEnd();
                    }
                    else if (_boundTextBlock != null)
                    {
                        _boundTextBlock.Text += logText + Environment.NewLine;
                    }
                    else if (_boundRichTextBox != null)
                    {
                        var paragraph = new Paragraph();
                        var run = new Run(logText)
                        {
                            Foreground = GetWpfLogLevelBrush(level)
                        };
                        paragraph.Inlines.Add(run);
                        _boundRichTextBox.Document.Blocks.Add(paragraph);
                        _boundRichTextBox.ScrollToEnd();
                    }
                });
            }
            catch (Exception)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [ERROR] 日志系统异常");
            }
        }
    }

    private static ConsoleColor GetLogLevelColor(LogLevel level) => level switch
    {
        LogLevel.Debug => ConsoleColor.Gray,
        LogLevel.Info => ConsoleColor.White,
        LogLevel.Warning => ConsoleColor.Yellow,
        LogLevel.Error => ConsoleColor.Red,
        LogLevel.Fatal => ConsoleColor.DarkRed,
        _ => ConsoleColor.White
    };

    private static SolidColorBrush GetWpfLogLevelBrush(LogLevel level) => level switch
    {
        LogLevel.Debug => new SolidColorBrush(Colors.Gray),
        LogLevel.Info => new SolidColorBrush(Colors.White),
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
