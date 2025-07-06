using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Drx.Sdk.Memory
{
    /// <summary>
    /// 提供内存Hook功能的类，支持通过进程名创建和管理Hook实例
    /// </summary>
    [ScriptClass("MemoryHook")]
    public class MemoryHook : IDisposable
    {
        private readonly IntPtr processHandle;
        private readonly string processName;
        private readonly Dictionary<string, HookInst?> hookInstances;
        private bool disposed = false;

        /// <summary>
        /// 通过进程名创建MemoryHook实例
        /// </summary>
        /// <param name="processName">要Hook的进程名称</param>
        public MemoryHook(string processName)
        {
            this.processName = processName;
            this.hookInstances = new Dictionary<string, HookInst?>();
            
            // 获取进程句柄
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                throw new ArgumentException($"无法找到进程: {processName}");
            }
            
            Process process = processes[0];
            processHandle = Kernel32.OpenProcess(
                Kernel32.ProcessAccess.PROCESS_VM_OPERATION | 
                Kernel32.ProcessAccess.PROCESS_VM_READ | 
                Kernel32.ProcessAccess.PROCESS_VM_WRITE | 
                Kernel32.ProcessAccess.PROCESS_QUERY_INFORMATION,
                false, 
                process.Id);
                
            if (processHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"无法获取进程句柄: {processName}");
            }
        }

        /// <summary>
        /// 创建新的Hook实例
        /// </summary>
        /// <param name="hookName">Hook实例名称</param>
        /// <param name="targetAddress">Hook目标地址</param>
        /// <returns>创建的Hook实例</returns>
        public HookInst? CreateHook(string hookName, IntPtr targetAddress)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(MemoryHook));
            
            if (string.IsNullOrEmpty(hookName))
                throw new ArgumentException("Hook名称不能为空", nameof(hookName));
            
            if (targetAddress == IntPtr.Zero)
                throw new ArgumentException("目标地址不能为零", nameof(targetAddress));
            
            // 如果已存在同名实例，先清除
            if (hookInstances.ContainsKey(hookName))
            {
                Clear(hookName);
            }
            
            // 分配内存空间用于代码洞
            IntPtr allocatedAddress = MemoryWriter.Alloc(targetAddress, 1024, processHandle);
            
            // 创建Hook实例
            HookInst? hookInst = new HookInst(processHandle, targetAddress, hookName, allocatedAddress);
            
            // 添加到实例集合
            hookInstances[hookName] = hookInst;
            
            return hookInst;
        }

        /// <summary>
        /// 获取指定名称的Hook实例，不存在则创建
        /// </summary>
        /// <param name="hookName">Hook实例名称</param>
        /// <returns>Hook实例</returns>
        public HookInst? GetInst(string hookName)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(MemoryHook));
                
            if (string.IsNullOrEmpty(hookName))
                throw new ArgumentException("Hook名称不能为空", nameof(hookName));
                
            // 如果已存在实例则返回
            if (hookInstances.TryGetValue(hookName, out HookInst? inst))
            {
                return inst;
            }
            
            // 不存在则创建一个新实例（但没有目标地址，需要后续设置）
            HookInst? newInst = new HookInst(processHandle, IntPtr.Zero, hookName, IntPtr.Zero);
            hookInstances[hookName] = newInst;
            return newInst;
        }

        /// <summary>
        /// 清除指定Hook实例
        /// </summary>
        /// <param name="hookName">Hook实例名称</param>
        /// <returns>操作是否成功</returns>
        public bool Clear(string hookName)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(MemoryHook));
                
            if (string.IsNullOrEmpty(hookName))
                throw new ArgumentException("Hook名称不能为空", nameof(hookName));
                
            // 如果实例存在则清除
            if (hookInstances.TryGetValue(hookName, out HookInst inst))
            {
                // 如果Hook激活，先禁用
                if (inst.IsEnabled)
                {
                    inst.Disable();
                }
                
                // 释放资源
                inst.Dispose();
                
                // 从字典中移除
                hookInstances.Remove(hookName);
                
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// 启用指定名称的Hook
        /// </summary>
        /// <param name="hookName">Hook实例名称</param>
        /// <returns>操作是否成功</returns>
        public bool Enable(string hookName)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(MemoryHook));
                
            if (hookInstances.TryGetValue(hookName, out HookInst inst))
            {
                return inst.Enable();
            }
            
            return false;
        }

        /// <summary>
        /// 禁用指定名称的Hook
        /// </summary>
        /// <param name="hookName">Hook实例名称</param>
        /// <returns>操作是否成功</returns>
        public bool Disable(string hookName)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(MemoryHook));
                
            if (hookInstances.TryGetValue(hookName, out HookInst inst))
            {
                return inst.Disable();
            }
            
            return false;
        }
        
        /// <summary>
        /// 验证进程句柄是否有效
        /// </summary>
        private bool IsProcessValid()
        {
            if (processHandle == IntPtr.Zero)
                return false;

            try
            {
                uint exitCode;
                return Kernel32.GetExitCodeProcess(processHandle, out exitCode) && exitCode == Kernel32.STILL_ACTIVE;
            }
            catch
            {
                return false;
            }
        }

        #region IDisposable Implementation
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // 清理所有Hook实例
                    foreach (var pair in hookInstances)
                    {
                        try
                        {
                            pair.Value.Dispose();
                        }
                        catch
                        {
                            // 忽略异常
                        }
                    }
                    hookInstances.Clear();
                }

                // 关闭进程句柄
                if (processHandle != IntPtr.Zero)
                {
                    Kernel32.CloseHandle(processHandle);
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MemoryHook()
        {
            Dispose(false);
        }
        #endregion
    }
}
