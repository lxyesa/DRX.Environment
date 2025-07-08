# IndexedRepository 类

`IndexedRepository<T>` 是一个泛型类，提供了简化的对象存储和检索功能，特别适用于需要通过唯一标识符访问对象的场景。它封装了XML数据库的索引系统，使开发者能够轻松实现对象的持久化存储。

## 基本信息

- **命名空间**: `Drx.Sdk.Network.DataBase`
- **类型**: 公共泛型类
- **类型参数**: `T` - 必须实现 `IIndexable`, `IXmlSerializable` 接口，并有一个无参构造函数
- **主要职责**: 提供对象的存储、检索和管理功能

## ExposedValue 类

`ExposedValue` 类用于配置在索引中暴露的对象内部值，使得可以通过这些值进行查找。

### 属性

- `Name`: 暴露值的名称，用作索引中的键
- `NodeName`: XML节点名称，指定要从中获取值的节点
- `KeyName`: XML属性名称，指定要获取的属性

### 构造函数

```csharp
public ExposedValue(string name, string nodeName, string keyName)
```

创建一个新的暴露值配置。

**参数**:
- `name`: 暴露值的名称
- `nodeName`: XML节点名称
- `keyName`: XML属性名称

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
- `_exposedValues`: 暴露值配置的字典

## 主要方法

### 暴露值相关方法

#### AddExposedValue

```csharp
public void AddExposedValue(ExposedValue exposedValue)
```

添加一个暴露值配置，使得可以通过该值进行查找。

**参数**:
- `exposedValue`: 暴露值配置

**示例**:
```csharp
var exposedValue = new ExposedValue("email", "info", "email");
userRepo.AddExposedValue(exposedValue);
```

```csharp
public void AddExposedValue(string name, string nodeName, string keyName)
```

添加一个暴露值配置，使得可以通过该值进行查找。

**参数**:
- `name`: 暴露值的名称
- `nodeName`: XML节点名称
- `keyName`: XML属性名称

**示例**:
```csharp
userRepo.AddExposedValue("email", "info", "email");
```

#### RemoveExposedValue

```csharp
public bool RemoveExposedValue(string name)
```

移除一个暴露值配置。

**参数**:
- `name`: 要移除的暴露值名称

**返回值**:
- 如果成功移除则返回true，否则返回false

**示例**:
```csharp
bool removed = userRepo.RemoveExposedValue("email");
```

#### GetByExposedValue

```csharp
public List<T> GetByExposedValue(string exposedValueName, string value)
```

通过暴露值查找对象。

**参数**:
- `exposedValueName`: 暴露值的名称
- `value`: 要查找的值

**返回值**:
- 匹配的对象列表

**示例**:
```csharp
var users = userRepo.GetByExposedValue("email", "john@example.com");
```

#### GetSingleByExposedValue

```csharp
public T GetSingleByExposedValue(string exposedValueName, string value)
```

通过暴露值查找单个对象（如果有多个匹配项，则返回第一个）。

**参数**:
- `exposedValueName`: 暴露值的名称
- `value`: 要查找的值

**返回值**:
- 匹配的对象，如果没有找到则返回null

**示例**:
```csharp
var user = userRepo.GetSingleByExposedValue("email", "john@example.com");
```

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

### Update

```csharp
public void Update(T item)
```

更新单个对象，但不立即保存更改。此方法适用于需要批量更新多个对象时提高性能。

**参数**:
- `item`: 要更新的对象

**行为**:
- 如果对象ID已存在，则更新现有对象
- 如果对象ID不存在，则创建新对象
- 不自动保存更改到磁盘，需要手动调用 `SaveChanges()`

**示例**:
```csharp
// 批量更新多个对象
foreach (var user in usersToUpdate)
{
    userRepo.Update(user);
}
// 一次性保存所有更改
userRepo.SaveChanges();
```

### Remove

```csharp
public bool Remove(string id)
```

从存储库中删除指定ID的对象。

**参数**:
- `id`: 要删除的对象ID

**返回值**:
- 如果对象存在并被成功删除则返回true，否则返回false

**行为**:
- 检查对象是否存在
- 从索引中删除对象引用
- 删除对象的数据文件
- 自动保存更改到磁盘

**示例**:
```csharp
bool deleted = userRepo.Remove("12345");
if (deleted)
{
    Console.WriteLine("User was successfully deleted");
}
else
{
    Console.WriteLine("User not found");
}
```

### SaveChanges

```csharp
public void SaveChanges()
```

保存所有挂起的更改到磁盘。在调用多个Update方法后使用此方法一次性保存所有更改。

**行为**:
- 将内存中的所有更改写入磁盘
- 重置脏标志

**示例**:
```csharp
// 更新多个对象
userRepo.Update(user1);
userRepo.Update(user2);
userRepo.Update(user3);

// 一次性保存所有更改
userRepo.SaveChanges();
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

// 删除用户
bool deleted = userRepo.Remove("user1");
Console.WriteLine(deleted ? "User deleted" : "User not found");

// 获取所有用户
var allUsers = userRepo.GetAll();
foreach (var u in allUsers)
{
    Console.WriteLine($"ID: {u.Id}, Name: {u.Name}");
}

// 关闭存储库
userRepo.Close();
```

### 使用暴露值进行查找

```csharp
// 创建存储库
var userRepo = new IndexedRepository<User>("data/users");

// 添加暴露值配置
userRepo.AddExposedValue("email", "info", "email");
userRepo.AddExposedValue("role", "info", "role");

// 保存用户
var user1 = new User { Id = "1", Name = "John", Email = "john@example.com", Role = "admin" };
var user2 = new User { Id = "2", Name = "Jane", Email = "jane@example.com", Role = "user" };
userRepo.Save(user1);
userRepo.Save(user2);

// 通过暴露值查找用户
var adminUsers = userRepo.GetByExposedValue("role", "admin");
Console.WriteLine($"Found {adminUsers.Count} admin users");

// 通过暴露值查找单个用户
var johnUser = userRepo.GetSingleByExposedValue("email", "john@example.com");
if (johnUser != null)
{
    Console.WriteLine($"Found user: {johnUser.Name}");
}

// 移除暴露值配置
userRepo.RemoveExposedValue("role");
```

### 批量操作

```csharp
// 创建存储库
var productRepo = new IndexedRepository<Product>("data/products", "prod_");

// 批量更新产品
var productsToUpdate = productRepo.GetAll();
foreach (var product in productsToUpdate)
{
    product.Price *= 1.1m; // 提价10%
    productRepo.Update(product); // 不立即保存
}

// 一次性保存所有更改
productRepo.SaveChanges();

// 批量删除产品
var productsToDelete = productRepo.GetAll().Where(p => p.Price > 100).ToList();
foreach (var product in productsToDelete)
{
    productRepo.Remove(product.Id);
}
```

### 与自定义对象一起使用

```
```