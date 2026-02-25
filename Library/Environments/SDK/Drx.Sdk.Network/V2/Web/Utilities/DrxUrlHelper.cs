using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Drx.Sdk.Network.V2.Web.Utilities
{
    /// <summary>
    /// URL 与 Query 处理工具类：为服务端与客户端提供统一的编码/解码、解析与构造方法。
    /// 目的：保证参数在 Client 侧正确编码，在 Server 侧正确解析；并提供简单易用的 API。
    /// 注：所有注释均为中文，遵守项目约定。
    /// </summary>
    public static class DrxUrlHelper
    {
        /// <summary>
        /// 将原始的 query 字符串（可能包含前导 '?'）解析为键->第一个值的字典。
        /// 例如："?a=1&b=2" 或 "a=1&b=2" 都可以解析。
        /// 当某个键对应多个值时，返回第一个。
        /// </summary>
        /// <param name="rawQuery">原始 query（可包含前导 '?'，也可为 null 或空）</param>
        /// <returns>不为 null 的字典（键为原始键名，值为已解码的第一个值）</returns>
        public static Dictionary<string, string> ParseQuery(string? rawQuery)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (string.IsNullOrEmpty(rawQuery)) return result;
                // QueryHelpers.ParseQuery 要求传入的字符串可以包含或不包含开头的 '?'
                var parsed = QueryHelpers.ParseQuery(rawQuery);
                foreach (var kv in parsed)
                {
                    StringValues vals = kv.Value;
                    if (vals.Count > 0)
                    {
                        result[kv.Key] = vals[0] ?? string.Empty;
                    }
                    else
                    {
                        result[kv.Key] = string.Empty;
                    }
                }
            }
            catch
            {
                // 解析出错时返回尽可能多的已解析项（容错），不要抛异常以免影响请求流程
            }

            return result;
        }

        /// <summary>
        /// 使用 URL 编码构建 query 字符串（以 '?' 开头）。
        /// 客户端在构造 URL 时应使用本方法以保证参数值被正确编码。
        /// </summary>
        /// <param name="parameters">参数字典（如果为 null 或空，返回空字符串）</param>
        /// <returns>以 '?' 开头的 query 字符串，或空字符串（当 parameters 为空时）</returns>
        public static string BuildQueryString(IDictionary<string, string>? parameters)
        {
            if (parameters == null || parameters.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            sb.Append('?');
            bool first = true;
            foreach (var kv in parameters)
            {
                if (!first) sb.Append('&');
                first = false;
                var key = kv.Key ?? string.Empty;
                var val = kv.Value ?? string.Empty;
                sb.Append(Uri.EscapeDataString(key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(val));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 对单个参数值或键进行编码，适用于客户端在构造 URL 时使用。
        /// </summary>
        /// <param name="value">要编码的值（可为 null）</param>
        /// <returns>编码后的字符串（null 输入返回空字符串）</returns>
        public static string Encode(string? value)
        {
            if (value == null) return string.Empty;
            return Uri.EscapeDataString(value);
        }

        /// <summary>
        /// 对编码后的值进行解码。
        /// </summary>
        /// <param name="value">编码后的值（可为 null）</param>
        /// <returns>解码后的原始值（null 输入返回空字符串）</returns>
        public static string Decode(string? value)
        {
            if (value == null) return string.Empty;
            try
            {
                // Uri.UnescapeDataString 在某些情况下可能抛异常或行为不一致，捕获异常以保证健壮性
                return Uri.UnescapeDataString(value);
            }
            catch
            {
                return value;
            }
        }

        /// <summary>
        /// 将一个 baseUrl 和参数字典安全拼接为完整的 URL。若 baseUrl 已包含 query，会在后面追加新的参数（使用 '&'）。
        /// </summary>
        /// <param name="baseUrl">基础 URL（例如 "https://example.com/api" 或 "/api/login"）</param>
        /// <param name="parameters">参数字典</param>
        /// <returns>拼接好的 URL（不会进行相对路径解析，仅字符串拼接）</returns>
        public static string BuildUrlWithQuery(string baseUrl, IDictionary<string, string>? parameters)
        {
            if (string.IsNullOrEmpty(baseUrl)) baseUrl = string.Empty;
            var qs = BuildQueryString(parameters);
            if (string.IsNullOrEmpty(qs)) return baseUrl;

            if (baseUrl.Contains('?'))
            {
                // baseUrl 已含有 query 部分，移除前导 '?' 并使用 '&' 追加
                return baseUrl + (baseUrl.EndsWith("&") || baseUrl.EndsWith("?") ? string.Empty : "&") + qs.Substring(1);
            }
            else
            {
                return baseUrl + qs;
            }
        }
    }
}
