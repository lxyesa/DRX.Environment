namespace DRX.Framework.Common.Enums;

public enum DllReason : uint
{
    DLL_PROCESS_ATTACH = 1,
    DLL_THREAD_ATTACH = 2,
    DLL_THREAD_DETACH = 3,
    DLL_PROCESS_DETACH = 0
}