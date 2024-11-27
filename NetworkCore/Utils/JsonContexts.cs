using System.Text.Json.Serialization;
using System.Collections.Generic;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(NetworkPacket))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(PacketType))]
public partial class BodyBuilderJsonContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(NetworkPacket))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(DateTime))]
public partial class NetworkPacketJsonContext : JsonSerializerContext
{
}