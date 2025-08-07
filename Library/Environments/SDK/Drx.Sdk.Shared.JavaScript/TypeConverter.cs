using System;
using System.Collections;
using System.Collections.Generic;

namespace Drx.Sdk.Shared.JavaScript
{
    /// <summary>
    /// .NET对象与JavaScript对象类型转换器
    /// </summary>
    public static class TypeConverter
    {
        /// <summary>
        /// .NET对象转JS可用对象（递归支持常用类型、集合、字典、自定义对象）
        /// </summary>
        public static object? ToJsValue(object? value)
        {
            if (value == null) return null;
            var type = value.GetType();

            if (type.IsPrimitive || value is string || value is decimal)
                return value;

            if (value is IDictionary dict)
            {
                var jsObj = new Dictionary<string, object?>();
                foreach (DictionaryEntry entry in dict)
                    jsObj[entry.Key.ToString() ?? string.Empty] = ToJsValue(entry.Value);
                return jsObj;
            }

            if (value is IEnumerable enumerable && !(value is string))
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                    list.Add(ToJsValue(item));
                return list;
            }

            // 自定义对象转为属性字典
            var props = type.GetProperties();
            var jsCustom = new Dictionary<string, object?>();
            foreach (var prop in props)
            {
                if (prop.CanRead)
                    jsCustom[prop.Name] = ToJsValue(prop.GetValue(value));
            }
            return jsCustom;
        }

        /// <summary>
        /// JS对象转.NET对象（仅支持基础类型和字典，复杂对象需手动处理）
        /// </summary>
        public static object? FromJsValue(object? jsValue, Type targetType)
        {
            if (jsValue == null) 
            {
                return Activator.CreateInstance(targetType);
            }
            if (targetType.IsInstanceOfType(jsValue)) return jsValue;

            if (targetType == typeof(string))
                return jsValue.ToString();

            if (targetType.IsPrimitive || targetType == typeof(decimal))
                return Convert.ChangeType(jsValue, targetType);

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elemType = targetType.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(targetType)!;
                foreach (var item in (IEnumerable)jsValue)
                {
                    var val = FromJsValue(item, elemType);
                    list.Add(val!);
                }
                return list;
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var keyType = targetType.GetGenericArguments()[0];
                var valueType = targetType.GetGenericArguments()[1];
                var dict = (IDictionary)Activator.CreateInstance(targetType)!;
                foreach (var kv in (IDictionary)jsValue)
                {
                    var key = Convert.ChangeType(kv, keyType);
                    var val = FromJsValue(((IDictionary)jsValue)[kv], valueType);
                    dict.Add(key!, val!);
                }
                return dict;
            }

            // 简单对象映射
            var obj = Activator.CreateInstance(targetType);
            var props = targetType.GetProperties();
            if (jsValue is IDictionary<string, object?> jsDict)
            {
                foreach (var prop in props)
                {
                    if (prop.CanWrite && jsDict.TryGetValue(prop.Name, out var v))
                    {
                        var val = FromJsValue(v, prop.PropertyType);
                        prop.SetValue(obj, val);
                    }
                }
            }
            return obj;
        }
    }
}