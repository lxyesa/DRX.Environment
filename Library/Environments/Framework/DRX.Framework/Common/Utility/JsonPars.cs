using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DRX.Framework.Common.Utility
{
    public static class JsonPars
    {
        private static readonly JsonSerializerOptions DefaultOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false, // 默认无缩进
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// 将对象序列化为JSON字符串
        /// </summary>
        public static string ToJson<T>(T value, bool writeIndented = false, JsonSerializerOptions? options = null)
        {
            try
            {
                var serializerOptions = options ?? DefaultOptions;
                if (writeIndented)
                {
                    serializerOptions = new JsonSerializerOptions(serializerOptions)
                    {
                        WriteIndented = true
                    };
                }
                return JsonSerializer.Serialize(value, serializerOptions);
            }
            catch (Exception ex)
            {
                throw new JsonException($"Failed to serialize object to JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 将JSON字符串反序列化为对象
        /// </summary>
        public static T? FromJson<T>(string json, JsonSerializerOptions? options = null)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
            }
            catch (Exception ex)
            {
                throw new JsonException($"Failed to deserialize JSON to object: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 解析JSON字符串为JsonNode
        /// </summary>
        public static JsonNode? ParseToNode(this string json)
        {
            try
            {
                return JsonNode.Parse(json, new JsonNodeOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                throw new JsonException($"Failed to parse JSON string: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 安全获取JSON节点的值
        /// </summary>
        public static T? GetValue<T>(this JsonNode? node, string path, T? defaultValue = default)
        {
            try
            {
                if (node == null) return defaultValue;

                var pathParts = path.Split('.');
                JsonNode? current = node;

                foreach (var part in pathParts)
                {
                    current = current?[part];
                    if (current == null) return defaultValue;
                }

                return current.GetValue<T>();
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 尝试将JSON字符串转换为对象
        /// </summary>
        public static bool TryParse<T>(string json, out T? result, JsonSerializerOptions? options = null)
        {
            try
            {
                result = FromJson<T>(json, options);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        /// <summary>
        /// 合并两个JSON对象
        /// </summary>
        public static JsonObject Merge(JsonObject source, JsonObject target)
        {
            var merged = new JsonObject();

            foreach (var kvp in source)
            {
                merged.Add(kvp.Key, kvp.Value?.DeepClone());
            }

            foreach (var kvp in target)
            {
                if (merged.ContainsKey(kvp.Key))
                {
                    if (merged[kvp.Key] is JsonObject sourceObj &&
                        kvp.Value is JsonObject targetObj)
                    {
                        merged[kvp.Key] = Merge(sourceObj, targetObj);
                    }
                    else
                    {
                        merged[kvp.Key] = kvp.Value?.DeepClone();
                    }
                }
                else
                {
                    merged.Add(kvp.Key, kvp.Value?.DeepClone());
                }
            }

            return merged;
        }

        /// <summary>
        /// 验证JSON字符串是否有效
        /// </summary>
        public static bool IsValidJson(string json)
        {
            try
            {
                JsonNode.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 格式化JSON字符串
        /// </summary>
        public static string Format(string json)
        {
            try
            {
                var obj = JsonNode.Parse(json);
                return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                throw new JsonException($"Failed to format JSON string: {ex.Message}", ex);
            }
        }
    }
}