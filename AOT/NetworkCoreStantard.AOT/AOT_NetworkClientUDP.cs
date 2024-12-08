#region 引用声明
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NetworkCoreStandard.Managers;
using NetworkCoreStandard.Api;
using System.Text;
using NetworkCoreStandard.Enums;
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

        #region 导出函数
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

        [UnmanagedCallersOnly(EntryPoint = "NetworkClientUDP_Start", CallConvs = new[] { typeof(CallConvStdcall) })]
        public static void NetworkClientUDP_Start()
        {
            _clientUDP?.Start();
        }

        [UnmanagedCallersOnly(EntryPoint = "NetworkClientUDP_AddListener", CallConvs = new[] { typeof(CallConvStdcall) })]
        public static void NetworkClientUDP_AddListener(IntPtr eventName, IntPtr callback)
        {
            var event_name = eventName.GetObject<string>();
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