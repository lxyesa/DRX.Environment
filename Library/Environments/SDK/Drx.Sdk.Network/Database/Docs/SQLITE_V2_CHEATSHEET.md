# SQLite V2 快速参考卡片

## 常用代码片段速查

### 初始化

```csharp
// 创建 ORM 实例
var db = new SqliteV2<User>("Data Source=app.db");

// 创建表
db.CreateTableIfNotExists();

// 删除表
db.DropTable();
```

---

## CRUD 操作

### 创建 (Create)

```csharp
// 单条插入
var user = new User { Name = "Alice", Email = "alice@example.com" };
db.Insert(user);

// 批量插入
var users = new List<User> { /* ... */ };
db.InsertBatch(users);

// 异步批量插入
await db.InsertBatchAsync(users, batchSize: 1000);
```

### 读取 (Read)

```csharp
// 查询所有
var all = db.SelectAll().ToList();

// 按 ID 查询
var user = db.SelectById(1);

// 条件查询
var active = db.SelectWhere(u => u.IsActive);

// 异步查询
var user = await db.SelectByIdAsync(1);

// 流式查询
await foreach (var u in db.SelectAllStreamAsync())
{
    // 处理
}

// 计数
int total = db.Count();
int activeCount = db.CountWhere(u => u.IsActive);
```

### 更新 (Update)

```csharp
// 单条更新
user.Age = 31;
db.Update(user);

// 批量更新
db.UpdateBatch(users);

// 异步批量更新
await db.UpdateBatchAsync(users);

// 条件更新
db.UpdateWhere(u => u.IsActive == false, u => u.LastLogin = DateTime.Now);
```

### 删除 (Delete)

```csharp
// 按 ID 删除
db.DeleteById(1);

// 批量删除
db.DeleteBatch(new[] { 1, 2, 3 });

// 条件删除
db.DeleteWhere(u => u.IsActive == false);

// 删除所有
db.DeleteAll();

// 异步删除
await db.DeleteByIdAsync(1);
```

---

## LINQ 查询

```csharp
// Where
var result = db.SelectWhere(u => u.Age > 18);

// FirstOrDefault
var first = db.SelectWhere(u => u.Name == "Alice").FirstOrDefault();

// Any
bool exists = db.SelectWhere(u => u.Id == 1).Any();

// Count
int count = db.SelectWhere(u => u.IsActive).Count();

// Select
var names = db.SelectAll().Select(u => u.Name).ToList();

// OrderBy
var sorted = db.SelectAll().OrderBy(u => u.Age).ToList();

// GroupBy
var grouped = db.SelectAll().GroupBy(u => u.Status).ToList();

// Sum
decimal total = db.SelectAll().Sum(u => u.Balance);

// Average
double avg = db.SelectAll().Average(u => u.Age);
```

---

## 事务管理

```csharp
// 基础事务
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

// 手动回滚
await uow.RollbackAsync();
```

---

## 子表操作

```csharp
// 获取主表
var player = db.SelectById(1);

// 添加子表项
player.Achievements.Add(new Achievement { Title = "First Win" });

// 批量添加
player.Achievements.AddRange(achievements);

// 移除子表项
player.Achievements.Remove(achievement);

// 清空子表
player.Achievements.Clear();

// 查询子表
var unlocked = player.Achievements
    .Where(a => a.IsUnlocked)
    .ToList();

// 子表计数
int count = player.Achievements.Count();

// 异步枚举子表
await foreach (var achievement in player.Achievements)
{
    // 处理
}

// 同步到数据库
await db.UpdateAsync(player);
```

---

## 批处理缓冲

```csharp
// 创建缓冲区
var buffer = new SqliteBatchBuffer<User>(db, batchSize: 1000);

// 添加项
buffer.Add(user1);
buffer.Add(user2);

// 自动批处理，当缓冲区满时自动刷新

// 手动刷新
await buffer.FlushAsync();

// 获取缓冲区项数
int count = buffer.GetBufferedCount();

// 清空缓冲区
buffer.Clear();
```

---

## 条件查询模式

```csharp
// 简单条件
db.SelectWhere(u => u.Age > 18)

// 多条件 AND
db.SelectWhere(u => u.Age > 18 && u.IsActive)

// 多条件 OR
db.SelectWhere(u => u.Status == "VIP" || u.Balance > 1000)

// 字符串操作
db.SelectWhere(u => u.Name.Contains("John"))
db.SelectWhere(u => u.Email.EndsWith("@example.com"))
db.SelectWhere(u => u.Name.StartsWith("A"))

// 数值比较
db.SelectWhere(u => u.Age >= 18 && u.Age <= 65)

// 日期比较
db.SelectWhere(u => u.CreatedAt > DateTime.Now.AddDays(-7))

// NULL 检查
db.SelectWhere(u => u.MiddleName != null)
db.SelectWhere(u => u.DeletedAt == null)
```

---

## 异步操作

```csharp
// 异步单个操作
var user = await db.SelectByIdAsync(1);
await db.InsertAsync(user);
await db.UpdateAsync(user);
await db.DeleteByIdAsync(1);

// 异步批量操作
await db.InsertBatchAsync(users);
await db.UpdateBatchAsync(users);
await db.DeleteBatchAsync(ids);

// 异步条件查询
var active = await db.SelectWhereAsync(u => u.IsActive);

// 异步流式查询
await foreach (var user in db.SelectAllStreamAsync())
{
    // 处理
}

// 异步事务
using var uow = new SqliteUnitOfWork<User>(db);
await uow.BeginTransactionAsync();
await uow.CommitAsync();
```

---

## 性能优化

```csharp
// ✅ 快速：批量操作
await db.InsertBatchAsync(items, batchSize: 1000);

// ❌ 慢速：逐条操作
foreach (var item in items)
{
    db.Insert(item);
}

// ✅ 快速：流式查询
await foreach (var item in db.SelectAllStreamAsync())
{
    ProcessItem(item);
}

// ❌ 慢速：一次性加载
var all = db.SelectAll().ToList();

// ✅ 快速：条件查询
db.SelectWhere(u => u.IsActive)

// ❌ 慢速：加载后过滤
db.SelectAll().Where(u => u.IsActive).ToList()

// ✅ 快速：使用缓冲区
var buffer = new SqliteBatchBuffer<User>(db);
foreach (var item in items)
{
    buffer.Add(item);
}
await buffer.FlushAsync();
```

---

## 错误处理

```csharp
// 检查 null
var user = db.SelectById(1);
if (user != null)
{
    // 处理
}

// 使用 null 合并
var name = user?.Name ?? "Unknown";

// 使用 FirstOrDefault
var user = db.SelectWhere(u => u.Id == 1).FirstOrDefault();

// 异常处理
try
{
    db.Insert(user);
}
catch (SqliteException ex)
{
    Console.WriteLine($"数据库错误: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"操作错误: {ex.Message}");
}
```

---

## 数据模型

```csharp
// 主表
public class User : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public string TableName => "users";
}

// 子表
public class Order : IDataTableV2
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int ParentId { get; set; }
    public string OrderNumber { get; set; }
    public decimal Amount { get; set; }

    public string TableName => "orders";
}

// 主表包含子表
public class Customer : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; }
    public TableList<Order> Orders { get; set; } = new();

    public string TableName => "customers";
}
```

---

## 常用属性

```csharp
// 获取表名
string tableName = db.GetTableName();

// 获取连接字符串
string connStr = db.ConnectionString;

// 子表同步状态
bool isSynced = tableList.IsSynchronized;

// 子表项数
int count = tableList.Count;

// 子表是否为空
bool isEmpty = tableList.Count == 0;
```

---

## 调试技巧

```csharp
// 性能计时
var sw = Stopwatch.StartNew();
var result = db.SelectAll().ToList();
sw.Stop();
Console.WriteLine($"耗时: {sw.ElapsedMilliseconds}ms");

// 内存监测
var before = GC.GetTotalMemory(true);
var result = db.SelectAll().ToList();
var after = GC.GetTotalMemory(true);
Console.WriteLine($"内存增长: {(after - before) / 1024 / 1024}MB");

// 查询计数
int count = db.Count();
Console.WriteLine($"总记录数: {count}");

// 条件计数
int activeCount = db.CountWhere(u => u.IsActive);
Console.WriteLine($"活跃用户: {activeCount}");
```

---

## 连接字符串示例

```csharp
// 基础
"Data Source=app.db"

// 增加超时
"Data Source=app.db;Timeout=30"

// WAL 模式（更好的并发）
"Data Source=app.db;Mode=Wal"

// 只读
"Data Source=app.db;Mode=ReadOnly"

// 内存数据库
"Data Source=:memory:"
```

---

## 常见模式

### 分页查询
```csharp
int pageSize = 10;
int pageNumber = 1;
var page = db.SelectAll()
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToList();
```

### 搜索
```csharp
string keyword = "john";
var results = db.SelectWhere(u =>
    u.Name.Contains(keyword) ||
    u.Email.Contains(keyword));
```

### 排序
```csharp
var sorted = db.SelectAll()
    .OrderByDescending(u => u.CreatedAt)
    .ToList();
```

### 去重
```csharp
var unique = db.SelectAll()
    .DistinctBy(u => u.Email)
    .ToList();
```

### 统计
```csharp
var stats = new
{
    Total = db.Count(),
    Active = db.CountWhere(u => u.IsActive),
    Inactive = db.CountWhere(u => !u.IsActive)
};
```
