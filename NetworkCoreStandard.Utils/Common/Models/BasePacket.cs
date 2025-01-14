using NetworkCoreStandard.Utils.Common.Models;
using NetworkCoreStandard.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace NetworkCoreStandard.Common.Models
{
    /// <summary>
    /// 表示网络数据包的基类。
    /// </summary>
    public abstract class BasePacket
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

        private PacketObject _headers = new PacketObject();
        private PacketObject _body = new PacketObject();
        private PacketObject _state = new PacketObject();
        private string _hash = string.Empty;

        /// <summary>
        /// 获取或设置数据包的头部。
        /// </summary>
        public PacketObject Headers { get => _headers; set => _headers = value; }

        /// <summary>
        /// 获取或设置数据包的主体。
        /// </summary>
        public PacketObject Body { get => _body; set => _body = value; }

        /// <summary>
        /// 获取或设置数据包的状态。
        /// </summary>
        public PacketObject State { get => _state; set => _state = value; }

        /// <summary>
        /// 获取数据包的哈希值。
        /// </summary>
        public string Hash { get => _hash; private set => _hash = value; }

        /// <summary>
        /// 初始化 <see cref="BasePacket"/> 类的新实例。
        /// </summary>
        public BasePacket()
        {
            Headers = new PacketObject();
            Body = new PacketObject();
            State = new PacketObject();
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
                        { "body", Body.ToJsonObject() },
                        { "state", State.ToJsonObject() },
                        { "hash", JsonValue.Create(Hash) }
                    };

            // 使用缓存的属性信息
            var properties = GetCachedProperties(GetType());
            foreach (var prop in properties)
            {
                var value = prop.GetValue(this);
                if (value != null)
                {
                    jsonObject.Add(prop.Name, JsonValue.Create(value));
                }
            }

            return JsonPars.ToJson(jsonObject);
        }

        /// <summary>
        /// 从 JSON 字符串创建数据包。
        /// </summary>
        /// <typeparam name="T">数据包的类型。</typeparam>
        /// <param name="json">JSON 字符串。</param>
        /// <param name="key">用于哈希验证的密钥。</param>
        /// <returns>数据包的实例。</returns>
        /// <exception cref="ArgumentException">当 JSON 格式无效时抛出。</exception>
        /// <exception cref="InvalidOperationException">当哈希验证失败时抛出。</exception>
        public static T FromJson<T>(string json, string? key = null) where T : BasePacket, new()
        {
            var jsonObject = JsonPars.ParseToNode(json)?.AsObject()
                ?? throw new ArgumentException("Invalid JSON format", nameof(json));

            var packet = new T();

            if (jsonObject.TryGetPropertyValue("headers", out var headers))
                packet.Headers = PacketObject.FromJsonObject(headers!.AsObject());

            if (jsonObject.TryGetPropertyValue("body", out var body))
                packet.Body = PacketObject.FromJsonObject(body!.AsObject());

            if (jsonObject.TryGetPropertyValue("state", out var state))
                packet.State = PacketObject.FromJsonObject(state!.AsObject());

            if (jsonObject.TryGetPropertyValue("hash", out var hash))
                packet.Hash = hash!.GetValue<string>();

            // 使用缓存的属性信息
            var properties = GetCachedProperties(typeof(T));
            foreach (var prop in properties)
            {
                if (jsonObject.TryGetPropertyValue(prop.Name, out var value) &&
                    value != null)
                {
                    try
                    {
                        var convertedValue = value.GetValue<object>();
                        if (convertedValue != null)
                        {
                            prop.SetValue(packet, Convert.ChangeType(convertedValue, prop.PropertyType));
                        }
                    }
                    catch (InvalidCastException) { /* 忽略类型转换失败 */ }
                }
            }

            // 验证哈希值
            if (key != null && !packet.VerifySHA256(key, json, packet.Hash))
            {
                throw new InvalidOperationException("Hash verification failed");
            }

            return packet;
        }

        private static PropertyInfo[] GetCachedProperties(Type type)
        {
            return PropertyCache.GetOrAdd(type, t => t
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != nameof(Headers) &&
                           p.Name != nameof(Body) &&
                           p.Name != nameof(Hash) &&
                           p.Name != nameof(State) &&
                           p.CanRead &&
                           p.CanWrite)
                .ToArray());
        }

        /// <summary>
        /// 生成 SHA256 哈希值。
        /// </summary>
        /// <param name="key">密钥。</param>
        /// <param name="originalJson">原始 JSON 字符串。</param>
        /// <returns>生成的 SHA256 哈希值。</returns>
        public string GenerateSHA256(string key, string originalJson)
        {
            var jsonNode = JsonNode.Parse(originalJson);
            jsonNode!.AsObject().Remove("hash");
            string jsonStrWithoutHash = jsonNode.ToJsonString();

            string combinedStr = jsonStrWithoutHash + key;
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedStr));
                return Convert.ToBase64String(hashBytes);
            }
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
        /// <typeparam name="T">数据包的类型。</typeparam>
        /// <param name="data">字节数组。</param>
        /// <param name="key">密钥。</param>
        /// <returns>数据包的实例。</returns>
        /// <exception cref="ArgumentException">当密钥长度不是 16 个字符时抛出。</exception>
        public static T Unpack<T>(byte[] data, string key) where T : BasePacket, new()
        {
            if (key.Length != 16)
            {
                throw new ArgumentException("Key must be 16 characters long", nameof(key));
            }

            string jsonString = Encoding.UTF8.GetString(data);
            return FromJson<T>(jsonString, key);
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
