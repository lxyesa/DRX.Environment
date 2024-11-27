using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Runtime.InteropServices;
using System.Text.Unicode;

internal class BuilderInfo
{
    public Dictionary<string, object> Data { get; } = new();
    public DateTime CreatedTime { get; } = DateTime.UtcNow;
}

public static class NetworkPacketUtil
{
    public static NetworkPacket JsonToNetworkPacket(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            throw new ArgumentNullException(nameof(json));
        }
        var result = JsonSerializer.Deserialize<NetworkPacket>(json);
        if (result == null)
        {
            throw new InvalidOperationException("Deserialization returned null");
        }
        return result;
    }
}