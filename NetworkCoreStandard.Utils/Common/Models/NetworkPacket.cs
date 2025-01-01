using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NetworkCoreStandard.Utils.Common.Models
{
    /// <summary>
    /// 网络数据包结构
    /// </summary>
    public class NetworkPacket
    {
        private uint header;
        private JsonObject body;
        private uint requestIdentifier;
        private string hash;

        /// <summary>
        /// 初始化网络数据包
        /// </summary>
        public NetworkPacket()
        {
            header = 0;
            body = new JsonObject();
            requestIdentifier = 0;
            hash = string.Empty;
        }

        /// <summary>
        /// 设置包头
        /// </summary>
        /// <param name="header">包头值</param>
        /// <returns>当前网络包实例，支持链式调用</returns>
        public NetworkPacket SetHeader(uint header)
        {
            this.header = header;
            return this;
        }

        /// <summary>
        /// 获取包头
        /// </summary>
        /// <returns>包头值</returns>
        public uint GetHeader()
        {
            return header;
        }

        /// <summary>
        /// 设置包体的键值对
        /// </summary>
        /// <typeparam name="T">值的类型</typeparam>
        /// <param name="key">键名</param>
        /// <param name="value">值</param>
        /// <returns>当前网络包实例，支持链式调用</returns>
        public NetworkPacket SetBody<T>(string key, T value)
        {
            body[key] = JsonValue.Create(value);
            return this;
        }

        /// <summary>
        /// 获取完整的包体JSON字符串
        /// </summary>
        /// <returns>包体的JSON字符串表示</returns>
        public string GetBody()
        {
            return body.ToJsonString();
        }

        /// <summary>
        /// 获取包体中指定键的值
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="key">键名</param>
        /// <returns>指定类型的值</returns>
        /// <exception cref="KeyNotFoundException">当键不存在时抛出</exception>
        public T GetBody<T>(string key)
        {
            if (!body.ContainsKey(key))
            {
                throw new KeyNotFoundException("Key not found in body");
            }
            return body[key]!.GetValue<T>();
        }

        /// <summary>
        /// 设置请求标识符
        /// </summary>
        /// <param name="requestIdentifier">请求标识符值</param>
        /// <returns>当前网络包实例，支持链式调用</returns>
        public NetworkPacket SetRequestIdentifier(uint requestIdentifier)
        {
            this.requestIdentifier = requestIdentifier;
            return this;
        }

        /// <summary>
        /// 获取请求标识符
        /// </summary>
        /// <returns>请求标识符值</returns>
        public uint GetRequestIdentifier()
        {
            return requestIdentifier;
        }

        /// <summary>
        /// 将网络包序列化为JSON字符串
        /// </summary>
        /// <returns>JSON字符串</returns>
        public string ToJson()
        {
            var jsonObj = new JsonObject
            {
                ["h"] = JsonValue.Create(header),
                ["b"] = JsonNode.Parse(body.ToJsonString()), // 创建body的深拷贝
                ["r_id"] = JsonValue.Create(requestIdentifier),
                ["hash"] = JsonValue.Create(hash)
            };
            return jsonObj.ToJsonString();
        }

        /// <summary>
        /// 从JSON字符串反序列化为网络包
        /// </summary>
        /// <param name="jsonStr">JSON字符串</param>
        /// <returns>网络包实例</returns>
        /// <exception cref="ArgumentException">JSON字符串无效时抛出</exception>
        public static NetworkPacket FromJson(string jsonStr)
        {
            var packet = new NetworkPacket();
            var jsonNode = JsonNode.Parse(jsonStr);

            if (jsonNode == null)
                throw new ArgumentException("Invalid JSON string");

            packet.header = jsonNode["h"]!.GetValue<uint>();
            packet.body = jsonNode["b"]!.AsObject();
            packet.requestIdentifier = jsonNode["r_id"]!.GetValue<uint>();
            packet.hash = jsonNode["hash"]!.GetValue<string>();

            return packet;
        }

        public static NetworkPacket FormBytes(byte[] bytes)
        {
            return Deserialize(bytes);
        }

        public static NetworkPacket FormBytes(byte[] bytes, string key)
        {
            return Deserialize(bytes, key);
        }

        /// <summary>
        /// 将字节数组反序列化为网络包
        /// </summary>
        /// <param name="data">字节数组</param>
        /// <param name="key">密钥（可选）</param>
        /// <returns>网络包实例</returns>
        /// <exception cref="ArgumentException">数据为空时抛出</exception>
        /// <exception cref="InvalidOperationException">反序列化失败时抛出</exception>
        public static NetworkPacket Deserialize(byte[] data, string? key = null)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Data cannot be empty");
            }

            try
            {
                string jsonStr = Encoding.UTF8.GetString(data);

                if (key != null && !jsonStr.Contains("hash"))
                {
                    throw new InvalidOperationException("Hash not found in data");
                }

                if (key != null)
                {
                    var jsonNode = JsonNode.Parse(jsonStr);
                    string hash = jsonNode!["hash"]!.GetValue<string>();

                    bool isValid = new NetworkPacket().VerifySHA256(key, jsonStr, hash);
                    if (!isValid)
                    {
                        throw new InvalidOperationException("Data has been tampered");
                    }
                }

                return FromJson(jsonStr);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to deserialize network packet: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 将网络包序列化为字节数组
        /// </summary>
        /// <param name="packet">要序列化的网络包</param>
        /// <param name="key">密钥（可选）</param>
        /// <returns>序列化后的字节数组</returns>
        public byte[] Serialize(string? key = null)
        {
            if (key != null)
            {
                hash = GenerateSHA256(key, ToJson());
            }
            string jsonStr = ToJson();
            return Encoding.UTF8.GetBytes(jsonStr);
        }

        /// <summary>
        /// 将网络包序列化为字节数组
        /// </summary>
        /// <param name="packet">要序列化的网络包</param>
        /// <param name="key">密钥（可选）</param>
        /// <returns>序列化后的字节数组</returns>
        public static byte[] Serialize(NetworkPacket packet, string? key = null)
        {
            if (key != null)
            {
                packet.hash = packet.GenerateSHA256(key, packet.ToJson());
            }
            string jsonStr = packet.ToJson();
            return Encoding.UTF8.GetBytes(jsonStr);
        }

        /// <summary>
        /// 生成 SHA256 哈希值
        /// </summary>
        /// <param name="key">密钥</param>
        /// <param name="originalJson">原始 JSON 字符串</param>
        /// <returns>生成的 SHA256 哈希值</returns>
        public string GenerateSHA256(string key, string originalJson)
        {
            var jsonNode = JsonNode.Parse(originalJson);
            jsonNode!.AsObject().Remove("hash");
            string jsonStrWithoutHash = jsonNode.ToJsonString();

            string combinedStr = jsonStrWithoutHash + key;
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedStr));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// 验证 SHA256 哈希值
        /// </summary>
        /// <param name="key">密钥</param>
        /// <param name="originalJson">原始 JSON 字符串</param>
        /// <param name="hash">要验证的哈希值</param>
        /// <returns>验证结果，true 表示验证通过，false 表示验证失败</returns>
        public bool VerifySHA256(string key, string originalJson, string hash)
        {
            string generatedHash = GenerateSHA256(key, originalJson);
            return generatedHash == hash;
        }
    }
}
