using System;
using System.Collections.Generic;

namespace Drx.Sdk.Shared.JavaScript.Engine
{
    /// <summary>
    /// JavaScript 引擎注册能力（全局对象与委托注册、注册查询）。
    /// </summary>
    public sealed partial class JavaScriptEngine
    {
        /// <inheritdoc/>
        public void RegisterGlobal(string name, object value)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("注册名不能为空。", nameof(name));

            _runtime.AddHostObject(name, value);
            _globalsCatalog[name] = value?.GetType() ?? typeof(object);
        }

        /// <inheritdoc/>
        public void RegisterGlobal(string name, Delegate method)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("注册名不能为空。", nameof(name));
            if (method == null) throw new ArgumentNullException(nameof(method));

            _runtime.AddHostObject(name, method);
            _globalsCatalog[name] = method.GetType();
        }

        /// <inheritdoc/>
        public void RegisterHostType(string name, Type type)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("注册名不能为空。", nameof(name));
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            _runtime.AddHostType(name, type);
            _globalsCatalog[name] = type;
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, Type> GetRegisteredGlobals()
        {
            ThrowIfDisposed();
            return new Dictionary<string, Type>(_globalsCatalog, StringComparer.Ordinal);
        }
    }
}
