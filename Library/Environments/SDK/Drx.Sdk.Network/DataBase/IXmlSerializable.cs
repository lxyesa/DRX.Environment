using System;

namespace Drx.Sdk.Network.DataBase
{
    /// <summary>
    /// 定义可以序列化为XML的对象的接口
    /// </summary>
    public interface IXmlSerializable
    {
        /// <summary>
        /// 将对象写入XML节点
        /// </summary>
        /// <param name="node">目标XML节点</param>
        void WriteToXml(IXmlNode node);

        /// <summary>
        /// 从XML节点读取对象数据
        /// </summary>
        /// <param name="node">源XML节点</param>
        void ReadFromXml(IXmlNode node);
    }
} 