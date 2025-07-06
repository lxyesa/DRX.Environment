using System.ComponentModel;
using System.Runtime.InteropServices;
using static Drx.Sdk.Native.Kernel32;

namespace Drx.Sdk.Events
{
    /// <summary>
    /// 进程调试事件参数
    /// </summary>
    public class DebugEventArgs : EventArgs
    {
        /// <summary>
        /// 调试事件信息
        /// </summary>
        public DEBUG_EVENT DebugEvent { get; }

        /// <summary>
        /// 初始化调试事件参数
        /// </summary>
        /// <param name="debugEvent">调试事件</param>
        public DebugEventArgs(DEBUG_EVENT debugEvent)
        {
            DebugEvent = debugEvent;
        }
    }

    /// <summary>
    /// 用于监听进程调试事件的类
    /// </summary>
    public sealed class ProcessDebuggerListener : IDisposable
    {
        private readonly uint _processId;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _debuggerTask;
        private bool _isDisposed;

        /// <summary>
        /// 当调试事件发生时触发
        /// </summary>
        public event EventHandler<DebugEventArgs>? DebugEvent;

        /// <summary>
        /// 创建新的进程调试监听器
        /// </summary>
        /// <param name="processId">要监听的进程ID</param>
        /// <exception cref="InvalidOperationException">无法附加到进程</exception>
        public ProcessDebuggerListener(uint processId)
        {
            _processId = processId;
            _cancellationTokenSource = new CancellationTokenSource();

            // 尝试附加到进程
            if (!DebugActiveProcess(processId))
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"无法附加到进程 {processId}: {new Win32Exception(error).Message}");
            }

            // 在单独的线程中启动调试事件循环
            _debuggerTask = Task.Run(DebugEventLoop, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// 从DEBUG_EVENT结构的debugInfo字段中提取异常信息
        /// </summary>
        /// <param name="debugInfo">调试信息字节数组</param>
        /// <returns>异常信息结构</returns>
        public static ExceptionInfo GetExceptionInfoFromDebugEvent(byte[] debugInfo)
        {
            // 定义异常记录的结构并填充数据
            var exceptionInfo = new ExceptionInfo();

            if (debugInfo.Length < 24) return exceptionInfo; // 确保数组有足够的数据
            exceptionInfo.ExceptionCode = BitConverter.ToUInt32(debugInfo, 0);
            exceptionInfo.ExceptionFlags = BitConverter.ToUInt32(debugInfo, 4);
            exceptionInfo.ExceptionAddress = BitConverter.ToUInt64(debugInfo, 8);

            return exceptionInfo;
        }

        /// <summary>
        /// 获取异常代码的描述文本
        /// </summary>
        /// <param name="exceptionCode">异常代码</param>
        /// <returns>异常描述</returns>
        public static string GetExceptionCodeDescription(uint exceptionCode)
        {
            return exceptionCode switch
            {
                0x80000001 => "未处理的异常",
                0xC0000005 => "访问违规",
                0xC0000094 => "除以零",
                0xC00000FD => "堆栈溢出",
                0xC0000409 => "堆损坏",
                0xC0000374 => "堆损坏",
                _ => "未知异常"
            };
        }

        /// <summary>
        /// 调试事件监听循环
        /// </summary>
        private void DebugEventLoop()
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // 等待调试事件
                    if (!WaitForDebugEvent(out var debugEvent, 100))
                    {
                        // 如果超时或没有事件，继续循环
                        continue;
                    }

                    // 触发事件
                    DebugEvent?.Invoke(this, new DebugEventArgs(debugEvent));

                    // 处理完事件后继续执行进程
                    var continueStatus = debugEvent.dwDebugEventCode == EXCEPTION_DEBUG_EVENT
                        ? DBG_EXCEPTION_NOT_HANDLED
                        : DBG_CONTINUE;

                    ContinueDebugEvent(debugEvent.dwProcessId, debugEvent.dwThreadId, continueStatus);
                }
            }
            finally
            {
                // 确保释放进程
                try
                {
                    DebugActiveProcessStop(_processId);
                }
                catch
                {
                    // 忽略释放过程中的错误
                }
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        private void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                // 停止调试任务
                _cancellationTokenSource.Cancel();
                try
                {
                    _debuggerTask.Wait(1000);
                }
                catch
                {
                    // 忽略等待过程中的错误
                }

                // 释放资源
                _cancellationTokenSource.Dispose();
            }

            // 确保停止调试进程
            try
            {
                DebugActiveProcessStop(_processId);
            }
            catch
            {
                // 忽略释放过程中的错误
            }

            _isDisposed = true;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~ProcessDebuggerListener()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// 异常信息结构
    /// </summary>
    public struct ExceptionInfo
    {
        /// <summary>
        /// 异常代码
        /// </summary>
        public uint ExceptionCode;

        /// <summary>
        /// 异常标志
        /// </summary>
        public uint ExceptionFlags;

        /// <summary>
        /// 异常发生的地址
        /// </summary>
        public ulong ExceptionAddress;
    }
}