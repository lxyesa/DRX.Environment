using System;
using System.Collections;
using System.Collections.Generic;

namespace Drx.Sdk.Shared.JavaScript.Conversion
{
    public sealed partial class TypeConverter
    {
        /// <summary>
        /// 将 .NET 对象转换为 JS 引擎可接受的值。
        /// 自定义策略优先；回退到内置规则（基元、字符串、集合、字典、自定义对象属性映射）。
        /// </summary>
        /// <param name="value">源 .NET 对象，可为 null。</param>
        /// <returns>JS 兼容值，null 输入返回 null。</returns>
        private object? ConvertToJs(object? value)
        {
            if (value == null) return null;

            var type = value.GetType();

            // 1. 先检查自定义策略（允许覆盖内置行为）
            var (matched, strategyResult) = TryCustomStrategy(value, type);
            if (matched) return strategyResult;

            // 2. 基元类型、string、decimal 直接透传
            if (type.IsPrimitive || value is string || value is decimal)
                return value;

            // 3. 字典 → Dictionary<string, object?>
            if (value is IDictionary dict)
            {
                var jsObj = new Dictionary<string, object?>();
                foreach (DictionaryEntry entry in dict)
                    jsObj[entry.Key.ToString() ?? string.Empty] = ConvertToJs(entry.Value);
                return jsObj;
            }

            // 4. 非字符串可枚举 → List<object?>
            if (value is IEnumerable enumerable)
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                    list.Add(ConvertToJs(item));
                return list;
            }

            // 5. 自定义对象 → 属性字典（仅公开可读属性）
            var props = type.GetProperties();
            var jsCustom = new Dictionary<string, object?>(props.Length);
            foreach (var prop in props)
            {
                if (prop.CanRead)
                    jsCustom[prop.Name] = ConvertToJs(prop.GetValue(value));
            }
            return jsCustom;
        }
    }
}
