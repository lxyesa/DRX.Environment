using System.Runtime.InteropServices;
using System.Text;

namespace NDVServerLib.Api
{
    public class Win32API
    {
        [DllImport("kernel32.dll")] public static extern bool AllocConsole();
        [DllImport("kernel32.dll")] public static extern bool FreeConsole();
        [DllImport("kernel32.dll")] public static extern nint LoadLibrary(string dllToLoad);
        [DllImport("kernel32.dll")] public static extern nint GetProcAddress(nint hModule, string procedureName);
        [DllImport("user32.dll")] public static extern bool MessageBeep(uint uType);
        [DllImport("user32.dll")] public static extern int MessageBox(nint hWnd, string lpText, string lpCaption, uint uType);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] public static extern nint GetModuleHandle(nint moduleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern int GetModuleFileName(
            nint hModule,
            StringBuilder lpFilename,
            int nSize
        );

        // 扩展方法 - 获取当前模块路径
        public static string GetCurrentModulePath()
        {
            var modulePtr = GetModuleHandle(nint.Zero);
            var modulePath = new StringBuilder(260); // MAX_PATH
            GetModuleFileName(modulePtr, modulePath, modulePath.Capacity);
            return modulePath.ToString();
        }

        // 扩展方法 - 获取指定模块路径
        public static string GetModulePath(nint moduleHandle)
        {
            var modulePath = new StringBuilder(260);
            GetModuleFileName(moduleHandle, modulePath, modulePath.Capacity);
            return modulePath.ToString();
        }
    }
}