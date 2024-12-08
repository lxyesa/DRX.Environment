using System;
using NetworkCoreStandard.Events;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;
using NetworkCoreStandard.Utils.Extensions;

namespace NetworkCoreStandard.Extensions
{
    public static class NetworkClientExtension
    {
        public static void BeginHeartbeatListener(this NetworkClient client, bool isDebugging = false)
        {
            if (isDebugging)
            {
                Logger.Log("Client", "拓展:心跳包拓展组件已启动");
            }

            client.AddListener("OnDataReceived", (sender, args) =>
            {
                if (args.Packet != null && args.Packet.Header == "heartbeat")
                {
                    if (isDebugging)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 收到心跳包");
                    }
                }
            });

            client.AddListener("OnConnected", (sender, args) =>
            {
                if (isDebugging)
                {
                    Logger.Log("Client", "已连接到服务器");
                }

                client.DoTickAsync(() =>
                {
                    client.Send(new NetworkPacket().SetHeader("heartbeat"));
                }, 30 * 1000, "Heartbeat");
            });
        }
    }
}
