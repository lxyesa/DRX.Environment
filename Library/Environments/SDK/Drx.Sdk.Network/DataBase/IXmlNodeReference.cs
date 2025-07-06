using System;

namespace Drx.Sdk.Network.DataBase
{
    /// <summary>
    /// XML节点引用接口，定义对外部XML文件引用的基本操作
    /// </summary>
    public interface IXmlNodeReference
    {
        /// <summary>
        /// 获取引用路径
        /// </summary>
        string ReferencePath { get; }
        
        /// <summary>
        /// 解析引用并获取引用的节点
        /// </summary>
        /// <returns>引用的节点</returns>
        IXmlNode ResolveReference();
        
        /// <summary>
        /// 更新引用路径
        /// </summary>
        /// <param name="newPath">新的引用路径</param>
        void UpdatePath(string newPath);
    }
} 