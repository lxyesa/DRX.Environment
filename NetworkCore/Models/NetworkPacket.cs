using System;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

public class NetworkPacket
{
    //==========================================//
    //            Public Properties            //
    //==========================================//
    public string Header { get; set; }        // 请求头，通常在非httpAPI请求中保持为空
    public string Body { get; set; }          // 请求体，它是一个字节数组，可以是任何数据
    public string Key { get; set; }           // 请求密钥，用于回应
    public PacketType Type { get; set; }      // 请求类型

    //==========================================//
    //               Constructors             //
    //==========================================//
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = null,
        AllowTrailingCommas = true
    };

    public NetworkPacket()
    {
        Header = string.Empty;
        Body = string.Empty;     // 修改默认值为空字符串
        Key = string.Empty;
        Type = PacketType.Unknown;
    }

    public NetworkPacket(string header, object body, string key, PacketType type)
    {
        Header = header;
        // 使用源生成的序列化器
        Body = JsonSerializer.Serialize(body, typeof(object), NetworkPacketJsonContext.Default);
        Key = key;
        Type = type;
    }

    //==========================================//
    //            Public Methods              //
    //==========================================//
    public virtual byte[] Serialize()
    {
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
            TypeInfoResolver = NetworkPacketJsonContext.Default
        };
        
        string jsonString = JsonSerializer.Serialize(this, NetworkPacketJsonContext.Default.NetworkPacket);
        return Encoding.UTF8.GetBytes(jsonString);
    }

    public static NetworkPacket Deserialize(byte[] data)
    {
        string jsonString = Encoding.UTF8.GetString(data);
        var packet = JsonSerializer.Deserialize<NetworkPacket>(jsonString, DefaultOptions);
        if (packet == null)
        {
            throw new InvalidOperationException("反序列化失败");
        }
        return packet;
    }

    public virtual T GetBodyObject<T>() where T : class
    {
        if (Body == null)
        {
            throw new InvalidOperationException("数据包体为空");
        }
        // 修改反序列化方式
        var result = JsonSerializer.Deserialize<T>(Body, new JsonSerializerOptions
        {
            TypeInfoResolver = NetworkPacketJsonContext.Default
        });
        if (result == null)
        {
            throw new InvalidOperationException("反序列化Body失败");
        }
        return result;
    }

    public virtual string GetBodyValue(string key)
    {
        if (Body == null)
        {
            throw new InvalidOperationException("Packet body is null");
        }
        using var doc = JsonDocument.Parse(Body);
        if (doc.RootElement.TryGetProperty(key, out var value))
        {
            return value.ToString();
        }
        return string.Empty;
    }

    public virtual void PutBody(string bodyJson){
        Body = bodyJson;
    }
}

//==========================================//
//               Packet Type              //
//==========================================//
public enum PacketType
{
    Unknown = 0x00,    // 未知，通常不做回应会直接断开连接
    Request = 0x01,    // 请求，需要回应
    Response = 0x02,   // ���应，对请求的回应，必须有请求的Key
    Heartbeat = 0x03,  // 心跳包，用于保持连接
    Error = 0x04,      // 错误，用于回应错误消息，或直接断开连接
    Message = 0x05,     // 消息，通常服务端无需回应，客户端也不需要回应
    Data = 0x06,        // 数据，通常由服务端下发，客户端无需回应，但客户端需要手动解析这类包。
    Command = 0x07     // 命令，通常由服务器执��、下发，但客户端也可以发送命令，需要回应客户端是否拥有权限执行命令，或命令执行结果。
}
