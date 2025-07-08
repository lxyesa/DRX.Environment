# SqliteUnified 统一数据库操作类

## 概述

`SqliteUnified<T>` 是一个统一的 SQLite 数据库操作封装类，合并了原来 `Sqlite<T>` 和 `SqliteRelationship` 两个类的功能，提供了完整的数据库操作和关联表处理能力。

## 主要特性

### 基础功能
- ✅ 自动表结构创建和更新
- ✅ 基础 CRUD 操作（创建、读取、更新、删除）
- ✅ 批量操作支持
- ✅ 条件查询
- ✅ 数据修复功能

### 关联表功能
- ✅ 自动关联表数据保存
- ✅ 自动关联表数据加载
- ✅ 关联表数据修复
- ✅ 单个关联项操作
- ✅ 关联表条件查询

### 特性支持
- ✅ `[SqliteIgnore]` - 忽略不需要保存的属性
- ✅ `[SqliteRelation]` - 标记关联表属性
- ✅ JSON 序列化复杂类型
- ✅ 枚举类型支持
- ✅ 可空类型支持

## 快速开始

### 1. 定义数据模型

```csharp
public class User : IDataBase
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    
    // 关联表属性
    [SqliteRelation("Order", "UserId")]
    public List<Order> Orders { get; set; } = new List<Order>();
    
    // 忽略的属性
    [SqliteIgnore]
    public string TemporaryData { get; set; }
}

public class Order : IDataBase
{
    public int Id { get; set; }
    public int UserId { get; set; }  // 外键
    public string ProductName { get; set; }
    public decimal Amount { get; set; }
}
```

### 2. 基础操作

```csharp
// 初始化数据库
var userDb = new SqliteUnified<User>("users.db");

// 创建用户
var user = new User
{
    Id = 1,
    Username = "john_doe",
    Email = "john@example.com"
};

// 添加订单
user.Orders.Add(new Order
{
    Id = 1,
    UserId = 1,
    ProductName = "Laptop",
    Amount = 999.99m
});

// 保存（会自动保存关联的订单）
userDb.Save(user);

// 查询（会自动加载关联的订单）
var loadedUser = userDb.FindById(1);

// 条件查询
var users = userDb.Read(new Dictionary<string, object>
{
    { "Email", "john@example.com" }
});

// 删除
userDb.Delete(user);
```

### 3. 修复操作

```csharp
// 根据单个条件修复记录
bool wasUpdated = userDb.Repair(user, "Username", "john_doe");

// 根据多个条件修复记录
bool wasFound = userDb.Repair(user, new Dictionary<string, object>
{
    { "Username", "john_doe" },
    { "Email", "old@example.com" }
});
```

### 4. 关联表操作

```csharp
var userId = 1;

// 修复单个关联项
var updatedOrder = new Order
{
    UserId = userId,
    ProductName = "Laptop",
    Amount = 899.99m  // 更新价格
};
userDb.RepairRelationshipItem(userId, updatedOrder, "UserId", "ProductName", typeof(Order));

// 查询关联项
var expensiveOrders = userDb.QueryRelationship(userId, 
    new Dictionary<string, object> { { "Amount", 500m } }, 
    "UserId", 
    typeof(Order));

// 添加关联项
var newOrder = new Order { UserId = userId, ProductName = "Mouse", Amount = 29.99m };
userDb.AddRelationshipItem(userId, newOrder, "UserId", typeof(Order));

// 更新关联项
newOrder.Amount = 25.99m;
userDb.UpdateRelationshipItem(newOrder, typeof(Order));

// 删除关联项
userDb.DeleteRelationshipItem(newOrder, typeof(Order));
```

### 5. 批量操作

```csharp
var users = new List<User>
{
    new User { Id = 1, Username = "user1", Email = "user1@example.com" },
    new User { Id = 2, Username = "user2", Email = "user2@example.com" }
};

// 批量保存
userDb.SaveAll(users);

// 批量查询
var allUsers = userDb.Read();

// 条件删除
int deletedCount = userDb.DeleteWhere(new Dictionary<string, object>
{
    { "Username", "user1" }
});
```

## API 参考

### 构造函数
```csharp
public SqliteUnified(string path)
```

### 基础操作方法
- `Save(T item)` - 保存单个对象
- `SaveAll(IEnumerable<T> items)` - 批量保存对象
- `Read(Dictionary<string, object>? whereConditions = null)` - 条件查询
- `ReadSingle(string where, object value)` - 查询单个对象
- `FindById(int id)` - 根据ID查询
- `Delete(T item)` - 删除对象
- `DeleteWhere(Dictionary<string, object> whereConditions)` - 条件删除
- `Repair(T item, Dictionary<string, object> identifierConditions)` - 修复记录
- `Repair(T item, string propertyName, object propertyValue)` - 简单修复

### 关联表操作方法
- `RepairRelationship(...)` - 修复关联表数据
- `RepairRelationshipItem(...)` - 修复单个关联项
- `QueryRelationship(...)` - 查询关联项
- `UpdateRelationshipItem(...)` - 更新关联项
- `DeleteRelationshipItem(...)` - 删除关联项
- `AddRelationshipItem(...)` - 添加关联项

## 特性说明

### SqliteIgnoreAttribute
用于标记不需要保存到数据库的属性。

```csharp
[SqliteIgnore]
public string TemporaryData { get; set; }
```

### SqliteRelationAttribute
用于标记关联表属性，指定关联表名和外键属性。

```csharp
[SqliteRelation("Order", "UserId")]
public List<Order> Orders { get; set; }
```

参数说明：
- `tableName` - 关联表名（通常与关联类型名相同）
- `foreignKeyProperty` - 外键属性名

## 支持的数据类型

### 基础类型
- 整数类型：`int`, `long`, `short`, `uint`, `ulong`, `ushort`, `byte`, `sbyte`
- 浮点类型：`float`, `double`, `decimal`
- 其他类型：`bool`, `string`, `DateTime`, `Guid`, `byte[]`
- 枚举类型
- 可空类型

### 复杂类型
- 集合类型（自动 JSON 序列化）
- 字典类型（自动 JSON 序列化）
- 自定义类型（自动 JSON 序列化）

## 事务处理

所有操作都自动使用事务处理，确保数据一致性：
- 保存操作包含主表和关联表的事务
- 批量操作使用单个事务
- 操作失败时自动回滚

## 注意事项

1. **接口约束**：泛型类型 `T` 必须实现 `IDataBase` 接口并有无参构造函数
2. **主键要求**：`IDataBase.Id` 属性作为主键，必须是 `int` 类型
3. **关联表约束**：关联表中的对象也必须实现 `IDataBase` 接口
4. **外键命名**：关联表中的外键属性名必须与 `SqliteRelationAttribute` 中指定的名称一致
5. **线程安全**：每个实例不是线程安全的，多线程环境下请为每个线程创建独立实例

## 迁移指南

### 从 Sqlite<T> 迁移
直接替换类名即可，API 完全兼容：
```csharp
// 原来
var db = new Sqlite<User>("users.db");

// 现在
var db = new SqliteUnified<User>("users.db");
```

### 从 SqliteRelationship 迁移
关联表操作现在集成在主类中：
```csharp
// 原来
var relationDb = new SqliteRelationship("users.db");
relationDb.SaveRelationship<User, Order>(userId, orders, "Order", "UserId");

// 现在（在 Save 方法中自动处理，也可以单独操作）
userDb.RepairRelationship(userId, orders, "Order", "UserId", "SomeProperty", typeof(Order));
```

## 示例项目

完整示例请查看 `SqliteUnifiedExample.cs` 文件，包含：
- 基础操作示例
- 修复操作示例  
- 关联表操作示例
- 批量操作示例
