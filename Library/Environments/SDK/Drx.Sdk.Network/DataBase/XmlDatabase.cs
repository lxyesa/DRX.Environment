using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;

namespace Drx.Sdk.Network.DataBase
{
    /// <summary>
    /// XML数据库，用于创建和管理XML文件
    /// </summary>
    public class XmlDatabase
    {
        private Dictionary<string, XmlNode> _rootNodes = new Dictionary<string, XmlNode>();
        private Dictionary<string, XmlDocument> _documents = new Dictionary<string, XmlDocument>();
        
        /// <summary>
        /// 创建XML数据库实例
        /// </summary>
        public XmlDatabase()
        {
        }
        
        /// <summary>
        /// 创建根节点，如果文件不存在则创建新文件
        /// </summary>
        /// <param name="filePath">XML文件路径</param>
        /// <returns>根节点</returns>
        public XmlNode CreateRoot(string filePath)
        {
            // 规范化路径
            filePath = Path.GetFullPath(filePath);
            
            // 检查是否已加载
            if (_rootNodes.TryGetValue(filePath, out var existingNode))
                return existingNode;
                
            XmlDocument doc;
            XmlElement rootElement;
            
            // 检查文件是否存在
            if (File.Exists(filePath))
            {
                // 加载现有文件
                doc = new XmlDocument();
                doc.Load(filePath);
                rootElement = doc.DocumentElement;
            }
            else
            {
                // 创建新文件
                doc = new XmlDocument();
                XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
                doc.AppendChild(xmlDeclaration);
                
                // 创建根元素
                rootElement = doc.CreateElement("Root");
                doc.AppendChild(rootElement);
                
                // 确保目录存在
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // 保存文件
                doc.Save(filePath);
            }
            
            // 创建根节点
            var rootNode = new XmlNode(rootElement, doc, filePath, this);
            
            // 缓存文档和节点
            _documents[filePath] = doc;
            _rootNodes[filePath] = rootNode;
            
            return rootNode;
        }
        
        /// <summary>
        /// 打开现有的XML文件
        /// </summary>
        /// <param name="filePath">XML文件路径</param>
        /// <returns>根节点</returns>
        public XmlNode OpenRoot(string filePath)
        {
            // 规范化路径
            filePath = Path.GetFullPath(filePath);
            
            // 检查是否已加载
            if (_rootNodes.TryGetValue(filePath, out var existingNode))
                return existingNode;
                
            // 检查文件是否存在
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"找不到XML文件: {filePath}");
                
            // 加载文件
            XmlDocument doc = new XmlDocument();
            doc.Load(filePath);
            
            // 创建根节点
            var rootNode = new XmlNode(doc.DocumentElement, doc, filePath, this);
            
            // 缓存文档和节点
            _documents[filePath] = doc;
            _rootNodes[filePath] = rootNode;
            
            return rootNode;
        }
        
        /// <summary>
        /// 保存所有更改到磁盘
        /// </summary>
        public void SaveChanges()
        {
            foreach (var rootNode in _rootNodes.Values)
            {
                rootNode.Save();
            }
        }
        
        /// <summary>
        /// 关闭指定的XML文件
        /// </summary>
        /// <param name="filePath">XML文件路径</param>
        public void CloseFile(string filePath)
        {
            // 规范化路径
            filePath = Path.GetFullPath(filePath);
            
            // 保存更改
            if (_rootNodes.TryGetValue(filePath, out var rootNode))
            {
                rootNode.Save();
                _rootNodes.Remove(filePath);
            }
            
            // 移除文档
            _documents.Remove(filePath);
        }
        
        /// <summary>
        /// 关闭所有打开的XML文件
        /// </summary>
        public void CloseAll()
        {
            // 保存所有更改
            SaveChanges();
            
            // 清除缓存
            _rootNodes.Clear();
            _documents.Clear();
        }
    }
} 