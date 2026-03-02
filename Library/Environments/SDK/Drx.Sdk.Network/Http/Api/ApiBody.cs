using System;
using System.Text.Json.Nodes;
using Drx.Sdk.Network.Http.Protocol;

namespace Drx.Sdk.Network.Http.Api
{
    /// <summary>
    /// API 请求体解析工具。
    /// 提供安全的 JSON Body 解析和类型安全的字段提取，消除重复的空检查代码。
    /// <para>
    /// 使用示例：
    /// <code>
    /// if (!ApiBody.TryParse(request, out var body, out var error))
    ///     return error;
    /// 
    /// var name = body.String("name");              // 获取字符串，null 安全
    /// var age = body.Int("age", defaultValue: 0);  // 获取整数，带默认值
    /// </code>
    /// </para>
    /// </summary>
    public static class ApiBody
    {
        /// <summary>
        /// 安全解析请求体为 JsonNode。
        /// 如果 Body 为空或无法解析，返回 false 并输出标准错误响应。
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <param name="body">解析后的 JsonNode（成功时非 null）</param>
        /// <param name="error">失败时的错误响应（成功时为 null）</param>
        /// <returns>解析是否成功</returns>
        public static bool TryParse(HttpRequest request, out JsonNode body, out IActionResult? error)
        {
            body = null!;
            error = null;

            if (string.IsNullOrEmpty(request.Body))
            {
                error = ApiResult.BadRequest("请求体不能为空");
                return false;
            }

            try
            {
                var parsed = JsonNode.Parse(request.Body);
                if (parsed == null)
                {
                    error = ApiResult.BadRequest("无法解析 JSON 请求体");
                    return false;
                }
                body = parsed;
                return true;
            }
            catch (Exception)
            {
                error = ApiResult.BadRequest("无效的 JSON 格式");
                return false;
            }
        }

        /// <summary>
        /// 从 JsonNode 安全提取字符串值
        /// </summary>
        /// <param name="node">JSON 节点</param>
        /// <param name="key">字段名</param>
        /// <param name="defaultValue">默认值（字段不存在或为空时返回）</param>
        public static string? String(this JsonNode node, string key, string? defaultValue = null)
        {
            var value = node[key]?.ToString();
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        /// <summary>
        /// 从 JsonNode 安全提取字符串值并 Trim
        /// </summary>
        public static string? TrimmedString(this JsonNode node, string key, string? defaultValue = null)
        {
            var value = node[key]?.ToString()?.Trim();
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        /// <summary>
        /// 从 JsonNode 安全提取整数值
        /// </summary>
        /// <param name="node">JSON 节点</param>
        /// <param name="key">字段名</param>
        /// <param name="defaultValue">默认值</param>
        public static int Int(this JsonNode node, string key, int defaultValue = 0)
        {
            var raw = node[key]?.ToString();
            return int.TryParse(raw, out var v) ? v : defaultValue;
        }

        /// <summary>
        /// 从 JsonNode 安全提取长整数值
        /// </summary>
        public static long Long(this JsonNode node, string key, long defaultValue = 0)
        {
            var raw = node[key]?.ToString();
            return long.TryParse(raw, out var v) ? v : defaultValue;
        }

        /// <summary>
        /// 从 JsonNode 安全提取双精度浮点数值
        /// </summary>
        public static double Double(this JsonNode node, string key, double defaultValue = 0.0)
        {
            var raw = node[key]?.ToString();
            return double.TryParse(raw, out var v) ? v : defaultValue;
        }

        /// <summary>
        /// 从 JsonNode 安全提取布尔值
        /// </summary>
        public static bool Bool(this JsonNode node, string key, bool defaultValue = false)
        {
            var raw = node[key]?.ToString();
            return bool.TryParse(raw, out var v) ? v : defaultValue;
        }

        /// <summary>
        /// 检查 JsonNode 中是否存在指定字段且不为 null
        /// </summary>
        public static bool Has(this JsonNode node, string key)
        {
            return node[key] != null;
        }

        /// <summary>
        /// 安全提取必需的字符串字段。如果不存在或为空，返回错误。
        /// </summary>
        /// <param name="node">JSON 节点</param>
        /// <param name="key">字段名</param>
        /// <param name="value">提取到的值</param>
        /// <param name="error">失败时的错误响应</param>
        /// <param name="fieldDisplayName">字段显示名（用于错误提示），默认用 key</param>
        /// <returns>是否成功获取到非空值</returns>
        public static bool RequireString(this JsonNode node, string key, out string value, out IActionResult? error, string? fieldDisplayName = null)
        {
            value = node[key]?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(value))
            {
                error = ApiResult.BadRequest($"缺少必需字段: {fieldDisplayName ?? key}");
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>
        /// 安全提取必需的正整数字段。
        /// </summary>
        public static bool RequirePositiveInt(this JsonNode node, string key, out int value, out IActionResult? error, string? fieldDisplayName = null)
        {
            var raw = node[key]?.ToString();
            if (!int.TryParse(raw, out value) || value <= 0)
            {
                error = ApiResult.BadRequest($"{fieldDisplayName ?? key} 必须是大于 0 的整数");
                value = 0;
                return false;
            }
            error = null;
            return true;
        }
    }
}
