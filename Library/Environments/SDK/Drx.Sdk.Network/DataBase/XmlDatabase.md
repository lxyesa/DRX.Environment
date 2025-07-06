# XmlDatabase 类

`XmlDatabase` 是XML数据库系统的核心类，负责创建、打开、管理和保存XML文件。它提供了对XML文档的基本操作，并维护文件的状态和缓存。

## 基本信息

- **命名空间**: `Drx.Sdk.Network.DataBase`
- **类型**: 公共类
- **主要职责**: 管理XML文件的生命周期和根节点操作

## 构造函数

```csharp
public XmlDatabase()
```

创建一个新的XML数据库实例，用于管理XML文件。

## 主要属性

该类没有公开的属性，内部维护以下私有字段:

- `_rootNodes`: 存储文件路径到根节点的映射字典
- `_documents`: 存储文件路径到XML文档的映射字典

## 主要方法

### CreateRoot

```csharp
public XmlNode CreateRoot(string filePath)
```

创建或打开XML文件的根节点。如果文件不存在，则创建一个新的XML文件。

**参数**:
- `filePath`: XML文件的路径

**返回值**:
- 文件的根节点 (`XmlNode` 对象)

**行为**:
1. 规范化文件路径
2. 检查是否已加载该文件
3. 如果文件存在，则加载它
4. 如果文件不存在，则创建新文件，包括XML声明和根元素
5. 创建并返回根节点对象

**示例**:
```csharp
var database = new XmlDatabase();
var rootNode = database.CreateRoot("data/config.xml");
```

### OpenRoot

```csharp
public XmlNode OpenRoot(string filePath)
```

打开现有的XML文件。如果文件不存在，则抛出异常。

**参数**:
- `filePath`: XML文件的路径

**返回值**:
- 文件的根节点 (`XmlNode` 对象)

**异常**:
- `FileNotFoundException`: 如果指定的文件不存在

**示例**:
```csharp
var database = new XmlDatabase();
try
{
    var rootNode = database.OpenRoot("data/settings.xml");
    // 使用根节点...
}
catch (FileNotFoundException)
{
    Console.WriteLine("设置文件不存在");
}
```

### SaveChanges

```csharp
public void SaveChanges()
```

将所有已修改的XML文件保存到磁盘。

**行为**:
- 遍历所有根节点，调用每个节点的 `Save()` 方法

**示例**:
```csharp
var database = new XmlDatabase();
var rootNode = database.CreateRoot("data/users.xml");
rootNode.PushString("user", "name", "John");
database.SaveChanges(); // 保存更改到磁盘
```

### CloseFile

```csharp
public void CloseFile(string filePath)
```

关闭指定的XML文件，保存更改并从内存中移除。

**参数**:
- `filePath`: 要关闭的XML文件路径

**行为**:
1. 规范化文件路径
2. 如果文件已加载，保存更改并从缓存中移除

**示例**:
```csharp
var database = new XmlDatabase();
var rootNode = database.CreateRoot("data/temp.xml");
// 使用文件...
database.CloseFile("data/temp.xml"); // 关闭并保存文件
```

### CloseAll

```csharp
public void CloseAll()
```

关闭所有打开的XML文件，保存更改并清除缓存。

**行为**:
1. 调用 `SaveChanges()` 保存所有更改
2. 清除根节点和文档缓存

**示例**:
```csharp
var database = new XmlDatabase();
// 打开多个文件...
database.CloseAll(); // 关闭所有文件
```

## 使用场景

### 基本文件操作

```csharp
// 创建数据库实例
var database = new XmlDatabase();

// 创建新文件
var configNode = database.CreateRoot("config.xml");
configNode.PushString("app", "name", "MyApp");
configNode.PushString("app", "version", "1.0.0");

// 保存更改
database.SaveChanges();

// 关闭文件
database.CloseFile("config.xml");
```

### 多文件管理

```csharp
var database = new XmlDatabase();

// 打开多个配置文件
var generalConfig = database.CreateRoot("configs/general.xml");
var userConfig = database.CreateRoot("configs/user.xml");
var systemConfig = database.CreateRoot("configs/system.xml");

// 修改配置
generalConfig.PushString("settings", "language", "zh-CN");
userConfig.PushString("profile", "username", "user123");

// 一次性保存所有更改
database.SaveChanges();

// 关闭所有文件
database.CloseAll();
```

## 注意事项

- `XmlDatabase` 会缓存已打开的文件，避免重复打开同一文件
- 调用 `SaveChanges()` 时，只会保存已修改的文件
- 为了避免资源泄漏，应在不再需要时调用 `CloseFile()` 或 `CloseAll()`
- 文件路径会被规范化，以确保正确识别相同文件 