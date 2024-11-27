using System;
using System.Text.Json;
using NetworkCommonLibrary.Models;

namespace NetworkCommonLibrary.Builder;
public class PacketBuilder
{
    private NetworkPacket packet;

    public PacketBuilder()
    {
        packet = new NetworkPacket();
    }

    public PacketBuilder SetHeader(string header)
    {
        packet.Header = header;
        return this;
    }

    public PacketBuilder SetBody(string jsonBody)
    {
        // 将 JSON 字符串反序列化为对象
        packet.Body = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBody);
        return this;
    }

    public PacketBuilder GenerateKey()
    {
        packet.Key = Guid.NewGuid().ToString("N");
        return this;
    }

    public string Builder(int type)
    {
        packet.Type = type;
        var options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return JsonSerializer.Serialize(packet, options);
    }
}