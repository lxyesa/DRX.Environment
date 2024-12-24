using System;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;
using NetworkCoreStandard.Utils.Common.Models;
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
                if (args.Packet != null && args.Packet.GetObject<NetworkPacket>().GetHeader() == 3)
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

                client.AddTask(() =>
                {
                    client.Send(new NetworkPacket().SetHeader(3));
                }, 30 * 1000, "Heartbeat");
            });
        }

        /// <summary>
        /// 发送心跳包
        /// </summary>
        /// <param name="client">客户端</param>
        /// <param name="packet">心跳包</param>
        /// <returns></returns>
        public static void SendHeartbeat(this NetworkClient client, NetworkPacket packet)
        {
            client.Send(packet);
        }

        /// <summary>
        /// 以字节数组形式发送心跳包
        /// </summary>
        /// <param name="client">客户端</param>
        /// <param name="packet">心跳包的字节数组</param>
        /// <returns></returns>
        public static void SendHeartbeat(this NetworkClient client, byte[] packet)
        {
            client.Send(packet.GetObject<NetworkPacket>());
        }
    }
}
