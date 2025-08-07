using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ClearScript.V8;

namespace Drx.Sdk.Shared.JavaScript
{
    /// <summary>
    /// ClearScript运行库封装器，负责引擎生命周期、类型注册、脚本执行、全局变量与类型绑定。
    /// </summary>
    public class ClearScriptEngineWrapper : IDisposable
    {
        private V8ScriptEngine? _engine;
        private V8ScriptEngine? _context;
        private bool _disposed;

        public ClearScriptEngineWrapper()
        {
            try
            {
                _engine = new V8ScriptEngine();
                _context = _engine;
            }
            catch (Exception)
            {
                Console.Error.WriteLine("[ClearScript] 未检测到 Microsoft.ClearScript.V8.V8ScriptEngine，已启用降级兼容模式。请参考 https://github.com/microsoft/ClearScript 下载并安装运行库。");
                throw new InvalidOperationException("ClearScript 运行库未安装，已启用降级兼容模式。");
            }
            if (_engine == null)
                throw new InvalidOperationException("无法创建 Microsoft.ClearScript.V8.V8ScriptEngine 实例");
            if (_context == null)
                throw new InvalidOperationException("无法创建 Microsoft.ClearScript.V8.V8ScriptEngine 实例");
        }

        /// <summary>
        /// 注册全局变量
        /// </summary>
        public virtual void RegisterGlobal(string name, object? value)
        {
            if (_context == null) throw new ObjectDisposedException(nameof(_context));
            _context.AddHostObject(name, value);
        }

        /// <summary>
        /// 注册类型到JS运行时
        /// </summary>
        public virtual void RegisterType(ScriptTypeMetadata meta)
        {
            if (_context == null) throw new ObjectDisposedException(nameof(_context));
            // ClearScript支持类型导出
            _context.AddHostType(meta.ExportName, meta.Type);
        }

        /// <summary>
        /// 执行JS脚本（字符串）
        /// </summary>
        public virtual object Execute(string script, int maxRetry = 1)
        {
            var context = new ScriptExecutionContext
            {
                Script = script,
                StartTime = DateTime.Now,
                RetryCount = 0,
                Caller = nameof(Execute)
            };
            int retry = 0;
            Exception? lastEx = null;
            while (retry < maxRetry)
            {
                try
                {
                    if (_context == null) throw new ObjectDisposedException(nameof(_context));
                    _context.Execute(script);
                    context.Duration = DateTime.Now - context.StartTime;
                    return null;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    context.RetryCount = retry + 1;
                    if (retry == maxRetry - 1)
                        throw ConvertException(ex, context);
                }
                retry++;
            }
            throw ConvertException(lastEx ?? new Exception("未知异常"), context);
        }

        /// <summary>
        /// 执行JS脚本（文件）
        /// </summary>
        public virtual object ExecuteFile(string filePath, int maxRetry = 1)
        {
            var script = System.IO.File.ReadAllText(filePath);
            var context = new ScriptExecutionContext
            {
                Script = script,
                FilePath = filePath,
                StartTime = DateTime.Now,
                RetryCount = 0,
                Caller = nameof(ExecuteFile)
            };
            int retry = 0;
            Exception? lastEx = null;
            while (retry < maxRetry)
            {
                try
                {
                    if (_context == null) throw new ObjectDisposedException(nameof(_context));
                    _context.Execute(System.IO.File.ReadAllText(filePath));
                    context.Duration = DateTime.Now - context.StartTime;
                    return null;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    context.RetryCount = retry + 1;
                    if (retry == maxRetry - 1)
                        throw ConvertException(ex, context);
                }
                retry++;
            }
            throw ConvertException(lastEx ?? new Exception("未知异常"), context);
        }

        /// <summary>
        /// 异步执行JS脚本
        /// </summary>
        public virtual async Task<object> ExecuteAsync(string script, int maxRetry = 1)
        {
            var context = new ScriptExecutionContext
            {
                Script = script,
                StartTime = DateTime.Now,
                RetryCount = 0,
                Caller = nameof(ExecuteAsync)
            };
            int retry = 0;
            Exception? lastEx = null;
            while (retry < maxRetry)
            {
                try
                {
                    if (_context == null) throw new ObjectDisposedException(nameof(_context));
                    await Task.Run(() => _context.Execute(script));
                    context.Duration = DateTime.Now - context.StartTime;
                    return null;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    context.RetryCount = retry + 1;
                    if (retry == maxRetry - 1)
                        throw ConvertException(ex, context);
                }
                retry++;
            }
            throw ConvertException(lastEx ?? new Exception("未知异常"), context);
        }

        /// <summary>
        /// 类型转换与异常转换扩展点
        /// </summary>
        private Exception ConvertException(Exception? ex)
        {
            return ConvertException(ex, null);
        }

        private Exception ConvertException(Exception? ex, ScriptExecutionContext? context)
        {
            if (ex == null)
                return new JavaScriptException("未知异常", null, null, null, context);
            // 可扩展为JS->.NET异常映射
            string errorType = ex.GetType().Name;
            string errorLocation = ex.TargetSite?.ToString() ?? string.Empty;
            string scriptStack = ex.StackTrace ?? string.Empty;
            return new JavaScriptException(
                ex.Message,
                errorType,
                errorLocation,
                scriptStack,
                context,
                ex
            );
        }
    
        /// <summary>
        /// MockClearScriptEngine：ClearScript缺失时的降级兼容实现
        /// </summary>
        public class MockClearScriptEngine : ClearScriptEngineWrapper
        {
            public override void RegisterGlobal(string name, object? value)
            {
                Console.Error.WriteLine($"[ClearScript][Mock] RegisterGlobal({name}) 被调用，当前为降级模式，未生效。");
            }
    
            public override void RegisterType(ScriptTypeMetadata meta)
            {
                Console.Error.WriteLine($"[ClearScript][Mock] RegisterType({meta}) 被调用，当前为降级模式，未生效。");
            }
    
            public override object Execute(string script, int maxRetry = 1)
            {
                throw new InvalidOperationException("ClearScript 运行库未安装，无法执行脚本。请安装 ClearScript 或参考文档进行配置。");
            }
    
            public override object ExecuteFile(string filePath, int maxRetry = 1)
            {
                throw new InvalidOperationException("ClearScript 运行库未安装，无法执行脚本文件。请安装 ClearScript 或参考文档进行配置。");
            }
    
            public override Task<object> ExecuteAsync(string script, int maxRetry = 1)
            {
                throw new InvalidOperationException("ClearScript 运行库未安装，无法异步执行脚本。请安装 ClearScript 或参考文档进行配置。");
            }
    
            public override void Dispose()
            {
                // 无需释放资源
            }
        }

        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _context?.Dispose();
            _engine?.Dispose();
        }
    }

    /// <summary>
    /// JavaScript运行时异常
    /// </summary>
    public class JavaScriptEngineException : Exception
    {
        public JavaScriptEngineException(string message, Exception inner) : base(message, inner) { }
    }
}