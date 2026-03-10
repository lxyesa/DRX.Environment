// Copyright (c) DRX SDK — Paperclip 路径操作脚本桥接层
// 职责：将 System.IO.Path 常用方法导出到 JS/TS 脚本
// 关键依赖：System.IO

using System;
using System.IO;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 路径操作脚本桥接层。提供路径拼接、解析、扩展名操作等静态 API。
/// </summary>
public static class PathBridge
{
    /// <summary>拼接多个路径片段。</summary>
    public static string combine(string path1, string path2)
        => Path.Combine(path1, path2);

    /// <summary>拼接三个路径片段。</summary>
    public static string combine3(string path1, string path2, string path3)
        => Path.Combine(path1, path2, path3);

    /// <summary>获取绝对路径。</summary>
    public static string getFullPath(string path)
        => Path.GetFullPath(path);

    /// <summary>获取文件名（含扩展名）。</summary>
    public static string getFileName(string path)
        => Path.GetFileName(path);

    /// <summary>获取文件名（不含扩展名）。</summary>
    public static string getFileNameWithoutExtension(string path)
        => Path.GetFileNameWithoutExtension(path);

    /// <summary>获取扩展名（含 '.'）。</summary>
    public static string getExtension(string path)
        => Path.GetExtension(path);

    /// <summary>获取父目录路径。</summary>
    public static string? getDirectoryName(string path)
        => Path.GetDirectoryName(path);

    /// <summary>修改扩展名。</summary>
    public static string changeExtension(string path, string extension)
        => Path.ChangeExtension(path, extension);

    /// <summary>判断路径是否有扩展名。</summary>
    public static bool hasExtension(string path)
        => Path.HasExtension(path);

    /// <summary>判断是否为绝对路径。</summary>
    public static bool isPathRooted(string path)
        => Path.IsPathRooted(path);

    /// <summary>获取路径根部分。</summary>
    public static string? getPathRoot(string path)
        => Path.GetPathRoot(path);

    /// <summary>生成唯一临时文件路径（不创建文件）。</summary>
    public static string getTempFileName()
        => Path.GetTempFileName();

    /// <summary>生成随机文件名。</summary>
    public static string getRandomFileName()
        => Path.GetRandomFileName();

    /// <summary>获取系统临时目录。</summary>
    public static string getTempPath()
        => Path.GetTempPath();

    /// <summary>获取相对路径。</summary>
    public static string getRelativePath(string relativeTo, string path)
        => Path.GetRelativePath(relativeTo, path);

    /// <summary>获取目录分隔符。</summary>
    public static char directorySeparator()
        => Path.DirectorySeparatorChar;

    /// <summary>规范化路径分隔符为当前系统风格。</summary>
    public static string normalize(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        return Path.GetFullPath(path);
    }
}
