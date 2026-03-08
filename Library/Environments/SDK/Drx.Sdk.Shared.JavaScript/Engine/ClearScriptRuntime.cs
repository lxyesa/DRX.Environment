using System;
using System.Threading.Tasks;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Microsoft.ClearScript.V8;

namespace Drx.Sdk.Shared.JavaScript.Engine
{
    /// <summary>
    /// ClearScript V8 运行时实现，是整个项目中唯一引用 Microsoft.ClearScript 的类。
    /// 合并了原 ClearScriptEngineWrapper 中的冗余双字段（_engine/_context）为单一 _engine 字段。
    /// 依赖：Microsoft.ClearScript.V8；实现 IScriptEngineRuntime。
    /// </summary>
    internal sealed class ClearScriptRuntime : IScriptEngineRuntime
    {
        /// <summary>单一 V8 引擎字段，消除原 _engine/_context 冗余。</summary>
        private V8ScriptEngine? _engine;
        private bool _disposed;

        /// <summary>
        /// 初始化 V8 引擎实例。若 ClearScript 运行库未安装则抛出 InvalidOperationException。
        /// </summary>
        public ClearScriptRuntime()
        {
            try
            {
                _engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableTaskPromiseConversion);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "[ClearScriptRuntime] 未检测到 Microsoft.ClearScript.V8.V8ScriptEngine，已启用降级兼容模式。" +
                    "请参考 https://github.com/microsoft/ClearScript 下载并安装运行库。");
                throw new InvalidOperationException(
                    "ClearScript 运行库未安装，无法创建 V8ScriptEngine 实例。", ex);
            }
        }

        /// <inheritdoc/>
        public void AddHostObject(string name, object? value)
        {
            EnsureNotDisposed();
            _engine!.AddHostObject(name, value);
        }

        /// <inheritdoc/>
        public void AddHostType(string name, Type type)
        {
            EnsureNotDisposed();
            _engine!.AddHostType(name, type);
        }

        /// <inheritdoc/>
        /// <remarks>使用 V8ScriptEngine.Evaluate 以正确返回脚本最后一个表达式的值。</remarks>
        public object? Evaluate(string script)
        {
            EnsureNotDisposed();
            return _engine!.Evaluate(script);
        }

        /// <inheritdoc/>
        public void Execute(string script)
        {
            EnsureNotDisposed();
            _engine!.Execute(script);
        }

        /// <inheritdoc/>
        public Task<object?> EvaluateAsync(string script)
        {
            EnsureNotDisposed();
            return Task.Run<object?>(() => _engine!.Evaluate(script));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _engine?.Dispose();
            _engine = null;
            _disposed = true;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ClearScriptRuntime));
        }
    }
}
