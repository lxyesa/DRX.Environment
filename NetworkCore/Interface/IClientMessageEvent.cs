
using System.Net.Sockets;

public interface IClientMessageEvent
{
    void OnConnected(TcpClient client);                    // 连接成功时触发
    void OnDisconnected(TcpClient client);                // 断开连接时触发
    void OnHeartbeatResponse(NetworkPacket packet);        // 收到心跳包响应时触发
    void OnDataReceived(NetworkPacket packet);            // 收到数据包时触发
    void OnErrorReceived(NetworkPacket packet);           // 收到错误消息时触发
    void OnMessageReceived(NetworkPacket packet);         // 收到普通消息时触发
    void OnResponseReceived(NetworkPacket packet);        // 收到响应消息时触发
}