using System;
using NetworkCoreStandard.Attributes;
using NetworkCoreStandard.Components;
using NetworkCoreStandard.Enums;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;
using NetworkCoreStandard.Utils.Common;
using NetworkCoreStandard.Utils.Common.Models;
using NetworkCoreStandard.Utils.Extensions;

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
                _ = args.Socket.AddComponent<HeartBeatComponent>();
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
                if (args.Packet?.GetObject<NetworkPacket>().GetHeader() == 3)
                {
                    if (args.Socket.GetComponent<HeartBeatComponent>() is HeartBeatComponent heartbeat)
                    {
                        heartbeat.UpdateHeartbeat();
                        if (isDebugging)
                        {
                            Logger.Log("Server", $"客户端 {args.Socket.RemoteEndPoint} 发送心跳包");
                        }

                        server.Send(args.Socket, new NetworkPacket()
                            .SetHeader(3).ToJson().GetBytes());

                        _ = server.PushEventAsync("OnHeartbeatReceived", new NetworkEventArgs(
                            socket: args.Socket,
                            eventType: NetworkEventType.HandlerEvent,
                            packet: args.Packet
                        ));
                    }
                }
            });

            // 心跳检查
            _ = server.AddTask(() =>
            {
                foreach (DRXSocket socket in server.GetConnectedSockets())
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

        // 额外监听器
        public static void BeginExtraListener(this NetworkServer server, bool isDebugging = false)
        {
            if (isDebugging)
            {
                Logger.Log("Server", "拓展:额外拓展组件已启动");
            }

            server.AddListener("OnDataReceived", (sender, args) =>
            {
                if (isDebugging)
                {
                    Logger.Log("Server", $"客户端 {args.Socket.RemoteEndPoint} 发送数据包");
                }

                // 注册包
                if (args.Packet?.GetObject<NetworkPacket>().GetHeader() == 1)
                {
                    if (args.Packet?.GetObject<NetworkPacket>().GetRequestIdentifier() != 2) // 不是注册包
                    {
                        return;
                    }
                    if (isDebugging)
                    {
                        Logger.Log("Server", $"客户端 {args.Socket.RemoteEndPoint} 发送注册包");
                    }

                    _ = server.PushEventAsync("OnRegister", new NetworkEventArgs(
                        socket: args.Socket,
                        eventType: NetworkEventType.HandlerEvent,
                        message: "接收到注册包",
                        packet: args.Packet
                    ));
                }

                // 登录包
                if (args.Packet?.GetObject<NetworkPacket>().GetHeader() == 1)
                {
                    if (args.Packet?.GetObject<NetworkPacket>().GetRequestIdentifier() != 1) // 不是登录包
                    {
                        return;
                    }
                    if (isDebugging)
                    {
                        Logger.Log("Server", $"客户端 {args.Socket.RemoteEndPoint} 发送登录包");
                    }

                    _ = server.PushEventAsync("OnLogin", new NetworkEventArgs(
                        socket: args.Socket,
                        eventType: NetworkEventType.HandlerEvent,
                        message: "接收到登录包",
                        packet: args.Packet
                    ));
                }
            });
        }

        public static void GetClient(this NetworkServer server, string remoteEndPoint)
        {
            foreach (DRXSocket socket in server.GetConnectedSockets())
            {
                if (socket.RemoteEndPoint.ToString() == remoteEndPoint)
                {
                    Logger.Log("Server", $"客户端 {remoteEndPoint} 已连接");
                    return;
                }
            }

            Logger.Log("Server", $"客户端 {remoteEndPoint} 未连接");
        }

        public static void GetClient(this NetworkServer server, int id)
        {
            foreach (DRXSocket socket in server.GetConnectedSockets())
            {
                if (socket.GetComponent<ClientComponent>()?.Id == id)
                {
                    Logger.Log("Server", $"客户端 {id} 已连接");
                    return;
                }
            }

            Logger.Log("Server", $"客户端 {id} 未连接");
        }
    }
}