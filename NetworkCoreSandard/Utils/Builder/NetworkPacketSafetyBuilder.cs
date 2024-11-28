using System;
using System.Text.Json;

namespace NetworkCoreStandard.Utils.Builder;

public class NetworkPacketSafetyBuilder
{
    private Dictionary<string, object> data;
    public NetworkPacketSafetyBuilder()
    {
        data = new Dictionary<string, object>();
    }

    public NetworkPacketSafetyBuilder Put(string key, object value)
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
