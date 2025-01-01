using NetworkCoreStandard.Utils.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;
using NetworkCoreStandard.Utils.Common.Models;
using NetworkCoreStandard.Utils.Extensions;

namespace NetworkCoreStandard.Common
{
    public class DRXClient : DRXBehaviour
    {
        protected DRXSocket _socket;
        protected string _serverIP;
        protected int _serverPort;
        protected bool _isConnected;
        protected DateTime _serverLastActionTime;

        public bool IsConnected => _isConnected;

        /// <summary>
        /// 初始化网络客户端
        /// </summary>
        public DRXClient(string serverIP, int serverPort) : base()
        {
            _serverIP = serverIP;
            _serverPort = serverPort;
            _socket = new DRXSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _isConnected = false;
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        public virtual void Connect()
        {
            try
            {
                _ = _socket.BeginConnect(new IPEndPoint(IPAddress.Parse(_serverIP), _serverPort),
                    new AsyncCallback(ConnectCallback), null);
            }
            catch (Exception ex)
            {
                _ = PushEventAsync("OnError", new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"连接服务器时发生错误: {ex.Message}"
                ));
            }
        }

        /// <summary>
        /// 处理连接回调
        /// </summary>
        protected virtual void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                _socket.EndConnect(ar);
                _isConnected = true;

                _ = PushEventAsync("OnConnected", new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent
                ));

                BeginReceive();
            }
            catch (Exception ex)
            {
                _ = PushEventAsync("OnError", new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"连接服务器时发生错误: {ex.Message}"
                ));
            }
        }

        /// <summary>
        /// 发送数据包
        /// </summary>
        public virtual void Send(NetworkPacket packet, string key)
        {
            if (!_isConnected)
            {
                _ = PushEventAsync("OnError", new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: "未连接到服务器"
                ));
                return;
            }
            try
            {
                byte[] data = packet.Serialize(key);

                _ = _socket.BeginSend(data, 0, data.Length, SocketFlags.None,
                    new AsyncCallback(SendCallback), null);

                _ = PushEventAsync("OnDataSent", new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"发送数据包: {packet.GetHeader()}",
                    packet: packet.GetBytes()
                ));
            }
            catch (Exception ex)
            {
                _ = PushEventAsync("OnError", new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"发送数据包时发生错误: {ex.Message}"
                ));
                HandleDisconnect();
            }
        }

        // 发送数据回调
        protected virtual void SendCallback(IAsyncResult ar)
        {
            try
            {
                _ = _socket.EndSend(ar);
            }
            catch (Exception ex)
            {
                _ = PushEventAsync("OnError", new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"发送数据包时发生错误: {ex.Message}"
                ));
                HandleDisconnect();
            }
        }

        /// <summary>
        /// 开始接收数据
        /// </summary>
        protected virtual void BeginReceive()
        {
            byte[] buffer = new byte[8192];
            try
            {
                _ = _socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None,
                    new AsyncCallback(ReceiveCallback), buffer);
            }
            catch
            {
                HandleDisconnect();
            }
        }

        /// <summary>
        /// 处理接收到的数据
        /// </summary>
        protected virtual void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                int bytesRead = _socket.EndReceive(ar);
                if (bytesRead > 0)
                {
                    byte[] buffer = (byte[])ar.AsyncState;
                    NetworkPacket packet = NetworkPacket.Deserialize(buffer.Take(bytesRead).ToArray());

                    _ = PushEventAsync("OnDataReceived", new NetworkEventArgs(
                        socket: _socket,
                        eventType: NetworkEventType.HandlerEvent,
                        message: $"收到数据包: {packet.GetHeader()}",
                        packet: buffer.Take(bytesRead).ToArray()
                    ));

                    _ = PushEventAsync(1, new NetworkEventArgs(
                        socket: _socket,
                        eventType: NetworkEventType.HandlerEvent,
                        message: $"收到数据包: {packet.GetHeader()}",
                        packet: buffer.Take(bytesRead).ToArray()
                    ));

                    // 继续接收数据
                    BeginReceive();
                }
                else
                {
                    HandleDisconnect();
                }
            }
            catch
            {
                HandleDisconnect();
            }
        }

        /// <summary>
        /// 处理断开连接
        /// </summary>
        protected virtual void HandleDisconnect()
        {
            if (_isConnected)
            {
                _ = PushEventAsync("OnDisconnected", new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent
                ));
                _socket.Close();
            }
        }

        /// <summary>
        /// 主动断开连接
        /// </summary>
        public virtual void Disconnect()
        {
            HandleDisconnect();
        }
    }
}
