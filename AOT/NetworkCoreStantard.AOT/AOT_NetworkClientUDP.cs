#region 引用声明
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NetworkCoreStandard.Enums;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Utils;
using NetworkCoreStandard.Utils.Extensions;
#endregion

namespace NetworkCoreStandard.AOT
{
    public class AOT_NetworkClientUDP
    {
        #region 字段
        private static NetworkClientUDP? _clientUDP;
        #endregion

        #region 构造函数
        #endregion

        #region 委托
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void NetworkEventCallback(IntPtr args);
        #endregion

        #region 导出函数
        
        /// <summary>
        /// DllMain函数，用于处理DLL的加载和卸载事件。
        /// </summary>
        /// <param name="hModule">DLL模块的句柄</param>
        /// <param name="ulReasonForCall">调用原因代码</param>
        /// <param name="lpReserved">保留参数</param>
        /// <returns></returns>
        [UnmanagedCallersOnly(EntryPoint = "DllMain", CallConvs = new[] { typeof(CallConvStdcall) })]
        public static bool DllMain(IntPtr hModule, uint ulReasonForCall, IntPtr lpReserved)
        {
            switch ((DllReason)ulReasonForCall)
            {
                case DllReason.DLL_PROCESS_ATTACH:
                    HandleProcessAttach();
                    break;

                case DllReason.DLL_PROCESS_DETACH:
                    HandleProcessDetach();
                    break;
            }
            return true;
        }

        
        /// <summary>
        /// 创建一个新的UDP客户端。
        /// </summary>
        [UnmanagedCallersOnly(EntryPoint = "NetworkClientUDP_Start", CallConvs = new[] { typeof(CallConvStdcall) })]
        public static void NetworkClientUDP_Start()
        {
            _clientUDP?.Start();
            Logger.Log("NetworkClientUDP", "UDP客户端已启动");
        }


        #endregion

        #region 辅助方法
        private static void HandleProcessAttach()
        {
            // var modulePtr = Win32API.GetModuleHandle(IntPtr.Zero);
            // var modulePath = new StringBuilder(260);
            // _ = Win32API.GetModuleFileName(modulePtr, modulePath, modulePath.Capacity);
            _clientUDP = new NetworkClientUDP(8460);
        }

        private static void HandleProcessDetach()
        {
            // Add any cleanup code here
        }
        #endregion
    }
}