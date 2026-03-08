using System;
using System.IO;
using System.Threading.Tasks;

namespace Drx.Sdk.Shared.JavaScript
{
    /// <summary>
    /// JavaScript 静态门面执行能力（Execute 系列）。
    /// </summary>
    public static partial class JavaScript
    {
        /// <summary>
        /// 执行 JS 脚本（字符串）。
        /// </summary>
        public static object? Execute(string scriptString)
        {
            return Engine.Execute(scriptString);
        }

        /// <summary>
        /// 异步执行 JS 脚本（字符串）。
        /// </summary>
        public static async Task<bool> ExecuteAsync(string scriptString)
        {
            await Engine.ExecuteAsync(scriptString).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// 泛型异步执行。
        /// </summary>
        public static async Task<T> ExecuteAsync<T>(string scriptString)
        {
            return await Engine.ExecuteAsync<T>(scriptString).ConfigureAwait(false);
        }

        /// <summary>
        /// 执行 JS 脚本（文件）。
        /// </summary>
        public static object? ExecuteFile(string filePath)
        {
            EnsureScriptFilePath(filePath);
            return Engine.ExecuteFile(filePath);
        }

        /// <summary>
        /// 泛型执行脚本文件。
        /// </summary>
        public static T ExecuteFile<T>(string filePath)
        {
            EnsureScriptFilePath(filePath);
            return Engine.ExecuteFile<T>(filePath);
        }

        private static void EnsureScriptFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("脚本文件路径不能为空", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("找不到指定的脚本文件", filePath);
        }
    }
}
