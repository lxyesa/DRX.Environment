// Copyright (c) DRX SDK — Paperclip 文件操作脚本桥接层
// 职责：将 System.IO.File / FileStream / StreamReader / StreamWriter / BinaryReader / BinaryWriter
//       常用能力导出到 JS/TS 脚本，同时提供 DrxFileStream 可控流句柄。
// 关键依赖：System.IO

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 文件操作脚本桥接层。提供文件读写、复制、移动、信息查询等静态 API。
/// </summary>
public static class FileBridge
{
    // ── 存在性 ────────────────────────────────────────

    /// <summary>判断文件是否存在。</summary>
    public static bool exists(string path)
        => File.Exists(path);

    // ── 读取 ──────────────────────────────────────────

    /// <summary>以 UTF-8 读取文件全部文本。</summary>
    public static string readAllText(string path, string? encoding = null)
        => File.ReadAllText(path, ResolveEncoding(encoding));

    /// <summary>以 UTF-8 读取文件所有行，返回字符串数组。</summary>
    public static string[] readAllLines(string path, string? encoding = null)
        => File.ReadAllLines(path, ResolveEncoding(encoding));

    /// <summary>读取文件全部字节，返回 Base64 字符串。</summary>
    public static string readAllBytesBase64(string path)
        => Convert.ToBase64String(File.ReadAllBytes(path));

    // ── 写入 ──────────────────────────────────────────

    /// <summary>将文本写入文件（覆盖），默认 UTF-8（无 BOM）。</summary>
    public static void writeAllText(string path, string content, string? encoding = null)
        => File.WriteAllText(path, content, ResolveEncoding(encoding));

    /// <summary>将字符串数组写入文件（每行一个元素，覆盖）。</summary>
    public static void writeAllLines(string path, string[] lines, string? encoding = null)
        => File.WriteAllLines(path, lines, ResolveEncoding(encoding));

    /// <summary>将 Base64 字节写入文件（覆盖）。</summary>
    public static void writeAllBytesBase64(string path, string base64)
        => File.WriteAllBytes(path, Convert.FromBase64String(base64));

    // ── 追加 ──────────────────────────────────────────

    /// <summary>向文件追加文本（不存在则创建）。</summary>
    public static void appendAllText(string path, string content, string? encoding = null)
        => File.AppendAllText(path, content, ResolveEncoding(encoding));

    /// <summary>向文件追加多行（不存在则创建）。</summary>
    public static void appendAllLines(string path, string[] lines, string? encoding = null)
        => File.AppendAllLines(path, lines, ResolveEncoding(encoding));

    // ── 异步读写 ──────────────────────────────────────

    /// <summary>异步读取文件全部文本。</summary>
    public static Task<string> readAllTextAsync(string path, string? encoding = null)
        => File.ReadAllTextAsync(path, ResolveEncoding(encoding));

    /// <summary>异步写入文本。</summary>
    public static Task writeAllTextAsync(string path, string content, string? encoding = null)
        => File.WriteAllTextAsync(path, content, ResolveEncoding(encoding));

    /// <summary>异步追加文本。</summary>
    public static Task appendAllTextAsync(string path, string content, string? encoding = null)
        => File.AppendAllTextAsync(path, content, ResolveEncoding(encoding));

    // ── 管理 ──────────────────────────────────────────

    /// <summary>复制文件。</summary>
    public static void copy(string sourceFileName, string destFileName, bool overwrite = false)
        => File.Copy(sourceFileName, destFileName, overwrite);

    /// <summary>移动（重命名）文件。</summary>
    public static void move(string sourceFileName, string destFileName, bool overwrite = false)
        => File.Move(sourceFileName, destFileName, overwrite);

    /// <summary>删除文件（不存在时不报错）。</summary>
    public static void delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    // ── 信息 ──────────────────────────────────────────

    /// <summary>获取文件大小（字节）。</summary>
    public static long getSize(string path)
        => new FileInfo(path).Length;

    /// <summary>获取文件创建时间（ISO 8601 本地时间字符串）。</summary>
    public static string getCreationTime(string path)
        => File.GetCreationTime(path).ToString("o");

    /// <summary>获取文件最后修改时间（ISO 8601 本地时间字符串）。</summary>
    public static string getLastWriteTime(string path)
        => File.GetLastWriteTime(path).ToString("o");

    /// <summary>获取文件最后访问时间（ISO 8601 本地时间字符串）。</summary>
    public static string getLastAccessTime(string path)
        => File.GetLastAccessTime(path).ToString("o");

    // ── 流 ───────────────────────────────────────────

    /// <summary>打开文件流。mode: read / write / append / readWrite</summary>
    public static DrxFileStreamHandle openStream(string path, string mode = "read")
    {
        var (fileMode, fileAccess) = mode.ToLowerInvariant() switch
        {
            "write"     => (FileMode.Create,   FileAccess.Write),
            "append"    => (FileMode.Append,   FileAccess.Write),
            "readwrite" => (FileMode.OpenOrCreate, FileAccess.ReadWrite),
            _           => (FileMode.Open,     FileAccess.Read),
        };
        return new DrxFileStreamHandle(new FileStream(path, fileMode, fileAccess, FileShare.Read));
    }

    /// <summary>打开文本读取器（StreamReader）。</summary>
    public static DrxStreamReaderHandle openReader(string path, string? encoding = null)
        => new DrxStreamReaderHandle(new StreamReader(path, ResolveEncoding(encoding)));

    /// <summary>打开文本写入器（StreamWriter，覆盖模式）。</summary>
    public static DrxStreamWriterHandle openWriter(string path, bool append = false, string? encoding = null)
        => new DrxStreamWriterHandle(new StreamWriter(path, append, ResolveEncoding(encoding)));

    // ── 内部工具 ──────────────────────────────────────

    private static Encoding ResolveEncoding(string? name) => name?.ToLowerInvariant() switch
    {
        "utf8" or "utf-8" or null => new UTF8Encoding(false),
        "utf8bom" or "utf-8-bom"  => new UTF8Encoding(true),
        "ascii"                    => Encoding.ASCII,
        "unicode" or "utf-16"      => Encoding.Unicode,
        "utf-32"                   => Encoding.UTF32,
        _                          => Encoding.GetEncoding(name!),
    };
}

// ── 流句柄 ────────────────────────────────────────────────────────────────────

/// <summary>
/// 文件流句柄（底层 <see cref="FileStream"/>）。
/// 通过 <see cref="FileBridge.openStream"/> 创建；使用完毕须调用 <see cref="close"/>。
/// </summary>
public sealed class DrxFileStreamHandle : IDisposable
{
    private readonly FileStream _stream;

    internal DrxFileStreamHandle(FileStream stream) => _stream = stream;

    /// <summary>流当前位置（字节偏移）。</summary>
    public long position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    /// <summary>流总长度（字节）。</summary>
    public long length => _stream.Length;

    /// <summary>是否可读。</summary>
    public bool canRead => _stream.CanRead;

    /// <summary>是否可写。</summary>
    public bool canWrite => _stream.CanWrite;

    /// <summary>是否可定位（Seek）。</summary>
    public bool canSeek => _stream.CanSeek;

    /// <summary>读取字节，返回 Base64 字符串；读到末尾返回空字符串。</summary>
    /// <param name="count">最多读取字节数，默认 4096。</param>
    public string readBytes(int count = 4096)
    {
        var buf = new byte[count];
        int n = _stream.Read(buf, 0, count);
        return n == 0 ? "" : Convert.ToBase64String(buf, 0, n);
    }

    /// <summary>将 Base64 字节写入流。</summary>
    public void writeBytes(string base64)
    {
        var data = Convert.FromBase64String(base64);
        _stream.Write(data, 0, data.Length);
    }

    /// <summary>定位流到指定位置。origin: begin / current / end</summary>
    public long seek(long offset, string origin = "begin")
    {
        var so = origin.ToLowerInvariant() switch
        {
            "current" => SeekOrigin.Current,
            "end"     => SeekOrigin.End,
            _         => SeekOrigin.Begin,
        };
        return _stream.Seek(offset, so);
    }

    /// <summary>刷新缓冲区到磁盘。</summary>
    public void flush() => _stream.Flush();

    /// <summary>截断/扩展流到指定长度。</summary>
    public void setLength(long length) => _stream.SetLength(length);

    /// <summary>异步读取，返回 Base64 字符串。</summary>
    public async Task<string> readBytesAsync(int count = 4096)
    {
        var buf = new byte[count];
        int n = await _stream.ReadAsync(buf, 0, count);
        return n == 0 ? "" : Convert.ToBase64String(buf, 0, n);
    }

    /// <summary>异步写入 Base64 字节。</summary>
    public async Task writeBytesAsync(string base64)
    {
        var data = Convert.FromBase64String(base64);
        await _stream.WriteAsync(data, 0, data.Length);
    }

    /// <summary>关闭并释放流资源。</summary>
    public void close() => _stream.Dispose();

    void IDisposable.Dispose() => _stream.Dispose();
}

/// <summary>
/// 文本读取器句柄（底层 <see cref="StreamReader"/>）。
/// 通过 <see cref="FileBridge.openReader"/> 创建；使用完毕须调用 <see cref="close"/>。
/// </summary>
public sealed class DrxStreamReaderHandle : IDisposable
{
    private readonly StreamReader _reader;

    internal DrxStreamReaderHandle(StreamReader reader) => _reader = reader;

    /// <summary>是否已到流末尾。</summary>
    public bool endOfStream => _reader.EndOfStream;

    /// <summary>读取一行，到末尾返回 null。</summary>
    public string? readLine() => _reader.ReadLine();

    /// <summary>读取所有剩余文本。</summary>
    public string readToEnd() => _reader.ReadToEnd();

    /// <summary>异步读取一行。</summary>
    public Task<string?> readLineAsync() => _reader.ReadLineAsync();

    /// <summary>异步读取所有剩余文本。</summary>
    public Task<string> readToEndAsync() => _reader.ReadToEndAsync();

    /// <summary>关闭读取器。</summary>
    public void close() => _reader.Dispose();

    void IDisposable.Dispose() => _reader.Dispose();
}

/// <summary>
/// 文本写入器句柄（底层 <see cref="StreamWriter"/>）。
/// 通过 <see cref="FileBridge.openWriter"/> 创建；使用完毕须调用 <see cref="close"/>。
/// </summary>
public sealed class DrxStreamWriterHandle : IDisposable
{
    private readonly StreamWriter _writer;

    internal DrxStreamWriterHandle(StreamWriter writer) => _writer = writer;

    /// <summary>自动刷新（每次 write 后立即 flush）。</summary>
    public bool autoFlush
    {
        get => _writer.AutoFlush;
        set => _writer.AutoFlush = value;
    }

    /// <summary>写入文本（不换行）。</summary>
    public void write(string text) => _writer.Write(text);

    /// <summary>写入文本并换行。</summary>
    public void writeLine(string? text = null) => _writer.WriteLine(text);

    /// <summary>异步写入文本。</summary>
    public Task writeAsync(string text) => _writer.WriteAsync(text);

    /// <summary>异步写入文本并换行。</summary>
    public Task writeLineAsync(string? text = null) => _writer.WriteLineAsync(text);

    /// <summary>刷新写入器缓冲区。</summary>
    public void flush() => _writer.Flush();

    /// <summary>关闭写入器。</summary>
    public void close() => _writer.Dispose();

    void IDisposable.Dispose() => _writer.Dispose();
}
