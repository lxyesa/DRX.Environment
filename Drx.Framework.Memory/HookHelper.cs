using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Framework.Memory
{
    public class HookHelper : IDisposable
    {
        private readonly Process _process;
        private readonly ProcessModule _baseModule;
        private readonly List<byte[]> _asmInstructions = new();
        private HookOriginalCode? _originalCode;
        private bool _isHooked;

        /// <summary>
        /// Gets the handle to the target process
        /// </summary>
        public IntPtr ProcessHandle => _process.Handle;

        public HookHelper(string processName, string baseModuleName)
        {
            _process = Process.GetProcessesByName(processName).FirstOrDefault() 
                ?? throw new ArgumentException($"Process {processName} not found");
            
            _baseModule = _process.Modules.Cast<ProcessModule>()
                .FirstOrDefault(m => m.ModuleName.Equals(baseModuleName, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Module {baseModuleName} not found");
        }

        public void AddAsm(byte[] asmCode)
        {
            if (_isHooked)
                throw new InvalidOperationException("Cannot add assembly code after hook is installed");
            
            _asmInstructions.Add(asmCode);
        }

        public void InstallHook(IntPtr targetAddr)
        {
            if (_isHooked)
                throw new InvalidOperationException("Hook is already installed");

            // Calculate total size needed for all instructions plus the final jump
            int totalSize = _asmInstructions.Sum(x => x.Length) + 14; // 14 bytes for final jump
            
            // Ensure we save enough original bytes (with proper alignment)
            int bytesToSave = Math.Max(totalSize, 16); // Minimum 16 bytes for alignment
            _originalCode = new HookOriginalCode(targetAddr, bytesToSave);
            
            // Allocate memory for our code
            IntPtr newCodeAddr = VirtualAllocEx(_process.Handle, IntPtr.Zero, (uint)totalSize,
                AllocationType.Commit | AllocationType.Reserve,
                MemoryProtection.ExecuteReadWrite);
            
            if (newCodeAddr == IntPtr.Zero)
                throw new InvalidOperationException("Failed to allocate memory for hook code");

            // Save original code
            _originalCode.SaveOriginalBytes();

            // Write all assembly instructions
            int currentOffset = 0;
            foreach (byte[] instruction in _asmInstructions)
            {
                WriteProcessMemory(_process.Handle, newCodeAddr + currentOffset, 
                    instruction, instruction.Length, out _);
                currentOffset += instruction.Length;
            }

            // Add final jump back
            byte[] jumpBack = BuildJump64(targetAddr + bytesToSave);
            WriteProcessMemory(_process.Handle, newCodeAddr + currentOffset, 
                jumpBack, jumpBack.Length, out _);

            // Write jump to our code at original location
            byte[] jumpToHook = BuildJump64(newCodeAddr);
            VirtualProtect(_originalCode.Address, (UIntPtr)jumpToHook.Length, 
                PAGE_EXECUTE_READWRITE, out uint oldProtect);
            
            WriteProcessMemory(_process.Handle, _originalCode.Address, 
                jumpToHook, jumpToHook.Length, out _);
            
            VirtualProtect(_originalCode.Address, (UIntPtr)jumpToHook.Length, 
                oldProtect, out _);

            _isHooked = true;
        }

        public void RemoveHook()
        {
            if (!_isHooked || _originalCode == null)
                throw new InvalidOperationException("No hook is installed");

            _originalCode.RestoreOriginalBytes();
            _isHooked = false;
        }

        private static byte[] BuildJump64(IntPtr address)
        {
            var code = new byte[14];
            code[0] = 0x48; // REX.W
            code[1] = 0xB8; // mov rax, imm64
            var addrBytes = BitConverter.GetBytes(address.ToInt64());
            Buffer.BlockCopy(addrBytes, 0, code, 2, 8);
            code[10] = 0xFF; // jmp rax
            code[11] = 0xE0;
            code[12] = 0x90; // NOP (padding)
            code[13] = 0x90; // NOP (padding)
            return code;
        }

        public void Dispose()
        {
            if (_isHooked)
                RemoveHook();
            
            _asmInstructions.Clear();
            GC.SuppressFinalize(this);
        }

        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, 
            uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, 
            uint dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
            byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);
    }
}
