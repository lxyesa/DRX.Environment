using System;
using System.Collections.Generic;
using Drx.Sdk.Shared.JavaScript.Abstractions;

namespace Drx.Sdk.Shared.JavaScript.Conversion
{
    /// <summary>
    /// .NET 与 JavaScript 值之间的类型转换器核心调度实现。
    /// 采用 partial class 拆分为三个文件：
    ///   TypeConverter.cs（本文件）— 策略管道与接口调度；
    ///   TypeConverter.ToJs.cs — .NET → JS 转换逻辑；
    ///   TypeConverter.FromJs.cs — JS → .NET 转换逻辑。
    /// 依赖：ITypeConverter, ITypeConversionStrategy（Abstractions 命名空间）。
    /// </summary>
    public sealed partial class TypeConverter : ITypeConverter
    {
        private readonly List<ITypeConversionStrategy> _strategies = new();

        /// <inheritdoc/>
        public void RegisterStrategy(ITypeConversionStrategy strategy)
        {
            if (strategy == null) throw new ArgumentNullException(nameof(strategy));
            _strategies.Add(strategy);
            // 按优先级降序排列，Priority 越高越先匹配
            _strategies.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        /// <inheritdoc/>
        public object? ToJsValue(object? value) => ConvertToJs(value);

        /// <inheritdoc/>
        public object? FromJsValue(object? jsValue, Type targetType) => ConvertFromJs(jsValue, targetType);

        /// <inheritdoc/>
        public T? FromJsValue<T>(object? jsValue) => (T?)FromJsValue(jsValue, typeof(T));

        /// <summary>
        /// 尝试通过已注册的自定义策略进行转换。
        /// 按优先级依次检查，返回第一个匹配策略的结果；无匹配时返回 <see langword="null"/>。
        /// </summary>
        /// <param name="value">源值，可为 null。</param>
        /// <param name="targetType">目标 .NET 类型。</param>
        /// <returns>策略转换结果，或 null（无匹配策略时）。</returns>
        private (bool matched, object? result) TryCustomStrategy(object? value, Type targetType)
        {
            var sourceType = value?.GetType() ?? typeof(object);
            foreach (var strategy in _strategies)
            {
                if (strategy.CanConvert(sourceType, targetType))
                    return (true, strategy.Convert(value, targetType));
            }
            return (false, null);
        }
    }
}
