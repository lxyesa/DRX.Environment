// Copyright (c) DRX SDK — Paperclip 进程操作脚本桥接层
// 职责：将 System.Diagnostics.Process 常用能力导出到 JS/TS 脚本
// 关键依赖：System.Diagnostics

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 进程操作脚本桥接层。提供启动外部进程、执行命令并获取输出等静态 API。
/// </summary>
public static class ProcessBridge
{
    /// <summary>
    /// 启动外部进程，等待完成并返回 { exitCode, stdout, stderr }。
    /// </summary>
    public static object run(string fileName, string arguments = "")
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new { exitCode = process.ExitCode, stdout, stderr };
    }

    /// <summary>
    /// 异步启动外部进程，等待完成并返回 { exitCode, stdout, stderr }。
    /// </summary>
    public static async Task<object> runAsync(string fileName, string arguments = "")
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new { exitCode = process.ExitCode, stdout, stderr };
    }

    /// <summary>
    /// 执行 shell 命令（Windows: cmd /c, Linux/Mac: /bin/sh -c）。
    /// </summary>
    public static object exec(string command)
    {
        string shell, args;
        if (OperatingSystem.IsWindows())
        {
            shell = "cmd.exe";
            args = $"/c {command}";
        }
        else
        {
            shell = "/bin/sh";
            args = $"-c \"{command.Replace("\"", "\\\"")}\"";
        }
        return run(shell, args);
    }

    /// <summary>
    /// 异步执行 shell 命令。
    /// </summary>
    public static Task<object> execAsync(string command)
    {
        string shell, args;
        if (OperatingSystem.IsWindows())
        {
            shell = "cmd.exe";
            args = $"/c {command}";
        }
        else
        {
            shell = "/bin/sh";
            args = $"-c \"{command.Replace("\"", "\\\"")}\"";
        }
        return runAsync(shell, args);
    }

    /// <summary>
    /// 启动进程但不等待完成（后台执行），返回进程 ID。
    /// </summary>
    public static int start(string fileName, string arguments = "")
    {
        var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.Start();
        return process.Id;
    }

    /// <summary>
    /// 按进程 ID 终止进程。
    /// </summary>
    public static void kill(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
            // 进程已结束，忽略
        }
    }

    /// <summary>
    /// 判断指定 ID 的进程是否正在运行。
    /// </summary>
    public static bool isRunning(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
