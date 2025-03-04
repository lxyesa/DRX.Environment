using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Drx.Sdk.Handle;
using Drx.Sdk.Input;
using Drx.Sdk.Memory;
using Drx.Sdk.Network;
using Drx.Sdk.Script;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Functions;
using Drx.Sdk.Script.Interfaces;
using Keystone;

class Program
{
    private static MemoryHook? _hook;
    static async Task Main(string[] args)
    {
        try
        {
            var val = AssemblerHelper.ToBytes64("je 27C07E50000", new IntPtr(0x27C0A060001));
            System.Console.WriteLine(val.ToString("X"));
        }
        catch (Exception ex)
        {
            System.Console.WriteLine(ex.Message);
        }
    }
}