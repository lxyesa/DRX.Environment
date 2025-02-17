using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Memory
{
    public class ProcessUtility
    {
        // 内存分配和进程访问的常量
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint MEM_RELEASE = 0x8000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        /// <summary>
        /// 通过进程名获取进程句柄，包含所需的访问权限
        /// </summary>
        public static IntPtr GetProcessHandleWithAccess(string processName)
        {
            Process process = Process.GetProcessesByName(processName).FirstOrDefault();
            if (process == null)
            {
                throw new ArgumentException($"未找到进程 {processName}");
            }

            const uint desiredAccess = PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE |
                                     PROCESS_QUERY_INFORMATION;

            IntPtr handle = OpenProcess(desiredAccess, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"无法打开进程 {processName}（PID: {process.Id}）, 错误码: {error}");
            }

            return handle;
        }
    }
}
