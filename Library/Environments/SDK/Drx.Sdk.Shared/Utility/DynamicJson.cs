using System;
using System.Collections;
using System.Dynamic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Drx.Sdk.Shared.Utility
{
    /// <summary>
    /// 基于 <see cref="DynamicObject"/> 的 JsonNode 动态包装器。
    /// 可以通过 dynamic 语法使用点号访问 JSON 属性（例如：dynamic d = node.AsDynamic(); var name = d.Name;）。
    ///
    /// 说明：
    /// - 对象字段会返回另一个 <see cref="DynamicJson"/>（包装对应的 JsonNode）。
    /// - 值类型返回对应的 CLR 原始类型（string/int/bool 等）。
    /// - 对于数组，索引访问会返回包装后的节点或原始值。
    /// - 写操作（TrySetMember）会尝试在底层 JsonObject 上设置或新增字段（若底层不是 JsonObject，则写失败）。
    /// - 该类型只是对 JsonNode 的动态友好包装，底层 JsonNode 仍然是可变的；若在多线程共享，请自行复制（例如序列化再解析）。
    /// </summary>
    public sealed class DynamicJson : DynamicObject
    {
        private readonly JsonNode? _node;
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
 
        /// <summary>
        /// 创建 DynamicJson 包装器，包装一个新的 JsonObject（默认）。
        /// </summary>
        public DynamicJson() : this(new JsonObject()) { }
 
        /// <summary>
        /// 创建 DynamicJson 包装器。
        /// </summary>
        /// <param name="node">要包装的 JsonNode（可为 null）。</param>
        public DynamicJson(JsonNode? node)
        {
            _node = node;
        }

        /// <summary>
        /// 从 JsonNode 获取一个动态包装（如果 node 为 null 返回 null）。
        /// </summary>
        /// <param name="node">要包装的 JsonNode。</param>
        /// <returns>包装对象或 null。</returns>
        public static DynamicJson? From(JsonNode? node) => node is null ? null : new DynamicJson(node);

        /// <summary>
        /// 尝试通过属性名获取成员值（点号访问），例如 d.Name。
        /// 若属性存在并为对象/数组，则返回另一个 <see cref="DynamicJson"/>；若为值则返回对应 CLR 值。
        /// 不存在时返回 null（成功，result 为 null）。
        /// </summary>
        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            result = null;
            if (_node is null) return true;

            if (_node is JsonObject jo)
            {
                // JsonObject 支持 TryGetPropertyValue
                if (jo.TryGetPropertyValue(binder.Name, out var child))
                {
                    result = WrapNode(child);
                    return true;
                }

                // 兼容：尝试大小写不敏感查找
                foreach (var kv in jo)
                {
                    if (string.Equals(kv.Key, binder.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        result = WrapNode(kv.Value);
                        return true;
                    }
                }

                // 未找到属性，返回 null（作为成功）
                result = null;
                return true;
            }

            // 若不是 JsonObject，则无法通过属性名获取，返回 null
            result = null;
            return true;
        }

        /// <summary>
        /// 尝试设置属性（点号赋值），仅当底层是 JsonObject 时有效。
        /// 支持将简单 CLR 值（string/int/bool/JsonNode 等）赋给字段。
        /// </summary>
        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            if (_node is not JsonObject jo) return false;
 
            try
            {
                if (value is JsonNode jn)
                {
                    jo[binder.Name] = jn;
                    return true;
                }
 
                // 常见原始类型短路处理
                if (value is string or char or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
                {
                    jo[binder.Name] = JsonValue.Create(value);
                    return true;
                }
 
                // IEnumerable（排除 string）：手动构建 JsonArray 更高效
                if (value is IEnumerable ie && value is not string)
                {
                    var ja = new JsonArray();
                    foreach (var item in ie)
                    {
                        if (item is JsonNode inNode) ja.Add(inNode);
                        else if (item is string or char or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
                            ja.Add(JsonValue.Create(item));
                        else
                        {
                            var node = JsonSerializer.SerializeToNode(item, _jsonOptions);
                            ja.Add(node);
                        }
                    }
                    jo[binder.Name] = ja;
                    return true;
                }
 
                // 其它 POCO：使用 SerializeToNode（比先 ToString 再 Parse 更好）
                jo[binder.Name] = JsonSerializer.SerializeToNode(value, _jsonOptions);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 支持通过索引访问数组或对象（例如 d[0] 或 d["name"]）。
        /// 对象的字符串索引与 TryGetMember 行为类似。
        /// </summary>
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
        {
            result = null;
            if (_node is null) return true;

            if (indexes.Length == 1)
            {
                var idx = indexes[0];
                if (idx is string s && _node is JsonObject jo)
                {
                    if (jo.TryGetPropertyValue(s, out var child))
                    {
                        result = WrapNode(child);
                        return true;
                    }

                    result = null;
                    return true;
                }

                if (idx is int i && _node is JsonArray ja)
                {
                    if (i >= 0 && i < ja.Count)
                    {
                        result = WrapNode(ja[i]);
                        return true;
                    }

                    result = null;
                    return true;
                }
            }

            return base.TryGetIndex(binder, indexes, out result);
        }

        /// <summary>
        /// 支持将动态对象转换为具体类型，例如 ((Person)d). 或者 (int)d.Value。
        /// 会尝试使用 JsonSerializer 将底层节点反序列化为目标类型。
        /// </summary>
        public override bool TryConvert(ConvertBinder binder, out object? result)
        {
            result = null;
            if (_node is null)
            {
                // 尝试转换 null 到值类型时返回失败
                if (binder.Type.IsValueType && Nullable.GetUnderlyingType(binder.Type) is null)
                    return false;
                return true;
            }
 
            try
            {
                // 对于常见原始类型，先尝试直接提取
                if (_node is JsonValue jv)
                {
                    // 使用泛型 GetValue<T> 获取目标类型值
                    var method = typeof(JsonValue).GetMethod("GetValue", Type.EmptyTypes)?.MakeGenericMethod(binder.Type);
                    if (method is not null)
                    {
                        try
                        {
                            result = method.Invoke(jv, null);
                            return true;
                        }
                        catch
                        {
                            // 继续走 JsonSerializer 路径
                        }
                    }
                }
 
                // 通用方式：序列化为字符串后用 JsonSerializer 反序列化为目标类型
                var json = _node.ToJsonString();
                if (json is null) return false;
                result = JsonSerializer.Deserialize(json, binder.Type);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }
 
        /// <summary>
        /// 返回底层 JsonNode（可能为 null）。
        /// </summary>
        public JsonNode? ToJsonNode() => _node;
 
        /// <summary>
        /// 将底层 JsonNode 序列化为 JSON 字符串（同步）。
        /// </summary>
        public string? ToJsonString()
        {
            return _node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }
 
        /// <summary>
        /// 异步将底层 JsonNode 序列化为 JSON 字符串。
        /// </summary>
        public async Task<string?> ToJsonStringAsync(CancellationToken cancellationToken = default)
        {
            if (_node is null) return null;
            using var ms = new MemoryStream();
            // 指定类型为实际节点类型以避免歧义
            await JsonSerializer.SerializeAsync(ms, _node, _node.GetType(), _jsonOptions, cancellationToken).ConfigureAwait(false);
            ms.Position = 0;
            using var sr = new StreamReader(ms);
            return await sr.ReadToEndAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 将 JsonNode 包装为合适的对象：如果是对象/数组返回 DynamicJson，如果是值返回 CLR 值（或 null）。
        /// </summary>
        private static object? WrapNode(JsonNode? node)
        {
            if (node is null) return null;
            if (node is JsonObject or JsonArray) return new DynamicJson(node);
            if (node is JsonValue jv)
            {
                try
                {
                    // 试获取常见类型，降级到 string
                    return jv.GetValue<object>();
                }
                catch
                {
                    return jv.ToJsonString();
                }
            }

            return node.ToJsonString();
        }
    }

    /// <summary>
    /// JsonNode / string 的扩展方法，便于直接获取 dynamic 对象。
    /// </summary>
    public static class DynamicJsonExtensions
    {
        /// <summary>
        /// 将 JsonNode 包装为动态对象，便于使用点号访问属性。
        /// 返回 null 表示输入为 null。
        /// </summary>
        public static dynamic? AsDynamic(this JsonNode? node)
        {
            return DynamicJson.From(node);
        }

        /// <summary>
        /// 将 JSON 字符串解析后包装为动态对象（等于 JsonNode.Parse 后 AsDynamic）。
        /// 返回 null 表示输入为空或解析失败。
        /// </summary>
        public static dynamic? ToDynamicObject(this string? json)
        {
            var node = JsonNode.Parse(json ?? string.Empty);
            return DynamicJson.From(node);
        }
    }
}
