using System;
using System.Runtime.InteropServices;
using Drx.Sdk.Script;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Interfaces;
using Drx.Sdk.Win32;

namespace Drx.Sdk.Handle;

[ScriptClass("process")]
public class Process : IScript
{
    /// <summary>
    /// 获取进程句柄
    /// </summary>
    /// <param name="processId">进程ID</param>
    /// <param name="access">访问权限</param>
    /// <returns>进程句柄，失败返回IntPtr.Zero</returns>
    public static IntPtr GetProcessHandle(int processId, Kernel32.ProcessAccess access = Kernel32.ProcessAccess.PROCESS_ALL_ACCESS)
    {
        try
        {
            IntPtr handle = Kernel32.OpenProcess(access, false, processId);
            if (handle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                System.Console.WriteLine($"获取进程句柄失败，错误代码：{error}");
            }
            return handle;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"获取进程句柄时发生异常：{ex.Message}");
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 关闭进程句柄
    /// </summary>
    /// <param name="handle">进程句柄</param>
    /// <returns>是否成功关闭</returns>
    public static bool CloseProcessHandle(IntPtr handle)
    {
        try
        {
            if (handle != IntPtr.Zero)
            {
                return Kernel32.CloseHandle(handle);
            }
            return false;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"关闭进程句柄时发生异常：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 通过进程名获取进程ID
    /// </summary>
    /// <param name="processName">进程名称（不包含.exe后缀）</param>
    /// <returns>进程ID，如果未找到返回-1</returns>
    public static int GetProcessIdByName(string processName)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessesByName(processName).FirstOrDefault();
            if (process != null)
            {
                var processId = process.Id;
                return processId;
            }
            else
            {
                System.Console.WriteLine($"未找到进程：{processName}");
                return -1;
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"获取进程ID时发生异常：{ex.Message}");
            return -1;
        }
    }

    public static System.Diagnostics.Process? GetProcessById(int processId)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(processId);
            return process;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"获取进程时发生异常：{ex.Message}");
            return null;
        }
    }

    public static System.Diagnostics.Process? GetProcessByName(string processName)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessesByName(processName).FirstOrDefault();
            return process;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"获取进程时发生异常：{ex.Message}");
            return null;
        }
    }

    public static IntPtr GetProcessHandlerByName(string processName)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessesByName(processName).FirstOrDefault();
            if (process != null)
            {
                var processId = process.Id;
                var handle = Kernel32.OpenProcess(Kernel32.ProcessAccess.PROCESS_ALL_ACCESS, false, processId);
                if (handle == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    System.Console.WriteLine($"获取进程句柄失败，错误代码：{error}");
                }
                return handle;
            }
            else
            {
                System.Console.WriteLine($"未找到进程：{processName}");
                return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"获取进程句柄时发生异常：{ex.Message}");
            return IntPtr.Zero;
        }
    }
}