using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

        /// <summary>
        /// 向节点添加列表数据
        /// </summary>
        public IXmlNode PushList<T>(string nodeName, string keyName, List<T> list)
        {
            if (list == null || list.Count == 0)
            {
                return PushString(nodeName, keyName, string.Empty);
            }

            string serializedList;
            
            if (typeof(T) == typeof(string))
            {
                serializedList = string.Join(",", list.Cast<string>());
            }
            else if (typeof(T) == typeof(int))
            {
                serializedList = string.Join(",", list.Cast<int>().Select(v => v.ToString(CultureInfo.InvariantCulture)));
            }
            else if (typeof(T) == typeof(float))
            {
                serializedList = string.Join(",", list.Cast<float>().Select(v => v.ToString(CultureInfo.InvariantCulture)));
            }
            else if (typeof(T) == typeof(decimal))
            {
                serializedList = string.Join(",", list.Cast<decimal>().Select(v => v.ToString(CultureInfo.InvariantCulture)));
            }
            else if (typeof(T) == typeof(bool))
            {
                serializedList = string.Join(",", list.Cast<bool>().Select(v => v.ToString()));
            }
            else if (typeof(T) == typeof(DateTime))
            {
                serializedList = string.Join(",", list.Cast<DateTime>().Select(v => v.ToString("o")));
            }
            else
            {
                serializedList = string.Join(",", list.Select(v => v.ToString()));
            }

            return PushString(nodeName, keyName, serializedList);
        }

        /// <summary>
        /// 获取列表数据
        /// </summary>
        public List<T> GetList<T>(string nodeName, string keyName)
        {
            string value = GetString(nodeName, keyName);
            if (string.IsNullOrEmpty(value))
            {
                return new List<T>();
            }

            string[] parts = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<T>(parts.Length);

            foreach (var part in parts)
            {
                if (typeof(T) == typeof(string))
                {
                    result.Add((T)(object)part);
                }
                else if (typeof(T) == typeof(int))
                {
                    if (int.TryParse(part, NumberStyles.Any, CultureInfo.InvariantCulture, out var intValue))
                    {
                        result.Add((T)(object)intValue);
                    }
                }
                else if (typeof(T) == typeof(float))
                {
                    if (float.TryParse(part, NumberStyles.Any, CultureInfo.InvariantCulture, out var floatValue))
                    {
                        result.Add((T)(object)floatValue);
                    }
                }
                else if (typeof(T) == typeof(decimal))
                {
                    if (decimal.TryParse(part, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue))
                    {
                        result.Add((T)(object)decimalValue);
                    }
                }
                else if (typeof(T) == typeof(bool))
                {
                    if (bool.TryParse(part, out var boolValue))
                    {
                        result.Add((T)(object)boolValue);
                    }
                }
                else if (typeof(T) == typeof(DateTime))
                {
                    if (DateTime.TryParse(part, null, DateTimeStyles.RoundtripKind, out var dateValue))
                    {
                        result.Add((T)(object)dateValue);
                    }
                }
                else
                {
                    // 尝试使用默认转换
                    try
                    {
                        var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(T));
                        if (converter.CanConvertFrom(typeof(string)))
                        {
                            result.Add((T)converter.ConvertFromString(part));
                        }
                    }
                    catch
                    {
                        // 忽略转换错误
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 向节点添加字典数据
        /// </summary>
        public IXmlNode PushDictionary<TKey, TValue>(string nodeName, string keyName, Dictionary<TKey, TValue> dictionary)
        {
            if (dictionary == null || dictionary.Count == 0)
            {
                return PushString(nodeName, keyName, string.Empty);
            }

            // 序列化格式：key1:value1,key2:value2,...
            string serializedDict = string.Join(",", dictionary.Select(kv => 
            {
                string keyStr = FormatDictionaryComponent(kv.Key);
                string valueStr = FormatDictionaryComponent(kv.Value);
                return $"{keyStr}:{valueStr}";
            }));

            return PushString(nodeName, keyName, serializedDict);
        }

        /// <summary>
        /// 获取字典数据
        /// </summary>
        public Dictionary<TKey, TValue> GetDictionary<TKey, TValue>(string nodeName, string keyName)
        {
            string value = GetString(nodeName, keyName);
            if (string.IsNullOrEmpty(value))
            {
                return new Dictionary<TKey, TValue>();
            }

            var result = new Dictionary<TKey, TValue>();
            string[] pairs = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var pair in pairs)
            {
                string[] parts = pair.Split(new[] { ':' }, 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                if (TryParseDictionaryComponent<TKey>(parts[0], out var key) && 
                    TryParseDictionaryComponent<TValue>(parts[1], out var val))
                {
                    result[key] = val;
                }
            }

            return result;
        }

        private XmlElement GetChildElement(string name)
        {
            // 不使用XPath选择，而是直接遍历子节点查找匹配的名称
            foreach (System.Xml.XmlNode childNode in _element.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.Element && childNode.Name == name)
                {
                    return (XmlElement)childNode;
                }
            }
            return null;
        }

        private XmlElement GetOrCreateChildElement(string name)
        {
            var element = GetChildElement(name);
            if (element == null)
            {
                // 检查节点名称是否合法
                if (!IsValidXmlName(name))
                {
                    // 如果不合法，替换为安全的XML名称
                    name = MakeSafeXmlName(name);
                }
                
                element = _element.OwnerDocument.CreateElement(name);
                _element.AppendChild(element);
            }
            return element;
        }
        
        // 验证XML节点名称是否合法
        private bool IsValidXmlName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
                
            // 检查节点名称是否符合XML规范
            try
            {
                // 尝试创建一个临时元素来验证名称
                var doc = new XmlDocument();
                doc.CreateElement(name);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        // 将任意字符串转换为安全的XML节点名称
        private string MakeSafeXmlName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "item";
                
            // 移除或替换非法字符
            StringBuilder result = new StringBuilder();
            
            // 第一个字符必须是字母或下划线
            if (!char.IsLetter(name[0]) && name[0] != '_')
                result.Append('_');
                
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
                    result.Append(c);
                else
                    result.Append('_'); // 替换非法字符为下划线
            }
            
            string safeName = result.ToString();
            return string.IsNullOrEmpty(safeName) ? "item" : safeName;
        }

        // 辅助方法：格式化字典组件
        private string FormatDictionaryComponent<T>(T value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (typeof(T) == typeof(string))
            {
                // 对字符串进行编码，避免特殊字符冲突
                return Uri.EscapeDataString((string)(object)value);
            }
            else if (typeof(T) == typeof(DateTime))
            {
                return ((DateTime)(object)value).ToString("o");
            }
            else
            {
                return value.ToString();
            }
        }

        // 辅助方法：解析字典组件
        private bool TryParseDictionaryComponent<T>(string value, out T result)
        {
            result = default;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            try
            {
                if (typeof(T) == typeof(string))
                {
                    // 解码字符串
                    result = (T)(object)Uri.UnescapeDataString(value);
                    return true;
                }
                else if (typeof(T) == typeof(int))
                {
                    if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var intValue))
                    {
                        result = (T)(object)intValue;
                        return true;
                    }
                }
                else if (typeof(T) == typeof(float))
                {
                    if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var floatValue))
                    {
                        result = (T)(object)floatValue;
                        return true;
                    }
                }
                else if (typeof(T) == typeof(decimal))
                {
                    if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue))
                    {
                        result = (T)(object)decimalValue;
                        return true;
                    }
                }
                else if (typeof(T) == typeof(bool))
                {
                    if (bool.TryParse(value, out var boolValue))
                    {
                        result = (T)(object)boolValue;
                        return true;
                    }
                }
                else if (typeof(T) == typeof(DateTime))
                {
                    if (DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out var dateValue))
                    {
                        result = (T)(object)dateValue;
                        return true;
                    }
                }
                else
                {
                    // 尝试使用默认转换
                    var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(T));
                    if (converter.CanConvertFrom(typeof(string)))
                    {
                        result = (T)converter.ConvertFromString(value);
                        return true;
                    }
                }
            }
            catch
            {
                // 忽略转换错误
            }

            return false;
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