using System;
using NetworkCoreStandard.Attributes;
using NetworkCoreStandard.Components;
using NetworkCoreStandard.Enums;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Events;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard.Extensions
{
    public static class NetworkServerExtension
    {
        public static void BeginHeartBeatListener(this NetworkServer server, int intervalMs, TimeUnit timeUnit, int timeout, bool isDebugging = false)
        {
            if (isDebugging)
            {
                Logger.Log("Server", "拓展:心跳包拓展组件已启动");
            }

            // 监听客户端连接事件
            server.AddListener("OnClientConnected", (sender, args) =>
            {
                if (isDebugging)
                {
                    Logger.Log("Server", $"客户端 {args.Socket.RemoteEndPoint} 已连接");
                    Logger.Log("Server", $"当前连接数：{server.GetConnectedSockets().Count}");
                }

                // 直接在Socket上添加组件
                args.Socket.AddComponent<HeartBeatComponent>();
                switch (timeUnit)
                {
                    case TimeUnit.Second:
                        args.Socket.GetComponent<HeartBeatComponent>()?.SetHeartbeatTimeout(timeout * 1000);
                        break;
                    case TimeUnit.Minute:
                        args.Socket.GetComponent<HeartBeatComponent>()?.SetHeartbeatTimeout(timeout * 1000 * 60);
                        break;
                    case TimeUnit.Hour:
                        args.Socket.GetComponent<HeartBeatComponent>()?.SetHeartbeatTimeout(timeout * 1000 * 60 * 60);
                        break;
                    case TimeUnit.Millisecond:
                        args.Socket.GetComponent<HeartBeatComponent>()?.SetHeartbeatTimeout(timeout);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });

            server.AddListener("OnDataReceived", (sender, args) =>
            {
                if (args.Packet?.Header == "heartbeat")
                {
                    if (args.Socket.GetComponent<HeartBeatComponent>() is HeartBeatComponent heartbeat)
                    {
                        heartbeat.UpdateHeartbeat();
                        if (isDebugging)
                        {
                            Logger.Log("Server", $"客户端 {args.Socket.RemoteEndPoint} 发送心跳包");
                        }

                        server.Send(args.Socket, new NetworkPacket()
                            .SetHeader("heartbeat")
                            .SetType((int)PacketType.HeartBeat));

                        _ = server.RaiseEventAsync("OnHeartbeatReceived", new NetworkEventArgs(
                            socket: args.Socket,
                            eventType: NetworkEventType.HandlerEvent,
                            message: "接收到心跳包",
                            packet: args.Packet
                        ));
                    }
                }
            });

            // 心跳检查
            server.DoTickAsync(() =>
            {
                foreach (var socket in server.GetConnectedSockets())
                {
                    if (socket.GetComponent<HeartBeatComponent>() is HeartBeatComponent heartbeat)
                    {
                        if (heartbeat.IsTimeout())
                        {
                            if (isDebugging)
                            {
                                Logger.Log("Server", $"客户端 {socket.RemoteEndPoint} 心跳超时，断开连接");
                            }
                            server.DisconnectClient(socket);
                        }
                    }
                }
            }, intervalMs, "HeartbeatCheck");
        }
    }
}