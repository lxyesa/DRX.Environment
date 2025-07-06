using System;
using System.Text;
using System.IO;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Interfaces;

namespace Drx.Sdk.Script.Functions;

[ScriptClass("file")]
public class Files : IScript
{
    public static string readalltext(string path)
    {
        return System.IO.File.ReadAllText(path);
    }

    public static string[] readalllines(string path)
    {
        return System.IO.File.ReadAllLines(path);
    }

    public static void writealltext(string path, string contents)
    {
        System.IO.File.WriteAllText(path, contents);
    }

    public static void writealllines(string path, string[] contents)
    {
        System.IO.File.WriteAllLines(path, contents);
    }

    public static void appendalltext(string path, string contents)
    {
        System.IO.File.AppendAllText(path, contents);
    }

    public static void copy(string sourceFileName, string destFileName, bool overwrite = false)
    {
        System.IO.File.Copy(sourceFileName, destFileName, overwrite);
    }

    public static void delete(string path)
    {
        System.IO.File.Delete(path);
    }

    public static bool exists(string path)
    {
        return System.IO.File.Exists(path);
    }

    public static void move(string sourceFileName, string destFileName)
    {
        System.IO.File.Move(sourceFileName, destFileName);
    }

    public static DateTime getcreationtime(string path)
    {
        return System.IO.File.GetCreationTime(path);
    }

    public static DateTime getlastaccesstime(string path)
    {
        return System.IO.File.GetLastAccessTime(path);
    }

    public static DateTime getlastwritetime(string path)
    {
        return System.IO.File.GetLastWriteTime(path);
    }

    public static FileAttributes getattributes(string path)
    {
        return System.IO.File.GetAttributes(path);
    }

    public static void setattributes(string path, FileAttributes fileAttributes)
    {
        System.IO.File.SetAttributes(path, fileAttributes);
    }

    public static object readalltextasync(string path)
    {
        var task = System.IO.File.ReadAllTextAsync(path);
        
        return task;
    }

    public static object readalllinesasync(string path)
    {
        var task = System.IO.File.ReadAllLinesAsync(path);
        
        
        return task;
    }

    public static object? writealltextasync(string path, string contents)
    {
        var task = System.IO.File.WriteAllTextAsync(path, contents);
        
        
        return task;
    }

    public static object? writealllinesasync(string path, string[] contents)
    {
        var task = System.IO.File.WriteAllLinesAsync(path, contents);
        
        return task;
    }

    public static object? appendalltextasync(string path, string contents)
    {
        var task = System.IO.File.AppendAllTextAsync(path, contents);
        
        return task;
    }
}