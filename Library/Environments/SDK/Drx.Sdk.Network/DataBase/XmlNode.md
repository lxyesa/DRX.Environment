# XmlNode 类

`XmlNode` 是XML数据库系统的核心组件之一，实现了 `IXmlNode` 接口，提供了对XML节点的各种操作。它封装了底层的 `XmlElement` 对象，提供了更高级的数据操作功能。

## 基本信息

- **命名空间**: `Drx.Sdk.Network.DataBase`
- **类型**: 公共类
- **实现接口**: `IXmlNode`
- **主要职责**: 封装XML节点操作，提供数据读写功能

## 构造函数

```csharp
public XmlNode(XmlElement element, IXmlNode parent = null)
```

创建一个新的XML节点实例。

**参数**:
- `element`: 底层的XML元素
- `parent`: 父节点（可选）

```csharp
public XmlNode(XmlElement element, XmlDocument document, string filePath, XmlDatabase database, XmlNode parent = null)
```

创建一个新的XML节点实例，用于XmlDatabase内部使用。

**参数**:
- `element`: 底层的XML元素
- `document`: XML文档
- `filePath`: 文件路径
- `database`: 数据库实例
- `parent`: 父节点（可选）

## 属性

### Name

```csharp
public string Name { get; }
```

获取节点的名称。

### Parent

```csharp
public IXmlNode Parent { get; }
```

获取节点的父节点。

### IsDirty

```csharp
public bool IsDirty { get; }
```

获取节点是否已被修改。

## 主要方法

### 数据写入方法

#### PushString

```csharp
public IXmlNode PushString(string nodeName, string keyName, params string[] values)
```

向节点添加字符串类型数据。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称
- `values`: 字符串值数组

**返回值**:
- 当前节点实例，支持链式调用

**示例**:
```csharp
node.PushString("user", "name", "John Doe");
node.PushString("tags", "keywords", "xml", "database", "storage");
```

#### PushInt

```csharp
public IXmlNode PushInt(string nodeName, string keyName, params int[] values)
```

向节点添加整数类型数据。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称
- `values`: 整数值数组

**返回值**:
- 当前节点实例，支持链式调用

**示例**:
```csharp
node.PushInt("user", "age", 30);
node.PushInt("scores", "points", 10, 20, 30, 40);
```

#### PushFloat

```csharp
public IXmlNode PushFloat(string nodeName, string keyName, params float[] values)
```

向节点添加浮点数类型数据。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称
- `values`: 浮点数值数组

**返回值**:
- 当前节点实例，支持链式调用

**示例**:
```csharp
node.PushFloat("product", "price", 19.99f);
node.PushFloat("coordinates", "position", 10.5f, 20.3f, 30.7f);
```

#### PushBool

```csharp
public IXmlNode PushBool(string nodeName, string keyName, bool value)
```

向节点添加布尔类型数据。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称
- `value`: 布尔值

**返回值**:
- 当前节点实例，支持链式调用

**示例**:
```csharp
node.PushBool("settings", "enabled", true);
```

#### PushDecimal

```csharp
public IXmlNode PushDecimal(string nodeName, string keyName, decimal value)
```

向节点添加十进制数类型数据。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称
- `value`: 十进制数值

**返回值**:
- 当前节点实例，支持链式调用

**示例**:
```csharp
node.PushDecimal("finance", "amount", 1234.56m);
```

#### PushNode

```csharp
public IXmlNode PushNode(string nodeName)
```

创建子节点。

**参数**:
- `nodeName`: 子节点名称

**返回值**:
- 新创建的子节点

**示例**:
```csharp
var childNode = node.PushNode("child");
childNode.PushString("data", "value", "some value");
```

#### PushNode<T>

```csharp
public IXmlNode PushNode<T>(string nodeName, List<T> values) where T : IXmlSerializable, new()
```

创建带有数据列表的子节点。

**参数**:
- `nodeName`: 子节点名称
- `values`: 可序列化对象列表

**返回值**:
- 新创建的子节点

**示例**:
```csharp
var users = new List<User> { new User { Name = "John" }, new User { Name = "Jane" } };
node.PushNode("users", users);
```

#### PushReference

```csharp
public IXmlNodeReference PushReference(string nodeName, string path)
```

创建对外部XML文件的引用。

**参数**:
- `nodeName`: 子节点名称
- `path`: 外部XML文件路径

**返回值**:
- 引用节点

**示例**:
```csharp
var reference = node.PushReference("config", "configs/settings.xml");
```

### 数据读取方法

#### GetString

```csharp
public string GetString(string nodeName, string keyName, string defaultValue = "")
```

获取字符串类型数据。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称
- `defaultValue`: 默认值（可选）

**返回值**:
- 字符串值，如果不存在则返回默认值

**示例**:
```csharp
string name = node.GetString("user", "name", "Unknown");
```

#### GetStringArray

```csharp
public string[] GetStringArray(string nodeName, string keyName)
```

获取字符串数组。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称

**返回值**:
- 字符串数组

**示例**:
```csharp
string[] tags = node.GetStringArray("metadata", "tags");
```

#### GetInt

```csharp
public int GetInt(string nodeName, string keyName, int defaultValue = 0)
```

获取整数类型数据。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称
- `defaultValue`: 默认值（可选）

**返回值**:
- 整数值，如果不存在则返回默认值

**示例**:
```csharp
int age = node.GetInt("user", "age", 18);
```

#### GetIntArray

```csharp
public int[] GetIntArray(string nodeName, string keyName)
```

获取整数数组。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称

**返回值**:
- 整数数组

**示例**:
```csharp
int[] scores = node.GetIntArray("game", "scores");
```

#### GetFloat

```csharp
public float GetFloat(string nodeName, string keyName, float defaultValue = 0.0f)
```

获取浮点数类型数据。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称
- `defaultValue`: 默认值（可选）

**返回值**:
- 浮点数值，如果不存在则返回默认值

**示例**:
```csharp
float price = node.GetFloat("product", "price", 0.0f);
```

#### GetFloatArray

```csharp
public float[] GetFloatArray(string nodeName, string keyName)
```

获取浮点数数组。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称

**返回值**:
- 浮点数数组

**示例**:
```csharp
float[] coordinates = node.GetFloatArray("position", "coords");
```

#### GetBool

```csharp
public bool GetBool(string nodeName, string keyName, bool defaultValue = false)
```

获取布尔类型数据。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称
- `defaultValue`: 默认值（可选）

**返回值**:
- 布尔值，如果不存在则返回默认值

**示例**:
```csharp
bool enabled = node.GetBool("settings", "enabled", false);
```

#### GetIntNullable

```csharp
public int? GetIntNullable(string nodeName, string keyName)
```

获取可空整数类型数据。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称

**返回值**:
- 整数值，如果不存在则返回null

**示例**:
```csharp
int? optionalValue = node.GetIntNullable("data", "value");
if (optionalValue.HasValue)
{
    Console.WriteLine($"Value: {optionalValue.Value}");
}
```

#### GetDecimalNullable

```csharp
public decimal? GetDecimalNullable(string nodeName, string keyName)
```

获取可空十进制数类型数据。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称

**返回值**:
- 十进制数值，如果不存在则返回null

**示例**:
```csharp
decimal? amount = node.GetDecimalNullable("transaction", "amount");
```

#### GetNode

```csharp
public IXmlNode GetNode(string nodeName)
```

获取子节点。

**参数**:
- `nodeName`: 子节点名称

**返回值**:
- 子节点，如果不存在则返回null

**示例**:
```csharp
var settingsNode = node.GetNode("settings");
if (settingsNode != null)
{
    bool darkMode = settingsNode.GetBool("theme", "darkMode");
}
```

#### GetList<T>

```csharp
public List<T> GetList<T>(string nodeName) where T : IXmlSerializable, new()
```

获取对象列表。

**参数**:
- `nodeName`: 子节点名称

**返回值**:
- 对象列表

**示例**:
```csharp
var users = node.GetList<User>("users");
foreach (var user in users)
{
    Console.WriteLine(user.Name);
}
```

#### GetReference

```csharp
public IXmlNodeReference GetReference(string nodeName)
```

获取引用节点。

**参数**:
- `nodeName`: 子节点名称

**返回值**:
- 引用节点，如果不是引用则返回null

**示例**:
```csharp
var configRef = node.GetReference("config");
if (configRef != null)
{
    var configNode = configRef.ResolveReference();
    // 使用配置节点...
}
```

#### GetChildren

```csharp
public IEnumerable<IXmlNode> GetChildren()
```

获取所有子节点。

**返回值**:
- 子节点列表

**示例**:
```csharp
foreach (var child in node.GetChildren())
{
    Console.WriteLine($"Child node: {child.Name}");
}
```

### 路径操作方法

#### GetOrCreateNode

```csharp
public IXmlNode GetOrCreateNode(string path)
```

获取或创建子节点。

**参数**:
- `path`: 节点路径，格式为"node1/node2/node3"

**返回值**:
- 子节点

**示例**:
```csharp
var deepNode = node.GetOrCreateNode("settings/display/theme");
deepNode.PushString("color", "background", "#FFFFFF");
```

#### GetNodeByPath

```csharp
public IXmlNode GetNodeByPath(string path)
```

获取节点（通过路径）。

**参数**:
- `path`: 节点路径，格式为"node1/node2/node3"

**返回值**:
- 子节点，如果不存在则返回null

**示例**:
```csharp
var themeNode = node.GetNodeByPath("settings/display/theme");
if (themeNode != null)
{
    string background = themeNode.GetString("color", "background");
}
```

### 序列化方法

#### SerializeList<T>

```csharp
public IXmlNode SerializeList<T>(string nodeName, IEnumerable<T> items) where T : IXmlSerializable, new()
```

将对象列表序列化到XML节点。

**参数**:
- `nodeName`: 节点名称
- `items`: 对象列表

**返回值**:
- 当前节点实例，支持链式调用

**示例**:
```csharp
var users = new List<User> { new User { Name = "John" }, new User { Name = "Jane" } };
node.SerializeList("users", users);
```

#### DeserializeList<T>

```csharp
public List<T> DeserializeList<T>(string nodeName) where T : IXmlSerializable, new()
```

从XML节点反序列化对象列表。

**参数**:
- `nodeName`: 节点名称

**返回值**:
- 对象列表

**示例**:
```csharp
var users = node.DeserializeList<User>("users");
```

### 其他方法

#### Push

```csharp
public IXmlNode Push(string nodeName, string keyName, object value)
```

向节点添加通用对象数据，根据对象类型自动选择适当的Push方法。

**参数**:
- `nodeName`: 子节点名称
- `keyName`: 属性名称
- `value`: 对象值

**返回值**:
- 当前节点实例，支持链式调用

**示例**:
```csharp
node.Push("data", "value", 123);       // 使用PushInt
node.Push("data", "name", "test");     // 使用PushString
node.Push("data", "enabled", true);    // 使用PushBool
node.Push("data", "price", 19.99f);    // 使用PushFloat
```

#### Save

```csharp
public void Save()
```

保存更改到文件。

**示例**:
```csharp
node.PushString("user", "name", "John");
node.Save(); // 保存更改到磁盘
```

#### GetUnderlyingElement

```csharp
public XmlElement GetUnderlyingElement()
```

获取底层XML元素。

**返回值**:
- 底层的`XmlElement`对象

**示例**:
```csharp
XmlElement element = node.GetUnderlyingElement();
// 直接使用XmlElement API
```

## 内部类

### XmlNodeReferenceImpl

`XmlNodeReferenceImpl` 是 `XmlNode` 的内部类，实现了 `IXmlNodeReference` 接口，用于处理对外部XML文件的引用。

## 使用场景

### 基本数据操作

```csharp
var database = new XmlDatabase();
var rootNode = database.CreateRoot("data.xml");

// 写入各种类型的数据
rootNode.PushString("user", "name", "John Doe");
rootNode.PushInt("user", "age", 30);
rootNode.PushBool("settings", "darkMode", true);
rootNode.PushFloat("position", "coordinates", 10.5f, 20.3f);

// 读取数据
string name = rootNode.GetString("user", "name");
int age = rootNode.GetInt("user", "age");
bool darkMode = rootNode.GetBool("settings", "darkMode");
float[] coords = rootNode.GetFloatArray("position", "coordinates");

database.SaveChanges();
```

### 嵌套节点操作

```csharp
var rootNode = database.CreateRoot("config.xml");

// 创建嵌套节点
var appNode = rootNode.PushNode("application");
appNode.PushString("info", "name", "MyApp");
appNode.PushString("info", "version", "1.0.0");

var settingsNode = appNode.PushNode("settings");
settingsNode.PushBool("display", "showToolbar", true);
settingsNode.PushString("theme", "color", "blue");

// 通过路径访问深层节点
var themeNode = rootNode.GetNodeByPath("application/settings/theme");
if (themeNode != null)
{
    string color = themeNode.GetString("color");
}

database.SaveChanges();
```

### 对象序列化

```csharp
public class User : IXmlSerializable
{
    public string Name { get; set; }
    public int Age { get; set; }
    
    public void WriteToXml(IXmlNode node)
    {
        node.PushString("info", "name", Name);
        node.PushInt("info", "age", Age);
    }
    
    public void ReadFromXml(IXmlNode node)
    {
        Name = node.GetString("info", "name");
        Age = node.GetInt("info", "age");
    }
}

// 序列化对象列表
var users = new List<User>
{
    new User { Name = "John", Age = 30 },
    new User { Name = "Jane", Age = 25 }
};

var rootNode = database.CreateRoot("users.xml");
rootNode.SerializeList("users", users);

// 反序列化对象列表
var loadedUsers = rootNode.DeserializeList<User>("users");
```

## 注意事项

- 节点操作会标记节点为"脏"状态，需要调用 `Save()` 或 `database.SaveChanges()` 保存更改
- 获取不存在的节点或属性时，会返回默认值或null，不会抛出异常
- 路径操作使用"/"分隔节点名称，可以访问或创建多层嵌套节点
- 对于数组类型的数据，内部使用逗号分隔的字符串存储 