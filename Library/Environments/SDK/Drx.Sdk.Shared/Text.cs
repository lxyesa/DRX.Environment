using System;
using System.Text;
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
    private string? _cachedString;
    private string? _cachedColorPrefix;   // 颜色前缀缓存，颜色改变时重置
    private bool _isDirty;

    // -------------------------------------------------------------------------
    // ANSI 转义码常量（const string 编译期内联，比 char[] 少一次数组复制）
    // -------------------------------------------------------------------------
    private const string BoldCode      = "\x1B[1m";
    private const string ItalicCode    = "\x1B[3m";
    private const string UnderlineCode = "\x1B[4m";
    private const string ResetCode     = "\x1B[0m";
    private const string RgbPrefixHead = "\x1B[38;2;"; // 后跟 R;G;Bm

    // -------------------------------------------------------------------------
    // ConsoleColor → ANSI 前缀字符串（预计算，避免运行时拼接）
    // 枚举值 0-15 与 ConsoleColor 顺序一一对应
    // -------------------------------------------------------------------------
    private static readonly string[] ConsoleColorPrefixes = BuildConsoleColorPrefixes();

    private static string[] BuildConsoleColorPrefixes()
    {
        // ConsoleColor 枚举顺序：Black DarkBlue DarkGreen DarkCyan DarkRed DarkMagenta DarkYellow Gray
        //                         DarkGray Blue Green Cyan Red Magenta Yellow White
        int[] codes = { 30, 34, 32, 36, 31, 35, 33, 37, 90, 94, 92, 96, 91, 95, 93, 97 };
        var result = new string[codes.Length];
        for (int i = 0; i < codes.Length; i++)
            result[i] = $"\x1B[{codes[i]}m";
        return result;
    }

    // -------------------------------------------------------------------------
    // 构造函数
    // -------------------------------------------------------------------------

    /// <summary>初始化一个新的 Text 实例。</summary>
    /// <param name="text">初始文本内容。</param>
    public Text(string text)
    {
        _builder = new StringBuilder(text ?? string.Empty);
        _isDirty = true;
    }

    // -------------------------------------------------------------------------
    // 公共 API（链式调用）
    // -------------------------------------------------------------------------

    /// <summary>设置文本颜色（使用 ConsoleColor）。</summary>
    public Text SetColor(ConsoleColor color)
    {
        _consoleColor = color;
        _rgbColor = null;
        _cachedColorPrefix = null;
        _isDirty = true;
        return this;
    }

    /// <summary>设置文本颜色（使用 RGB 值）。</summary>
    public Text SetColor(int r, int g, int b)
    {
        _rgbColor = (r, g, b);
        _consoleColor = null;
        _cachedColorPrefix = null;
        _isDirty = true;
        return this;
    }

    /// <summary>设置文本为粗体。</summary>
    public Text SetBold()      { _bold      = true; _isDirty = true; return this; }

    /// <summary>设置文本为斜体。</summary>
    public Text SetItalic()    { _italic    = true; _isDirty = true; return this; }

    /// <summary>设置文本为下划线。</summary>
    public Text SetUnderline() { _underline = true; _isDirty = true; return this; }

    /// <summary>追加文本内容。</summary>
    public Text Append(string text)
    {
        _builder.Append(text ?? string.Empty);
        _isDirty = true;
        return this;
    }

    /// <summary>获取当前原始文本长度（不含 ANSI 转义码）。</summary>
    public int Length => _builder.Length;

    /// <summary>检查是否为空文本。</summary>
    public bool IsEmpty => _builder.Length == 0;

    /// <summary>清空缓存，强制下次调用 ToString 时重新生成字符串。</summary>
    public void InvalidateCache()
    {
        _cachedString = null;
        _cachedColorPrefix = null;
        _isDirty = true;
    }

    // -------------------------------------------------------------------------
    // ToString —— 使用 string.Create 实现零中间堆分配
    // -------------------------------------------------------------------------

    /// <summary>生成包含 ANSI 转义码的最终字符串。</summary>
    public override string ToString()
    {
        if (!_isDirty && _cachedString is not null)
            return _cachedString;

        string colorPrefix = GetColorPrefix();
        int textLen = _builder.Length;

        // 精确计算最终字符串长度，避免任何扩容
        int totalLen = textLen + ResetCode.Length;
        if (_bold)      totalLen += BoldCode.Length;
        if (_italic)    totalLen += ItalicCode.Length;
        if (_underline) totalLen += UnderlineCode.Length;
        totalLen += colorPrefix.Length;

        // 如果没有任何样式，直接返回原始文本（无 ANSI 开销）
        if (totalLen == textLen + ResetCode.Length
            && !_bold && !_italic && !_underline && colorPrefix.Length == 0)
        {
            // 退化路径：仅含 ResetCode，保持与添加 ResetCode 的行为一致
            // 若完全无样式且无颜色，直接返回原始文本字符串以节省内存
            _cachedString = _builder.ToString();
            _isDirty = false;
            return _cachedString;
        }

        // 通过 string.Create 一次性写入，整个路径无额外堆分配
        var state = (self: this, colorPrefix, textLen);
        _cachedString = string.Create(totalLen, state, static (span, s) =>
        {
            var (self, cp, tLen) = s;
            int pos = 0;

            if (self._bold)      { BoldCode.AsSpan().CopyTo(span[pos..]);      pos += BoldCode.Length; }
            if (self._italic)    { ItalicCode.AsSpan().CopyTo(span[pos..]);    pos += ItalicCode.Length; }
            if (self._underline) { UnderlineCode.AsSpan().CopyTo(span[pos..]); pos += UnderlineCode.Length; }

            if (cp.Length > 0)
            {
                cp.AsSpan().CopyTo(span[pos..]);
                pos += cp.Length;
            }

            // StringBuilder.CopyTo(Span<char>) 直接写入，无中间字符串
            self._builder.CopyTo(0, span[pos..], tLen);
            pos += tLen;

            ResetCode.AsSpan().CopyTo(span[pos..]);
        });

        _isDirty = false;
        return _cachedString;
    }

    // -------------------------------------------------------------------------
    // operator+ —— 直接操作 StringBuilder，移除 StringBuilderPool
    // -------------------------------------------------------------------------

    /// <summary>将两个 Text 实例连接起来（右侧样式优先）。</summary>
    public static Text operator +(Text left, Text right)
    {
        if (left is null)  return right;
        if (right is null) return left;

        // 拼接原始文本，构造新实例
        var result = new Text(left._builder.Length == 0
            ? right._builder.ToString()
            : right._builder.Length == 0
                ? left._builder.ToString()
                : string.Concat(left._builder.ToString(), right._builder.ToString()));

        // 合并样式（右侧优先）
        result._bold        = right._bold      || left._bold;
        result._italic      = right._italic    || left._italic;
        result._underline   = right._underline || left._underline;
        result._consoleColor = right._consoleColor ?? left._consoleColor;
        result._rgbColor     = right._rgbColor     ?? left._rgbColor;
        // _cachedColorPrefix 保持 null，由 GetColorPrefix 延迟计算
        return result;
    }

    // -------------------------------------------------------------------------
    // 私有辅助方法
    // -------------------------------------------------------------------------

    /// <summary>获取颜色 ANSI 前缀字符串（带缓存）。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetColorPrefix()
    {
        if (_cachedColorPrefix is not null)
            return _cachedColorPrefix;

        if (_rgbColor.HasValue)
        {
            var (r, g, b) = _rgbColor.Value;
            _cachedColorPrefix = BuildRgbPrefix(r, g, b);
        }
        else if (_consoleColor.HasValue)
        {
            int idx = (int)_consoleColor.Value;
            _cachedColorPrefix = (uint)idx < (uint)ConsoleColorPrefixes.Length
                ? ConsoleColorPrefixes[idx]
                : "\x1B[37m"; // 越界时回退到灰色
        }
        else
        {
            _cachedColorPrefix = string.Empty;
        }

        return _cachedColorPrefix;
    }

    /// <summary>
    /// 使用 stackalloc 构造 RGB ANSI 前缀字符串，格式：\x1B[38;2;R;G;Bm。
    /// 全程无堆分配，仅最终 new string(span) 产生一次托管堆写入。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string BuildRgbPrefix(int r, int g, int b)
    {
        // 最大长度：\x1B[38;2; (8) + 3+1+3+1+3 (11) + m (1) = 20 字符
        Span<char> buf = stackalloc char[20];
        RgbPrefixHead.AsSpan().CopyTo(buf);
        int pos = RgbPrefixHead.Length;

        r.TryFormat(buf[pos..], out int w); pos += w;
        buf[pos++] = ';';
        g.TryFormat(buf[pos..], out w);     pos += w;
        buf[pos++] = ';';
        b.TryFormat(buf[pos..], out w);     pos += w;
        buf[pos++] = 'm';

        return new string(buf[..pos]);
    }
}