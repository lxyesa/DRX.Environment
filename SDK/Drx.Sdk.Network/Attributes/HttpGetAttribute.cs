using System;

namespace Drx.Sdk.Network.Attributes
{
    /// <summary>
    /// 标记一个方法为HTTP GET请求的特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class HttpGetAttribute : Attribute
    {
        /// <summary>
        /// 获取GET请求的路径
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 初始化HttpGetAttribute的新实例
        /// </summary>
        /// <param name="path">GET请求的路径</param>
        public HttpGetAttribute(string path)
        {
            Path = path;
        }
    }
}
