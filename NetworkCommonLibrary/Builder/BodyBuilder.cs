using System;
using System.Text.Json;

namespace NetworkCommonLibrary.Builder;

public class BodyBuilder
{
    private Dictionary<string, object> data;

    public BodyBuilder()
    {
        data = new Dictionary<string, object>();
    }

    public BodyBuilder Put(string key, object value)
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