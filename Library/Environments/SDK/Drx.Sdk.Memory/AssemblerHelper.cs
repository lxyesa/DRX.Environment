using Drx.Sdk.Memory.Utils;
using Drx.Sdk.Script.Attributes;
using Keystone;

namespace Drx.Sdk.Memory
{
    [ScriptClass("AssemblerHelper")]
    public class AssemblerHelper
    {
        /// <summary>
        /// 将汇编代码转换为机器码字节
        /// </summary>
        /// <param name="assembly">要转换的汇编代码</param>
        /// <param name="address">起始地址，默认为0</param>
        /// <returns>转换后的字节数组</returns>
        public static byte[] ToBytes64(
            string assembly, 
            IntPtr address = 0)
        {
            DllLoaderUtility.InitializeKeystone();
            
            try
            {
                const Architecture architecture = Architecture.X86;
                using var keystone = new Engine(architecture, Mode.X64);
                // keystone.ThrowOnError = true;
                    
                // 执行汇编转换
                var result = keystone.Assemble(assembly, (ulong)address);
                    
                return result.Buffer;
            }
            catch (Exception ex)
            {
                throw new Exception($"汇编过程中发生错误: {ex.Message}", ex);
            }
        }
    }
}