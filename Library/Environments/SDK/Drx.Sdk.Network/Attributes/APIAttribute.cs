using System;

namespace Drx.Sdk.Network.Attributes
{
    /// <summary>
    /// 标记一个类作为API的基类，并定义其基础路径
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class APIAttribute : Attribute
    {
        /// <summary>
        /// 获取API的基础路径
        /// </summary>
        public string BasePath { get; }

        /// <summary>
        /// 初始化APIAttribute的新实例
        /// </summary>
        /// <param name="basePath">API的基础路径</param>
        public APIAttribute(string basePath)
        {
            BasePath = basePath;
        }
    }
}
