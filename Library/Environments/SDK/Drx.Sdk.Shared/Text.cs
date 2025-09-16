using System;
using System.Text;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Drx.Sdk.Shared;

/// <summary>
/// 提供用于生成带ANSI转义码的文本类，支持颜色、样式设置和链式调用。
/// </summary>
public class Text
{
    private readonly StringBuilder _builder;
    private bool _bold;
    private bool _italic;
    private bool _underline;
    private ConsoleColor? _consoleColor;
    private (int r, int g, int b)? _rgbColor;
    private string _cachedString;
    private bool _isDirty;

    // 预定义ANSI转义码常量 - 使用静态字段
    private static readonly char[] BoldCodeChars = { '\e', '[', '1', 'm' };
    private static readonly char[] ItalicCodeChars = { '\e', '[', '3', 'm' };
    private static readonly char[] UnderlineCodeChars = { '\e', '[', '4', 'm' };
    private static readonly char[] ResetCodeChars = { '\e', '[', '0', 'm' };
    private static readonly char[] ColorPrefixChars = { '\e', '[', '3', '8', ';', '2', ';' };
    private static readonly char[] ColorSuffixChars = { 'm' };

    // 颜色码数组，使用静态字段
    private static readonly int[] AnsiColorCodesArray = new[]
    {
        30, // Black
        34, // DarkBlue
        32, // DarkGreen
        36, // DarkCyan
        31, // DarkRed
        35, // DarkMagenta
        33, // DarkYellow
        37, // Gray
        90, // DarkGray
        94, // Blue
        92, // Green
        96, // Cyan
        91, // Red
        95, // Magenta
        93, // Yellow
        97  // White
    };

    // StringBuilder对象池，避免频繁分配
    private static class StringBuilderPool
    {
        private const int MaxCapacity = 1024;
        private const int MaxPooledCapacity = 512;

        [ThreadStatic]
        private static StringBuilder? _cachedInstance;

        public static StringBuilder Rent(int capacity)
        {
            StringBuilder? builder = _cachedInstance;
            if (builder != null && builder.Capacity >= capacity)
            {
                _cachedInstance = null;
                builder.Clear();
                return builder;
            }
            return new StringBuilder(Math.Max(capacity, 16));
        }

        public static void Return(StringBuilder builder)
        {
            if (builder.Capacity <= MaxPooledCapacity)
            {
                _cachedInstance = builder;
            }
        }
    }

    /// <summary>
    /// 初始化一个新的Text实例。
    /// </summary>
    /// <param name="text">初始文本内容。</param>
    public Text(string text)
    {
        _builder = new StringBuilder(text ?? string.Empty);
        _isDirty = true;
    }

    /// <summary>
    /// 设置文本颜色（使用ConsoleColor）。
    /// </summary>
    /// <param name="color">控制台颜色。</param>
    /// <returns>当前Text实例，支持链式调用。</returns>
    public Text SetColor(ConsoleColor color)
    {
        _consoleColor = color;
        _rgbColor = null; // 重置RGB颜色
        _isDirty = true;
        return this;
    }

    /// <summary>
    /// 设置文本颜色（使用RGB值）。
    /// </summary>
    /// <param name="r">红色分量（0-255）。</param>
    /// <param name="g">绿色分量（0-255）。</param>
    /// <param name="b">蓝色分量（0-255）。</param>
    /// <returns>当前Text实例，支持链式调用。</returns>
    public Text SetColor(int r, int g, int b)
    {
        _rgbColor = (r, g, b);
        _consoleColor = null; // 重置ConsoleColor
        _isDirty = true;
        return this;
    }

    /// <summary>
    /// 设置文本为粗体。
    /// </summary>
    /// <returns>当前Text实例，支持链式调用。</returns>
    public Text SetBold()
    {
        _bold = true;
        _isDirty = true;
        return this;
    }

    /// <summary>
    /// 设置文本为斜体。
    /// </summary>
    /// <returns>当前Text实例，支持链式调用。</returns>
    public Text SetItalic()
    {
        _italic = true;
        _isDirty = true;
        return this;
    }

    /// <summary>
    /// 设置文本为下划线。
    /// </summary>
    /// <returns>当前Text实例，支持链式调用。</returns>
    public Text SetUnderline()
    {
        _underline = true;
        _isDirty = true;
        return this;
    }

    /// <summary>
    /// 追加文本内容。
    /// </summary>
    /// <param name="text">要追加的文本。</param>
    /// <returns>当前Text实例，支持链式调用。</returns>
    public Text Append(string text)
    {
        _builder.Append(text ?? string.Empty);
        _isDirty = true;
        return this;
    }

    /// <summary>
    /// 生成包含ANSI转义码的最终字符串。
    /// </summary>
    /// <returns>带样式的字符串。</returns>
    public override string ToString()
    {
        if (!_isDirty && _cachedString != null)
        {
            return _cachedString;
        }

        // 预估容量以减少重新分配
        int estimatedCapacity = _builder.Length + 20; // 基础容量 + ANSI码空间
        if (_bold) estimatedCapacity += BoldCodeChars.Length;
        if (_italic) estimatedCapacity += ItalicCodeChars.Length;
        if (_underline) estimatedCapacity += UnderlineCodeChars.Length;
        if (_rgbColor.HasValue) estimatedCapacity += ColorPrefixChars.Length + 12; // RGB颜色最长11字符 + m
        if (_consoleColor.HasValue) estimatedCapacity += 5; // \e[XXm
        estimatedCapacity += ResetCodeChars.Length;

        var sb = StringBuilderPool.Rent(estimatedCapacity);

        try
        {
            // 应用样式
            if (_bold) sb.Append(BoldCodeChars);
            if (_italic) sb.Append(ItalicCodeChars);
            if (_underline) sb.Append(UnderlineCodeChars);

            // 应用颜色
            if (_rgbColor.HasValue)
            {
                var (r, g, b) = _rgbColor.Value;
                sb.Append(ColorPrefixChars);
                AppendInt32(sb, r);
                sb.Append(';');
                AppendInt32(sb, g);
                sb.Append(';');
                AppendInt32(sb, b);
                sb.Append(ColorSuffixChars);
            }
            else if (_consoleColor.HasValue)
            {
                int colorCode = GetAnsiColorCode(_consoleColor.Value);
                sb.Append('\e').Append('[').Append(colorCode).Append('m');
            }

            // 添加文本内容
            sb.Append(_builder);

            // 重置样式
            sb.Append(ResetCodeChars);

            _cachedString = sb.ToString();
            _isDirty = false;
            return _cachedString;
        }
        finally
        {
            StringBuilderPool.Return(sb);
        }
    }

    /// <summary>
    /// 将两个Text实例连接起来。
    /// </summary>
    /// <param name="left">左侧Text实例。</param>
    /// <param name="right">右侧Text实例。</param>
    /// <returns>新的Text实例，包含两个文本的组合。</returns>
    public static Text operator +(Text left, Text right)
    {
        if (left == null) return right;
        if (right == null) return left;

        // 优化：预估容量避免重新分配
        int totalCapacity = left._builder.Length + right._builder.Length + 1; // +1 for space or other separator
        var combinedBuilder = StringBuilderPool.Rent(totalCapacity);

        try
        {
            combinedBuilder.Append(left._builder);
            combinedBuilder.Append(right._builder);

            var result = new Text(combinedBuilder.ToString());
            // 合并样式（右侧样式优先）
            result._bold = right._bold;
            result._italic = right._italic;
            result._underline = right._underline;
            result._consoleColor = right._consoleColor;
            result._rgbColor = right._rgbColor;
            result._isDirty = true; // 新实例总是脏的

            return result;
        }
        finally
        {
            StringBuilderPool.Return(combinedBuilder);
        }
    }

    /// <summary>
    /// 清空缓存，强制下次调用ToString时重新生成字符串。
    /// </summary>
    public void InvalidateCache()
    {
        _cachedString = string.Empty;
        _isDirty = true;
    }

    /// <summary>
    /// 获取当前文本长度（不包含ANSI转义码）。
    /// </summary>
    public int Length => _builder.Length;

    /// <summary>
    /// 检查是否为空文本。
    /// </summary>
    public bool IsEmpty => _builder.Length == 0;

    /// <summary>
    /// 高效地将整数转换为字符串，避免装箱。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendInt32(StringBuilder sb, int value)
    {
        // 对于0-255范围的RGB值，使用快速路径
        if (value >= 0 && value <= 255)
        {
            if (value < 10)
            {
                sb.Append((char)('0' + value));
            }
            else if (value < 100)
            {
                sb.Append((char)('0' + value / 10));
                sb.Append((char)('0' + value % 10));
            }
            else
            {
                sb.Append((char)('0' + value / 100));
                int remainder = value % 100;
                sb.Append((char)('0' + remainder / 10));
                sb.Append((char)('0' + remainder % 10));
            }
        }
        else
        {
            sb.Append(value);
        }
    }

    /// <summary>
    /// 获取ConsoleColor对应的ANSI颜色码。
    /// </summary>
    /// <param name="color">控制台颜色。</param>
    /// <returns>ANSI颜色码。</returns>
    private static int GetAnsiColorCode(ConsoleColor color)
    {
        int index = (int)color;
        if (index >= 0 && index < AnsiColorCodesArray.Length)
        {
            return AnsiColorCodesArray[index];
        }
        return 37; // 默认灰色
    }
}