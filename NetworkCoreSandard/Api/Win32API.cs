using System.Runtime.InteropServices;
using System.Text;

namespace NetworkCoreStandard.Api
{
    public class Win32API
    {
        [DllImport("kernel32.dll")] public static extern bool AllocConsole();
        [DllImport("kernel32.dll")] public static extern bool FreeConsole();
        [DllImport("kernel32.dll")] public static extern IntPtr LoadLibrary(string dllToLoad);
        [DllImport("kernel32.dll")] public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);
        [DllImport("user32.dll")] public static extern bool MessageBeep(uint uType);
        [DllImport("user32.dll")] public static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] public static extern IntPtr GetModuleHandle(IntPtr moduleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern int GetModuleFileName(
            IntPtr hModule,
            StringBuilder lpFilename,
            int nSize
        );

        // 扩展方法 - 获取当前模块路径
        public static string GetCurrentModulePath()
        {
            var modulePtr = GetModuleHandle(IntPtr.Zero);
            var modulePath = new StringBuilder(260); // MAX_PATH
            GetModuleFileName(modulePtr, modulePath, modulePath.Capacity);
            return modulePath.ToString();
        }

        // 扩展方法 - 获取指定模块路径
        public static string GetModulePath(IntPtr moduleHandle)
        {
            var modulePath = new StringBuilder(260);
            GetModuleFileName(moduleHandle, modulePath, modulePath.Capacity);
            return modulePath.ToString();
        }
    }
}