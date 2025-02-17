using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Drx.Framework.Memory
{
    internal class HookOriginalCode
    {
        public IntPtr Address { get; }
        private readonly byte[] _originalBytes;
        private readonly int _size;

        public HookOriginalCode(IntPtr address, int size)
        {
            Address = address;
            _size = size;
            _originalBytes = new byte[size];
        }

        public void SaveOriginalBytes()
        {
            if (!ReadProcessMemory(
                Process.GetCurrentProcess().Handle,
                Address,
                _originalBytes,
                _size,
                out _))
            {
                throw new InvalidOperationException("Failed to save original code");
            }
        }

        public void RestoreOriginalBytes()
        {
            VirtualProtect(Address, (UIntPtr)_size, 0x40, out uint oldProtect);
            
            if (!WriteProcessMemory(
                Process.GetCurrentProcess().Handle,
                Address,
                _originalBytes,
                _size,
                out _))
            {
                throw new InvalidOperationException("Failed to restore original code");
            }

            VirtualProtect(Address, (UIntPtr)_size, oldProtect, out _);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
            byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
            byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize,
            uint flNewProtect, out uint lpflOldProtect);
    }
}
