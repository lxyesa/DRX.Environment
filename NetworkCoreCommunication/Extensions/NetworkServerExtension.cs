using System;
using NetworkCoreStandard.Attributes;
using NetworkCoreStandard.Components;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Events;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard.Extensions
{
    [LuaExport("netex", ExportMembers = true)]
    public static class NetworkServerExtension
    {
        [LuaExport]
        public static void BeginHeartBeatListener(this NetworkServer server, int intervalMs, bool isDebugging = false)
        {
            if (isDebugging)
            {
                Logger.Log("Server", "拓展:心跳包拓展组件已启动");
            }

            // 监听客户端连接事件。
            server.AddListener("OnClientConnected", (sender, args) =>
            {
                if (isDebugging)
                {
                    Logger.Log("Server", $"客户端 {args.RemoteEndPoint} 已连接");
                    Logger.Log("Server", $"当前连接数：{server.GetClients().Count}");
                }
                args.Model?.AddComponent<HeartBeatComponent>();
                args.Model?.GetComponent<HeartBeatComponent>()?.SetHeartbeatTimeout(10 * 60 * 1000);
            });

            server.AddListener("OnDataReceived", (sender, args) =>
            {
                if (args.Packet?.Header == "heartbeat")
                {
                    if (args.Model?.GetComponent<HeartBeatComponent>() is HeartBeatComponent heartbeat)
                    {
                        heartbeat.UpdateHeartbeat();
                        if (isDebugging)
                        {
                            Logger.Log("Server", $"客户端 {args.Socket.RemoteEndPoint} 发送心跳包");
                        }
                        // 回复心跳包
                        server.Send(args.Socket, new NetworkPacket()
                            .SetHeader("heartbeat")
                            .SetType((int)PacketType.HeartBeat));

                        _ = server.RaiseEventAsync("OnHeartbeatReceived", new NetworkEventArgs(
                            model: args.Model,
                            socket: args.Socket,
                            packet: args.Packet,
                            eventType: NetworkEventType.HandlerEvent,
                            message: "接收到心跳包"
                        ));

                        // 回收内存
                        GC.Collect();
                    }
                }
            });

            server.DoTickAsync(() =>
            {
                foreach (Client client in server.GetClients())
                {
                    if (client.GetComponent<HeartBeatComponent>() is HeartBeatComponent heartbeat)
                    {
                        if (heartbeat.IsTimeout())
                        {
                            // 超时断开连接
                            if (isDebugging)
                            {
                                Logger.Log("Server", $"客户端 {client.Id} 心跳超时，断开连接");
                            }
                            
                            server.HandleDisconnect(client.GetSocket());
                        }
                    }
                }
            }, intervalMs, "HeartbeatCheck");
        }
    }
}
