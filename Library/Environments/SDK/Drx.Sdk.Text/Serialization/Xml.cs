using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;

namespace Drx.Sdk.Text.Serialization;

[AttributeUsage(AttributeTargets.Property)]
public class NonSerzAttribute : Attribute
{
}

public class Xml
{
    public static string Serialize<T>(T obj)
    {
        if (obj == null) return string.Empty;
        StringBuilder sb = new StringBuilder();
        using (StringWriter sw = new StringWriter(sb))
        using (XmlWriter writer = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true }))
        {
            if (writer == null) throw new InvalidOperationException("无法创建 XmlWriter 实例");
            SerializeValue(writer, obj, CleanName(obj.GetType().Name));
        }
        return sb.ToString();
    }

    public static void SerializeToFile<T>(T obj, string filePath)
    {
        try
        {
            Debug.WriteLine($"[Xml.cs] Attempting to serialize object of type {typeof(T)} to file: {filePath}");
            string xml = Serialize(obj);
            File.WriteAllText(filePath, xml);
            Debug.WriteLine($"[Xml.cs] Successfully wrote to file: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Xml.cs] ERROR during serialization to file {filePath}: {ex}");
            throw;
        }
    }

    public static T Deserialize<T>(string xml) where T : new()
    {
        using (StringReader sr = new StringReader(xml))
        using (XmlReader reader = XmlReader.Create(sr))
        {
            // Move to the root element
            while (reader.NodeType != XmlNodeType.Element && reader.Read()) { }
            
            if (reader.EOF) return new T();

            return (T)DeserializeValue(reader, typeof(T));
        }
    }

    public static T DeserializeFromFile<T>(string filePath) where T : new()
    {
        string xml = File.ReadAllText(filePath);
        return Deserialize<T>(xml);
    }

    private static void SerializeObject(XmlWriter writer, object obj, string elementName)
    {
        if (obj == null)
            return;

        writer.WriteStartElement(elementName);

        Type type = obj.GetType();
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (PropertyInfo property in properties)
        {
            // Skip indexer properties
            if (property.GetIndexParameters().Length > 0)
                continue;

            // Skip properties marked with NonSerz attribute
            if (property.GetCustomAttribute<NonSerzAttribute>() != null)
                continue;

            // Only process public and protected properties
            bool isPublic = property.GetMethod?.IsPublic ?? false;
            bool isProtected = property.GetMethod?.IsFamily ?? false;
            
            if (!isPublic && !isProtected)
                continue;

            object? value = property.GetValue(obj);
            SerializeValue(writer, value!, property.Name);
        }

        writer.WriteEndElement();
    }

    private static void SerializeValue(XmlWriter writer, object? value, string elementName)
    {
        if (value == null)
        {
            writer.WriteStartElement(elementName);
            writer.WriteAttributeString("null", "true");
            writer.WriteEndElement();
            return;
        }

        Type type = value.GetType();

        // Handle primitive types and strings
        if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime) || type == typeof(decimal))
        {
            writer.WriteStartElement(elementName);
            writer.WriteString(value.ToString());
            writer.WriteEndElement();
        }
        // Handle collections
        else if (value is IEnumerable enumerable && !(value is string))
        {
            writer.WriteStartElement(elementName);
            
            foreach (var item in enumerable)
            {
                string itemName = item != null ? CleanName(item.GetType().Name) : "Item";
                SerializeValue(writer, item!, itemName);
            }
            
            writer.WriteEndElement();
        }
        // Handle complex objects
        else
        {
            SerializeObject(writer, value, elementName);
        }
    }

    private static object? DeserializeObject(XmlReader reader, Type type)
    {
        // Skip until we find an element
        while (reader.NodeType != XmlNodeType.Element && reader.Read()) { }
        
        if (reader.NodeType != XmlNodeType.Element)
            return null;

        // Check for null
        if (reader.GetAttribute("null") == "true")
        {
            reader.Skip();
            return null;
        }

        object instance = Activator.CreateInstance(type) ?? throw new InvalidOperationException($"无法创建类型 {type} 的实例");
        Dictionary<string, PropertyInfo> properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                                        .Where(p => p.GetCustomAttribute<NonSerzAttribute>() == null)
                                                        .ToDictionary(p => p.Name);

        if (reader.IsEmptyElement)
        {
            reader.Read();
            return instance;
        }

        reader.ReadStartElement();

        while (reader.NodeType != XmlNodeType.EndElement)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                string propertyName = reader.Name;
                
                if (properties.TryGetValue(propertyName, out PropertyInfo property))
                {
                    // Only process public and protected properties
                    bool isPublic = property.SetMethod?.IsPublic ?? false;
                    bool isProtected = property.SetMethod?.IsFamily ?? false;
                    
                    if (isPublic || isProtected)
                    {
                        object value = DeserializeValue(reader, property.PropertyType);
                        property.SetValue(instance, value);
                        continue;
                    }
                }
            }
            
            reader.Skip();
        }

        reader.ReadEndElement();
        return instance;
    }

    private static object? DeserializeValue(XmlReader reader, Type type)
    {
        // Check for null
        if (reader.GetAttribute("null") == "true")
        {
            reader.Skip();
            return null;
        }

        // Handle primitive types and strings
        if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime) || type == typeof(decimal))
        {
            string value = reader.ReadElementContentAsString();
            
            if (type == typeof(string))
                return value;
            else if (type == typeof(int))
                return int.Parse(value);
            else if (type == typeof(bool))
                return bool.Parse(value);
            else if (type == typeof(double))
                return double.Parse(value);
            else if (type == typeof(float))
                return float.Parse(value);
            else if (type == typeof(DateTime))
                return DateTime.Parse(value);
            else if (type == typeof(decimal))
                return decimal.Parse(value);
            else
                return Convert.ChangeType(value, type);
        }
        // Handle collections
        else if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            if (reader.IsEmptyElement)
            {
                reader.Read(); // Consume the empty element
                return Activator.CreateInstance(type); // Return an empty collection
            }

            if (type.IsArray)
            {
                List<object?> items = new List<object?>();
                Type elementType = type.GetElementType() ?? typeof(object);
                
                reader.ReadStartElement();
                while (reader.NodeType != XmlNodeType.EndElement)
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        items.Add(DeserializeValue(reader, elementType));
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
                reader.ReadEndElement();

                Array array = Array.CreateInstance(elementType, items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    array.SetValue(items[i], i);
                }
                return array;
            }
            else // It's a list or other collection
            {
                IList list = (IList)(Activator.CreateInstance(type) ?? throw new InvalidOperationException($"无法创建类型 {type} 的实例"));
                Type elementType = type.GetGenericArguments()[0];

                reader.ReadStartElement();
                while (reader.NodeType != XmlNodeType.EndElement)
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        list.Add(DeserializeValue(reader, elementType));
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
                reader.ReadEndElement();
                return list;
            }
        }
        // Handle complex objects
        else
        {
            return DeserializeObject(reader, type);
        }
    }

    private static string CleanName(string name)
    {
        return name.Split('`')[0];
    }
}

