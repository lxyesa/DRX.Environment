using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace Drx.Sdk.Network
{
    public class HttpPacket
    {
        // 将Contents字段更改为Dictionary<string, object>类型
        public Dictionary<string, object>? Contents { get; set; } = new Dictionary<string, object>();

        // 添加一个方法，名为ToJson，返回一个JsonNode对象
        public virtual JsonNode? ToJson()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
                // 缩进为false
                WriteIndented = false
            };
            return JsonSerializer.SerializeToNode(this, options);
        }

        public virtual string ToJsonString()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
                WriteIndented = false
            };
            return JsonSerializer.Serialize(this, options);
        }

        public override string ToString()
        {
            return ToJsonString();
        }

        // 添加一个方法，名为Encrypt，返回加密后的字符串
        public string Encrypt(string key, string iv)
        {
            var json = ToJson().ToString();
            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(key);
                aes.IV = Convert.FromBase64String(iv);

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (var sw = new StreamWriter(cs))
                        {
                            sw.Write(json);
                        }
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        // 修改AddContent方法，接受name和value两个参数，并将它们添加到Contents字典中
        public void AddContent(string name, object value)
        {
            if (Contents == null)
            {
                Contents = new Dictionary<string, object>();
            }
            Contents[name] = value;
        }
        public T? GetContent<T>(string name)
        {
            if (Contents != null && Contents.TryGetValue(name, out var value))
            {
                return (T?)value;
            }
            return default;
        }
        public static HttpPacket FromJson(string jsonString)
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
                WriteIndented = false,
                PropertyNameCaseInsensitive = true  // 添加大小写不敏感选项
            };

            try
            {
                // 直接反序列化整个对象
                var packet = JsonSerializer.Deserialize<HttpPacket>(jsonString, options);
                return packet ?? new HttpPacket();
            }
            catch (JsonException)
            {
                // 如果直接反序列化失败，尝试手动解析
                var jsonNode = JsonNode.Parse(jsonString);
                var packet = new HttpPacket();

                if (jsonNode?["Contents"] is JsonObject contentsObj)
                {
                    packet.Contents = contentsObj.Deserialize<Dictionary<string, object>>(options);
                }

                return packet;
            }
        }
    }
}
