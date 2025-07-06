using System;
using System.Runtime.InteropServices;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Interfaces;

namespace Drx.Sdk.Native;

[ScriptClass("user32")]
public class User32 : IScript
{
    
    // =====================================================
    // Delegates
    // =====================================================
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // =====================================================
    // Functions (EnumWindows)
    // =====================================================
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    // =====================================================
    // Functions (GetWindowText)
    // =====================================================
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    // =====================================================
    // Functions (GetWindowThreadProcessId)
    // =====================================================
    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    // =====================================================
    // Functions (IsWindowVisible)
    // =====================================================
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    // =====================================================
    // Functions (ShowWindow)
    // =====================================================
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // =====================================================
    // Functions (GetForegroundWindow)
    // =====================================================
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    // =====================================================
    // Functions (SetForegroundWindow)
    // =====================================================
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    // =====================================================
    // Functions (SendMessage)
    // =====================================================
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    // =====================================================
    // Functions (PostMessage)
    // =====================================================
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    // =====================================================
    // Functions (GetDesktopWindow)
    // =====================================================
    [DllImport("user32.dll")]
    public static extern IntPtr GetDesktopWindow();

    // =====================================================
    // Functions (GetWindowRect)
    // =====================================================
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    // =====================================================
    // Functions (MoveWindow)
    // =====================================================
    [DllImport("user32.dll")]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    // =====================================================
    // Functions (FindWindow)
    // =====================================================
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    // =====================================================
    // Functions (CreateWindowEx)
    // =====================================================
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr CreateWindowEx(
       int dwExStyle,
       string lpClassName,
       string lpWindowName,
       int dwStyle,
       int x,
       int y,
       int nWidth,
       int nHeight,
       IntPtr hWndParent,
       IntPtr hMenu,
       IntPtr hInstance,
       IntPtr lpParam);

    // =====================================================
    // Functions (DefWindowProc)
    // =====================================================
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // =====================================================
    // Functions (RegisterClassEx)
    // =====================================================
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern short RegisterClassEx(ref WNDCLASSEX lpwcx);

    // =====================================================
    // Functions (GetDlgItem)
    // =====================================================
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr GetDlgItem(IntPtr hDlg, int nIDDlgItem);

    // =====================================================
    // Functions (SetDlgItemText)
    // =====================================================
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool SetDlgItemText(IntPtr hDlg, int nIDDlgItem, string lpString);

    // =====================================================
    // Functions (GetDlgItemText)
    // =====================================================
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetDlgItemText(IntPtr hDlg, int nIDDlgItem, System.Text.StringBuilder lpString, int nMaxCount);

    // =====================================================
    // Structs
    // =====================================================
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
