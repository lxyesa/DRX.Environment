using System.Text;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using DRX.Framework.Common.Models;
using DRX.Framework.Common.Utility;

namespace DRX.Framework.Common.Base
{
    /// <summary>
    /// 表示网络数据包的基类。
    /// </summary>
    /// <typeparam name="T">数据包的具体类型。</typeparam>
    public abstract class BasePacket<T> where T : BasePacket<T>, new()
    {
        private ArgObject _headers = new ArgObject();
        private ArgObject _data = new ArgObject();
        private ArgObject _state = new ArgObject();
        private string _hash = string.Empty;

        /// <summary>
        /// 获取或设置数据包的头部。
        /// </summary>
        public ArgObject Headers { get => _headers; set => _headers = value; }

        /// <summary>
        /// 获取或设置数据包的主体。
        /// </summary>
        public ArgObject Data { get => _data; set => _data = value; }

        /// <summary>
        /// 获取或设置数据包的状态。
        /// </summary>
        public ArgObject State { get => _state; set => _state = value; }

        /// <summary>
        /// 获取数据包的哈希值。
        /// </summary>
        public string Hash { get => _hash; private set => _hash = value; }

        /// <summary>
        /// 初始化 <see cref="BasePacket{T}"/> 类的新实例。
        /// </summary>
        public BasePacket()
        {
            Headers = new ArgObject();
            Data = new ArgObject();
            State = new ArgObject();
            Hash = string.Empty;
        }

        /// <summary>
        /// 将数据包转换为 JSON 字符串。
        /// </summary>
        /// <returns>数据包的 JSON 字符串表示。</returns>
        public virtual string ToJson()
        {
            var jsonObject = new JsonObject
            {
                { "headers", Headers.ToJsonObject() },
                { "data", Data.ToJsonObject() },
                { "state", State.ToJsonObject() },
                { "hash", JsonValue.Create(Hash) }
            };

            AddPropertiesToJson(jsonObject);

            return JsonPars.ToJson(jsonObject);
        }

        /// <summary>
        /// 从 JSON 字符串创建数据包。
        /// </summary>
        /// <param name="json">JSON 字符串。</param>
        /// <param name="key">用于哈希验证的密钥。</param>
        /// <returns>数据包的实例。</returns>
        /// <exception cref="ArgumentException">当 JSON 格式无效时抛出。</exception>
        /// <exception cref="InvalidOperationException">当哈希验证失败时抛出。</exception>
        public static T FromJson(string json, string? key = null)
        {
            var jsonNode = json.ParseToNode();
            if (jsonNode is not JsonObject jsonObject)
            {
                throw new ArgumentException("Invalid JSON format", nameof(json));
            }

            var packet = new T();

            if (jsonObject.TryGetPropertyValue("headers", out var headers))
            {
                packet.Headers = ArgObject.FromJsonObject(headers.AsObject());
            }

            if (jsonObject.TryGetPropertyValue("data", out var data))
            {
                packet.Data = ArgObject.FromJsonObject(data.AsObject());
            }

            if (jsonObject.TryGetPropertyValue("state", out var state))
            {
                packet.State = ArgObject.FromJsonObject(state.AsObject());
            }

            if (jsonObject.TryGetPropertyValue("hash", out var hash))
            {
                packet.Hash = hash.GetValue<string>() ?? string.Empty;
            }

            packet.SetPropertiesFromJson(jsonObject);

            // 验证哈希值
            if (key != null && !packet.VerifySHA256(key, json, packet.Hash))
            {
                throw new InvalidOperationException("Hash verification failed");
            }

            return packet;
        }

        /// <summary>
        /// 将属性添加到 JSON 对象中。
        /// </summary>
        /// <param name="jsonObject">JSON 对象。</param>
        protected abstract void AddPropertiesToJson(JsonObject jsonObject);

        /// <summary>
        /// 从 JSON 对象中设置属性。
        /// </summary>
        /// <param name="jsonObject">JSON 对象。</param>
        protected abstract void SetPropertiesFromJson(JsonObject jsonObject);

        /// <summary>
        /// 生成 SHA256 哈希值。
        /// </summary>
        /// <param name="key">密钥。</param>
        /// <param name="originalJson">原始 JSON 字符串。</param>
        /// <returns>生成的 SHA256 哈希值。</returns>
        public string GenerateSHA256(string key, string originalJson)
        {
            var jsonNode = JsonNode.Parse(originalJson);
            if (jsonNode is JsonObject jo)
            {
                jo.Remove("hash");
                string jsonStrWithoutHash = jo.ToJsonString();
                string combinedStr = jsonStrWithoutHash + key;

                using var sha256 = SHA256.Create();
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedStr));
                return Convert.ToBase64String(hashBytes);
            }

            throw new ArgumentException("Invalid JSON structure for hashing", nameof(originalJson));
        }

        public T TryGenerateHash(string key16char)
        {
            var packet = new T();

            try
            {
                string jsonString = ToJson();
                Hash = GenerateSHA256(key16char, jsonString);
                jsonString = ToJson(); // 更新后的 JSON 字符串包含哈希值
                packet = FromJson(jsonString, key16char);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Hash generation failed", ex);
            }
            return packet;
        }

        /// <summary>
        /// 验证 SHA256 哈希值。
        /// </summary>
        /// <param name="key">密钥。</param>
        /// <param name="originalJson">原始 JSON 字符串。</param>
        /// <param name="hash">要验证的哈希值。</param>
        /// <returns>验证结果，true 表示验证通过，false 表示验证失败。</returns>
        public bool VerifySHA256(string key, string originalJson, string hash)
        {
            string generatedHash = GenerateSHA256(key, originalJson);
            return generatedHash == hash;
        }

        /// <summary>
        /// 打包数据包为字节数组。
        /// </summary>
        /// <param name="key">密钥。</param>
        /// <returns>字节数组。</returns>
        /// <exception cref="ArgumentException">当密钥长度不是 16 个字符时抛出。</exception>
        public byte[] Pack(string key)
        {
            if (key.Length != 16)
            {
                throw new ArgumentException("Key must be 16 characters long", nameof(key));
            }

            string jsonString = ToJson();
            Hash = GenerateSHA256(key, jsonString);
            jsonString = ToJson(); // 更新后的 JSON 字符串包含哈希值

            return Encoding.UTF8.GetBytes(jsonString);
        }

        /// <summary>
        /// 解包字节数组为数据包。
        /// </summary>
        /// <param name="data">字节数组。</param>
        /// <param name="key">密钥。</param>
        /// <returns>数据包的实例。</returns>
        /// <exception cref="ArgumentException">当密钥长度不是 16 个字符时抛出。</exception>
        public static T Unpack(byte[] data, string key)
        {
            if (key.Length != 16)
            {
                throw new ArgumentException("Key must be 16 characters long", nameof(key));
            }

            string jsonString = Encoding.UTF8.GetString(data);

            return FromJson(jsonString, key);
        }

        /// <summary>
        /// 解包 Base64 字符串为数据包。
        /// </summary>
        /// <param name="base64">Base64 编码的字符串。</param>
        /// <param name="key">用于哈希验证的密钥。</param>
        /// <returns>数据包的实例。</returns>
        /// <exception cref="ArgumentException">当密钥长度不是 16 个字符或 Base64 格式无效时抛出。</exception>
        public static T UnpackBase64(string base64, string key)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                throw new ArgumentException("Base64 字符串不能为空。", nameof(base64));
            }

            // 去除字符串两端的引号
            string trimmedBase64 = base64.Trim('"');

            byte[] decodedBytes;
            try
            {
                decodedBytes = Convert.FromBase64String(trimmedBase64);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("提供的字符串不是有效的 Base64 格式。", nameof(base64), ex);
            }

            return Unpack(decodedBytes, key);
        }

        /// <summary>
        /// 验证请求 ID。
        /// </summary>
        /// <param name="originalReqId">原始请求 ID。</param>
        /// <param name="responseReqId">响应请求 ID。</param>
        /// <returns>验证结果，true 表示请求 ID 匹配，false 表示不匹配。</returns>
        public static bool VerifyRequestID(string originalReqId, string responseReqId)
        {
            return originalReqId == responseReqId;
        }
    }
}
