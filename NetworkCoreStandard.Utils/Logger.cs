using System;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace NetworkCoreStandard.Utils
{
    public static class Logger
    {
        private static readonly object _lock = new();
        private static readonly StringBuilder _sharedBuilder = new(128);
        private const string DateTimeFormat = "yyyy/MM/dd HH:mm:ss";
        
        private static TextBox? _boundTextBox;
        private static TextBlock? _boundTextBlock;
        private static RichTextBox? _boundRichTextBox;

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

        private static void LogInternal(LogLevel level, string header, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            lock (_lock)
            {
                try
                {
                    _ = _sharedBuilder.Clear();
                    _ = _sharedBuilder.Append('[')
                        .Append(DateTime.Now.ToString(DateTimeFormat))
                        .Append(']');

                    if (!string.IsNullOrEmpty(header))
                    {
                        _ = _sharedBuilder.Append(" [")
                            .Append(header)
                            .Append(']');
                    }

                    _ = _sharedBuilder.Append(" [")
                        .Append(level.ToString().ToUpper())
                        .Append("] ")
                        .Append(message)
                        .Append(Environment.NewLine);

                    string logText = _sharedBuilder.ToString();

                    // 控制台输出
                    if (_boundTextBox == null && _boundTextBlock == null && _boundRichTextBox == null)
                    {
                        Console.OutputEncoding = Encoding.UTF8;
                        var originalColor = Console.ForegroundColor;
                        Console.ForegroundColor = GetLogLevelColor(level);
                        Console.WriteLine(logText);
                        Console.ForegroundColor = originalColor;
                        return;
                    }

                    // WPF控件输出
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (_boundTextBox != null)
                        {
                            _boundTextBox.AppendText(logText);
                            _boundTextBox.ScrollToEnd();
                        }
                        else if (_boundTextBlock != null)
                        {
                            _boundTextBlock.Text += logText;
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
}