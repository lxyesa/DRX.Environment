using System;
using System.Text.Json;

namespace NetworkCoreStandard.Utils.Builder;

public class NetworkPacketBodyBuilder
{
    private Dictionary<string, object> data;

    public NetworkPacketBodyBuilder()
    {
        data = new Dictionary<string, object>();
    }

    public NetworkPacketBodyBuilder Put(string key, object value)
    {
        data[key] = value;
        return this;
    }

    public string Builder()
    {
        var options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return JsonSerializer.Serialize(data, options);
    }
}
