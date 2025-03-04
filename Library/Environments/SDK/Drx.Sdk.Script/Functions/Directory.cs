using System;
using System.IO;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Interfaces;
using Task = System.Threading.Tasks.Task;

namespace Drx.Sdk.Script.Functions;

[ScriptClass("directory")]
public class Directorys : IScript
{
    public static DirectoryInfo createdirectory(string path)
    {
        return System.IO.Directory.CreateDirectory(path);
    }

    public static void delete(string path, bool recursive = false)
    {
        System.IO.Directory.Delete(path, recursive);
    }

    public static bool exists(string path)
    {
        return System.IO.Directory.Exists(path);
    }

    public static string getcurrentdirectory()
    {
        return System.IO.Directory.GetCurrentDirectory();
    }

    public static void setcurrentdirectory(string path)
    {
        System.IO.Directory.SetCurrentDirectory(path);
    }

    public static string[] getdirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        return System.IO.Directory.GetDirectories(path, searchPattern, searchOption);
    }

    public static string[] getfiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        return System.IO.Directory.GetFiles(path, searchPattern, searchOption);
    }

    public static string[] getfilesystementries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        return System.IO.Directory.GetFileSystemEntries(path, searchPattern, searchOption);
    }

    public static string? getparent(string path)
    {
        return System.IO.Directory.GetParent(path)?.FullName;
    }

    public static void move(string sourceDirName, string destDirName)
    {
        System.IO.Directory.Move(sourceDirName, destDirName);
    }

    public static DateTime getcreationtime(string path)
    {
        return System.IO.Directory.GetCreationTime(path);
    }

    public static DateTime getlastaccesstime(string path)
    {
        return System.IO.Directory.GetLastAccessTime(path);
    }

    public static DateTime getlastwritetime(string path)
    {
        return System.IO.Directory.GetLastWriteTime(path);
    }

    public static string[] getlogicaldrives()
    {
        return System.IO.Directory.GetLogicalDrives();
    }

    public static object getfilesasync(string path, string searchPattern = "*", 
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var task = Task.Run(() => getfiles(path, searchPattern, searchOption));
        
        return task;
    }

    public static object getdirectoriesasync(string path, string searchPattern = "*", 
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var task = Task.Run(() => getdirectories(path, searchPattern, searchOption));
        
        return task;
    }

    public static IEnumerable<string> enumeratedirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        return System.IO.Directory.EnumerateDirectories(path, searchPattern, searchOption);
    }

    public static IEnumerable<string> enumeratefiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        return System.IO.Directory.EnumerateFiles(path, searchPattern, searchOption);
    }
}