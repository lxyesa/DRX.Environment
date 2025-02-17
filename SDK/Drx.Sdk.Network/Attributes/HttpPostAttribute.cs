using System;

namespace Drx.Sdk.Network.Attributes
{
    /// <summary>
    /// 标记一个方法为HTTP POST请求的特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class HttpPostAttribute : Attribute
    {
        /// <summary>
        /// 获取POST请求的路径
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 初始化HttpPostAttribute的新实例
        /// </summary>
        /// <param name="path">POST请求的路径</param>
        public HttpPostAttribute(string path)
        {
            Path = path;
        }
    }
}
