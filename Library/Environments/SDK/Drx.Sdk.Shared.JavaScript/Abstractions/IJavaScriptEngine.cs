using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Drx.Sdk.Shared.JavaScript.Abstractions
{
    /// <summary>
    /// 公共 JavaScript 引擎接口。
    /// 提供脚本执行、文件执行、函数调用和全局对象注册等能力。
    /// 依赖：IScriptEngineRuntime（底层运行时）、ITypeConverter（类型转换）。
    /// </summary>
    public interface IJavaScriptEngine : IDisposable
    {
        // ── 执行 ──────────────────────────────────────────────────────────

        /// <summary>执行脚本并返回结果。</summary>
        /// <param name="script">JavaScript 脚本字符串。</param>
        /// <returns>脚本结果，无值时为 null。</returns>
        object? Execute(string script);

        /// <summary>执行脚本并将结果转换为指定类型。</summary>
        /// <typeparam name="T">目标返回类型。</typeparam>
        /// <param name="script">JavaScript 脚本字符串。</param>
        /// <returns>转换后的结果值。</returns>
        T Execute<T>(string script);

        /// <summary>异步执行脚本并返回结果。</summary>
        /// <param name="script">JavaScript 脚本字符串。</param>
        ValueTask<object?> ExecuteAsync(string script);

        /// <summary>异步执行脚本并将结果转换为指定类型。</summary>
        /// <typeparam name="T">目标返回类型。</typeparam>
        /// <param name="script">JavaScript 脚本字符串。</param>
        ValueTask<T> ExecuteAsync<T>(string script);

        // ── 文件执行 ──────────────────────────────────────────────────────

        /// <summary>读取并执行脚本文件，返回结果。</summary>
        /// <param name="filePath">脚本文件的绝对或相对路径。</param>
        object? ExecuteFile(string filePath);

        /// <summary>读取并执行脚本文件，将结果转换为指定类型。</summary>
        /// <typeparam name="T">目标返回类型。</typeparam>
        /// <param name="filePath">脚本文件的绝对或相对路径。</param>
        T ExecuteFile<T>(string filePath);

        // ── 函数调用 ──────────────────────────────────────────────────────

        /// <summary>从指定脚本文件调用命名函数并传入参数。</summary>
        /// <param name="functionName">脚本中的函数名。</param>
        /// <param name="filePath">包含该函数的脚本文件路径。</param>
        /// <param name="args">传递给函数的参数列表。</param>
        object? CallFunction(string functionName, string filePath, params object[] args);

        /// <summary>从指定脚本文件调用命名函数，将结果转换为指定类型。</summary>
        /// <typeparam name="T">目标返回类型。</typeparam>
        /// <param name="functionName">脚本中的函数名。</param>
        /// <param name="filePath">包含该函数的脚本文件路径。</param>
        /// <param name="args">传递给函数的参数列表。</param>
        T CallFunction<T>(string functionName, string filePath, params object[] args);

        // ── 注册 ──────────────────────────────────────────────────────────

        /// <summary>向脚本全局环境注册一个宿主对象。</summary>
        /// <param name="name">脚本中可见的名称。</param>
        /// <param name="value">要注册的对象实例。</param>
        void RegisterGlobal(string name, object value);

        /// <summary>向脚本全局环境注册一个委托方法。</summary>
        /// <param name="name">脚本中可见的函数名。</param>
        /// <param name="method">要公开的委托。</param>
        void RegisterGlobal(string name, Delegate method);

        /// <summary>向脚本全局环境注册一个宿主类型（可直接调用静态成员并用于构造）。</summary>
        /// <param name="name">脚本中可见的类型名。</param>
        /// <param name="type">要注册的 .NET 类型。</param>
        void RegisterHostType(string name, Type type);

        // ── 查询 ──────────────────────────────────────────────────────────

        /// <summary>获取所有已注册的全局对象及其 CLR 类型映射。</summary>
        IReadOnlyDictionary<string, Type> GetRegisteredGlobals();

        /// <summary>获取所有已注册的脚本可见类名称列表。</summary>
        IReadOnlyList<string> GetRegisteredClasses();
    }
}
