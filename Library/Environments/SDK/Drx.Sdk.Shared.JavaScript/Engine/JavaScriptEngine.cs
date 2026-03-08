using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Microsoft.Extensions.Options;

namespace Drx.Sdk.Shared.JavaScript.Engine
{
    /// <summary>
    /// JavaScript 引擎核心实现（初始化与生命周期）。
    /// 依赖：IScriptEngineRuntime、IScriptBinder、IScriptRegistry、ITypeConverter、IOptions&lt;JavaScriptEngineOptions&gt;。
    /// </summary>
    public sealed partial class JavaScriptEngine : IJavaScriptEngine
    {
        private readonly IScriptEngineRuntime _runtime;
        private readonly IScriptBinder _binder;
        private readonly IScriptRegistry _registry;
        private readonly IScriptTypeScanner _scanner;
        private readonly ITypeConverter _converter;
        private readonly JavaScriptEngineOptions _options;

        private readonly ConcurrentDictionary<string, Type> _globalsCatalog = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, string> _scriptFileCache = new(StringComparer.OrdinalIgnoreCase);

        private bool _disposed;

        /// <summary>
        /// 初始化 <see cref="JavaScriptEngine"/>。
        /// </summary>
        public JavaScriptEngine(
            IScriptEngineRuntime runtime,
            IScriptBinder binder,
            IScriptRegistry registry,
            IScriptTypeScanner scanner,
            ITypeConverter converter,
            IOptions<JavaScriptEngineOptions> options)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _binder = binder ?? throw new ArgumentNullException(nameof(binder));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

            ScanAndRegisterExportedTypes();
            RegisterAllExportedTypes();
            ConfigureConverterIfNeeded();
            LoadPlugins();
        }

        private void ScanAndRegisterExportedTypes()
        {
            var scanAssemblies = _options.ScanAssemblies;
            var scanned = scanAssemblies is { Length: > 0 }
                ? _scanner.Scan(scanAssemblies)
                : _scanner.Scan();

            foreach (var metadata in scanned)
            {
                _registry.RegisterType(metadata);
            }
        }

        /// <summary>
        /// 将注册表中的导出类型全部绑定到脚本运行时。
        /// </summary>
        private void RegisterAllExportedTypes()
        {
            foreach (var metadata in _registry.GetAllExportedTypes())
            {
                _binder.BindType(_runtime, metadata);
            }
        }

        private void ConfigureConverterIfNeeded()
        {
            _options.ConfigureConverter?.Invoke(_converter);
        }

        private void LoadPlugins()
        {
            foreach (var plugin in _options.Plugins)
            {
                plugin.Initialize(this);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(JavaScriptEngine));
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetRegisteredClasses()
        {
            ThrowIfDisposed();
            return _registry.GetAllExportedTypes()
                .Select(meta => meta.ExportName)
                .Distinct(StringComparer.Ordinal)
                .ToList()
                .AsReadOnly();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var plugin in _options.Plugins)
            {
                plugin.Dispose();
            }

            _runtime.Dispose();
            _disposed = true;
        }
    }
}
