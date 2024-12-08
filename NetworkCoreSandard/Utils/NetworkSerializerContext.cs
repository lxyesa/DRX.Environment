using System.Text.Json;
using System.Text.Json.Serialization;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Models;

namespace NetworkCoreStandard.Utils;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default
)]
[JsonSerializable(typeof(NetworkPacket))]
[JsonSerializable(typeof(NetworkPacket[]))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<NetworkPacket>))]
[JsonSerializable(typeof(List<Dictionary<string, object>>))]
[JsonSerializable(typeof(List<NetworkPacket[]>))]
[JsonSerializable(typeof(List<List<NetworkPacket>>))]
[JsonSerializable(typeof(List<List<Dictionary<string, object>>>))]
[JsonSerializable(typeof(List<List<NetworkPacket[]>>))]
[JsonSerializable(typeof(List<List<List<NetworkPacket>>>))]
[JsonSerializable(typeof(System.EventArgs))]
[JsonSerializable(typeof(System.EventArgs[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(NetworkEventArgs))]
public partial class NetworkSerializerContext : JsonSerializerContext
{

}