using System;
using System.Runtime.InteropServices;
using Drx.Sdk.Script;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Interfaces;
using Drx.Sdk.Win32;

namespace Drx.Sdk.Handle;

[ScriptClass("thread")]
public class Thread : IScript
{
    /// <summary>
    /// 获取线程句柄
    /// </summary>
    /// <param name="threadId">线程ID</param>
    /// <param name="access">访问权限</param>
    /// <returns>线程句柄</returns>
    public static IntPtr GetThreadHandle(uint threadId, Kernel32.ThreadAccess access = Kernel32.ThreadAccess.THREAD_ALL_ACCESS)
    {
        return Kernel32.OpenThread(access, false, threadId);
    }

    /// <summary>
    /// 关闭线程句柄
    /// </summary>
    /// <param name="handle">线程句柄</param>
    /// <returns>是否成功关闭</returns>
    public static bool CloseThreadHandle(IntPtr handle)
    {
        return Kernel32.CloseHandle(handle);
    }
}
