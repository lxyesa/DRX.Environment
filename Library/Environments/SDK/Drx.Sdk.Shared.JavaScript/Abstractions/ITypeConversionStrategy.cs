using System;

namespace Drx.Sdk.Shared.JavaScript.Abstractions
{
    /// <summary>
    /// 可插拔类型转换策略接口。
    /// 通过 ITypeConverter.RegisterStrategy 注册，支持优先级排序。
    /// 依赖：无。
    /// </summary>
    public interface ITypeConversionStrategy
    {
        /// <summary>
        /// 策略优先级。值越高越先被尝试匹配。
        /// </summary>
        int Priority { get; }

        /// <summary>判断是否可处理从 sourceType 到 targetType 的转换。</summary>
        /// <param name="sourceType">源值的 .NET 类型。</param>
        /// <param name="targetType">目标 .NET 类型。</param>
        /// <returns>可处理时返回 true。</returns>
        bool CanConvert(Type sourceType, Type targetType);

        /// <summary>执行转换并返回结果。仅在 CanConvert 返回 true 时调用。</summary>
        /// <param name="value">源值，可为 null。</param>
        /// <param name="targetType">目标 .NET 类型。</param>
        /// <returns>转换后的值；无法转换时返回 null。</returns>
        object? Convert(object? value, Type targetType);
    }
}
