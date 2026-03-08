using System;

namespace Drx.Sdk.Shared.JavaScript.Abstractions
{
    /// <summary>
    /// .NET 与 JavaScript 值之间的类型转换抽象。
    /// 支持通过 RegisterStrategy 注册可插拔转换策略。
    /// 依赖：ITypeConversionStrategy（策略接口）。
    /// </summary>
    public interface ITypeConverter
    {
        /// <summary>将 .NET 对象转换为可传入脚本引擎的 JS 兼容值。</summary>
        /// <param name="value">源 .NET 对象，可为 null。</param>
        /// <returns>转换后的 JS 兼容值。</returns>
        object? ToJsValue(object? value);

        /// <summary>将脚本引擎返回的 JS 值转换为指定的 .NET 类型。</summary>
        /// <param name="jsValue">从脚本引擎获取的原始值，可为 null。</param>
        /// <param name="targetType">目标 .NET 类型。</param>
        /// <returns>转换后的 .NET 对象；无法转换时返回 null。</returns>
        object? FromJsValue(object? jsValue, Type targetType);

        /// <summary>将脚本引擎返回的 JS 值转换为泛型指定的 .NET 类型。</summary>
        /// <typeparam name="T">目标 .NET 类型。</typeparam>
        /// <param name="jsValue">从脚本引擎获取的原始值，可为 null。</param>
        /// <returns>转换后的 .NET 对象；无法转换时返回 default。</returns>
        T? FromJsValue<T>(object? jsValue);

        /// <summary>向转换管道注册自定义转换策略。高优先级策略优先匹配。</summary>
        /// <param name="strategy">要注册的转换策略实例。</param>
        void RegisterStrategy(ITypeConversionStrategy strategy);
    }
}
