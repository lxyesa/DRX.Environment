using System;
using System.Runtime.InteropServices;
using System.Text;
namespace Drx.Sdk.Native;
public class Kernel32
{
    // =====================================================
    // Structs
    // =====================================================
    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_INFO
    {
        public ushort processorArchitecture;
        private ushort reserved;
        public uint pageSize;
        public IntPtr minimumApplicationAddress;
        public IntPtr maximumApplicationAddress;
        public IntPtr activeProcessorMask;
        public uint numberOfProcessors;
        public uint processorType;
        public uint allocationGranularity;
        public ushort processorLevel;
        public ushort processorRevision;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [Flags]
    public enum ProcessAccess : uint
    {
        PROCESS_TERMINATE = 0x0001,
        PROCESS_CREATE_THREAD = 0x0002,
        PROCESS_SET_SESSIONID = 0x0004,
        PROCESS_VM_OPERATION = 0x0008,
        PROCESS_VM_READ = 0x0010,
        PROCESS_VM_WRITE = 0x0020,
        PROCESS_DUP_HANDLE = 0x0040,
        PROCESS_CREATE_PROCESS = 0x0080,
        PROCESS_SET_QUOTA = 0x0100,
        PROCESS_SET_INFORMATION = 0x0200,
        PROCESS_QUERY_INFORMATION = 0x0400,
        PROCESS_SUSPEND_RESUME = 0x0800,
        PROCESS_QUERY_LIMITED_INFORMATION = 0x1000,
        PROCESS_ALL_ACCESS = 0x000F0000 | 0x00100000 | 0xFFF
    }

    [Flags]
    public enum ThreadAccess : uint
    {
        TERMINATE = 0x0001,
        SUSPEND_RESUME = 0x0002,
        GET_CONTEXT = 0x0008,
        SET_CONTEXT = 0x0010,
        SET_INFORMATION = 0x0020,
        QUERY_INFORMATION = 0x0040,
        SET_THREAD_TOKEN = 0x0080,
        IMPERSONATE = 0x0100,
        DIRECT_IMPERSONATION = 0x0200,
        THREAD_ALL_ACCESS = 0xF0000 | 0x100000 | 0xFFFF
    }

    // =====================================================
    // Constants for Memory Operations
    // =====================================================
    public const uint MEM_COMMIT = 0x1000;
    public const uint MEM_RESERVE = 0x2000;
    public const uint MEM_RELEASE = 0x8000;
    public const uint PAGE_READWRITE = 0x04;
    public const uint PAGE_GUARD = 0x100;
    public const uint PAGE_NOACCESS = 0x01;
    public const uint PAGE_READONLY = 0x02;
    public const uint PAGE_WRITECOPY = 0x08;
    public const uint PAGE_EXECUTE = 0x10;
    public const uint PAGE_EXECUTE_READ = 0x20;
    public const uint PAGE_EXECUTE_READWRITE = 0x40;
    public const uint PAGE_EXECUTE_WRITECOPY = 0x80;
    public const uint PAGE_NOCACHE = 0x200;

    // =====================================================
    // Functions (ReadProcessMemory)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        [Out] byte[] lpBuffer,
        int dwSize,
        out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        [Out] byte[] lpBuffer,
        uint nSize,
        out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        nint lpBuffer,
        int dwSize,
        out IntPtr lpNumberOfBytesRead);

    // =====================================================
    // Functions (VirtualMemory)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern int VirtualQueryEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        ref MEMORY_BASIC_INFORMATION lpBuffer,
        uint dwLength);

    // =====================================================
    // Functions (SystemInfo)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);

    // =====================================================
    // Functions (VirtualFreeEx)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualFreeEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        uint dwSize,
        uint dwFreeType);

    // =====================================================
    // Functions (VirtualAllocEx)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    public static extern IntPtr VirtualAllocEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        uint dwSize,
        uint flAllocationType,
        uint flProtect);

    // =====================================================
    // Functions (WriteProcessMemory)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        uint nSize,
        out int lpNumberOfBytesWritten);

    // =====================================================
    // Functions (OpenProcess)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(ProcessAccess dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    // =====================================================
    // Functions (CloseHandle)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    // =====================================================
    // Functions (OpenThread)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

    // =====================================================
    // Functions (CreateRemoteThread)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateRemoteThread(
        IntPtr hProcess,
        IntPtr lpThreadAttributes, 
        uint dwStackSize,
        IntPtr lpStartAddress,
        IntPtr lpParameter,
        uint dwCreationFlags,
        out IntPtr lpThreadId);

    // =====================================================
    // Functions (GetExitCodeThread)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetExitCodeThread(
        IntPtr hThread,
        out uint lpExitCode);

    // =====================================================
    // Functions (SuspendThread)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SuspendThread(IntPtr hThread);

    // =====================================================
    // Functions (ResumeThread)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint ResumeThread(IntPtr hThread);

    // =====================================================
    // Functions (TerminateThread)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TerminateThread(
        IntPtr hThread,
        uint dwExitCode);

    // =====================================================
    // Functions (GetLastError)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern uint GetLastError();

    // =====================================================
    // Functions (GetCurrentProcessId)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentProcessId();

    // =====================================================
    // Functions (IsWow64Process)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWow64Process(
        IntPtr process,
        [MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

    // =====================================================
    // Functions (GetModuleHandle)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string moduleName);

    // =====================================================
    // Functions (GetProcAddress)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern IntPtr GetProcAddress(
        IntPtr hModule,
        string procName);

    // =====================================================
    // Functions (VirtualProtectEx)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualProtectEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        uint dwSize,
        uint flNewProtect,
        out uint lpflOldProtect);

    // =====================================================
    // Functions (FlushInstructionCache)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FlushInstructionCache(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        UIntPtr dwSize);

    // =====================================================
    // Functions (GetProcessHeap)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetProcessHeap();

    // =====================================================
    // Functions (HeapAlloc)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr HeapAlloc(
        IntPtr hHeap,
        uint dwFlags,
        UIntPtr dwBytes);

    // =====================================================
    // Functions (HeapFree)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool HeapFree(
        IntPtr hHeap,
        uint dwFlags,
        IntPtr lpMem);

    // =====================================================
    // Functions (VirtualLock)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualLock(
        IntPtr lpAddress,
        UIntPtr dwSize);

    // =====================================================
    // Functions (VirtualUnlock)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualUnlock(
        IntPtr lpAddress,
        UIntPtr dwSize);

    // =====================================================
    // Functions (CreateFileMapping)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateFileMapping(
        IntPtr hFile,
        IntPtr lpFileMappingAttributes,
        uint flProtect,
        uint dwMaximumSizeHigh,
        uint dwMaximumSizeLow,
        string lpName);

    // =====================================================
    // Functions (MapViewOfFile)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr MapViewOfFile(
        IntPtr hFileMappingObject,
        uint dwDesiredAccess,
        uint dwFileOffsetHigh,
        uint dwFileOffsetLow,
        UIntPtr dwNumberOfBytesToMap);

    // =====================================================
    // Functions (UnmapViewOfFile)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

    // =====================================================
    // Functions (VirtualAlloc)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr VirtualAlloc(
        IntPtr lpAddress,
        UIntPtr dwSize,
        uint flAllocationType,
        uint flProtect);

    // =====================================================
    // Functions (VirtualFree)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualFree(
        IntPtr lpAddress,
        UIntPtr dwSize,
        uint dwFreeType);

    // =====================================================
    // Functions (VirtualQuery)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern UIntPtr VirtualQuery(
        IntPtr lpAddress,
        ref MEMORY_BASIC_INFORMATION lpBuffer,
        UIntPtr dwLength);

    // =====================================================
    // Functions (GlobalAlloc)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    // =====================================================
    // Functions (GlobalFree)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GlobalFree(IntPtr hMem);

    // =====================================================
    // Functions (GlobalLock)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GlobalLock(IntPtr hMem);

    // =====================================================
    // Functions (GlobalUnlock)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GlobalUnlock(IntPtr hMem);

    // =====================================================
    // Functions (GetCurrentProcess)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentProcess();

    // =====================================================
    // Functions (GetCurrentThread)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentThread();

    // =====================================================
    // Functions (GetThreadId)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern uint GetThreadId(IntPtr thread);

    // =====================================================
    // Functions (GetProcessId)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern uint GetProcessId(IntPtr process);

    // =====================================================
    // Functions (TerminateProcess)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    // =====================================================
    // Functions (GetExitCodeProcess)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    // =====================================================
    // Functions (CreateProcess)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool CreateProcess(
        string lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        [In] ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    // =====================================================
    // Functions (GetThreadContext)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT lpContext);

    // =====================================================
    // Functions (SetThreadContext)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern bool SetThreadContext(IntPtr hThread, ref CONTEXT lpContext);

    // =====================================================
    // Functions (WaitForSingleObject)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    // =====================================================
    // Structs for Process/Thread Operations
    // =====================================================
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO
    {
        public Int32 cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public Int32 dwX;
        public Int32 dwY;
        public Int32 dwXSize;
        public Int32 dwYSize;
        public Int32 dwXCountChars;
        public Int32 dwYCountChars;
        public Int32 dwFillAttribute;
        public Int32 dwFlags;
        public Int16 wShowWindow;
        public Int16 cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CONTEXT
    {
        public uint ContextFlags;
        // 这里添加更多上下文字段，根据具体架构(x86/x64)有所不同
    }

    // =====================================================
    // Constants for Process/Thread Operations
    // =====================================================
    public const uint INFINITE = 0xFFFFFFFF;
    public const uint WAIT_ABANDONED = 0x00000080;
    public const uint WAIT_OBJECT_0 = 0x00000000;
    public const uint WAIT_TIMEOUT = 0x00000102;
    public const uint WAIT_FAILED = 0xFFFFFFFF;
    public const uint CREATE_SUSPENDED = 0x00000004;
    public const uint STILL_ACTIVE = 259;
    public const uint PROCESS_DEP_ENABLE = 0x00000001;
    public const uint PROCESS_DEP_DISABLE = 0x00000000;

    // =====================================================
    // Functions (GetCurrentDirectoryW)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern uint GetCurrentDirectory(uint nBufferLength, [Out] StringBuilder lpBuffer);

    // =====================================================
    // Functions (SetCurrentDirectoryW)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool SetCurrentDirectory(string lpPathName);

    // =====================================================
    // Functions (GetEnvironmentVariable)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern uint GetEnvironmentVariable(string lpName, StringBuilder lpBuffer, uint nSize);

    // =====================================================
    // Functions (SetEnvironmentVariable)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool SetEnvironmentVariable(string lpName, string lpValue);

    // =====================================================
    // Functions (QueryFullProcessImageName)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool QueryFullProcessImageName(
        IntPtr hProcess, 
        uint dwFlags, 
        StringBuilder lpExeName, 
        ref uint lpdwSize);

    // =====================================================
    // Functions (GetProcessTimes)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern bool GetProcessTimes(
        IntPtr hProcess,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpCreationTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpExitTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

    // =====================================================
    // Functions (DebugActiveProcess)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern bool DebugActiveProcess(uint dwProcessId);

    // =====================================================
    // Functions (DebugActiveProcessStop)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern bool DebugActiveProcessStop(uint dwProcessId);

    // =====================================================
    // Functions (WaitForDebugEvent)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern bool WaitForDebugEvent(out DEBUG_EVENT lpDebugEvent, uint dwMilliseconds);

    // =====================================================
    // Functions (ContinueDebugEvent)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern bool ContinueDebugEvent(
        uint dwProcessId,
        uint dwThreadId,
        uint dwContinueStatus);

    // =====================================================
    // Functions (SetProcessWorkingSetSize)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern bool SetProcessWorkingSetSize(
        IntPtr hProcess,
        UIntPtr dwMinimumWorkingSetSize,
        UIntPtr dwMaximumWorkingSetSize);

    // =====================================================
    // Additional Structs
    // =====================================================
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_EVENT
    {
        public uint dwDebugEventCode;
        public uint dwProcessId;
        public uint dwThreadId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 86)]
        public byte[] debugInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SECURITY_ATTRIBUTES
    {
        public uint nLength;
        public IntPtr lpSecurityDescriptor;
        public bool bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MODULEINFO
    {
        public IntPtr lpBaseOfDll;
        public uint SizeOfImage;
        public IntPtr EntryPoint;
    }

    // =====================================================
    // Functions (GetModuleInformation)
    // =====================================================
    [DllImport("psapi.dll", SetLastError = true)]
    public static extern bool GetModuleInformation(
        IntPtr hProcess,
        IntPtr hModule,
        out MODULEINFO lpmodinfo,
        uint cb);

    // =====================================================
    // Functions (SetThreadPriority)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern bool SetThreadPriority(IntPtr hThread, int nPriority);

    // =====================================================
    // Functions (GetThreadPriority)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern int GetThreadPriority(IntPtr hThread);

    // =====================================================
    // Functions (SetProcessPriorityBoost)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern bool SetProcessPriorityBoost(IntPtr hProcess, bool bDisablePriorityBoost);
    // =====================================================
    // Functions (CreateFile)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    // =====================================================
    // Functions (GetSystemTime)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern void GetSystemTime(out SYSTEMTIME lpSystemTime);

    // =====================================================
    // Functions (GetVersion)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern uint GetVersion();

    // =====================================================
    // Functions (GetComputerName)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetComputerName(StringBuilder lpBuffer, ref uint lpnSize);

    // =====================================================
    // Functions (GetSystemDirectory)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern uint GetSystemDirectory(StringBuilder lpBuffer, uint uSize);

    // =====================================================
    // Functions (LoadLibrary)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadLibrary(string lpFileName);

    // =====================================================
    // Functions (FreeLibrary)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeLibrary(IntPtr hModule);

    // =====================================================
    // Functions (GetModuleFileName)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern uint GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, uint nSize);

    // =====================================================
    // Functions (SetErrorMode)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern uint SetErrorMode(uint uMode);

    // =====================================================
    // Functions (GetProcessAffinityMask)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern bool GetProcessAffinityMask(
        IntPtr hProcess,
        out UIntPtr lpProcessAffinityMask,
        out UIntPtr lpSystemAffinityMask);

    // =====================================================
    // Functions (SetProcessAffinityMask)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern bool SetProcessAffinityMask(IntPtr hProcess, UIntPtr dwProcessAffinityMask);

    // =====================================================
    // Functions (CopyFile)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CopyFile(string lpExistingFileName, string lpNewFileName, bool bFailIfExists);

    // =====================================================
    // Functions (DeleteFile)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool DeleteFile(string lpFileName);

    // =====================================================
    // Functions (MoveFile)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool MoveFile(string lpExistingFileName, string lpNewFileName);

    // =====================================================
    // Functions (GetFileSize)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint GetFileSize(IntPtr hFile, IntPtr lpFileSizeHigh);

    // =====================================================
    // Functions (GetFileAttributes)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint GetFileAttributes(string lpFileName);

    // =====================================================
    // Functions (SetFileAttributes)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool SetFileAttributes(string lpFileName, uint dwFileAttributes);

    // =====================================================
    // Functions (GetLogicalDrives)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint GetLogicalDrives();

    // =====================================================
    // Functions (GetDriveType)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint GetDriveType(string lpRootPathName);

    // =====================================================
    // Functions (SetEndOfFile)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetEndOfFile(IntPtr hFile);

    // =====================================================
    // Functions (LockFileEx)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool LockFileEx(
        IntPtr hFile,
        uint dwFlags,
        uint dwReserved,
        uint nNumberOfBytesToLockLow,
        uint nNumberOfBytesToLockHigh,
        ref OVERLAPPED lpOverlapped);

    // =====================================================
    // Functions (UnlockFileEx)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool UnlockFileEx(
        IntPtr hFile,
        uint dwReserved,
        uint nNumberOfBytesToUnlockLow,
        uint nNumberOfBytesToUnlockHigh,
        ref OVERLAPPED lpOverlapped);

    // =====================================================
    // Functions (GetOverlappedResult)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetOverlappedResult(
        IntPtr hFile,
        ref OVERLAPPED lpOverlapped,
        out uint lpNumberOfBytesTransferred,
        bool bWait);

    // =====================================================
    // Functions (CancelIoEx)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CancelIoEx(IntPtr hFile, IntPtr lpOverlapped);

    // =====================================================
    // Functions (DeviceIoControl)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    // =====================================================
    // Functions (GetFileInformationByHandle)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetFileInformationByHandle(
        IntPtr hFile,
        out BY_HANDLE_FILE_INFORMATION lpFileInformation);

    // =====================================================
    // Functions (GetFileTime)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetFileTime(
        IntPtr hFile,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpCreationTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpLastAccessTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpLastWriteTime);

    // =====================================================
    // Functions (SetFileTime)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetFileTime(
        IntPtr hFile,
        ref System.Runtime.InteropServices.ComTypes.FILETIME lpCreationTime,
        ref System.Runtime.InteropServices.ComTypes.FILETIME lpLastAccessTime,
        ref System.Runtime.InteropServices.ComTypes.FILETIME lpLastWriteTime);

    // =====================================================
    // Functions (CreateDirectory)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateDirectory(string lpPathName, IntPtr lpSecurityAttributes);

    // =====================================================
    // Functions (RemoveDirectory)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool RemoveDirectory(string lpPathName);

    // =====================================================
    // Functions (GetDiskFreeSpace)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool GetDiskFreeSpace(
        string lpRootPathName,
        out uint lpSectorsPerCluster,
        out uint lpBytesPerSector,
        out uint lpNumberOfFreeClusters,
        out uint lpTotalNumberOfClusters);

    // =====================================================
    // Functions (FindFirstFile)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

    // =====================================================
    // Functions (FindNextFile)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

    // =====================================================
    // Functions (FindClose)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FindClose(IntPtr hFindFile);

    // =====================================================
    // Functions (GetFileSizeEx)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetFileSizeEx(IntPtr hFile, out long lpFileSize);

    // =====================================================
    // Functions (SetFilePointerEx)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetFilePointerEx(
        IntPtr hFile,
        long liDistanceToMove,
        out long lpNewFilePointer,
        uint dwMoveMethod);

    // =====================================================
    // Functions (GetFileType)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint GetFileType(IntPtr hFile);

    // =====================================================
    // Functions (IsDebuggerPresent)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsDebuggerPresent();

    // =====================================================
    // Functions (OutputDebugString)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern void OutputDebugString(string lpOutputString);

    // =====================================================
    // Functions (OpenProcessToken)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(
        IntPtr ProcessHandle,
        uint DesiredAccess,
        out IntPtr TokenHandle);

    // =====================================================
    // Functions (GetProcessVersion)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern uint GetProcessVersion(uint ProcessId);

    // =====================================================
    // Functions (GetSystemTimeAsFileTime)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern void GetSystemTimeAsFileTime(out System.Runtime.InteropServices.ComTypes.FILETIME lpSystemTimeAsFileTime);

    // =====================================================
    // Functions (GetTickCount)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern uint GetTickCount();

    // =====================================================
    // Functions (Sleep)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern void Sleep(uint dwMilliseconds);

    // =====================================================
    // Functions (ExitProcess)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern void ExitProcess(uint uExitCode);

    // =====================================================
    // Functions (GetStdHandle)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetStdHandle(uint nStdHandle);

    // =====================================================
    // Functions (SetStdHandle)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetStdHandle(uint nStdHandle, IntPtr hHandle);

    // =====================================================
    // Functions (CreatePipe)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CreatePipe(
        out IntPtr hReadPipe,
        out IntPtr hWritePipe,
        IntPtr lpPipeAttributes,
        uint nSize);

    // =====================================================
    // Functions (PeekNamedPipe)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool PeekNamedPipe(
        IntPtr hNamedPipe,
        IntPtr lpBuffer,
        uint nBufferSize,
        out uint lpBytesRead,
        IntPtr lpTotalBytesAvail,
        IntPtr lpBytesLeftThisMessage);

    // =====================================================
    // Functions (TransactNamedPipe)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TransactNamedPipe(
        IntPtr hNamedPipe,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesRead,
        IntPtr lpOverlapped);

    // =====================================================
    // Functions (CreateEvent)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateEvent(
        IntPtr lpEventAttributes,
        bool bManualReset,
        bool bInitialState,
        string lpName);

    // =====================================================
    // Functions (SetEvent)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetEvent(IntPtr hEvent);

    // =====================================================
    // Functions (ResetEvent)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ResetEvent(IntPtr hEvent);

    // =====================================================
    // Functions (PulseEvent)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool PulseEvent(IntPtr hEvent);

    // =====================================================
    // Functions (GetCommandLine)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetCommandLine();

    // =====================================================
    // Functions (GetEnvironmentStrings)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetEnvironmentStrings();

    // =====================================================
    // Functions (FreeEnvironmentStrings)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool FreeEnvironmentStrings(IntPtr lpszEnvironmentBlock);

    // =====================================================
    // Functions (ExpandEnvironmentStrings)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint ExpandEnvironmentStrings(string lpSrc, StringBuilder lpDst, uint nSize);

    // =====================================================
    // Functions (GetComputerNameEx)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool GetComputerNameEx(int NameType, StringBuilder lpBuffer, ref uint nSize);

    // =====================================================
    // Functions (GetUserName)
    // =====================================================
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool GetUserName(StringBuilder lpBuffer, ref uint nSize);

    // =====================================================
    // Functions (GetModuleHandleEx)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool GetModuleHandleEx(uint dwFlags, string lpModuleName, out IntPtr phModule);

    // =====================================================
    // Functions (GetModuleFileNameEx)
    // =====================================================
    [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint GetModuleFileNameEx(
        IntPtr hProcess,
        IntPtr hModule,
        StringBuilder lpFilename,
        uint nSize);

    // =====================================================
    // Additional Structs
    // =====================================================
    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
    }

    // WIN32_FIND_DATA struct
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WIN32_FIND_DATA
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    // OVERLAPPED struct
    [StructLayout(LayoutKind.Sequential)]
    public struct OVERLAPPED
    {
        public IntPtr Internal;
        public IntPtr InternalHigh;
        public uint Offset;
        public uint OffsetHigh;
        public IntPtr hEvent;
    }

    // BY_HANDLE_FILE_INFORMATION struct
    [StructLayout(LayoutKind.Sequential)]
    public struct BY_HANDLE_FILE_INFORMATION
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint dwVolumeSerialNumber;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint nNumberOfLinks;
        public uint nFileIndexHigh;
        public uint nFileIndexLow;
    }

    // =====================================================
    // Additional Constants
    // =====================================================
    // CreateFile Constants
    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;
    public const uint CREATE_NEW = 1;
    public const uint CREATE_ALWAYS = 2;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_SHARE_READ = 0x00000001;
    public const uint FILE_SHARE_WRITE = 0x00000002;
    public const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    // SetErrorMode Constants
    public const uint SEM_FAILCRITICALERRORS = 0x0001;
    public const uint SEM_NOGPFAULTERRORBOX = 0x0002;
    public const uint SEM_NOALIGNMENTFAULTEXCEPT = 0x0004;
    public const uint SEM_NOOPENFILEERRORBOX = 0x8000;

    // GetFileAttributes Constants
    public const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
    public const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
    public const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
    public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    public const uint FILE_ATTRIBUTE_ARCHIVE = 0x00000020;
    public const uint FILE_ATTRIBUTE_DEVICE = 0x00000040;
    public const uint FILE_ATTRIBUTE_TEMPORARY = 0x00000100;
    public const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200;
    public const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
    public const uint FILE_ATTRIBUTE_COMPRESSED = 0x00000800;
    public const uint FILE_ATTRIBUTE_OFFLINE = 0x00001000;
    public const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x00002000;
    public const uint FILE_ATTRIBUTE_ENCRYPTED = 0x00004000;

    // GetDriveType Constants
    public const uint DRIVE_UNKNOWN = 0x00;
    public const uint DRIVE_NO_ROOT_DIR = 0x01;
    public const uint DRIVE_REMOVABLE = 0x02;
    public const uint DRIVE_FIXED = 0x03;
    public const uint DRIVE_REMOTE = 0x04;
    public const uint DRIVE_CDROM = 0x05;
    public const uint DRIVE_RAMDISK = 0x06;

    // GetStdHandle Constants
    public const uint STD_INPUT_HANDLE = 0xFFFFFFF6;
    public const uint STD_OUTPUT_HANDLE = 0xFFFFFFF5;
    public const uint STD_ERROR_HANDLE = 0xFFFFFFF4;

    // GetFileType Constants
    public const uint FILE_TYPE_UNKNOWN = 0x0000;
    public const uint FILE_TYPE_DISK = 0x0001;
    public const uint FILE_TYPE_CHAR = 0x0002;
    public const uint FILE_TYPE_PIPE = 0x0003;
    public const uint FILE_TYPE_REMOTE = 0x8000;

    // SetFilePointerEx Constants
    public const uint FILE_BEGIN = 0x00000000;
    public const uint FILE_CURRENT = 0x00000001;
    public const uint FILE_END = 0x00000002;

    // LockFileEx Constants
    public const uint LOCKFILE_EXCLUSIVE_LOCK = 0x00000002;
    public const uint LOCKFILE_FAIL_IMMEDIATELY = 0x00000001;

    // =====================================================
    // Functions (GetConsoleWindow)
    // =====================================================
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetConsoleWindow();

    // =====================================================
    // Functions (GetConsoleScreenBufferInfo)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

    // =====================================================
    // Functions (SetConsoleTextAttribute)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleTextAttribute(IntPtr hConsoleOutput, ushort wAttributes);

    // =====================================================
    // Functions (FillConsoleOutputCharacter)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FillConsoleOutputCharacter(
        IntPtr hConsoleOutput,
        char cCharacter,
        uint nLength,
        COORD dwWriteCoord,
        out uint lpNumberOfCharsWritten);

    // =====================================================
    // Functions (FillConsoleOutputAttribute)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FillConsoleOutputAttribute(
        IntPtr hConsoleOutput,
        ushort wAttribute,
        uint nLength,
        COORD dwWriteCoord,
        out uint lpNumberOfAttrsWritten);

    // =====================================================
    // Functions (GetLargestConsoleWindowSize)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern COORD GetLargestConsoleWindowSize(IntPtr hConsoleOutput);

    // =====================================================
    // Functions (SetConsoleCursorPosition)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleCursorPosition(IntPtr hConsoleOutput, COORD dwCursorPosition);

    // =====================================================
    // Functions (GetConsoleCursorInfo)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetConsoleCursorInfo(IntPtr hConsoleOutput, out CONSOLE_CURSOR_INFO lpConsoleCursorInfo);

    // =====================================================
    // Functions (SetConsoleCursorInfo)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleCursorInfo(IntPtr hConsoleOutput, [In] ref CONSOLE_CURSOR_INFO lpConsoleCursorInfo);

    // =====================================================
    // Functions (ReadConsoleOutputCharacter)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool ReadConsoleOutputCharacter(
        IntPtr hConsoleOutput,
        [Out] StringBuilder lpCharacter,
        uint nLength,
        COORD dwReadCoord,
        out uint lpNumberOfCharsRead);

    // =====================================================
    // Functions (WriteConsoleOutputCharacter)
    // =====================================================
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool WriteConsoleOutputCharacter(
        IntPtr hConsoleOutput,
        string lpCharacter,
        uint nLength,
        COORD dwWriteCoord,
        out uint lpNumberOfCharsWritten);

    // =====================================================
    // Functions (ReadConsoleInput)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadConsoleInput(
        IntPtr hConsoleInput,
        [Out] INPUT_RECORD[] lpBuffer,
        uint nLength,
        out uint lpNumberOfEventsRead);

    // =====================================================
    // Functions (GetNumberOfConsoleInputEvents)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetNumberOfConsoleInputEvents(
        IntPtr hConsoleInput,
        out uint lpNumberOfEvents);

    // =====================================================
    // Functions (FlushConsoleInputBuffer)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FlushConsoleInputBuffer(IntPtr hConsoleInput);

    // =====================================================
    // Functions (GetConsoleMode)
    // =====================================================
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    // =====================================================
    // Additional Structs
    // =====================================================
    [StructLayout(LayoutKind.Explicit)]
    public struct INPUT_RECORD
    {
        [FieldOffset(0)]
        public ushort EventType;
        [FieldOffset(4)]
        public KEY_EVENT_RECORD KeyEvent;
        [FieldOffset(4)]
        public MOUSE_EVENT_RECORD MouseEvent;
        [FieldOffset(4)]
        public WINDOW_BUFFER_SIZE_RECORD WindowBufferSizeEvent;
        [FieldOffset(4)]
        public MENU_EVENT_RECORD MenuEvent;
        [FieldOffset(4)]
        public FOCUS_EVENT_RECORD FocusEvent;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct KEY_EVENT_RECORD
    {
        public bool bKeyDown;
        public ushort wRepeatCount;
        public ushort wVirtualKeyCode;
        public ushort wVirtualScanCode;
        public char uChar;
        public uint dwControlKeyState;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSE_EVENT_RECORD
    {
        public COORD dwMousePosition;
        public uint dwButtonState;
        public uint dwControlKeyState;
        public uint dwEventFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOW_BUFFER_SIZE_RECORD
    {
        public COORD dwSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MENU_EVENT_RECORD
    {
        public uint dwCommandId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FOCUS_EVENT_RECORD
    {
        public bool bSetFocus;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SMALL_RECT
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public COORD dwSize;
        public COORD dwCursorPosition;
        public ushort wAttributes;
        public SMALL_RECT srWindow;
        public COORD dwMaximumWindowSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CONSOLE_CURSOR_INFO
    {
        public uint dwSize;
        public bool bVisible;
    }

    // =====================================================
    // Additional Constants
    // =====================================================
    public const ushort KEY_EVENT = 0x0001;
    public const ushort MOUSE_EVENT = 0x0002;
    public const ushort WINDOW_BUFFER_SIZE_EVENT = 0x0004;
    public const ushort MENU_EVENT = 0x0008;
    public const ushort FOCUS_EVENT = 0x0010;

    // =====================================================
    // Enums
    // =====================================================
    public enum TernaryRasterOperations : uint
    {
        SRCCOPY = 0x00CC0020,
        SRCPAINT = 0x00EE0086,
        SRCAND = 0x008800C6,
        SRCINVERT = 0x00660046,
        SRCERASE = 0x00440328,
        NOTSRCCOPY = 0x00330008,
        NOTSRCPAINT = 0x001100A6,
        NOTSRCAND = 0x00001022,
        NOTSRCINVERT = 0x00990066,
        NOTSRCERASE = 0x00BB0226,
        MERGECOPY = 0x00C000CA,
        MERGEPAINT = 0x00BB0226,
        PATCOPY = 0x00F00021,
        PATPAINT = 0x00FB0A09,
        PATINVERT = 0x005A0049,
        DSTINVERT = 0x00550009,
        BLACKNESS = 0x00000042,
        WHITENESS = 0x00FF0062,
        NOMIRRORBITMAP = 0x80000000
    }

    public enum HookType : int
    {
        WH_MSGFILTER = -1,
        WH_JOURNALRECORD = 0,
        WH_JOURNALPLAYBACK = 1,
        WH_KEYBOARD = 2,
        WH_GETMESSAGE = 3,
        WH_CALLWNDPROC = 4,
        WH_CBT = 5,
        WH_SYSMSGFILTER = 6,
        WH_MOUSE = 7,
        WH_HARDWARE = 8,
        WH_DEBUG = 9,
        WH_SHELL = 10,
        WH_FOREGROUNDIDLE = 11,
        WH_CALLWNDPROCRET = 12,
        WH_KEYBOARD_LL = 13,
        WH_MOUSE_LL = 14,
        WH_THREAD_ROUTINE = 16
    }

    public enum SystemMetric : int
    {
        SM_CXSCREEN = 0,
        SM_CYSCREEN = 1,
        SM_CXVSCROLL = 2,
        SM_CYHSCROLL = 3,
        SM_CYCAPTION = 4,
        SM_CXBORDER = 5,
        SM_CYBORDER = 6,
        SM_CXDLGFRAME = 7,
        SM_CYDLGFRAME = 8,
        SM_CYVTHUMB = 9,
        SM_CXHTHUMB = 10,
        SM_CXICON = 11,
        SM_CYICON = 12,
        SM_CXCURSOR = 13,
        SM_CYCURSOR = 14,
        SM_CYMENU = 15,
        SM_CXFULLSCREEN = 16,
        SM_CYFULLSCREEN = 17,
        SM_CYKANJIWINDOW = 18,
        SM_MOUSEPRESENT = 19,
        SM_CYVSCROLL = 20,
        SM_CXHSCROLL = 21,
        SM_DEBUG = 22,
        SM_SWAPBUTTON = 23,
        SM_CXMIN = 28,
        SM_CYMIN = 29,
        SM_CXSIZE = 30,
        SM_CYSIZE = 31,
        SM_CXFRAME = 32,
        SM_CYFRAME = 33,
        SM_CXMINTRACK = 34,
        SM_CYMINTRACK = 35,
        SM_CXDOUBLECLK = 36,
        SM_CYDOUBLECLK = 37,
        SM_CXICONSPACING = 38,
        SM_CYICONSPACING = 39,
        SM_MENUDROPALIGNMENT = 40,
        SM_PENWINDOWS = 41,
        SM_DBCSENABLED = 42,
        SM_CMOUSEBUTTONS = 43,
        SM_CXFIXEDFRAME = 32,
        SM_CYFIXEDFRAME = 33,
        SM_CXSIZEFRAME = 32,
        SM_CYSIZEFRAME = 33,
        SM_SECURE = 44,
        SM_CXEDGE = 45,
        SM_CYEDGE = 46,
        SM_CXMINSPACING = 47,
        SM_CYMINSPACING = 48,
        SM_CXSMICON = 49,
        SM_CYSMICON = 50,
        SM_CYSMCAPTION = 51,
        SM_CXSMSIZE = 52,
        SM_CYSMSIZE = 53,
        SM_CXMENUSIZE = 54,
        SM_CYMENUSIZE = 55,
        SM_ARRANGE = 56,
        SM_CXMINIMIZED = 57,
        SM_CYMINIMIZED = 58,
        SM_MAXIMIZED = 59,
        SM_CXMAXTRACK = 59,
        SM_CYMAXTRACK = 60,
        SM_CXMAXIMIZED = 61,
        SM_CYMAXIMIZED = 62,
        SM_NETWORK = 63,
        SM_CLEANBOOT = 64,
        SM_CXDRAG = 68,
        SM_CYDRAG = 69,
        SM_SHOWSOUNDS = 70,
        SM_CXMENUCHECK = 71,
        SM_CYMENUCHECK = 72,
        SM_SLOWMACHINE = 73,
        SM_MIDEASTENABLED = 74,
        SM_MOUSEWHEELPRESENT = 75,
        SM_XVIRTUALSCREEN = 76,
        SM_YVIRTUALSCREEN = 77,
        SM_CXVIRTUALSCREEN = 78,
        SM_CYVIRTUALSCREEN = 79,
        SM_CMONITORS = 80,
        SM_SAMEDISPLAYFORMAT = 81,
        SM_IMMENABLED = 82,
        SM_CXFOCUSBORDER = 83,
        SM_CYFOCUSBORDER = 84,
        SM_TABLETPC = 86,
        SM_MEDIACENTER = 87,
        SM_STARTER = 88,
        SM_CMETRICS = 89,
        SM_REMOTESESSION = 0x1000,
        SM_SHUTTINGDOWN = 0x2000,
        SM_REMOTECONTROL = 0x2001,
        SM_CARETBLINKINGENABLED = 0x2006
    }

    public enum WindowLongIndex : int
    {
        GWL_WNDPROC = -4,
        GWL_HINSTANCE = -6,
        GWL_HWNDPARENT = -8,
        GWL_STYLE = -16,
        GWL_EXSTYLE = -20,
        GWL_USERDATA = -21,
        GWL_ID = -12
    }

    public enum WindowMessage : uint
    {
        WM_NULL = 0x0000,
        WM_CREATE = 0x0001,
        WM_DESTROY = 0x0002,
        WM_MOVE = 0x0003,
        WM_SIZE = 0x0005,
        WM_ACTIVATE = 0x0006,
        WM_SETFOCUS = 0x0007,
        WM_KILLFOCUS = 0x0008,
        WM_ENABLE = 0x000A,
        WM_SETREDRAW = 0x000B,
        WM_SETTEXT = 0x000C,
        WM_GETTEXT = 0x000D,
        WM_GETTEXTLENGTH = 0x000E,
        WM_PAINT = 0x000F,
        WM_CLOSE = 0x0010,
        WM_QUERYENDSESSION = 0x0011,
        WM_QUIT = 0x0012,
        WM_QUERYOPEN = 0x0013,
        WM_ERASEBKGND = 0x0014,
        WM_SYSCOLORCHANGE = 0x0015,
        WM_ENDSESSION = 0x0016,
        WM_SHOWWINDOW = 0x0018,
        WM_CTLCOLOR = 0x0019,
        WM_SETTINGCHANGE = 0x001A,
        WM_DEVMODECHANGE = 0x001B,
        WM_ACTIVATEAPP = 0x001C,
        WM_FONTCHANGE = 0x001D,
        WM_TIMECHANGE = 0x001E,
        WM_CANCELMODE = 0x001F,
        WM_SETCURSOR = 0x0020,
        WM_MOUSEACTIVATE = 0x0021,
        WM_CHILDACTIVATE = 0x0022,
        WM_QUEUESYNC = 0x0023,
        WM_GETMINMAXINFO = 0x0024,
        WM_PAINTICON = 0x0026,
        WM_ICONERASEBKGND = 0x0027,
        WM_NEXTDLGCTL = 0x0028,
        WM_SPCLDEL = 0x0029,
        WM_SETDEFID = 0x002A,
        WM_COMMAND = 0x0111,
        WM_SYSCOMMAND = 0x0112,
        WM_INITDIALOG = 0x011A,
        WM_TIMER = 0x0113,
        WM_HSCROLL = 0x0114,
        WM_VSCROLL = 0x0115,
        WM_INITMENU = 0x0116,
        WM_INITMENUPOPUP = 0x0117,
        WM_MENUSELECT = 0x011F,
        WM_MENUCHAR = 0x0120,
        WM_HELP = 0x0227,
        WM_CONTEXTMENU = 0x007B,
        WM_STYLECHANGING = 0x007C,
        WM_STYLECHANGED = 0x007D,
        WM_DISPLAYCHANGE = 0x007E,
        WM_GETICON = 0x007F,
        WM_SETICON = 0x0080,
        WM_NCCREATE = 0x0081,
        WM_NCDESTROY = 0x0082,
        WM_NCCALCSIZE = 0x0083,
        WM_NCHITTEST = 0x0084,
        WM_NCPAINT = 0x0085,
        WM_NCACTIVATE = 0x0086,
        WM_GETDLGCODE = 0x0087,
        WM_NCMOUSEMOVE = 0x00A0,
        WM_NCLBUTTONDOWN = 0x00A1,
        WM_NCLBUTTONUP = 0x00A2,
        WM_NCLBUTTONDBLCLK = 0x00A3,
        WM_NCRBUTTONDOWN = 0x00A4,
        WM_NCRBUTTONUP = 0x00A5,
        WM_NCRBUTTONDBLCLK = 0x00A6,
        WM_NCMBUTTONDOWN = 0x00A7,
        WM_NCMBUTTONUP = 0x00A8,
        WM_NCMBUTTONDBLCLK = 0x00A9,
        WM_NCXBUTTONDOWN = 0x00AB,
        WM_NCXBUTTONUP = 0x00AC,
        WM_NCXBUTTONDBLCLK = 0x00AD,
        WM_INPUT = 0x00FF,
        WM_KEYFIRST = 0x0100,
        WM_KEYDOWN = 0x0100,
        WM_KEYUP = 0x0101,
        WM_CHAR = 0x0102,
        WM_DEADCHAR = 0x0103,
        WM_SYSKEYDOWN = 0x0104,
        WM_SYSKEYUP = 0x0105,
        WM_SYSCHAR = 0x0106,
        WM_SYSDEADCHAR = 0x0107,
        WM_UNICHAR = 0x0109,
        WM_IME_STARTCOMPOSITION = 0x010D,
        WM_IME_ENDCOMPOSITION = 0x010E,
        WM_IME_COMPOSITION = 0x010F,
        WM_IME_KEYLAST = 0x010F,
        WM_MOUSEFIRST = 0x0200,
        WM_MOUSEMOVE = 0x0200,
        WM_LBUTTONDOWN = 0x0201,
        WM_LBUTTONUP = 0x0202,
        WM_LBUTTONDBLCLK = 0x0203,
        WM_RBUTTONDOWN = 0x0204,
        WM_RBUTTONUP = 0x0205,
        WM_RBUTTONDBLCLK = 0x0206,
        WM_MBUTTONDOWN = 0x0207,
        WM_MBUTTONUP = 0x0208,
        WM_MBUTTONDBLCLK = 0x0209,
        WM_MOUSEWHEEL = 0x020A,
        WM_XBUTTONDOWN = 0x020B,
        WM_XBUTTONUP = 0x020C,
        WM_XBUTTONDBLCLK = 0x020D,
        WM_MOUSELAST = 0x020D,
        WM_PARENTNOTIFY = 0x0210,
        WM_ENTERMENULOOP = 0x0211,
        WM_EXITMENULOOP = 0x0212,
        WM_NEXTMENU = 0x0213,
        WM_SIZING = 0x0214,
        WM_CAPTURECHANGED = 0x0215,
        WM_MOVING = 0x0216,
        WM_POWERBROADCAST = 0x0218,
        WM_DEVICECHANGE = 0x0219,
        WM_MDICREATE = 0x0220,
        WM_MDIDESTROY = 0x0221,
        WM_MDIACTIVATE = 0x0222,
        WM_MDIRESTORE = 0x0223,
        WM_MDINEXT = 0x0224,
        WM_MDIMAXIMIZE = 0x0225,
        WM_MDITILE = 0x0226,
        WM_MDICASCADE = 0x0227,
        WM_MDIICONARRANGE = 0x0228,
        WM_MDIGETACTIVE = 0x0229,
        WM_MDIALL = 0x0230,
        WM_MDISETMENU = 0x0230,
        WM_ENTERSIZEMOVE = 0x0231,
        WM_EXITSIZEMOVE = 0x0232,
        WM_DROPFILES = 0x0233,
        WM_MDIREFRESHMENU = 0x0234,
        WM_IME_SETCONTEXT = 0x0281,
        WM_IME_NOTIFY = 0x0282,
        WM_IME_CONTROL = 0x0283,
        WM_IME_COMPOSITIONFULL = 0x0284,
        WM_IME_SELECT = 0x0285,
        WM_IME_CHAR = 0x0286,
        WM_IME_REQUEST = 0x0288,
        WM_IME_UI = 0x0289,
        WM_COMMANDHELP = 0x0290,
        WM_QUERYUISTATE = 0x0291,
        WM_UPDATEUISTATE = 0x0292,
        WM_ENABLEUISTATE = 0x0293,
        WM_GETDIALOGCODE = 0x0087,
        WM_PRINT = 0x0317,
        WM_PRINTCLIENT = 0x0318,
        WM_APPCOMMAND = 0x0319,
        WM_THEMECHANGED = 0x031A,
        WM_CLIPBOARDUPDATE = 0x031D,
        WM_DWMCOMPOSITIONCHANGED = 0x031E,
        WM_DWMNCRENDERINGCHANGED = 0x031F,
        WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320,
        WM_DWMWINDOWMAXIMIZEDCHANGE = 0x0321,
        WM_GETTITLEBARINFOEX = 0x033F,
        WM_HANDHELDFIRST = 0x0358,
        WM_HANDHELDLAST = 0x035F,
        WM_AFXFIRST = 0x0360,
        WM_AFXLAST = 0x037F,
        WM_PENWINFIRST = 0x0380,
        WM_PENWINLAST = 0x038F,
        WM_USER = 0x0400,
        WM_REFLECT = WM_USER + 0x1C00,
        WM_CTLCOLORMSGBOX = 0x028A,
        WM_CTLCOLOREDIT = 0x0133,
        WM_CTLCOLORLISTBOX = 0x0134,
        WM_CTLCOLORBTN = 0x0135,
        WM_CTLCOLORDLG = 0x0136,
        WM_CTLCOLORSCROLLBAR = 0x0137,
        WM_CTLCOLORSTATIC = 0x0138,
        SBM_SETPOS = 0x0220,
        SBM_GETPOS = 0x0221,
        SBM_SETRANGE = 0x0222,
        SBM_GETRANGE = 0x0223,
        SBM_ENABLE_ARROWS = 0x0224,
        SBM_SETRANGEREDRAW = 0x0226,
        SBM_GETSCROLLINFO = 0x0233,
        SBM_SETSCROLLINFO = 0x0234,
        SBM_GETMAXPOS = 0x0231,
        SBM_ADJUSTCODE = 0x0232
    }

    // =====================================================
    // Structs
    // =====================================================
    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public WindowMessage message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public COORD pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public bool fErase;
        public SMALL_RECT rcPaint;
        public bool fRestore;
        public bool fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public COORD ptReserved;
        public COORD ptMaxSize;
        public COORD ptMaxPosition;
        public COORD ptMinTrackSize;
        public COORD ptMaxTrackSize;
    }

    // =====================================================
    // Constants
    // =====================================================
    public const int SW_SHOWNORMAL = 1;
    public const int SW_SHOWMINIMIZED = 2;
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_SHOW = 5;
    public const int SW_HIDE = 0;

    public const int WM_USER = 0x0400;

    public const int WS_OVERLAPPED = 0x00000000;
    public const int WS_POPUP = unchecked((int)0x80000000);
    public const int WS_CHILD = 0x40000000;
    public const int WS_MINIMIZE = 0x20000000;
    public const int WS_VISIBLE = 0x10000000;
    public const int WS_DISABLED = 0x08000000;
    public const int WS_CLIPSIBLINGS = 0x04000000;
    public const int WS_CLIPCHILDREN = 0x02000000;
    public const int WS_MAXIMIZE = 0x01000000;
    public const int WS_CAPTION = 0x00C00000;
    public const int WS_BORDER = 0x00800000;
    public const int WS_DLGFRAME = 0x00400000;
    public const int WS_VSCROLL = 0x00200000;
    public const int WS_HSCROLL = 0x00100000;
    public const int WS_SYSMENU = 0x00080000;
    public const int WS_THICKFRAME = 0x00040000;
    public const int WS_GROUP = 0x00020000;
    public const int WS_TABSTOP = 0x00010000;
    public const int WS_MINIMIZEBOX = 0x00020000;
    public const int WS_MAXIMIZEBOX = 0x00010000;
    public const int WS_TILED = WS_OVERLAPPED;
    public const int WS_ICONIC = WS_MINIMIZE;
    public const int WS_SIZEBOX = WS_THICKFRAME;

    public const int WM_GETSCROLLINFO = 0x0233;
    public const int WM_SETREDRAW = 0x000B;

    // =====================================================
    // Functions (User32)
    // =====================================================
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
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

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, WindowMessage Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr LoadIcon(IntPtr hInstance, string lpIconName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EndPaint(IntPtr hWnd, [In] ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetClientRect(IntPtr hWnd, out SMALL_RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int FillRect(IntPtr hDC, [In] ref SMALL_RECT lprc, IntPtr hbr);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateSolidBrush(int crColor);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetSystemMetrics(SystemMetric nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetCursorPos(out COORD lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(HookType idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    // 在 Kernel32 类中添加这些方法声明
    [DllImport("psapi.dll", SetLastError = true)]
    public static extern bool EnumProcessModules(
        IntPtr hProcess,
        [Out] IntPtr[] lphModule,
        uint cb,
        [Out] out uint lpcbNeeded);

    public const uint EXCEPTION_DEBUG_EVENT = 1;
    public const uint DBG_EXCEPTION_NOT_HANDLED = 0x80010001;
    public const uint DBG_CONTINUE = 0x00010002;
}
