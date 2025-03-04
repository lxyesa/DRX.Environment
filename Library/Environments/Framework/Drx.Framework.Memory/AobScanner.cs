using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Framework.Memory
{
    public class AobScanner : IDisposable
    {
        private readonly Process _process;
        private readonly ProcessModule _module;
        private readonly IntPtr _moduleBase;
        private readonly int _moduleSize;

        public AobScanner(string processName, string moduleName)
        {
            _process = Process.GetProcessesByName(processName).FirstOrDefault()
                ?? throw new ArgumentException($"Process {processName} not found");

            _module = _process.Modules.Cast<ProcessModule>()
                .FirstOrDefault(m => string.Equals(m.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Module {moduleName} not found in process {processName}");

            _moduleBase = _module.BaseAddress;
            _moduleSize = _module.ModuleMemorySize;
        }

        public unsafe List<IntPtr> ScanModule(string pattern)
        {
            // Read module memory
            byte[] moduleData = new byte[_moduleSize];
            if (!ReadProcessMemory(_process.Handle, _moduleBase, moduleData, _moduleSize, out _))
            {
                throw new InvalidOperationException($"Failed to read process memory. Error: {Marshal.GetLastWin32Error()}");
            }

            // Scan for pattern
            List<int> offsets = ScanPattern(moduleData, pattern);

            // Convert offsets to actual addresses
            var results = new List<IntPtr>();
            foreach (int offset in offsets)
            {
                results.Add(_moduleBase + offset);
            }

            return results;
        }

        private List<int> ScanPattern(byte[] data, string pattern)
        {
            var tokens = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var parsedPattern = new byte?[tokens.Length];

            for (int i = 0; i < tokens.Length; i++)
            {
                parsedPattern[i] = tokens[i] == "??" ? null : Convert.ToByte(tokens[i], 16);
            }

            var results = new List<int>();
            for (int i = 0; i <= data.Length - parsedPattern.Length; i++)
            {
                bool matched = true;
                for (int j = 0; j < parsedPattern.Length; j++)
                {
                    if (parsedPattern[j].HasValue && data[i + j] != parsedPattern[j].Value)
                    {
                        matched = false;
                        break;
                    }
                }
                if (matched)
                {
                    results.Add(i);
                }
            }
            return results;
        }

        public void Dispose()
        {
            _module?.Dispose();
            GC.SuppressFinalize(this);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer,
            int dwSize,
            out IntPtr lpNumberOfBytesRead);
    }
}
