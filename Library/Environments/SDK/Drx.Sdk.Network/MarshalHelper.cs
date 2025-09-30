using System;
using System.Net;
using System.Runtime.InteropServices;

namespace Drx.Sdk.Network;

public static class MarshalHelper
{
    public static IPEndPoint PtrToIPEndPoint(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) throw new ArgumentNullException(nameof(ptr));
        var handle = GCHandle.FromIntPtr(ptr);
        var target = handle.Target;

        // 情形一：直接是 IPEndPoint，直接返回
        if (target is IPEndPoint ep)
        {
            return ep;
        }

        // 情形二：目标是字符串（例如 "127.0.0.1:1234" 或 "[::1]:1234" 或 "hostname:port"），尝试解析
        if (target is string s)
        {
            try
            {
                string hostPart;
                string portPart;

                if (s.StartsWith("["))
                {
                    var idx = s.IndexOf(']');
                    if (idx <= 0) throw new FormatException("Invalid IPv6 endpoint format.");
                    hostPart = s.Substring(1, idx - 1);
                    if (s.Length <= idx + 2 || s[idx + 1] != ':') throw new FormatException("Invalid endpoint format, missing port.");
                    portPart = s.Substring(idx + 2);
                }
                else
                {
                    var lastColon = s.LastIndexOf(':');
                    if (lastColon <= 0) throw new FormatException("Invalid endpoint format, missing port.");
                    hostPart = s.Substring(0, lastColon);
                    portPart = s.Substring(lastColon + 1);
                }

                if (!int.TryParse(portPart, out var port)) throw new FormatException("Port is not a number.");

                if (System.Net.IPAddress.TryParse(hostPart, out var ip))
                {
                    return new IPEndPoint(ip, port);
                }

                var addresses = Dns.GetHostAddresses(hostPart);
                if (addresses.Length == 0) throw new InvalidOperationException("Host name could not be resolved.");
                return new IPEndPoint(addresses[0], port);
            }
            catch (Exception ex)
            {
                throw new InvalidCastException("Pointer target is a string but could not be parsed to IPEndPoint.", ex);
            }
        }

        // 其他类型，无法回退
        throw new InvalidCastException("Pointer does not point to a valid IPEndPoint or parsable string.");
    }
}
