using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace Drx.Sdk.Network.DataBase
{
    /// <summary>
    /// XML节点的实现类
    /// </summary>
    public class XmlNode : IXmlNode
    {
        private readonly XmlElement _element;
        private readonly IXmlNode _parent;
        private bool _isDirty;
        protected XmlDocument _document;
        protected string _filePath;
        protected XmlDatabase _database;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="element">XML元素</param>
        /// <param name="parent">父节点</param>
        public XmlNode(XmlElement element, IXmlNode parent = null)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            _parent = parent;
            _isDirty = false;
        }

        /// <summary>
        /// 构造函数 - 用于XmlDatabase
        /// </summary>
        /// <param name="element">XML元素</param>
        /// <param name="document">XML文档</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="database">数据库实例</param>
        /// <param name="parent">父节点</param>
        public XmlNode(XmlElement element, XmlDocument document, string filePath, XmlDatabase database, XmlNode parent = null)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _database = database;
            _parent = parent;
            _isDirty = false;
        }

        /// <summary>
        /// 获取节点名称
        /// </summary>
        public string Name => _element.Name;

        /// <summary>
        /// 获取父节点
        /// </summary>
        public IXmlNode Parent => _parent;

        /// <summary>
        /// 获取节点是否已修改
        /// </summary>
        public bool IsDirty => _isDirty;

        /// <summary>
        /// 获取底层XML元素
        /// </summary>
        public XmlElement GetUnderlyingElement() => _element;

        /// <summary>
        /// 保存更改到文件
        /// </summary>
        public void Save()
        {
            if (_isDirty && _document != null && !string.IsNullOrEmpty(_filePath))
            {
                _document.Save(_filePath);
                _isDirty = false;
            }
        }

        /// <summary>
        /// 向节点添加字符串类型数据
        /// </summary>
        public IXmlNode PushString(string nodeName, string keyName, params string[] values)
        {
            var node = GetOrCreateChildElement(nodeName);
            
            if (values.Length == 1)
            {
                node.SetAttribute(keyName, values[0]);
            }
            else if (values.Length > 1)
            {
                node.SetAttribute(keyName, string.Join(",", values));
            }
            
            _isDirty = true;
            return this;
        }

        /// <summary>
        /// 向节点添加整数类型数据
        /// </summary>
        public IXmlNode PushInt(string nodeName, string keyName, params int[] values)
        {
            var node = GetOrCreateChildElement(nodeName);
            
            if (values.Length == 1)
            {
                node.SetAttribute(keyName, values[0].ToString(CultureInfo.InvariantCulture));
            }
            else if (values.Length > 1)
            {
                node.SetAttribute(keyName, string.Join(",", values.Select(v => v.ToString(CultureInfo.InvariantCulture))));
            }
            
            _isDirty = true;
            return this;
        }

        /// <summary>
        /// 向节点添加浮点数类型数据
        /// </summary>
        public IXmlNode PushFloat(string nodeName, string keyName, params float[] values)
        {
            var node = GetOrCreateChildElement(nodeName);
            
            if (values.Length == 1)
            {
                node.SetAttribute(keyName, values[0].ToString(CultureInfo.InvariantCulture));
            }
            else if (values.Length > 1)
            {
                node.SetAttribute(keyName, string.Join(",", values.Select(v => v.ToString(CultureInfo.InvariantCulture))));
            }
            
            _isDirty = true;
            return this;
        }

        /// <summary>
        /// 创建子节点
        /// </summary>
        public IXmlNode PushNode(string nodeName)
        {
            var element = GetOrCreateChildElement(nodeName);
            XmlNode node;
            
            if (_document != null && _database != null)
            {
                node = new XmlNode(element, _document, _filePath, _database, this);
            }
            else
            {
                node = new XmlNode(element, this);
            }
            
            _isDirty = true;
            return node;
        }

        /// <summary>
        /// 创建带有数据列表的子节点
        /// </summary>
        public IXmlNode PushNode<T>(string nodeName, List<T> values) where T : IXmlSerializable, new()
        {
            var node = PushNode(nodeName);
            
            for (int i = 0; i < values.Count; i++)
            {
                var itemNode = node.PushNode("item");
                values[i].WriteToXml(itemNode);
            }
            
            _isDirty = true;
            return node;
        }

        /// <summary>
        /// 创建对外部XML文件的引用
        /// </summary>
        public IXmlNodeReference PushReference(string nodeName, string path)
        {
            var element = GetOrCreateChildElement(nodeName);
            element.SetAttribute("path", path);
            
            IXmlNodeReference reference;
            if (_document != null && _database != null)
            {
                reference = new XmlNodeReferenceImpl(element, _document, _filePath, _database, this, path);
            }
            else
            {
                reference = new XmlNodeReferenceImpl(element, this, path);
            }
            
            _isDirty = true;
            return reference;
        }

        /// <summary>
        /// 获取字符串类型数据
        /// </summary>
        public string GetString(string nodeName, string keyName, string defaultValue = "")
        {
            var node = GetChildElement(nodeName);
            if (node == null || !node.HasAttribute(keyName))
                return defaultValue;
                
            return node.GetAttribute(keyName);
        }

        /// <summary>
        /// 获取字符串数组
        /// </summary>
        public string[] GetStringArray(string nodeName, string keyName)
        {
            var value = GetString(nodeName, keyName);
            if (string.IsNullOrEmpty(value))
                return new string[0];
                
            return value.Split(',');
        }

        /// <summary>
        /// 获取整数类型数据
        /// </summary>
        public int GetInt(string nodeName, string keyName, int defaultValue = 0)
        {
            var value = GetString(nodeName, keyName);
            if (string.IsNullOrEmpty(value) || !int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return defaultValue;
                
            return result;
        }

        /// <summary>
        /// 获取整数数组
        /// </summary>
        public int[] GetIntArray(string nodeName, string keyName)
        {
            var values = GetStringArray(nodeName, keyName);
            if (values.Length == 0)
                return new int[0];
                
            return values
                .Select(v => int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0)
                .ToArray();
        }

        /// <summary>
        /// 获取浮点数类型数据
        /// </summary>
        public float GetFloat(string nodeName, string keyName, float defaultValue = 0.0f)
        {
            var value = GetString(nodeName, keyName);
            if (string.IsNullOrEmpty(value) || !float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return defaultValue;
                
            return result;
        }

        /// <summary>
        /// 获取浮点数数组
        /// </summary>
        public float[] GetFloatArray(string nodeName, string keyName)
        {
            var values = GetStringArray(nodeName, keyName);
            if (values.Length == 0)
                return new float[0];
                
            return values
                .Select(v => float.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0.0f)
                .ToArray();
        }

        /// <summary>
        /// 获取子节点
        /// </summary>
        public IXmlNode GetNode(string nodeName)
        {
            var element = GetChildElement(nodeName);
            if (element == null)
                return null;
                
            if (_document != null && _database != null)
            {
                return new XmlNode(element, _document, _filePath, _database, this);
            }
            else
            {
                return new XmlNode(element, this);
            }
        }

        /// <summary>
        /// 获取对象列表
        /// </summary>
        public List<T> GetList<T>(string nodeName) where T : IXmlSerializable, new()
        {
            var node = GetNode(nodeName);
            if (node == null)
                return new List<T>();
                
            var result = new List<T>();
            foreach (var childNode in node.GetChildren())
            {
                if (childNode.Name == "item")
                {
                    var item = new T();
                    item.ReadFromXml(childNode);
                    result.Add(item);
                }
            }
            
            return result;
        }

        /// <summary>
        /// 获取引用节点
        /// </summary>
        public IXmlNodeReference GetReference(string nodeName)
        {
            var element = GetChildElement(nodeName);
            if (element == null || !element.HasAttribute("path"))
                return null;
                
            var path = element.GetAttribute("path");
            
            if (_document != null && _database != null)
            {
                return new XmlNodeReferenceImpl(element, _document, _filePath, _database, this, path);
            }
            else
            {
                return new XmlNodeReferenceImpl(element, this, path);
            }
        }

        /// <summary>
        /// 获取所有子节点
        /// </summary>
        public IEnumerable<IXmlNode> GetChildren()
        {
            foreach (System.Xml.XmlNode childElement in _element.ChildNodes)
            {
                if (childElement.NodeType == XmlNodeType.Element)
                {
                    if (_document != null && _database != null)
                    {
                        yield return new XmlNode((XmlElement)childElement, _document, _filePath, _database, this);
                    }
                    else
                    {
                        yield return new XmlNode((XmlElement)childElement, this);
                    }
                }
            }
        }

        /// <summary>
        /// 向节点添加布尔类型数据
        /// </summary>
        public IXmlNode PushBool(string nodeName, string keyName, bool value)
        {
            return PushString(nodeName, keyName, value.ToString());
        }

        /// <summary>
        /// 获取布尔类型数据
        /// </summary>
        public bool GetBool(string nodeName, string keyName, bool defaultValue = false)
        {
            var valueStr = GetString(nodeName, keyName);
            if (string.IsNullOrEmpty(valueStr))
            {
                return defaultValue;
            }
            return bool.TryParse(valueStr, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 向节点添加十进制数类型数据
        /// </summary>
        public IXmlNode PushDecimal(string nodeName, string keyName, decimal value)
        {
            return PushString(nodeName, keyName, value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 获取可空整数类型数据
        /// </summary>
        public int? GetIntNullable(string nodeName, string keyName)
        {
            var valueStr = GetString(nodeName, keyName);
            if (string.IsNullOrEmpty(valueStr))
            {
                return null;
            }
            return int.TryParse(valueStr, out var result) ? result : (int?)null;
        }

        /// <summary>
        /// 获取可空十进制数类型数据
        /// </summary>
        public decimal? GetDecimalNullable(string nodeName, string keyName)
        {
            var valueStr = GetString(nodeName, keyName);
            if (string.IsNullOrEmpty(valueStr))
            {
                return null;
            }
            return decimal.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : (decimal?)null;
        }

        /// <summary>
        /// 获取或创建子节点
        /// </summary>
        public IXmlNode GetOrCreateNode(string path)
        {
            if (string.IsNullOrEmpty(path))
                return this;
                
            string[] parts = path.Split('/');
            IXmlNode current = this;
            
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;
                    
                var child = current.GetNode(part);
                if (child == null)
                    child = current.PushNode(part);
                    
                current = child;
            }
            
            return current;
        }

        /// <summary>
        /// 获取节点（通过路径）
        /// </summary>
        public IXmlNode GetNodeByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return this;
                
            string[] parts = path.Split('/');
            IXmlNode current = this;
            
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;
                    
                current = current.GetNode(part);
                if (current == null)
                    return null;
            }
            
            return current;
        }

        /// <summary>
        /// 将对象列表序列化到XML节点
        /// </summary>
        public IXmlNode SerializeList<T>(string nodeName, IEnumerable<T> items) where T : IXmlSerializable, new()
        {
            var list = items.ToList();
            PushNode(nodeName, list);
            return this;
        }

        /// <summary>
        /// 从XML节点反序列化对象列表
        /// </summary>
        public List<T> DeserializeList<T>(string nodeName) where T : IXmlSerializable, new()
        {
            return GetList<T>(nodeName);
        }

        /// <summary>
        /// 向节点添加通用对象数据，根据对象类型自动选择适当的Push方法
        /// </summary>
        public IXmlNode Push(string nodeName, string keyName, object value)
        {
            if (value == null)
            {
                return PushString(nodeName, keyName, string.Empty);
            }
            
            Type type = value.GetType();
            
            if (type == typeof(string))
            {
                return PushString(nodeName, keyName, (string)value);
            }
            else if (type == typeof(int))
            {
                return PushInt(nodeName, keyName, (int)value);
            }
            else if (type == typeof(float))
            {
                return PushFloat(nodeName, keyName, (float)value);
            }
            else if (type == typeof(bool))
            {
                return PushBool(nodeName, keyName, (bool)value);
            }
            else if (type == typeof(decimal))
            {
                return PushDecimal(nodeName, keyName, (decimal)value);
            }
            else if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType == typeof(string))
                {
                    return PushString(nodeName, keyName, (string[])value);
                }
                else if (elementType == typeof(int))
                {
                    return PushInt(nodeName, keyName, (int[])value);
                }
                else if (elementType == typeof(float))
                {
                    return PushFloat(nodeName, keyName, (float[])value);
                }
            }
            
            // 默认转换为字符串
            return PushString(nodeName, keyName, value.ToString());
        }

        private XmlElement GetChildElement(string name)
        {
            return _element.SelectSingleNode(name) as XmlElement;
        }

        private XmlElement GetOrCreateChildElement(string name)
        {
            var element = GetChildElement(name);
            if (element == null)
            {
                element = _element.OwnerDocument.CreateElement(name);
                _element.AppendChild(element);
            }
            return element;
        }
    }

    /// <summary>
    /// XML节点引用的实现类
    /// </summary>
    public class XmlNodeReferenceImpl : XmlNode, IXmlNodeReference
    {
        /// <summary>
        /// 获取引用路径
        /// </summary>
        public string ReferencePath { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="element">XML元素</param>
        /// <param name="parent">父节点</param>
        /// <param name="referencePath">引用路径</param>
        public XmlNodeReferenceImpl(XmlElement element, IXmlNode parent, string referencePath)
            : base(element, parent)
        {
            ReferencePath = referencePath;
        }

        /// <summary>
        /// 构造函数 - 用于XmlDatabase
        /// </summary>
        /// <param name="element">XML元素</param>
        /// <param name="document">XML文档</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="database">数据库实例</param>
        /// <param name="parent">父节点</param>
        /// <param name="referencePath">引用路径</param>
        public XmlNodeReferenceImpl(XmlElement element, XmlDocument document, string filePath, XmlDatabase database, XmlNode parent, string referencePath)
            : base(element, document, filePath, database, parent)
        {
            ReferencePath = referencePath;
        }

        /// <summary>
        /// 解析引用并获取引用的节点
        /// </summary>
        /// <returns>引用的节点</returns>
        public IXmlNode ResolveReference()
        {
            // 如果有数据库实例，使用它来解析引用
            if (_database != null && !string.IsNullOrEmpty(ReferencePath))
            {
                string resolvedPath = ReferencePath;
                
                // 如果是相对路径，则相对于当前文件的目录
                if (!Path.IsPathRooted(resolvedPath) && !string.IsNullOrEmpty(_filePath))
                {
                    string baseDir = Path.GetDirectoryName(_filePath);
                    resolvedPath = Path.GetFullPath(Path.Combine(baseDir, resolvedPath));
                }
                
                // 尝试打开引用的文件
                try
                {
                    return _database.OpenRoot(resolvedPath);
                }
                catch (Exception)
                {
                    // 如果打开失败，返回null
                    return null;
                }
            }
            
            return null;
        }

        /// <summary>
        /// 更新引用路径
        /// </summary>
        /// <param name="newPath">新的引用路径</param>
        public void UpdatePath(string newPath)
        {
            if (string.IsNullOrEmpty(newPath))
                throw new ArgumentNullException(nameof(newPath));

            ReferencePath = newPath;
            GetUnderlyingElement().SetAttribute("path", newPath);
        }
    }
} 