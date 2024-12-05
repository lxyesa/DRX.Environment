using System;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetworkCoreStandard.Attributes;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard.Models
{
    [LuaExport("NetworkPacket", ExportMembers = true)]
    public class NetworkPacket
    {
        [JsonPropertyName("h")]
        public string? Header { get; set; }
        [JsonPropertyName("b")]
        public object? Body { get; set; }
        [JsonPropertyName("t")]
        public int Type { get; set; }

        private Dictionary<string, object> bodyDict;

        [LuaExport] public static NetworkPacket Create() => new NetworkPacket();

        public NetworkPacket()
        {
            Header = string.Empty;
            Body = null;
            Type = 0;
            bodyDict = new Dictionary<string, object>();
        }

        [LuaExport]
        public virtual NetworkPacket SetHeader(string header)
        {
            Header = header;
            return this;
        }

        [LuaExport]
        public virtual NetworkPacket SetType(int type)
        {
            if (Enum.IsDefined(typeof(PacketType), type))
            {
                Type = type;
            }
            return this;
        }

        [LuaExport]
        public virtual NetworkPacket PutBody(string key, object value)
        {
            bodyDict[key] = value;
            return this;
        }

        [LuaExport]
        public virtual NetworkPacket Builder()
        {
            if (bodyDict.Count > 0)
            {
                // 将字典转换为数组格式
                Body = bodyDict.Select(kv => new Dictionary<string, object> { { kv.Key, kv.Value } }).ToArray();
            }
            else
            {
                Body = null;
            }
            return this;
        }

        [LuaExport]
        public virtual string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = false,
            });
        }

        [LuaExport]
        public virtual byte[] Serialize()
        {
            try
            {
                // 确保在序列化前调用 Builder()
                if (bodyDict.Count > 0)
                {
                    Builder();
                }

                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = false
                };

                // 创建一个匿名对象来序列化
                var serializableObject = new
                {
                    h = Header,
                    b = Body,
                    t = Type
                };

                return JsonSerializer.SerializeToUtf8Bytes(serializableObject, options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"序列化数据包时发生错误: {ex.Message}", ex);
            }
        }


        public static NetworkPacket Deserialize(byte[] data)
        {
            try
            {
                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    // 如果遇到未知的属性不要抛出异常
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                NetworkPacket? packet = JsonSerializer.Deserialize<NetworkPacket>(data, options);
                if (packet == null)
                {
                    throw new InvalidOperationException("反序列化结果为空");
                }
                return packet;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"反序列化数据包时发生错误: {ex.Message}", ex);
            }
        }

        [LuaExport]
        public virtual string[]? GetBody()
        {
            return Body as string[]; // 使用显式类型转换
        }

        public virtual string GetBodyString()
        {
            if (Body == null)
            {
                return "[]";
            }

            // 直接使用 JsonSerializer 序列化 Body
            return JsonSerializer.Serialize(Body);
        }

        [LuaExport]
        public virtual T? GetBodyValue<T>(string key)
        {
            if (bodyDict.TryGetValue(key, out object? value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }
                try
                {
                    // 尝试将值转换为请求的类型
                    return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
                }
                catch
                {
                    return default;
                }
            }
            return default;
        }

        [LuaExport]
        public virtual object? GetBodyValue(string key)
        {
            // 将body反序列化为字典
            if (Body is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                var dictionary = jsonElement.EnumerateArray()
                    .SelectMany(element => element.EnumerateObject())
                    .ToDictionary(property => property.Name, property => (object)property.Value);

                if (dictionary.TryGetValue(key, out object? value))
                {
                    return value;
                }
            }
            return null;
        }

        ~NetworkPacket()
        {
            bodyDict.Clear();
            bodyDict = null!;
        }
    }

    //==========================================//
    //               Packet Type              //
    //==========================================//
    public enum PacketType
    {
        Unknown = 0x00,    // 未知，通常不做回应会直接断开连接
        Request = 0x01,    // 请求，需要回应
        Response = 0x02,   // 响应，对请求的回应，必须有请求的Key
        HeartBeat = 0x03,  // 心跳包，用于保持连接
        Error = 0x04,      // 错误，用于回应错误消息，或直接断开连接
        Message = 0x05,     // 消息，通常服务端无需回应，客户端也不需要回应
        Data = 0x06,        // 数据，通常由服务端下发，客户端无需回应，但客户端需要手动解析这类包。
        Command = 0x07     // 命令，通常由服务器执行、下发，��客户端也可以发送命令，需要回应客户端是否拥有权限执行命令，或命令执行结果。
    }
}
