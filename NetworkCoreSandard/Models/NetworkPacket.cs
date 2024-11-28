using System;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetworkCoreStandard.Models
{
    public class NetworkPacket
    {
        public string? Header { get; set; }
        public string? Body { get; set; }
        public string? Key { get; set; }
        public int Type { get; set; }
        public string? Security { get; set; }

        public NetworkPacket(string header, object body, int type)
        {
            Header = header;
            Body = JsonSerializer.Serialize(body);
            Key = Guid.NewGuid().ToString();
            Type = type;
        }

        public NetworkPacket(string header, object body, int type, string key)
        {
            Header = header;
            Body = JsonSerializer.Serialize(body);
            Key = key;
            Type = type;
        }

        public NetworkPacket(string header, object body, PacketType type, string key)
        {
            Header = header;
            Body = JsonSerializer.Serialize(body);
            Key = key;
            Type = (int)type;
        }

        public void SetSecurity(string security)
        {
            Security = security;
        }

        public NetworkPacket()
        {
        }

        public string Unpack()
        {
            // 将自身序列化为Json字符串，返回
            return JsonSerializer.Serialize(this);
        }

        public static NetworkPacket Deserialize(byte[] data)
        {
            return JsonSerializer.Deserialize<NetworkPacket>(data) ?? new NetworkPacket();
        }

        public object? GetBodyValue(string key)
        {
            if (string.IsNullOrEmpty(Body))
                return null;

            using (JsonDocument document = JsonDocument.Parse(Body ?? string.Empty))
            {
                if (document.RootElement.TryGetProperty(key, out JsonElement element))
                {
                    switch (element.ValueKind)
                    {
                        case JsonValueKind.Number:
                            if (element.ToString().Contains("."))
                                return element.GetDouble();
                            return element.GetInt64();
                        default:
                            return element.ToString();
                    }
                }
            }
            return null;
        }

        public byte[] Serialize()
        {
            return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(this);
        }
    }

    //==========================================//
    //               Packet Type              //
    //==========================================//
    public enum PacketType
    {
        Unknown = 0x00,    // 未知，通常不做回应会直接断开连接
        Request = 0x01,    // 请求，需要回应
        Response = 0x02,   // 响应，对请求的回应，必须有请求的Key
        Heartbeat = 0x03,  // 心跳包，用于保持连接
        Error = 0x04,      // 错误，用于回应错误消息，或直接断开连接
        Message = 0x05,     // 消息，通常服务端无需回应，客户端也不需要回应
        Data = 0x06,        // 数据，通常由服务端下发，客户端无需回应，但客户端需要手动解析这类包。
        Command = 0x07     // 命令，通常由服务器执行、下发，��客户端也可以发送命令，需要回应客户端是否拥有权限执行命令，或命令执行结果。
    }
}
