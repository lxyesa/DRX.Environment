using System;
using System.Xml;
using System.IO;

namespace Drx.Sdk.Network.DataBase
{
    /// <summary>
    /// 表示对外部XML文件的引用
    /// </summary>
    public class XmlNodeReference : IXmlNodeReference
    {
        private XmlElement _element;
        private XmlDocument _document;
        private string _filePath;
        private XmlDatabase _database;
        private XmlNode _parent;
        private XmlNode _referencedNode;

        /// <summary>
        /// 获取引用路径
        /// </summary>
        public string ReferencePath => _element.GetAttribute("path");

        /// <summary>
        /// 内部构造函数，通过XmlNode创建
        /// </summary>
        internal XmlNodeReference(XmlElement element, XmlDocument document, string filePath, XmlDatabase database, XmlNode parent)
        {
            _element = element;
            _document = document;
            _filePath = filePath;
            _database = database;
            _parent = parent;
        }

        /// <summary>
        /// 解析引用并获取引用的节点
        /// </summary>
        /// <returns>引用的节点</returns>
        public IXmlNode ResolveReference()
        {
            if (_referencedNode != null)
                return _referencedNode;

            string path = ReferencePath;
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException("引用路径为空");

            // 如果是相对路径，则相对于当前文件的目录
            if (!Path.IsPathRooted(path))
            {
                string baseDirectory = Path.GetDirectoryName(_filePath);
                path = Path.Combine(baseDirectory, path);
            }

            // 检查文件是否存在
            if (!File.Exists(path))
                throw new FileNotFoundException($"找不到引用的XML文件: {path}");

            // 通过数据库加载引用的文件
            _referencedNode = _database.OpenRoot(path);
            return _referencedNode;
        }

        /// <summary>
        /// 更新引用路径
        /// </summary>
        /// <param name="newPath">新的引用路径</param>
        public void UpdatePath(string newPath)
        {
            _element.SetAttribute("path", newPath);
            _referencedNode = null; // 清除缓存的引用节点
        }
    }
} 