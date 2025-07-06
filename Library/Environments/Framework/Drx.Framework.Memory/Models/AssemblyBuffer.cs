namespace Drx.Framework.Memory
{
    public enum AllocationType : uint
    {
        Commit = 0x1000,
        Reserve = 0x2000,
        Reset = 0x80000,
        ResetUndo = 0x1000000,
    }

    public enum MemoryProtection : uint
    {
        Execute = 0x10,
        ExecuteRead = 0x20,
        ExecuteReadWrite = 0x40,
        ExecuteWriteCopy = 0x80,
        NoAccess = 0x01,
        ReadOnly = 0x02,
        ReadWrite = 0x04,
        WriteCopy = 0x08,
        GuardModifierflag = 0x100,
        NoCacheModifierflag = 0x200,
        WriteCombineModifierflag = 0x400
    }
}
