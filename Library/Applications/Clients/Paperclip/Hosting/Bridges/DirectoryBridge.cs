// Copyright (c) DRX SDK — Paperclip 目录操作脚本桥接层
// 职责：将 System.IO.Directory 常用能力导出到 JS/TS 脚本
// 关键依赖：System.IO

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 目录操作脚本桥接层。提供目录的创建、删除、枚举、移动等静态 API。
/// </summary>
public static class DirectoryBridge
{
    /// <summary>判断目录是否存在。</summary>
    public static bool exists(string path)
        => Directory.Exists(path);

    /// <summary>创建目录（含中间目录）。</summary>
    public static string create(string path)
    {
        var info = Directory.CreateDirectory(path);
        return info.FullName;
    }

    /// <summary>递归删除目录。</summary>
    public static void delete(string path, bool recursive = true)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive);
    }

    /// <summary>移动目录。</summary>
    public static void move(string sourceDirName, string destDirName)
        => Directory.Move(sourceDirName, destDirName);

    /// <summary>获取目录下的文件列表。</summary>
    public static string[] getFiles(string path, string searchPattern = "*", bool recursive = false)
    {
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(path, searchPattern, option);
    }

    /// <summary>获取子目录列表。</summary>
    public static string[] getDirectories(string path, string searchPattern = "*", bool recursive = false)
    {
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetDirectories(path, searchPattern, option);
    }

    /// <summary>获取目录下所有条目（文件+目录）。</summary>
    public static string[] getEntries(string path, string searchPattern = "*", bool recursive = false)
    {
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFileSystemEntries(path, searchPattern, option);
    }

    /// <summary>获取当前工作目录。</summary>
    public static string getCurrent()
        => Directory.GetCurrentDirectory();

    /// <summary>设置当前工作目录。</summary>
    public static void setCurrent(string path)
        => Directory.SetCurrentDirectory(path);

    /// <summary>获取临时目录路径。</summary>
    public static string getTempPath()
        => Path.GetTempPath();

    /// <summary>在临时目录下创建唯一子目录。</summary>
    public static string createTemp()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    /// <summary>复制整个目录（递归）。</summary>
    public static void copy(string sourceDir, string destDir, bool overwrite = false)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"源目录不存在：{sourceDir}");

        Directory.CreateDirectory(destDir);

        foreach (var file in dir.GetFiles())
        {
            file.CopyTo(Path.Combine(destDir, file.Name), overwrite);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            copy(subDir.FullName, Path.Combine(destDir, subDir.Name), overwrite);
        }
    }

    /// <summary>获取目录的总大小（字节）。</summary>
    public static long getSize(string path)
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists) return 0;
        return dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
    }
}
