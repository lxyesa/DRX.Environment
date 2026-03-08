using System;
using System.Threading.Tasks;

namespace Drx.Sdk.Shared.JavaScript.Abstractions
{
    /// <summary>
    /// 脚本引擎运行时抽象，隔离具体 V8/Jint 等实现。
    /// 负责底层脚本求值、宿主对象注册和生命周期管理。
    /// 依赖：无（底层接口）。
    /// </summary>
    public interface IScriptEngineRuntime : IDisposable
    {
        /// <summary>将宿主对象以指定名称注入脚本环境。</summary>
        /// <param name="name">脚本中可见的全局名称。</param>
        /// <param name="value">宿主对象实例，可为 null。</param>
        void AddHostObject(string name, object? value);

        /// <summary>将宿主类型以指定名称注入脚本环境，使脚本可通过 new 调用。</summary>
        /// <param name="name">脚本中可见的类型名称。</param>
        /// <param name="type">要暴露的 .NET 类型。</param>
        void AddHostType(string name, Type type);

        /// <summary>求值脚本并返回最后一个表达式的结果。</summary>
        /// <param name="script">要执行的脚本字符串。</param>
        /// <returns>脚本最后表达式的值，无结果时为 null。</returns>
        object? Evaluate(string script);

        /// <summary>执行脚本，不关心返回值。</summary>
        /// <param name="script">要执行的脚本字符串。</param>
        void Execute(string script);

        /// <summary>异步求值脚本并返回最后一个表达式的结果。</summary>
        /// <param name="script">要执行的脚本字符串。</param>
        /// <returns>脚本最后表达式的值，无结果时为 null。</returns>
        Task<object?> EvaluateAsync(string script);
    }
}
