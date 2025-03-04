using System;
using System.Runtime.InteropServices;
using Drx.Sdk.Script;
using Drx.Sdk.Script.Attributes;

namespace Drx.Sdk.Window;

[JSClass(typeof(WindowHandle), "WindowHandle")]
public class WindowHandle : Iscript
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    public static IntPtr GetWindowHandle(string windowName)
    {
        IntPtr foundHandle = IntPtr.Zero;
        
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            var sb = new System.Text.StringBuilder(256);
            if (GetWindowText(hWnd, sb, 256) > 0)
            {
                if (sb.ToString().Contains(windowName))
                {
                    foundHandle = hWnd;
                    return false; // 停止枚举
                }
            }
            return true; // 继续枚举
        }, IntPtr.Zero);

        return foundHandle;
    }

    public static IntPtr GetWindowHandle(int processId)
    {
        IntPtr foundHandle = IntPtr.Zero;
        
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            GetWindowThreadProcessId(hWnd, out int windowProcessId);
            if (windowProcessId == processId)
            {
                foundHandle = hWnd;
                return false; // 停止枚举
            }
            return true; // 继续枚举
        }, IntPtr.Zero);

        return foundHandle;
    }
}
