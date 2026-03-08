using System;
using System.Threading.Tasks;

namespace Drx.Sdk.Shared.JavaScript
{
    /// <summary>
    /// JavaScript 静态门面安全执行能力（TryExecute 系列）。
    /// </summary>
    public static partial class JavaScript
    {
        /// <summary>
        /// 安全异步执行 JS 脚本（字符串），捕获异常。
        /// </summary>
        public static async Task<(bool success, object result)> TryExecuteAsync(string scriptString)
        {
            try
            {
                var result = await Engine.ExecuteAsync<object>(scriptString).ConfigureAwait(false);
                return (true, result!);
            }
            catch (Exception ex)
            {
                return (false, $"[JavaScript.TryExecuteAsync] {ex.Message}");
            }
        }

        /// <summary>
        /// 安全执行 JS 脚本（文件），捕获异常。
        /// </summary>
        public static (bool success, object result) TryExecuteFile(string filePath)
        {
            try
            {
                var result = ExecuteFile(filePath);
                return (true, result!);
            }
            catch (Exception ex)
            {
                Logger.Error($"无法执行脚本文件 {filePath}：{ex}");
                return (false, $"[JavaScript.TryExecuteFile] {ex.Message}");
            }
        }

        /// <summary>
        /// 泛型安全执行 JS 脚本（字符串），捕获异常。
        /// </summary>
        public static (bool success, T result) TryExecute<T>(string scriptString)
            where T : notnull
        {
            try
            {
                object? result = Execute(scriptString);
                if (result is T t)
                    return (true, t);
                return (false, default!);
            }
            catch (Exception)
            {
                return (false, default!);
            }
        }

        /// <summary>
        /// 泛型安全异步执行 JS 脚本（字符串），捕获异常。
        /// </summary>
        public static async Task<(bool success, T result)> TryExecuteAsync<T>(string scriptString)
            where T : notnull
        {
            try
            {
                object? result = await Engine.ExecuteAsync<object>(scriptString).ConfigureAwait(false);
                if (result is T t)
                    return (true, t);
                return (false, default!);
            }
            catch (Exception)
            {
                return (false, default!);
            }
        }

        /// <summary>
        /// 泛型安全执行 JS 脚本（文件），捕获异常。
        /// </summary>
        public static (bool success, T result) TryExecuteFile<T>(string filePath)
            where T : notnull
        {
            try
            {
                object? result = ExecuteFile(filePath);
                if (result is T t)
                    return (true, t);
                return (false, default!);
            }
            catch (Exception)
            {
                return (false, default!);
            }
        }
    }
}
