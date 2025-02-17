using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Memory
{
    public static class AutoAssembler
    {
        private static Assembler _assembler = new Assembler();

        // --------------------------------------------------
        // P/Invoke
        // --------------------------------------------------
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer,
            uint nSize,
            out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            uint nSize,
            out int lpNumberOfBytesWritten);

        // -------------------------------------------------- 
        // DB 
        // --------------------------------------------------

        public static void DB(this IntPtr bAddress, IntPtr hProcess, params byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("字节数组不能为空", nameof(bytes));
            }

            int bytesWritten;
            if (!WriteProcessMemory(hProcess, bAddress, bytes, (uint)bytes.Length, out bytesWritten))
            {
                throw new InvalidOperationException($"写入内存失败: {Marshal.GetLastWin32Error()}");
            }

            if (bytesWritten != bytes.Length)
            {
                throw new InvalidOperationException("未能写入所有字节");
            }
        }
    }
}
