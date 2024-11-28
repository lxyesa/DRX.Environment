using System;
using System.Text.Json;
using NetworkCoreStandard.Models;

namespace NetworkCoreStandard.Utils.Builder;

public class NetworkPacketBuilder
{
    private NetworkPacket packet;
    public NetworkPacketBuilder()
    {
        packet = new NetworkPacket();
    }

    public NetworkPacketBuilder SetHeader(string header)
    {
        packet.Header = header;
        return this;
    }

    public NetworkPacketBuilder SetBody(string jsonBody)
    {
        // 将 JSON 字符串反序列化为对象
        packet.Body = jsonBody;
        return this;
    }

    public NetworkPacketBuilder GenerateKey()
    {
        packet.Key = Guid.NewGuid().ToString("N");
        return this;
    }

    public string Builder(int type)
    {
        packet.Type = (int)(PacketType)type;
        var options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return JsonSerializer.Serialize(packet, options);
    }
}
