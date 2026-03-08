using System;
using System.Collections;
using System.Collections.Generic;

namespace Drx.Sdk.Shared.JavaScript.Conversion
{
    public sealed partial class TypeConverter
    {
        /// <summary>
        /// 将脚本引擎返回的 JS 值转换为指定 .NET 类型。
        /// 修复原实现 null 处理问题：
        ///   - jsValue 为 null 且 targetType 为引用类型 → 返回 null（原代码错误地调用 Activator.CreateInstance）；
        ///   - jsValue 为 null 且 targetType 为值类型 → 返回 default；
        ///   - jsValue 为 null 且 targetType 为 Nullable&lt;T&gt; → 返回 null。
        /// </summary>
        /// <param name="jsValue">从脚本引擎获取的原始值，可为 null。</param>
        /// <param name="targetType">目标 .NET 类型，不可为 null。</param>
        /// <returns>转换后的 .NET 对象；转换失败时返回 null 或 default。</returns>
        private object? ConvertFromJs(object? jsValue, Type targetType)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));

            // --- null 处理（修复原实现 bug）---
            if (jsValue == null)
            {
                // Nullable<T>：返回 null
                if (targetType.IsGenericType &&
                    targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    return null;

                // 值类型：返回 default(T)
                if (targetType.IsValueType)
                    return Activator.CreateInstance(targetType);

                // 引用类型：直接返回 null
                return null;
            }

            // 1. 先检查自定义策略
            var (matched, strategyResult) = TryCustomStrategy(jsValue, targetType);
            if (matched) return strategyResult;

            // 2. 目标类型已匹配（含子类）
            if (targetType.IsInstanceOfType(jsValue)) return jsValue;

            // 3. string
            if (targetType == typeof(string))
                return jsValue.ToString();

            // 4. 基元类型 / decimal
            if (targetType.IsPrimitive || targetType == typeof(decimal))
                return Convert.ChangeType(jsValue, targetType);

            // 5. Nullable<T> — 递归转换内层类型
            if (targetType.IsGenericType &&
                targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var inner = Nullable.GetUnderlyingType(targetType)!;
                return ConvertFromJs(jsValue, inner);
            }

            // 6. List<T>
            if (targetType.IsGenericType &&
                targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elemType = targetType.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(targetType)!;
                if (jsValue is IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                        list.Add(ConvertFromJs(item, elemType));
                }
                return list;
            }

            // 7. Dictionary<TKey, TValue>
            if (targetType.IsGenericType &&
                targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var keyType = targetType.GetGenericArguments()[0];
                var valueType = targetType.GetGenericArguments()[1];
                var dict = (IDictionary)Activator.CreateInstance(targetType)!;
                if (jsValue is IDictionary srcDict)
                {
                    foreach (DictionaryEntry kv in srcDict)
                    {
                        var key = Convert.ChangeType(kv.Key, keyType);
                        var val = ConvertFromJs(kv.Value, valueType);
                        dict.Add(key!, val!);
                    }
                }
                return dict;
            }

            // 8. 自定义对象：从 IDictionary<string, object?> 映射属性
            var obj = Activator.CreateInstance(targetType);
            if (jsValue is IDictionary<string, object?> jsDict)
            {
                foreach (var prop in targetType.GetProperties())
                {
                    if (prop.CanWrite && jsDict.TryGetValue(prop.Name, out var v))
                        prop.SetValue(obj, ConvertFromJs(v, prop.PropertyType));
                }
            }
            return obj;
        }
    }
}
