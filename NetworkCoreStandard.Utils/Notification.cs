using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Drawing;

namespace NetworkCoreStandard.Utils;

public class Notification
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class NOTIFYICONDATA
    {
        public int cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA));
        public IntPtr hWnd;
        public int uID;
        public NotifyFlags uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string? szTip;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string? szInfo;
        public int dwInfoFlags;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string? szInfoTitle;
        public int dwTimeout;
    }

    [Flags]
    public enum NotifyFlags
    {
        Message = 0x1,
        Icon = 0x2,
        Tip = 0x4,
        Info = 0x10
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool Shell_NotifyIcon(int dwMessage, NOTIFYICONDATA lpdata);

    private const int NIM_ADD = 0x0;
    private const int NIM_MODIFY = 0x1;
    private const int NIM_DELETE = 0x2;
    private const int NIIF_INFO = 0x1;
    
    private static NOTIFYICONDATA? notifyIcon;

    // 显示通知
    public static void ShowNotification(string title, string message, NotifyType type = NotifyType.Info)
    {
        if(notifyIcon == null)
        {
            notifyIcon = new NOTIFYICONDATA
            {
                hWnd = Process.GetCurrentProcess().MainWindowHandle,
                uID = 1,
                uFlags = NotifyFlags.Info | NotifyFlags.Icon | NotifyFlags.Message,
                dwInfoFlags = NIIF_INFO,
                szInfoTitle = title,
                szInfo = message
            };

            // 加载应用程序图标
            using (Icon appIcon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location))
            {
                if (appIcon != null)
                {
                    notifyIcon.hIcon = appIcon.Handle;
                }
            }

            // 添加通知图标
            Shell_NotifyIcon(NIM_ADD, notifyIcon);
        }
        else
        {
            // 更新现有通知
            notifyIcon.szInfoTitle = title;
            notifyIcon.szInfo = message;
            Shell_NotifyIcon(NIM_MODIFY, notifyIcon);
        }
    }

    // 移除通知图标
    public static void RemoveNotification()
    {
        if(notifyIcon != null)
        {
            Shell_NotifyIcon(NIM_DELETE, notifyIcon);
            notifyIcon = null;
        }
    }

    public enum NotifyType
    {
        Info,
        Warning,
        Error
    }
}
