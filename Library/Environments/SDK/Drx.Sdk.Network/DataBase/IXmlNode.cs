using System;
using System.Collections.Generic;
using System.Globalization;

namespace Drx.Sdk.Network.DataBase
{
    /// <summary>
    /// XML节点接口，定义XML节点的基本操作
    /// </summary>
    public interface IXmlNode
    {
        /// <summary>
        /// 获取节点名称
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// 获取父节点
        /// </summary>
        IXmlNode Parent { get; }
        
        /// <summary>
        /// 获取节点是否已修改
        /// </summary>
        bool IsDirty { get; }
        
        /// <summary>
        /// 向节点添加字符串类型数据
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <param name="values">字符串值列表</param>
        /// <returns>当前节点</returns>
        IXmlNode PushString(string nodeName, string keyName, params string[] values);
        
        /// <summary>
        /// 向节点添加整数类型数据
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <param name="values">整数值列表</param>
        /// <returns>当前节点</returns>
        IXmlNode PushInt(string nodeName, string keyName, params int[] values);
        
        /// <summary>
        /// 向节点添加浮点数类型数据
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <param name="values">浮点数值列表</param>
        /// <returns>当前节点</returns>
        IXmlNode PushFloat(string nodeName, string keyName, params float[] values);
        
        /// <summary>
        /// 创建子节点
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <returns>新创建的子节点</returns>
        IXmlNode PushNode(string nodeName);
        
        /// <summary>
        /// 创建带有数据列表的子节点
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="nodeName">节点名称</param>
        /// <param name="values">数据列表</param>
        /// <returns>新创建的子节点</returns>
        IXmlNode PushNode<T>(string nodeName, List<T> values) where T : IXmlSerializable, new();
        
        /// <summary>
        /// 创建对外部XML文件的引用
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="path">外部XML文件路径</param>
        /// <returns>引用节点</returns>
        IXmlNodeReference PushReference(string nodeName, string path);
        
        /// <summary>
        /// 获取字符串类型数据
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>字符串值</returns>
        string GetString(string nodeName, string keyName, string defaultValue = "");
        
        /// <summary>
        /// 获取字符串数组
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <returns>字符串数组</returns>
        string[] GetStringArray(string nodeName, string keyName);
        
        /// <summary>
        /// 获取整数类型数据
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>整数值</returns>
        int GetInt(string nodeName, string keyName, int defaultValue = 0);
        
        /// <summary>
        /// 获取整数数组
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <returns>整数数组</returns>
        int[] GetIntArray(string nodeName, string keyName);
        
        /// <summary>
        /// 获取浮点数类型数据
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>浮点数值</returns>
        float GetFloat(string nodeName, string keyName, float defaultValue = 0.0f);
        
        /// <summary>
        /// 获取浮点数数组
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <returns>浮点数数组</returns>
        float[] GetFloatArray(string nodeName, string keyName);
        
        /// <summary>
        /// 获取子节点
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <returns>子节点，如果不存在则返回null</returns>
        IXmlNode GetNode(string nodeName);
        
        /// <summary>
        /// 获取对象列表
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="nodeName">节点名称</param>
        /// <returns>对象列表</returns>
        List<T> GetList<T>(string nodeName) where T : IXmlSerializable, new();
        
        /// <summary>
        /// 获取引用节点
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <returns>引用节点，如果不是引用则返回null</returns>
        IXmlNodeReference GetReference(string nodeName);

        /// <summary>
        /// 获取所有子节点
        /// </summary>
        /// <returns>子节点列表</returns>
        IEnumerable<IXmlNode> GetChildren();
        
        /// <summary>
        /// 向节点添加布尔类型数据
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <param name="value">布尔值</param>
        /// <returns>当前节点</returns>
        IXmlNode PushBool(string nodeName, string keyName, bool value);
        
        /// <summary>
        /// 获取布尔类型数据
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>布尔值</returns>
        bool GetBool(string nodeName, string keyName, bool defaultValue = false);
        
        /// <summary>
        /// 向节点添加十进制数类型数据
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <param name="value">十进制数值</param>
        /// <returns>当前节点</returns>
        IXmlNode PushDecimal(string nodeName, string keyName, decimal value);
        
        /// <summary>
        /// 获取可空整数类型数据
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <returns>整数值，如果不存在则返回null</returns>
        int? GetIntNullable(string nodeName, string keyName);
        
        /// <summary>
        /// 获取可空十进制数类型数据
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <returns>十进制数值，如果不存在则返回null</returns>
        decimal? GetDecimalNullable(string nodeName, string keyName);
        
        /// <summary>
        /// 获取或创建子节点
        /// </summary>
        /// <param name="path">节点路径，格式为"node1/node2/node3"</param>
        /// <returns>子节点</returns>
        IXmlNode GetOrCreateNode(string path);
        
        /// <summary>
        /// 获取节点（通过路径）
        /// </summary>
        /// <param name="path">节点路径，格式为"node1/node2/node3"</param>
        /// <returns>子节点，如果不存在则返回null</returns>
        IXmlNode GetNodeByPath(string path);
        
        /// <summary>
        /// 将对象列表序列化到XML节点
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="nodeName">节点名称</param>
        /// <param name="items">对象列表</param>
        /// <returns>当前节点</returns>
        IXmlNode SerializeList<T>(string nodeName, IEnumerable<T> items) where T : IXmlSerializable, new();
        
        /// <summary>
        /// 从XML节点反序列化对象列表
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="nodeName">节点名称</param>
        /// <returns>对象列表</returns>
        List<T> DeserializeList<T>(string nodeName) where T : IXmlSerializable, new();
        
        /// <summary>
        /// 向节点添加通用对象数据，根据对象类型自动选择适当的Push方法
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="keyName">键名</param>
        /// <param name="value">对象值</param>
        /// <returns>当前节点</returns>
        IXmlNode Push(string nodeName, string keyName, object value);
    }
} 