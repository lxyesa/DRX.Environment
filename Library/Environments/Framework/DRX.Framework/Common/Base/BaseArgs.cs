using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DRX.Framework.Common.Base
{
    /// <summary>
    /// 基础参数类，支持转换为 JSON 字符串或字节数组，并允许继承类覆盖方法以实现自定义序列化。
    /// 使用高性能的属性访问器优化序列化和反序列化过程。
    /// 此类本身不包含任何属性，所有可序列化的属性应由子类实现。
    /// </summary>
    public class BaseArgs<TArgs> : EventArgs, IDisposable where TArgs : BaseArgs<TArgs>, new()
    {
        /// <summary>
        /// 释放资源。
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 将参数打包为字节数组。
        /// </summary>
        /// <returns>字节数组。</returns>
        public virtual byte[] Pack()
        {
            string jsonString = ToJson();
            return Encoding.UTF8.GetBytes(jsonString);
        }

        /// <summary>
        /// 从字节数组解包创建 <see cref="BaseArgs{TArgs}"/> 实例。
        /// </summary>
        /// <param name="data">字节数组。</param>
        /// <returns>新的 <see cref="BaseArgs{TArgs}"/> 实例。</returns>
        public static TArgs Unpack(byte[] data)
        {
            string jsonString = Encoding.UTF8.GetString(data);
            return FromJson(jsonString);
        }

        /// <summary>
        /// 从字符串解包创建 <see cref="BaseArgs{TArgs}"/> 实例。
        /// </summary>
        /// <param name="jsonString">JSON 字符串。</param>
        /// <returns>新的 <see cref="BaseArgs{TArgs}"/> 实例。</returns>
        public static TArgs Unpack(string jsonString)
        {
            return FromJson(jsonString);
        }

        /// <summary>
        /// 将参数转换为 JSON 字符串。
        /// </summary>
        /// <returns>JSON 字符串。</returns>
        public virtual string ToJson()
        {
            var jsonObject = ToJsonObject();
            return jsonObject.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }

        /// <summary>
        /// 将参数转换为 JSON 对象。允许子类覆盖以添加自定义字段。
        /// </summary>
        /// <returns>JSON 对象。</returns>
        protected virtual JsonObject ToJsonObject()
        {
            var jsonObject = new JsonObject();
            var accessors = PropertyAccessorCache<TArgs>.Accessors;

            foreach (var accessor in accessors)
            {
                var value = accessor.Getter((TArgs)this);
                if (value != null)
                {
                    jsonObject.Add(accessor.Name, JsonValue.Create(value));
                }
            }

            return jsonObject;
        }

        /// <summary>
        /// 从 JSON 字符串创建 <see cref="BaseArgs{TArgs}"/> 实例。
        /// </summary>
        /// <param name="json">JSON 字符串。</param>
        /// <returns>新的 <see cref="BaseArgs{TArgs}"/> 实例。</returns>
        public static TArgs FromJson(string json)
        {
            var jsonObject = JsonNode.Parse(json)?.AsObject()
                ?? throw new ArgumentException("无效的 JSON 格式", nameof(json));
            return FromJsonObject(jsonObject);
        }

        /// <summary>
        /// 从 JSON 对象创建 <see cref="BaseArgs{TArgs}"/> 实例。允许子类覆盖以实现自定义反序列化。
        /// </summary>
        /// <param name="jsonObject">JSON 对象。</param>
        /// <returns>新的 <see cref="BaseArgs{TArgs}"/> 实例。</returns>
        protected static TArgs FromJsonObject(JsonObject jsonObject)
        {
            var args = new TArgs();
            var accessors = PropertyAccessorCache<TArgs>.Accessors;

            foreach (var accessor in accessors)
            {
                if (jsonObject.TryGetPropertyValue(accessor.Name, out var value) && value != null)
                {
                    try
                    {
                        var deserializedValue = value.Deserialize(accessor.PropertyType, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            WriteIndented = false
                        });
                        accessor.Setter(args, deserializedValue);
                    }
                    catch (InvalidCastException)
                    {
                        // 忽略类型转换失败
                    }
                }
            }

            return args;
        }

        /// <summary>
        /// 静态泛型类，用于缓存每个类型的属性访问器。
        /// </summary>
        /// <typeparam name="TArgs">参数类型。</typeparam>
        private static class PropertyAccessorCache<TArgs> where TArgs : BaseArgs<TArgs>, new()
        {
            public static readonly PropertyAccessor[] Accessors;

            static PropertyAccessorCache()
            {
                var properties = typeof(TArgs).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                            .Where(p => p.CanRead && p.CanWrite)
                                            .ToArray();

                Accessors = properties.Select(p => new PropertyAccessor
                {
                    Name = p.Name,
                    PropertyType = p.PropertyType,
                    Getter = CreateGetter(p),
                    Setter = CreateSetter(p)
                }).ToArray();
            }

            /// <summary>
            /// 创建属性的 getter 委托。
            /// </summary>
            /// <param name="propertyInfo">属性信息。</param>
            /// <returns>getter 委托。</returns>
            private static Func<TArgs, object?> CreateGetter(PropertyInfo propertyInfo)
            {
                var instance = Expression.Parameter(typeof(TArgs), "instance");
                var property = Expression.Property(instance, propertyInfo);
                var convert = Expression.Convert(property, typeof(object));
                return Expression.Lambda<Func<TArgs, object?>>(convert, instance).Compile();
            }

            /// <summary>
            /// 创建属性的 setter 委托。
            /// </summary>
            /// <param name="propertyInfo">属性信息。</param>
            /// <returns>setter 委托。</returns>
            private static Action<TArgs, object?> CreateSetter(PropertyInfo propertyInfo)
            {
                var instance = Expression.Parameter(typeof(TArgs), "instance");
                var value = Expression.Parameter(typeof(object), "value");
                var convert = Expression.Convert(value, propertyInfo.PropertyType);
                var property = Expression.Property(instance, propertyInfo);
                var assign = Expression.Assign(property, convert);
                return Expression.Lambda<Action<TArgs, object?>>(assign, instance, value).Compile();
            }

            /// <summary>
            /// 属性访问器，用于快速访问属性的 getter 和 setter。
            /// </summary>
            public class PropertyAccessor
            {
                public string Name { get; set; } = default!;
                public Type PropertyType { get; set; } = default!;
                public Func<TArgs, object?> Getter { get; set; } = default!;
                public Action<TArgs, object?> Setter { get; set; } = default!;
            }
        }
    }
}
