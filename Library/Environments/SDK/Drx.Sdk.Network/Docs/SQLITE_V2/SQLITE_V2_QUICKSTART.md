# SQLite V2 快速开始指南

## 5 分钟快速上手

### 1. 安装

```bash
# 通过 NuGet 安装
dotnet add package Drx.Sdk.Network
```

### 2. 定义数据模型

```csharp
using Drx.Sdk.Network.DataBase.Sqlite.V2;

// 定义主表
public class User : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public int Age { get; set; }
    public bool IsActive { get; set; }

    public string TableName => "users";
}
```

### 3. 初始化数据库

```csharp
// 创建 ORM 实例
var db = new SqliteV2<User>("Data Source=app.db");

// 创建表
db.CreateTableIfNotExists();
```

### 4. 基本操作

```csharp
// 插入
var user = new User
{
    Name = "Alice",
    Email = "alice@example.com",
    Age = 25,
    IsActive = true
};
db.Insert(user);

// 查询
var alice = db.SelectById(1);
var allUsers = db.SelectAll().ToList();
var activeUsers = db.SelectWhere(u => u.IsActive).ToList();

// 更新
alice.Age = 26;
db.Update(alice);

// 删除
db.DeleteById(1);
```

### 5. 异步操作

```csharp
// 异步批量插入
var users = new List<User> { /* ... */ };
await db.InsertBatchAsync(users);

// 异步查询
var user = await db.SelectByIdAsync(1);

// 异步流式查询
await foreach (var u in db.SelectAllStreamAsync())
{
    Console.WriteLine(u.Name);
}
```

---

## 常见任务

### 批量插入 10000 条记录

```csharp
var users = Enumerable.Range(1, 10000)
    .Select(i => new User
    {
        Name = $"User{i}",
        Email = $"user{i}@example.com",
        Age = 20 + (i % 50),
        IsActive = i % 2 == 0
    })
    .ToList();

await db.InsertBatchAsync(users, batchSize: 1000);
```

### 条件查询

```csharp
// 简单条件
var adults = db.SelectWhere(u => u.Age >= 18);

// 复杂条件
var result = db.SelectWhere(u =>
    u.IsActive &&
    u.Age > 18 &&
    u.Email.Contains("@example.com"));
```

### 事务处理

```csharp
using var uow = new SqliteUnitOfWork<User>(db);
await uow.BeginTransactionAsync();

try
{
    uow.Add(newUser);
    uow.Modify(existingUser);
    uow.Remove(userToDelete);

    await uow.CommitAsync();
}
catch
{
    // 自动回滚
    throw;
}
```

### 子表操作

```csharp
public class Player : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; }
    public TableList<Achievement> Achievements { get; set; } = new();

    public string TableName => "players";
}

public class Achievement : IDataTableV2
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int ParentId { get; set; }
    public string Title { get; set; }
    public DateTime UnlockedAt { get; set; }

    public string TableName => "achievements";
}

// 使用
var player = db.SelectById(1);
player.Achievements.Add(new Achievement
{
    Title = "First Victory",
    UnlockedAt = DateTime.Now
});
await db.UpdateAsync(player);
```

---

## 性能提示

| 操作 | 推荐做法 | 避免做法 |
|------|---------|---------|
| 插入 1000+ 条 | `InsertBatchAsync()` | 循环 `Insert()` |
| 查询大数据集 | `SelectAllStreamAsync()` | `SelectAll().ToList()` |
| 更新多条 | `UpdateBatchAsync()` | 循环 `Update()` |
| 删除多条 | `DeleteBatchAsync()` | 循环 `DeleteById()` |
| 复杂操作 | `SqliteUnitOfWork` | 单个操作 |

---

## 下一步

- 📖 [完整指南](SQLITE_V2_GUIDE.md) - 深入了解所有特性
- 🔧 [API 参考](SQLITE_V2_API_REFERENCE.md) - 详细的 API 文档
- ⚡ [性能优化](SQLITE_V2_PERFORMANCE.md) - 性能调优技巧
- 📊 [子表系统](SQLITE_V2_SUBTABLE_SYSTEM.md) - 一对多关系管理
- 💾 [事务管理](SQLITE_V2_TRANSACTIONS.md) - 事务和工作单元模式
- ✅ [最佳实践](SQLITE_V2_BEST_PRACTICES.md) - 设计和编码建议
