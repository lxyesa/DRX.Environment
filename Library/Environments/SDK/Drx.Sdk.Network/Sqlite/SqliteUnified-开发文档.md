# SqliteUnified<T> 开发文档

## 概述
`SqliteUnified<T>` 是一个基于 Microsoft.Data.Sqlite 的通用数据库操作类，支持主表与一对一/一对多关联表的自动管理。它适用于需要简单、高效、自动化 SQLite 数据存储的 .NET 应用。

---

## 主要特性
- 自动创建主表和关联表（支持一对一、一对多、数组、字典、链表等集合类型）
- 支持泛型实体，类型安全
- 支持基本 CRUD 操作（Push/Query/Update/Delete/GetAll）
- 支持异步（Async）与同步 API，灵活适配不同场景
- 事务保护，数据一致性强
- 自动同步和加载关联表及集合数据
- 支持属性自动映射，无需手写 SQL
- 自动表结构生成与变更兼容

---

## 快速上手

### 1. 定义数据模型（支持字典、链表、数组等集合类型）
```csharp
public class User : IDataBase
{
    public int Id { get; set; }
    public string TableName => null; // 可选，默认用类名
    public string Name { get; set; }
    public List<UserProfile> Profiles { get; set; } = new List<UserProfile>(); // 一对多

    // 新增：支持数组、字典、链表等集合类型
    public int[] Scores { get; set; } = new int[] { 90, 80, 100 };
    public Dictionary<string, UserProfile> ProfileDict { get; set; } = new Dictionary<string, UserProfile>
    {
        { "work", new UserProfile { Phone = "010-88888888", Address = "公司", Birthday = new DateTime(1990,1,1), Age = 33 } },
        { "home", new UserProfile { Phone = "010-66666666", Address = "家", Birthday = new DateTime(1990,2,2), Age = 33 } }
    };
    public LinkedList<string> Tags { get; set; } = new LinkedList<string>(new[] { "VIP", "测试", "活跃" });
}

public class UserProfile : IDataTable
{
    public int ParentId { get; set; } // 外键，自动赋值
    public string TableName => "UserProfile";
    public string Phone { get; set; }
    public string Address { get; set; }
    public DateTime Birthday { get; set; }
    public int Age { get; set; }
}
```

### 2. 初始化数据库
```csharp
var db = new SqliteUnified<User>("users.db");
```

### 3. 新增/保存数据（同步/异步）
```csharp
// 同步
var user = new User { Name = "张三" };
user.Profiles.Add(new UserProfile { Phone = "13800000000" });
db.Push(user); // 自动保存主表和所有子表

// 异步
await db.PushAsync(user, cancellationToken);
```

### 4. 查询数据
```csharp
// 同步
var users = db.Query("Name", "张三");
var user = db.QueryById(1);

// 异步
var usersAsync = await db.QueryAsync("Name", "张三");
var userAsync = await db.QueryByIdAsync(1);
```

### 5. 更新数据
```csharp
user.Name = "李四";
db.Update(user); // 同步
await db.UpdateAsync(user); // 异步
```

### 6. 删除数据
```csharp
db.Delete(user.Id); // 同步
await db.DeleteAsync(user.Id); // 异步
```

### 7. 获取所有数据
```csharp
var all = db.GetAll(); // 同步
var allAsync = await db.GetAllAsync(); // 异步
```

---

## 进阶用法

### 字典、链表、数组等集合类型支持

#### 1. 字典类型（Dictionary）
```csharp
public class User : IDataBase
{
    public int Id { get; set; }
    public Dictionary<string, UserProfile> ProfileDict { get; set; }
}
```
- 字典的 Key 支持基础类型（如 string、int），Value 支持基础类型或实现 IDataTable 的复杂类型。
- 每个字典会自动生成独立的子表，Key/Value 自动映射。

#### 2. 链表类型（LinkedList）
```csharp
public class User : IDataBase
{
    public int Id { get; set; }
    public LinkedList<string> Tags { get; set; }
}
```
- 支持基础类型和实现 IDataTable 的复杂类型元素。

#### 3. 数组类型
```csharp
public class User : IDataBase
{
    public int Id { get; set; }
    public int[] Scores { get; set; }
}
```
- 支持基础类型和实现 IDataTable 的复杂类型元素。

### 一对一关联
```csharp
public class User : IDataBase
{
    public int Id { get; set; }
    public UserSettings Settings { get; set; } // 只要类型实现 IDataTable 即可
}

public class UserSettings : IDataTable
{
    public int ParentId { get; set; }
    public string TableName => "UserSettings";
    public string Theme { get; set; }
}
```

### 一对多关联
```csharp
public class User : IDataBase
{
    public int Id { get; set; }
    public List<UserProfile> Profiles { get; set; } = new List<UserProfile>();
}
```

---

## 同步与异步 API 对比

| 功能         | 同步方法                | 异步方法（推荐高并发/界面无阻塞场景） |
|--------------|------------------------|--------------------------------------|
| 新增/保存    | Push                   | PushAsync                            |
| 查询         | Query/QueryById        | QueryAsync/QueryByIdAsync            |
| 更新         | Update                 | UpdateAsync                          |
| 删除         | Delete                 | DeleteAsync                          |
| 获取全部     | GetAll                 | GetAllAsync                          |

- 异步方法均支持 CancellationToken，适合 UI、服务端等高并发或需响应取消的场景。
- 所有异步方法内部均使用 Microsoft.Data.Sqlite 的异步 API。

---

## 接口说明

### IDataBase
- `int Id { get; set; }`：主键
- `string TableName { get; }`：可选，表名

### IDataTable
- `int ParentId { get; set; }`：父表主键，自动赋值
- `string TableName { get; }`：子表名

---

## 主要方法
- `Push(T entity)` / `PushAsync(T entity, CancellationToken)`：插入或更新主表及所有关联表、集合数据（包括字典、链表、数组）
- `Query(string propertyName, object value)` / `QueryAsync(...)`：按属性查询
- `QueryById(int id)` / `QueryByIdAsync(int id)`：按主键查询
- `Update(T entity)` / `UpdateAsync(T entity)`：更新主表及所有关联表、集合数据
- `Delete(int id)` / `DeleteAsync(int id)`：删除主表及所有关联表、集合数据
- `GetAll()` / `GetAllAsync()`：获取所有主表数据及其关联表、集合数据

---

## 底层机制与设计要点
- 支持属性类型为 `IDataTable`（一对一）、`List<IDataTable>`（一对多）、`Dictionary<K,V>`（字典）、`LinkedList<T>`（链表）、`T[]`（数组）自动识别和映射
- 所有操作自动开启事务，保证数据一致性（同步/异步均如此）
- 自动创建和维护主表及所有关联表、集合表结构，无需手动建表
- 只需定义 POCO 类并实现接口，无需手写 SQL
- 支持可空类型、基础类型、枚举类型
- 自动类型映射，支持 DateTime、bool、int、double、string 等
- 支持表结构变更自动兼容（如新增字段自动补全）

---

## 注意事项
- 泛型 T 必须实现 `IDataBase`，子表类型必须实现 `IDataTable`
- 主键 Id 必须为 int 类型
- 关联表属性必须为 `IDataTable`、`List<IDataTable>`、`Dictionary<K,V>`、`LinkedList<T>`、`T[]` 等集合类型
- 字典的 Key 必须为基础类型，Value 支持基础类型或实现 IDataTable 的类型
- 不支持多级嵌套关联（即子表不能再有子表，集合元素不能再包含集合）
- 不支持复杂类型自动序列化（如需支持请自行扩展）
- 异步方法需 .NET 6+ 和 Microsoft.Data.Sqlite 6.0+ 支持
- 若遇到表结构变更（如新增字段），建议重启应用或重新初始化数据库

---

## 常见问题与调试建议

**Q: 如何支持多表或多集合关联？**
A: 只需在主表类中添加多个 `IDataTable`、`List<IDataTable>`、`Dictionary<K,V>`、`LinkedList<T>`、`T[]` 属性即可，系统会自动建表和映射。

**Q: 字典、链表、数组等集合如何存储和读取？**
A: 每个集合类型会自动生成独立的子表，数据自动同步。读取时集合属性会自动还原为原始结构。

**Q: 如何忽略某些属性？**
A: 可用 `[NotMapped]` 或自定义扩展。

**Q: 支持异步吗？**
A: 已全面支持异步 API，所有主要操作均有 Async 版本。

**Q: 事务是否自动管理？**
A: 是，所有操作（同步/异步）均自动开启事务，异常时自动回滚。

**Q: 如何排查类型不兼容或表结构异常？**
A: 检查实体属性类型是否为基础类型、可空类型、枚举或实现了接口，避免复杂嵌套。表结构变更后建议重启应用。

**Q: 如何调试 SQL？**
A: 可在源码中断点 SqliteCommand 相关代码，或通过日志输出 SQL 语句。

---

## 参考
- 详见源码注释与 `README.md` 示例
- 适合 WinForms/WPF/控制台/服务端等 .NET 应用快速集成
- 官方文档：[Microsoft.Data.Sqlite](https://learn.microsoft.com/zh-cn/dotnet/standard/data/sqlite/)

---

如需更复杂的功能（如多级嵌套、自动迁移、属性忽略、JSON序列化等），可参考源码自行扩展。
