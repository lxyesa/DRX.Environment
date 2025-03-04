using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace Drx.Sdk.Ui.Wpf.Console
{
    /// <summary>
    /// 重定向控制台输出到文本控件的TextWriter实现
    /// </summary>
    public class RichTextBoxOutputWriter : TextWriter
    {
        private readonly UIElement _textControl;
        private readonly TextBlock? _textBlock;
        private readonly RichTextBox? _richTextBox;
        private readonly int _maxLines;
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly Dispatcher _dispatcher;
        private readonly bool _useColor;
        private readonly bool _appendNewLine;

        // 用于控制自动刷新的计时器
        private readonly System.Timers.Timer _autoFlushTimer;

        // 标记当前输出是否来自WriteLine
        private bool _isLineOutput = false;

        // 构造函数 - TextBlock版本
        public RichTextBoxOutputWriter(TextBlock textBlock, int maxLines = 1000, bool useColor = false, bool appendNewLine = true)
        {
            _textControl = textBlock ?? throw new ArgumentNullException(nameof(textBlock));
            _textBlock = textBlock;
            _richTextBox = null;
            _maxLines = maxLines;
            _dispatcher = _textControl.Dispatcher;
            _useColor = useColor;
            _appendNewLine = appendNewLine;

            // 设置自动刷新定时器，每100毫秒检查一次有无需要刷新的内容
            _autoFlushTimer = new System.Timers.Timer(100);
            _autoFlushTimer.Elapsed += (s, e) => FlushIfNeeded();
            _autoFlushTimer.Start();
        }

        // 构造函数 - RichTextBox版本
        public RichTextBoxOutputWriter(RichTextBox richTextBox, int maxLines = 1000, bool useColor = false, bool appendNewLine = true)
        {
            _textControl = richTextBox ?? throw new ArgumentNullException(nameof(richTextBox));
            _textBlock = null;
            _richTextBox = richTextBox;
            _maxLines = maxLines;
            _dispatcher = _textControl.Dispatcher;
            _useColor = useColor;
            _appendNewLine = appendNewLine;

            // 设置自动刷新定时器，每100毫秒检查一次有无需要刷新的内容
            _autoFlushTimer = new System.Timers.Timer(100);
            _autoFlushTimer.Elapsed += (s, e) => FlushIfNeeded();
            _autoFlushTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoFlushTimer.Stop();
                _autoFlushTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            _isLineOutput = false;
            _buffer.Append(value);
            if (value == '\n')
            {
                Flush();
            }
            else
            {
                // 立即刷新单个字符，确保实时显示
                Flush();
            }
        }

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;

            _isLineOutput = false;
            _buffer.Append(value);

            // 无论是否包含换行符，都立即刷新，确保内容实时显示
            Flush();
        }

        public override void WriteLine(string? value)
        {
            _isLineOutput = true;

            if (value != null)
            {
                _buffer.Append(value);
            }

            // 不在这里添加换行符，让UpdateTextControl方法根据_isLineOutput标记来处理换行
            // 避免双重换行效果

            Flush();
        }

        // 检查是否需要刷新缓冲区
        private void FlushIfNeeded()
        {
            if (_buffer.Length > 0)
            {
                if (_dispatcher.CheckAccess())
                {
                    Flush();
                }
                else
                {
                    _dispatcher.BeginInvoke(new Action(Flush));
                }
            }
        }

        public override void Flush()
        {
            if (_buffer.Length == 0) return;

            string content = _buffer.ToString();
            bool isLineOutput = _isLineOutput; // 保存当前状态
            _buffer.Clear();

            if (_dispatcher.CheckAccess())
            {
                UpdateTextControl(content, isLineOutput);
            }
            else
            {
                _dispatcher.BeginInvoke(new Action(() => UpdateTextControl(content, isLineOutput)));
            }
        }

        private void UpdateTextControl(string content, bool isLineOutput)
        {
            // 如果内容已经有换行符，则不需要在末尾再添加新段落
            bool hasTrailingNewline = content.EndsWith("\n") || content.EndsWith("\r\n");

            if (_richTextBox != null)
            {
                // 获取或创建段落
                Paragraph paragraph;
                if (_richTextBox.Document.Blocks.Count == 0)
                {
                    paragraph = new Paragraph();
                    paragraph.LineHeight = 1;
                    paragraph.Margin = new Thickness(0); // 减少段落间距
                    _richTextBox.Document.Blocks.Add(paragraph);
                }
                else
                {
                    paragraph = (Paragraph)_richTextBox.Document.Blocks.LastBlock;
                }

                // 如果是WriteLine的输出，但内容不以换行符结尾，就添加换行符
                string outputContent = content;
                if (isLineOutput && !hasTrailingNewline)
                {
                    // 如果是WriteLine，则在内容末尾添加一个LineBreak（而不是新段落）
                    paragraph.Inlines.Add(new Run(outputContent));
                    paragraph.Inlines.Add(new LineBreak());
                    return;
                }

                // 处理普通内容或已经带有换行符的内容
                if (_useColor && TryParseLogLevel(outputContent, out string text, out LogLevel level))
                {
                    // 添加带颜色的文本
                    var run = new Run(text)
                    {
                        Foreground = GetBrushForLogLevel(level)
                    };
                    paragraph.Inlines.Add(run);
                }
                else
                {
                    paragraph.Inlines.Add(new Run(outputContent));
                }

                // 如果内容中有换行符，则根据换行符分割并创建适当的换行效果
                if (outputContent.Contains("\n"))
                {
                    string[] lines = outputContent.Split(new[] { '\n' }, StringSplitOptions.None);
                    // 最后一行不需要添加LineBreak
                    for (int i = 0; i < lines.Length - 1; i++)
                    {
                        paragraph.Inlines.Add(new LineBreak());
                    }
                }
            }
            else if (_textBlock != null)
            {
                // TextBlock简单追加文本
                _textBlock.Text += content;
                if (isLineOutput && !hasTrailingNewline)
                {
                    _textBlock.Text += Environment.NewLine;
                }
            }

            // 限制最大行数
            if (_maxLines > 0)
            {
                if (_textBlock != null)
                {
                    var lines = _textBlock.Text.Split('\n');
                    if (lines.Length > _maxLines)
                    {
                        _textBlock.Text = string.Join("\n", lines, lines.Length - _maxLines, _maxLines);
                    }
                }
                else if (_richTextBox != null)
                {
                    // 限制RichTextBox的最大行数（通过计算LineBreak的数量）
                    int lineCount = 0;
                    List<Paragraph> paragraphs = new List<Paragraph>();

                    foreach (var block in _richTextBox.Document.Blocks)
                    {
                        if (block is Paragraph para)
                        {
                            paragraphs.Add(para);

                            // 计算段落中的行数（LineBreak数量+1）
                            int paraLines = 1;
                            foreach (var inline in para.Inlines)
                            {
                                if (inline is LineBreak)
                                {
                                    paraLines++;
                                }
                            }

                            lineCount += paraLines;
                        }
                    }

                    // 如果行数超过最大限制，从头部移除行
                    if (lineCount > _maxLines)
                    {
                        int linesToRemove = lineCount - _maxLines;
                        int removedLines = 0;

                        for (int i = 0; i < paragraphs.Count && removedLines < linesToRemove; i++)
                        {
                            Paragraph para = paragraphs[i];
                            List<Inline> inlines = new List<Inline>(para.Inlines);

                            // 移除该段落中的行，直到达到所需的移除行数
                            for (int j = 0; j < inlines.Count && removedLines < linesToRemove; j++)
                            {
                                para.Inlines.Remove(inlines[j]);
                                if (inlines[j] is LineBreak)
                                {
                                    removedLines++;
                                }

                                if (j == inlines.Count - 1) // 如果移除了最后一个内联元素
                                {
                                    removedLines++; // 算作移除了一整行
                                }
                            }

                            // 如果段落已空，则移除整个段落
                            if (!para.Inlines.Any())
                            {
                                _richTextBox.Document.Blocks.Remove(para);
                            }
                        }
                    }
                }
            }

            // 滚动到底部
            ScrollToEnd();
        }

        private void ScrollToEnd()
        {
            // 尝试查找父级ScrollViewer并滚动到底部
            if (_richTextBox != null)
            {
                _richTextBox.ScrollToEnd();
            }
            else
            {
                DependencyObject parent = _textControl;
                while (parent != null)
                {
                    parent = VisualTreeHelper.GetParent(parent);
                    if (parent is ScrollViewer scrollViewer)
                    {
                        scrollViewer.ScrollToEnd();
                        break;
                    }
                }
            }
        }

        private bool TryParseLogLevel(string content, out string text, out LogLevel level)
        {
            text = content;
            level = LogLevel.Info;

            // 简单尝试解析日志级别，可根据实际日志格式调整
            if (content.Contains("[DEBUG]"))
            {
                level = LogLevel.Debug;
                return true;
            }
            else if (content.Contains("[INFO]"))
            {
                level = LogLevel.Info;
                return true;
            }
            else if (content.Contains("[WARNING]") || content.Contains("[WARN]"))
            {
                level = LogLevel.Warning;
                return true;
            }
            else if (content.Contains("[ERROR]"))
            {
                level = LogLevel.Error;
                return true;
            }
            else if (content.Contains("[FATAL]"))
            {
                level = LogLevel.Fatal;
                return true;
            }

            return false;
        }

        private SolidColorBrush GetBrushForLogLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => new SolidColorBrush(Colors.Gray),
                LogLevel.Info => new SolidColorBrush(Colors.White),
                LogLevel.Warning => new SolidColorBrush(Colors.Yellow),
                LogLevel.Error => new SolidColorBrush(Colors.Red),
                LogLevel.Fatal => new SolidColorBrush(Colors.DarkRed),
                _ => new SolidColorBrush(Colors.White)
            };
        }
    }

    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    /// <summary>
    /// 控制台输出重定向工具类
    /// </summary>
    public static class ConsoleRedirector
    {
        private static readonly TextWriter OriginalOut = System.Console.Out;
        private static RichTextBoxOutputWriter? _customWriter;

        /// <summary>
        /// 重定向控制台输出到指定的TextBlock
        /// </summary>
        /// <param name="textBlock">目标TextBlock</param>
        /// <param name="maxLines">保留的最大行数</param>
        /// <param name="useColor">是否尝试使用颜色（根据日志级别）</param>
        public static void RedirectToTextBlock(TextBlock textBlock, int maxLines = 1000, bool useColor = false)
        {
            if (textBlock == null)
                throw new ArgumentNullException(nameof(textBlock));

            _customWriter = new RichTextBoxOutputWriter(textBlock, maxLines, useColor);
            System.Console.SetOut(_customWriter);
        }

        /// <summary>
        /// 重定向控制台输出到指定的RichTextBox
        /// </summary>
        /// <param name="richTextBox">目标RichTextBox</param>
        /// <param name="maxLines">保留的最大行数</param>
        /// <param name="useColor">是否尝试使用颜色（根据日志级别）</param>
        /// <param name="appendNewLine">是否在每行末尾添加换行符</param>
        public static void RedirectToRichTextBox(RichTextBox richTextBox, int maxLines = 1000, bool useColor = true, bool appendNewLine = false)
        {
            if (richTextBox == null)
                throw new ArgumentNullException(nameof(richTextBox));

            _customWriter = new RichTextBoxOutputWriter(richTextBox, maxLines, useColor, appendNewLine);
            System.Console.SetOut(_customWriter);
        }

        /// <summary>
        /// 恢复原始的控制台输出
        /// </summary>
        public static void RestoreOriginalOutput()
        {
            System.Console.SetOut(OriginalOut);
            _customWriter?.Dispose();
            _customWriter = null;
        }
    }
}
