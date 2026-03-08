namespace Drx.Sdk.Shared.JavaScript
{
    /// <summary>
    /// JavaScript 静态门面函数调用能力（Functions）。
    /// </summary>
    public static partial class JavaScript
    {
        /// <summary>
        /// 调用 JS 文件中的函数：CallFunction("function", filePath, params object[] args)。
        /// </summary>
        public static object? CallFunction(string functionName, string filePath, params object[] args)
            => Engine.CallFunction(functionName, filePath, args);

        /// <summary>
        /// 调用 JS 文件中的函数（泛型返回）。
        /// </summary>
        public static T CallFunction<T>(string functionName, string filePath, params object[] args)
            => Engine.CallFunction<T>(functionName, filePath, args);
    }
}
