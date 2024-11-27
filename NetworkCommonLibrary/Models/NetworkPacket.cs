using System;
using System.Text.Json;

namespace NetworkCommonLibrary.Models;

public class NetworkPacket
{
    public string? Header { get; set; }
    public object? Body { get; set; }
    public string? Key { get; set; }
    public int Type { get; set; }

    public NetworkPacket(string header, object body, int type)
    {
        Header = header;
        Body = body;
        Key = Guid.NewGuid().ToString();
        Type = type;
    }

    public NetworkPacket()
    {
    }

    public string Unpack()
    {
        // 将自身序列化为Json字符串，返回
        return JsonSerializer.Serialize(this);
    }

    public object? GetBodyValue(string key)
    {
        try
        {
            // 如果Body本身就是字符串，先尝试将其解析为JSON对象
            if (Body is string bodyStr)
            {
                try
                {
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(bodyStr);
                    if (jsonElement.ValueKind == JsonValueKind.Object &&
                        jsonElement.TryGetProperty(key, out JsonElement value))
                    {
                        return value.ValueKind switch
                        {
                            JsonValueKind.String => value.GetString(),
                            JsonValueKind.Number => value.GetDouble(),
                            JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
                            JsonValueKind.Object => JsonSerializer.Deserialize<object>(value.GetRawText()),
                            JsonValueKind.Array => JsonSerializer.Deserialize<object[]>(value.GetRawText()),
                            _ => null
                        };
                    }
                }
                catch (JsonException)
                {
                    // 如果不是有效的JSON字符串，返回null
                    return null;
                }
            }
            else if (Body != null)
            {
                // 如果Body是对象，先序列化再反序列化以确保正确处理
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(
                    JsonSerializer.Serialize(Body));

                if (jsonElement.ValueKind == JsonValueKind.Object &&
                    jsonElement.TryGetProperty(key, out JsonElement value))
                {
                    return value.ValueKind switch
                    {
                        JsonValueKind.String => value.GetString(),
                        JsonValueKind.Number => value.GetDouble(),
                        JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
                        JsonValueKind.Object => JsonSerializer.Deserialize<object>(value.GetRawText()),
                        JsonValueKind.Array => JsonSerializer.Deserialize<object[]>(value.GetRawText()),
                        _ => null
                    };
                }
            }

            return null;
        }
        catch (Exception)
        {
            // 出现任何异常都返回null，避免程序崩溃
            return null;
        }
    }
}

