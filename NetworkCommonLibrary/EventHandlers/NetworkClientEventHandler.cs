using System;
using System.Net.Sockets;
using NetworkCommonLibrary.Models;

namespace NetworkCommonLibrary.EventHandlers
{
    public abstract class NetworkClientEventHandler
    {
        /// <summary>
        /// 当收到数据包时触发
        /// </summary>
        /// <param name="client">TCP客户端实例</param>
        /// <param name="packet">接收到的数据包</param>
        public abstract void OnPacketReceived(TcpClient client, NetworkPacket packet);

        /// <summary>
        /// 当连接建立时触发
        /// </summary>
        /// <param name="client">TCP客户端实例</param>
        public abstract void OnConnected(TcpClient client);

        /// <summary>
        /// 当心跳包发送时触发
        /// </summary>
        /// <param name="client">TCP客户端实例</param>
        /// <param name="heartbeatPacket">发送的心跳包</param>
        public abstract void OnHeartbeatSent(TcpClient client, NetworkPacket heartbeatPacket);

        /// <summary>
        /// 当连接断开时触发
        /// </summary>
        /// <param name="client">TCP客户端实例</param>
        /// <param name="reason">断开原因</param>
        public abstract void OnDisconnected(TcpClient client, string reason);

        /// <summary>
        /// 当发送数据包时触发
        /// </summary>
        /// <param name="client">TCP客户端实例</param>
        /// <param name="packet">发送的数据包</param>
        public abstract void OnPacketSent(TcpClient client, NetworkPacket packet);
    }
}