# XML数据库系统

这是一个基于XML的轻量级数据库系统，提供了简单而强大的数据存储和检索功能。该系统主要用于需要持久化数据存储但不需要完整SQL数据库的应用场景。

## 核心功能

- **XML文件管理**：创建、打开、保存和关闭XML文件
- **节点操作**：读写XML节点数据，支持多种数据类型
- **引用系统**：支持跨文件引用，实现数据分离与重用
- **索引存储**：通过索引系统高效管理大量数据对象
- **序列化支持**：将.NET对象序列化到XML节点或从XML节点反序列化

## 主要组件

- [XmlDatabase](./XmlDatabase.md)：核心数据库类，负责文件管理
- [XmlNode](./XmlNode.md)：XML节点实现，提供数据操作接口
- [IndexedRepository](./IndexedRepository.md)：简化的对象仓储，用于存储和检索可索引对象
- [XmlNodeReference](./XmlNodeReference.md)：实现对外部XML文件的引用

## 使用场景

- 配置文件管理
- 小型应用的数据存储
- 分布式数据存储
- 需要人类可读数据格式的场景
- 数据导入/导出功能

## 快速入门

```csharp
// 创建数据库实例
var database = new XmlDatabase();

// 创建或打开XML文件
var rootNode = database.CreateRoot("data.xml");

// 写入数据
rootNode.PushString("user", "name", "John Doe");
rootNode.PushInt("user", "age", 30);

// 创建子节点
var prefsNode = rootNode.PushNode("preferences");
prefsNode.PushBool("settings", "darkMode", true);

// 保存更改
database.SaveChanges();

// 读取数据
string name = rootNode.GetString("user", "name");
int age = rootNode.GetInt("user", "age");
bool darkMode = rootNode.GetNode("preferences").GetBool("settings", "darkMode");
```

## 高级功能

- **索引系统**：通过`XmlDatabaseExtensions`提供的方法，可以实现基于索引的高效数据存储和检索
- **对象序列化**：实现`IXmlSerializable`接口，可以将自定义对象序列化到XML
- **引用管理**：通过`XmlNodeReference`实现跨文件数据引用

## 文档索引

- [XmlDatabase](./XmlDatabase.md) - 核心数据库类
- [XmlNode](./XmlNode.md) - XML节点实现
- [IXmlNode](./IXmlNode.md) - XML节点接口
- [XmlNodeReference](./XmlNodeReference.md) - XML节点引用
- [IXmlNodeReference](./IXmlNodeReference.md) - XML节点引用接口
- [IndexedRepository](./IndexedRepository.md) - 索引仓储
- [IIndexable](./IIndexable.md) - 可索引对象接口
- [IXmlSerializable](./IXmlSerializable.md) - XML序列化接口
- [XmlDatabaseExtensions](./XmlDatabaseExtensions.md) - 数据库扩展方法 