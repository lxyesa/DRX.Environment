using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Drx.Sdk.Shared.Utility
{
    public static class JsonUtility
    {
        /// <summary>
        /// 将 JSON 字符串解析为 <see cref="JsonObject"/>。
        /// 返回 null 表示输入为空或解析失败（不抛出异常）。
        /// </summary>
        /// <param name="json">要解析的 JSON 字符串。</param>
        /// <returns>成功时返回 JsonObject，否则返回 null。</returns>
        public static JsonObject? ParseToJsonObject(this string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var node = JsonNode.Parse(json);
                return node as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 尝试将 JSON 字符串解析为 JsonNode（可为对象/数组/值）。
        /// </summary>
        /// <param name="json">要解析的 JSON 字符串。</param>
        /// <param name="node">解析得到的节点（失败时为 null）。</param>
        /// <returns>解析成功返回 true，否则返回 false。</returns>
        public static bool TryParseJsonNode(string? json, out JsonNode? node)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                node = JsonNode.Parse(json);
                return node is not null;
            }
            catch
            {
                node = null;
                return false;
            }
        }

        /// <summary>
        /// 将对象序列化为格式化的 JSON 字符串（可选压缩/缩进）。
        /// 若 obj 为 null 返回 null。
        /// </summary>
        /// <param name="obj">要序列化的对象（可为 JsonNode、任意 POCO）。</param>
        /// <param name="indented">是否缩进输出，默认为 false。</param>
        /// <returns>JSON 字符串或 null。</returns>
        public static string? ToJsonString(object? obj, bool indented = false)
        {
            if (obj is null) return null;
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = indented,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                // 如果已经是 JsonNode，直接调用 ToJsonString
                if (obj is JsonNode jn)
                {
                    return jn.ToJsonString(options);
                }

                return JsonSerializer.Serialize(obj, options);
            }
            catch
            {
                return null;
            }
        }
    }
}
