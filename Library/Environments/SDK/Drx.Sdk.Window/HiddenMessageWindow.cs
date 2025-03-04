using System.Runtime.InteropServices;

namespace Drx.Sdk.Window
{
    public class HiddenMessageWindow
    {
        // Win32 API
        [DllImport("user32.dll")]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        public IntPtr Hwnd { get; private set; }

        public HiddenMessageWindow()
        {
            Hwnd = CreateWindowEx(
                0, "STATIC", "KeyboardListenerWindow",
                0, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            if (Hwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("创建消息窗口失败");
            }
        }

        public void Destroy()
        {
            if (Hwnd != IntPtr.Zero)
            {
                DestroyWindow(Hwnd);
                Hwnd = IntPtr.Zero;
            }
        }
    }
}
