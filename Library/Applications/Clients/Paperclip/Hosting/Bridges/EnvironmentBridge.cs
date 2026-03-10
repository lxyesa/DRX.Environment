// Copyright (c) DRX SDK — Paperclip 环境信息脚本桥接层
// 职责：将 System.Environment 常用能力导出到 JS/TS 脚本
// 关键依赖：System

using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Runtime.InteropServices;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 环境信息脚本桥接层。提供环境变量读写、操作系统信息、特殊文件夹路径等静态 API。
/// </summary>
public static class EnvironmentBridge
{
    #region 环境变量

    /// <summary>获取环境变量值。</summary>
    public static string? getVariable(string name)
        => Environment.GetEnvironmentVariable(name);

    /// <summary>设置环境变量（仅当前进程）。</summary>
    public static void setVariable(string name, string? value)
        => Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);

    /// <summary>获取当前进程的所有环境变量。</summary>
    public static object getAllVariables()
    {
        var result = new ExpandoObject() as IDictionary<string, object?>;
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = entry.Key?.ToString();
            if (!string.IsNullOrEmpty(key))
                result[key] = entry.Value?.ToString();
        }
        return result;
    }

    #endregion

    #region 系统信息

    /// <summary>获取操作系统名称描述。</summary>
    public static string osDescription()
        => RuntimeInformation.OSDescription;

    /// <summary>获取运行时架构（x64/Arm64 等）。</summary>
    public static string osArchitecture()
        => RuntimeInformation.OSArchitecture.ToString();

    /// <summary>获取处理器数量。</summary>
    public static int processorCount()
        => Environment.ProcessorCount;

    /// <summary>获取机器名。</summary>
    public static string machineName()
        => Environment.MachineName;

    /// <summary>获取当前用户名。</summary>
    public static string userName()
        => Environment.UserName;

    /// <summary>获取 .NET 运行时版本。</summary>
    public static string runtimeVersion()
        => RuntimeInformation.FrameworkDescription;

    /// <summary>判断是否为 64 位操作系统。</summary>
    public static bool is64BitOS()
        => Environment.Is64BitOperatingSystem;

    /// <summary>判断是否为 64 位进程。</summary>
    public static bool is64BitProcess()
        => Environment.Is64BitProcess;

    /// <summary>获取系统运行时间（毫秒）。</summary>
    public static long tickCount()
        => Environment.TickCount64;

    /// <summary>获取换行符。</summary>
    public static string newLine()
        => Environment.NewLine;

    #endregion

    #region 特殊目录

    /// <summary>获取用户主目录。</summary>
    public static string userHome()
        => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>获取桌面路径。</summary>
    public static string desktop()
        => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    /// <summary>获取我的文档路径。</summary>
    public static string documents()
        => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    /// <summary>获取 AppData 路径。</summary>
    public static string appData()
        => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>获取 LocalAppData 路径。</summary>
    public static string localAppData()
        => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>获取临时目录路径。</summary>
    public static string tempPath()
        => System.IO.Path.GetTempPath();

    #endregion

    #region 命令行

    /// <summary>获取命令行参数数组。</summary>
    public static string[] commandLineArgs()
        => Environment.GetCommandLineArgs();

    /// <summary>获取当前进程 ID。</summary>
    public static int processId()
        => Environment.ProcessId;

    #endregion
}
