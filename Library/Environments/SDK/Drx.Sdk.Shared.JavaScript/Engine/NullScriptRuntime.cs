using System;
using System.Threading.Tasks;
using Drx.Sdk.Shared.JavaScript.Abstractions;

namespace Drx.Sdk.Shared.JavaScript.Engine
{
    /// <summary>
    /// 空运行时降级实现，当 ClearScript 不可用时作为替代。
    /// AddHostObject/AddHostType 静默忽略；任何脚本执行调用均抛出 InvalidOperationException。
    /// 不依赖 ClearScript，可在测试或受限环境中安全加载。
    /// 依赖：仅 IScriptEngineRuntime。
    /// </summary>
    internal sealed class NullScriptRuntime : IScriptEngineRuntime
    {
        /// <inheritdoc/>
        public void AddHostObject(string name, object? value) { }

        /// <inheritdoc/>
        public void AddHostType(string name, Type type) { }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">脚本引擎运行时不可用。</exception>
        public object? Evaluate(string script)
            => throw new InvalidOperationException("脚本引擎运行时不可用。");

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">脚本引擎运行时不可用。</exception>
        public void Execute(string script)
            => throw new InvalidOperationException("脚本引擎运行时不可用。");

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">脚本引擎运行时不可用。</exception>
        public Task<object?> EvaluateAsync(string script)
            => throw new InvalidOperationException("脚本引擎运行时不可用。");

        /// <inheritdoc/>
        public void Dispose() { }
    }
}
