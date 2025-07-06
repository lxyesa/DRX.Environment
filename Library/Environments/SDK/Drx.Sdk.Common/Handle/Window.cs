using Drx.Sdk.Native;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Interfaces;

namespace Drx.Sdk.Common.Handle;

[ScriptClass("window")]
public class Window : IScript
{
    public static IntPtr GetWindowHandle(string windowName)
    {
        IntPtr foundHandle = IntPtr.Zero;
        
        User32.EnumWindows((hWnd, lParam) =>
        {
            if (!User32.IsWindowVisible(hWnd))
                return true;

            var sb = new System.Text.StringBuilder(256);
            if (User32.GetWindowText(hWnd, sb, 256) > 0)
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
        
        User32.EnumWindows((hWnd, lParam) =>
        {
            if (!User32.IsWindowVisible(hWnd))
                return true;

            User32.GetWindowThreadProcessId(hWnd, out int windowProcessId);
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
