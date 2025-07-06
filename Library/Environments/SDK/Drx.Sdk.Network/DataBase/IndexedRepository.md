# IndexedRepository 类

`IndexedRepository<T>` 是一个泛型类，提供了简化的对象存储和检索功能，特别适用于需要通过唯一标识符访问对象的场景。它封装了XML数据库的索引系统，使开发者能够轻松实现对象的持久化存储。

## 基本信息

- **命名空间**: `Drx.Sdk.Network.DataBase`
- **类型**: 公共泛型类
- **类型参数**: `T` - 必须实现 `IIndexable`, `IXmlSerializable` 接口，并有一个无参构造函数
- **主要职责**: 提供对象的存储、检索和管理功能

## 构造函数

```csharp
public IndexedRepository(string repositoryPath, string keyPrefix = "")
```

初始化 IndexedRepository 的新实例。

**参数**:
- `repositoryPath`: 存储数据和索引文件的根目录路径
- `keyPrefix`: 可选的键前缀，用于确保生成的键是有效的XML标签名（例如 "user_"）

**示例**:
```csharp
var userRepo = new IndexedRepository<User>("data/users", "user_");
```

## 内部字段

- `_database`: XmlDatabase 实例，用于底层数据存储
- `_repositoryPath`: 存储库的根目录路径
- `_indexFilePath`: 索引文件的路径
- `_keyPrefix`: 键前缀，用于生成有效的XML标签名
- `_config`: 索引系统配置

## 主要方法

### Get

```csharp
public T Get(string id)
```

通过ID从存储库中检索单个对象。

**参数**:
- `id`: 对象的唯一ID

**返回值**:
- 反序列化的对象，如果未找到则返回null

**示例**:
```csharp
var user = userRepo.Get("12345");
if (user != null)
{
    Console.WriteLine($"Found user: {user.Name}");
}
```

### GetAll

```csharp
public List<T> GetAll()
```

从存储库中检索所有对象。

**返回值**:
- 所有反序列化对象的列表

**示例**:
```csharp
var allUsers = userRepo.GetAll();
Console.WriteLine($"Total users: {allUsers.Count}");
foreach (var user in allUsers)
{
    Console.WriteLine($"User: {user.Name}");
}
```

### Save

```csharp
public void Save(T item)
```

保存或更新存储库中的单个对象。

**参数**:
- `item`: 要保存的对象

**行为**:
- 如果对象ID已存在，则更新现有对象
- 如果对象ID不存在，则创建新对象
- 自动保存更改到磁盘

**示例**:
```csharp
var user = new User { Id = "12345", Name = "John Doe" };
userRepo.Save(user);
```

### SaveAll

```csharp
public void SaveAll(IEnumerable<T> items)
```

将对象集合保存到存储库，覆盖任何现有索引。通常用于初始数据填充。

**参数**:
- `items`: 要保存的对象集合

**行为**:
- 完全覆盖旧索引和数据文件
- 为每个对象创建新的数据文件
- 自动保存更改到磁盘

**注意**:
- 此方法会删除现有的索引和数据文件，谨慎使用

**示例**:
```csharp
var users = new List<User>
{
    new User { Id = "1", Name = "John" },
    new User { Id = "2", Name = "Jane" },
    new User { Id = "3", Name = "Bob" }
};
userRepo.SaveAll(users);
```

### Close

```csharp
public void Close()
```

关闭底层数据库实例持有的所有文件句柄。

**行为**:
- 调用底层数据库的 `CloseAll()` 方法
- 释放所有文件资源

**示例**:
```csharp
userRepo.Close();
```

## 使用场景

### 基本的CRUD操作

```csharp
// 创建存储库
var userRepo = new IndexedRepository<User>("data/users");

// 创建和保存用户
var newUser = new User { Id = "user1", Name = "John Doe", Email = "john@example.com" };
userRepo.Save(newUser);

// 检索用户
var user = userRepo.Get("user1");
if (user != null)
{
    Console.WriteLine($"User: {user.Name}, Email: {user.Email}");
    
    // 更新用户
    user.Email = "john.doe@example.com";
    userRepo.Save(user);
}

// 获取所有用户
var allUsers = userRepo.GetAll();
foreach (var u in allUsers)
{
    Console.WriteLine($"ID: {u.Id}, Name: {u.Name}");
}

// 关闭存储库
userRepo.Close();
```

### 批量数据导入

```csharp
// 创建存储库
var productRepo = new IndexedRepository<Product>("data/products", "prod_");

// 准备批量数据
var products = new List<Product>();
for (int i = 1; i <= 100; i++)
{
    products.Add(new Product
    {
        Id = i.ToString(),
        Name = $"Product {i}",
        Price = 10.0m + i
    });
}

// 批量保存（会覆盖现有数据）
productRepo.SaveAll(products);

// 验证导入
var count = productRepo.GetAll().Count;
Console.WriteLine($"Imported {count} products");

// 关闭存储库
productRepo.Close();
```

### 与自定义对象一起使用

```csharp
// 定义符合要求的类
public class Customer : IIndexable, IXmlSerializable
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
    public DateTime RegistrationDate { get; set; }
    
    public void WriteToXml(IXmlNode node)
    {
        node.PushString("info", "name", Name);
        node.PushString("info", "address", Address);
        node.PushString("info", "registrationDate", RegistrationDate.ToString("o"));
    }
    
    public void ReadFromXml(IXmlNode node)
    {
        Name = node.GetString("info", "name");
        Address = node.GetString("info", "address");
        
        var dateStr = node.GetString("info", "registrationDate");
        if (DateTime.TryParse(dateStr, out var date))
        {
            RegistrationDate = date;
        }
    }
}

// 使用存储库
var customerRepo = new IndexedRepository<Customer>("data/customers");

// 添加客户
var customer = new Customer
{
    Id = "c001",
    Name = "ACME Corp",
    Address = "123 Main St",
    RegistrationDate = DateTime.Now
};
customerRepo.Save(customer);

// 检索客户
var retrievedCustomer = customerRepo.Get("c001");
```

## 工作原理

1. 存储库在指定目录中创建一个索引文件（`index.xml`）和多个数据文件
2. 每个对象存储在单独的XML文件中，文件名基于对象ID
3. 索引文件包含从对象ID到数据文件的映射
4. 读取对象时，存储库首先查找索引，然后加载相应的数据文件
5. 保存对象时，存储库更新数据文件和索引（如果需要）

## 文件结构示例

```
data/users/
  ├── index.xml          # 索引文件，包含ID到文件的映射
  ├── user_1.xml         # 用户1的数据文件
  ├── user_2.xml         # 用户2的数据文件
  └── user_3.xml         # 用户3的数据文件
```

## 注意事项

- 对象必须实现 `IIndexable` 接口，提供唯一ID
- 对象必须实现 `IXmlSerializable` 接口，处理XML序列化和反序列化
- 对象必须有一个无参构造函数，用于反序列化
- `SaveAll` 方法会删除所有现有数据，谨慎使用
- 为避免资源泄漏，应在不再需要存储库时调用 `Close()` 方法 