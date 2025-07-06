using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Drx.Sdk.Network.DataBase.Configuration;
using Drx.Sdk.Network.DataBase.Helpers;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using System.Globalization;

namespace Drx.Sdk.Network.DataBase
{
    /// <summary>
    /// XML数据库扩展方法
    /// </summary>
    public static class XmlDatabaseExtensions
    {
        /// <summary>
        /// 创建对外部XML文件的引用，并确保目标文件存在
        /// </summary>
        /// <param name="node">父节点</param>
        /// <param name="nodeName">节点名称</param>
        /// <param name="path">外部XML文件路径</param>
        /// <param name="database">数据库实例，用于创建目标文件</param>
        /// <returns>引用节点</returns>
        public static IXmlNodeReference EnsureReference(this IXmlNode node, string nodeName, string path, XmlDatabase database)
        {
            var reference = node.PushReference(nodeName, path);
            
            // 确保目标文件存在
            if (!File.Exists(path))
            {
                database.CreateRoot(path);
            }
            
            return reference;
        }

        /// <summary>
        /// 将列表保存为多文件结构
        /// </summary>
        /// <typeparam name="T">要序列化的对象类型</typeparam>
        /// <param name="database">数据库实例</param>
        /// <param name="indexFilePath">索引文件路径</param>
        /// <param name="dataDirectory">数据文件存储目录</param>
        /// <param name="items">要保存的列表</param>
        /// <param name="itemsPerFile">每个文件的最大项目数</param>
        /// <param name="listNodeName">在索引文件中代表此列表的节点名称</param>
        /// <returns>索引文件的根节点</returns>
        public static IXmlNode SaveAsSplitFiles<T>(
            this XmlDatabase database,
            string indexFilePath,
            string dataDirectory,
            IEnumerable<T> items,
            int itemsPerFile,
            string listNodeName) where T : IXmlSerializable, new()
        {
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            var indexNode = database.CreateRoot(indexFilePath);
            var listNode = indexNode.GetOrCreateNode(listNodeName);
            var itemsList = items.ToList();
            var totalPages = (int)Math.Ceiling((double)itemsList.Count / itemsPerFile);

            listNode.PushInt("metadata", "itemsPerFile", itemsPerFile);
            listNode.PushInt("metadata", "totalItems", itemsList.Count);
            listNode.PushInt("metadata", "totalPages", totalPages);

            for (int i = 0; i < totalPages; i++)
            {
                var chunk = itemsList.Skip(i * itemsPerFile).Take(itemsPerFile).ToList();
                var partFileName = Path.Combine(dataDirectory, $"part_{i}.xml");
                
                var partRoot = database.CreateRoot(partFileName);
                partRoot.SerializeList("items", chunk);
                database.SaveChanges();

                // Make path relative to index file
                var relativePath = GetRelativePath(indexFilePath, partFileName);
                listNode.PushReference($"part_{i}", relativePath);
            }

            database.SaveChanges();
            return indexNode;
        }

        /// <summary>
        /// 从多文件结构加载完整列表
        /// </summary>
        public static List<T> LoadFromSplitFiles<T>(this XmlDatabase database, string indexFilePath, string listNodeName) where T : IXmlSerializable, new()
        {
            var indexNode = database.OpenRoot(indexFilePath);
            var listNode = indexNode.GetNode(listNodeName);
            if (listNode == null)
            {
                throw new DirectoryNotFoundException($"Node '{listNodeName}' not found in index file '{indexFilePath}'.");
            }

            var totalPages = listNode.GetInt("metadata", "totalPages");
            var allItems = new List<T>();

            var indexFileDir = Path.GetDirectoryName(Path.GetFullPath(indexFilePath));

            for (int i = 0; i < totalPages; i++)
            {
                var partReference = listNode.GetReference($"part_{i}");
                if (partReference != null)
                {
                    // Resolve relative path
                    var partPath = Path.Combine(indexFileDir, partReference.ReferencePath);
                    var partRoot = database.OpenRoot(partPath);
                    var items = partRoot.DeserializeList<T>("items");
                    allItems.AddRange(items);
                }
            }

            return allItems;
        }

        private static string GetRelativePath(string fromPath, string toPath)
        {
            var fromUri = new Uri(Path.GetFullPath(fromPath));
            var toUri = new Uri(Path.GetFullPath(toPath));

            if (fromUri.Scheme != toUri.Scheme) { return toPath; }

            var relativeUri = fromUri.MakeRelativeUri(toUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        /// <summary>
        /// 将数据集合保存到自动化索引系统中
        /// </summary>
        /// <typeparam name="T">要序列化的对象类型</typeparam>
        /// <param name="database">数据库实例</param>
        /// <param name="items">要保存的数据集合</param>
        /// <param name="config">索引系统配置</param>
        /// <param name="keySelector">用于从每个项目中提取唯一键的函数</param>
        /// <param name="categorySelector">（可选）用于将项目分类到子目录的函数</param>
        /// <returns>索引文件的根节点</returns>
        public static IXmlNode SaveToIndexSystem<T>(
            this XmlDatabase database,
            IEnumerable<T> items,
            IndexSystemConfig config,
            Func<T, string> keySelector,
            Func<T, string> categorySelector = null) where T : IXmlSerializable, new()
        {
            if (config.AutoCreateDirectories && !Directory.Exists(config.RootPath))
            {
                Directory.CreateDirectory(config.RootPath);
            }

            var indexFilePath = Path.Combine(config.RootPath, "index.xml");
            var indexNode = database.CreateRoot(indexFilePath);
            var indexElement = indexNode.GetOrCreateNode("index");

            foreach (var item in items)
            {
                var itemKey = keySelector(item);
                try 
                {
                    XmlConvert.VerifyName(itemKey);
                }
                catch (XmlException)
                {
                    // Maybe log a warning or skip? For now, we skip.
                    continue;
                }

                var fileName = config.UseHashForFilenames 
                    ? HashHelper.GetHash(item.GetHashCode().ToString()) + ".xml"
                    : itemKey + ".xml";
                
                var dataFilePath = Path.Combine(config.RootPath, fileName);
                var itemRoot = database.CreateRoot(dataFilePath);
                item.WriteToXml(itemRoot);
                
                indexElement.PushReference(itemKey, fileName);
            }
            
            return indexNode;
        }

        /// <summary>
        /// Creates or updates a single item within an automated index system.
        /// If the item exists, its data file is overwritten. If not, a new data file and index entry are created.
        /// </summary>
        /// <typeparam name="T">The type of the object to serialize.</typeparam>
        /// <param name="database">The database instance.</param>
        /// <param name="item">The item to save or update.</param>
        /// <param name="config">The index system configuration.</param>
        /// <param name="keySelector">A function to extract a unique key from the item.</param>
        public static void UpdateInIndexSystem<T>(
            this XmlDatabase database,
            T item,
            IndexSystemConfig config,
            Func<T, string> keySelector) where T : IXmlSerializable, new()
        {
            if (config.AutoCreateDirectories && !Directory.Exists(config.RootPath))
            {
                Directory.CreateDirectory(config.RootPath);
            }

            var indexFilePath = Path.Combine(config.RootPath, "index.xml");
            var indexNode = database.CreateRoot(indexFilePath);
            var indexElement = indexNode.GetOrCreateNode("index");

            var itemKey = keySelector(item);
            try
            {
                XmlConvert.VerifyName(itemKey);
            }
            catch (XmlException)
            {
                throw new ArgumentException("The key selected for the item is not a valid XML tag name.", nameof(keySelector));
            }

            var itemReference = indexElement.GetReference(itemKey);
            string fileName;

            if (itemReference != null && !string.IsNullOrEmpty(itemReference.ReferencePath))
            {
                // Item exists, use its existing file path.
                fileName = itemReference.ReferencePath;
            }
            else
            {
                // New item, create a new file name.
                fileName = itemKey + ".xml";
                // Add the new reference to the index.
                indexElement.PushReference(itemKey, fileName);
            }
            
            var dataFilePath = Path.Combine(config.RootPath, fileName);
            var itemRoot = database.CreateRoot(dataFilePath);
            item.WriteToXml(itemRoot);
        }

        /// <summary>
        /// 从自动化索引系统中加载所有数据对象。
        /// </summary>
        /// <typeparam name="T">要反序列化的对象类型。</typeparam>
        /// <param name="database">数据库实例。</param>
        /// <param name="indexFilePath">索引文件的完整路径。</param>
        /// <param name="filter">（可选）一个委托，用于在加载后对结果进行筛选。</param>
        /// <returns>反序列化对象的列表。</returns>
        public static List<T> LoadFromIndexSystem<T>(this XmlDatabase database, string indexFilePath, Func<T, bool> filter = null) where T : IXmlSerializable, new()
        {
            var indexNode = database.OpenRoot(indexFilePath).GetNode("index");
            if (indexNode == null) return new List<T>();

            var allItems = new List<T>();
            var indexFileDir = Path.GetDirectoryName(Path.GetFullPath(indexFilePath));
            
            var allKeyNodes = indexNode.GetChildren(); 

            foreach (var keyNode in allKeyNodes)
            {
                if (keyNode == null) continue;

                var keyNodeAsNode = keyNode as XmlNode;
                var keyNodeElement = keyNodeAsNode?.GetUnderlyingElement();
                if (keyNodeElement == null) continue;

                var relativePath = keyNodeElement.GetAttribute("path");
                
                if(!string.IsNullOrEmpty(relativePath))
                {
                    var itemPath = Path.GetFullPath(Path.Combine(indexFileDir, relativePath));
                    var itemRoot = database.OpenRoot(itemPath);
                    var item = new T();
                    item.ReadFromXml(itemRoot);

                    if (filter == null || filter(item))
                    {
                        allItems.Add(item);
                    }
                }
            }
            return allItems;
        }

        /// <summary>
        /// Loads a single item from an automated index system by its key.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize.</typeparam>
        /// <param name="database">The database instance.</param>
        /// <param name="indexFilePath">The full path to the index file.</param>
        /// <param name="key">The unique key of the item to load.</param>
        /// <returns>The deserialized object, or null if not found.</returns>
        public static T LoadSingleFromIndexSystem<T>(this XmlDatabase database, string indexFilePath, string key) where T : IXmlSerializable, new()
        {
            if (!File.Exists(indexFilePath))
            {
                // Or throw an exception, depending on desired behavior.
                return default(T);
            }

            var indexNode = database.OpenRoot(indexFilePath);
            var indexElement = indexNode.GetNode("index");
            if (indexElement == null)
            {
                return default(T);
            }

            var itemReference = indexElement.GetReference(key);
            if (itemReference == null || string.IsNullOrEmpty(itemReference.ReferencePath))
            {
                return default(T);
            }

            var indexFileDir = Path.GetDirectoryName(Path.GetFullPath(indexFilePath));
            var dataFilePath = Path.Combine(indexFileDir, itemReference.ReferencePath);

            if (!File.Exists(dataFilePath))
            {
                // Log warning: "Index points to a file that does not exist."
                return default(T);
            }
            
            var itemRoot = database.OpenRoot(dataFilePath);
            var item = new T();
            item.ReadFromXml(itemRoot);

            return item;
        }

        // This method needs to be added to XmlNode to expose the underlying element for the dummy GetAllChildren
        public static System.Xml.XmlElement GetUnderlyingElement(this XmlNode node)
        {
            // This would use reflection or a new public property.
            var field = typeof(XmlNode).GetField("_element", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(node) as System.Xml.XmlElement;
        }
    }
} 