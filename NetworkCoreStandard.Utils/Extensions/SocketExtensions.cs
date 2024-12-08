using System;
using System.Diagnostics;
using System.Net.Sockets;
using NetworkCoreStandard.Utils.Common;

namespace NetworkCoreStandard.Utils.Extensions;

public static class SocketExtensions
{
    /// <summary>
    /// 接管基础Socket并转换为DRXSocket
    /// </summary>
    public static DRXSocket TakeOver<T>(this Socket baseSocket) where T : DRXSocket
    {
        // 使用 DuplicateAndClose 方法创建新的 Socket 句柄
        SocketInformation info = baseSocket.DuplicateAndClose(Process.GetCurrentProcess().Id);

        // 创建新的 DRXSocket
        return new DRXSocket(info);
    }
}