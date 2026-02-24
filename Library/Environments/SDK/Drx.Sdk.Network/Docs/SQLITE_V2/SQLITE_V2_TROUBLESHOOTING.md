# SQLite V2 故障排除与常见问题

## 目录
- [常见错误](#常见错误)
- [故障排除](#故障排除)
- [常见问题 FAQ](#常见问题-faq)
- [性能问题诊断](#性能问题诊断)
- [调试技巧](#调试技巧)

---

## 常见错误

### 1. InvalidOperationException: 事务未开始

**错误信息：**
```
System.InvalidOperationException: 事务未开始
```

**原因：**
```csharp
// ❌ 错误：没有调用 BeginTransactionAsync
var uow = new SqliteUnitOfWork<User>(db);
await uow.CommitAsync();  // 异常！
```

**解决方案：**
```csharp
// ✅ 正确：先开始事务
var uow = new SqliteUnitOfWork<User>(db);
await uow.BeginTransactionAsync();  // 必须先调用
await uow.CommitAsync();
```

### 2. SqliteException: 主键冲突

**错误信息：**
```
Microsoft.Data.Sqlite.SqliteException: UNIQUE constraint failed: users.id
```

**原因：**
```csharp
// ❌ 错误：插入重复的 ID
var user1 = new User { Id = 1, Name = "Alice" };
var user2 = new User { Id = 1, Name = "Bob" };

db.Insert(user1);
db.Insert(user2);  // 异常！ID 重复
```

**解决方案：**
```csharp
// ✅ 方案 1：让数据库自动生成 ID
var user = new User { Name = "Alice" };  // 不设置 ID
db.Insert(user);

// ✅ 方案 2：检查 ID 是否存在
if (db.SelectById(1) == null)
{
    db.Insert(user);
}

// ✅ 方案 3：使用 GUID 作为 ID
var user = new User { Id = Guid.NewGuid().GetHashCode(), Name = "Alice" };
db.Insert(user);
```

### 3. SqliteException: 数据库被锁定

**错误信息：**
```
Microsoft.Data.Sqlite.SqliteException: database is locked
```

**原因：**
```csharp
// ❌ 错误：多个连接同时写入
var db1 = new SqliteV2<User>("Data Source=app.db");
var db2 = new SqliteV2<User>("Data Source=app.db");

db1.Insert(user1);
db2.Insert(user2);  // 可能被锁定
```

**解决方案：**
```csharp
// ✅ 方案 1：使用单一数据库实例
var db = new SqliteV2<User>("Data Source=app.db");
db.Insert(user1);
db.Insert(user2);

// ✅ 方案 2：增加超时时间
var connectionString = "Data Source=app.db;Timeout=30";
var db = new SqliteV2<User>(connectionString);

// ✅ 方案 3：使用 WAL 模式
var connectionString = "Data Source=app.db;Mode=Wal";
var db = new SqliteV2<User>(connectionString);
```

### 4. NullReferenceException: 对象为空

**错误信息：**
```
System.NullReferenceException: Object reference not set to an instance of an object
```

**原因：**
```csharp
// ❌ 错误：没有检查查询结果
var user = db.SelectById(999);
Console.WriteLine(user.Name);  // 异常！user 为 null
```

**解决方案：**
```csharp
// ✅ 方案 1：检查 null
var user = db.SelectById(999);
if (user != null)
{
    Console.WriteLine(user.Name);
}

// ✅ 方案 2：使用 null 合并运算符
var user = db.SelectById(999);
Console.WriteLine(user?.Name ?? "未找到");

// ✅ 方案 3：使用 FirstOrDefault
var user = db.SelectWhere(u => u.Id == 999).FirstOrDefault();
if (user != null)
{
    Console.WriteLine(user.Name);
}
```

### 5. InvalidOperationException: 序列中没有元素

**错误信息：**
```
System.InvalidOperationException: Sequence contains no matching element
```

**原因：**
```csharp
// ❌ 错误：使用 First() 而不是 FirstOrDefault()
var user = db.SelectWhere(u => u.Id == 999).First();  // 异常！
```

**解决方案：**
```csharp
// ✅ 使用 FirstOrDefault()
var user = db.SelectWhere(u => u.Id == 999).FirstOrDefault();
if (user != null)
{
    // 处理
}
```

---

## 故障排除

### 问题：插入速度很慢

**诊断步骤：**

1. 检查是否使用了批量操作
```csharp
// ❌ 慢：逐条插入
foreach (var user in users)
{
    db.Insert(user);
}

// ✅ 快：批量插入
await db.InsertBatchAsync(users);
```

2. 检查批次大小
```csharp
// 调整批次大小
await db.InsertBatchAsync(users, batchSize: 500);
await db.InsertBatchAsync(users, batchSize: 2000);
```

3. 检查是否在事务中
```csharp
// ✅ 推荐：在事务中批量插入
using var uow = new SqliteUnitOfWork<User>(db);
await uow.BeginTransactionAsync();
await db.InsertBatchAsync(users);
await uow.CommitAsync();
```

### 问题：查询速度很慢

**诊断步骤：**

1. 检查是否加载了过多数据
```csharp
// ❌ 慢：加载所有数据
var allUsers = db.SelectAll().ToList();

// ✅ 快：流式处理
await foreach (var user in db.SelectAllStreamAsync())
{
    ProcessUser(user);
}
```

2. 检查查询条件
```csharp
// ❌ 慢：查询所有后过滤
var result = db.SelectAll()
    .Where(u => u.Age > 18)
    .ToList();

// ✅ 快：在数据库中过滤
var result = db.SelectWhere(u => u.Age > 18);
```

3. 添加索引
```csharp
// 在数据库中创建索引
// CREATE INDEX idx_age ON users(age);
// CREATE INDEX idx_email ON users(email);
```

### 问题：内存占用过高

**诊断步骤：**

1. 检查是否一次性加载大数据集
```csharp
// ❌ 高内存：加载 100 万条
var allUsers = db.SelectAll().ToList();

// ✅ 低内存：流式处理
await foreach (var user in db.SelectAllStreamAsync())
{
    ProcessUser(user);
}
```

2. 检查缓存大小
```csharp
// 定期清理缓存
db.ClearCache();
```

3. 使用内存分析工具
```csharp
var before = GC.GetTotalMemory(true);
// 执行操作
var after = GC.GetTotalMemory(true);
Console.WriteLine($"内存增长: {(after - before) / 1024 / 1024}MB");
```

### 问题：并发操作出错

**诊断步骤：**

1. 检查是否使用了异步操作
```csharp
// ❌ 可能出错：同步操作
var user1 = db.SelectById(1);
var user2 = db.SelectById(2);

// ✅ 推荐：异步操作
var user1 = await db.SelectByIdAsync(1);
var user2 = await db.SelectByIdAsync(2);
```

2. 使用事务隔离
```csharp
using var uow = new SqliteUnitOfWork<User>(db);
await uow.BeginTransactionAsync();
// 所有操作在同一事务中
await uow.CommitAsync();
```

---

## 常见问题 FAQ

### Q: 如何创建表？
A: 调用 `CreateTableIfNotExists()`
```csharp
var db = new SqliteV2<User>("Data Source=app.db");
db.CreateTableIfNotExists();
```

### Q: 如何删除表？
A: 调用 `DropTable()`
```csharp
db.DropTable();
```

### Q: 如何获取表名？
A: 调用 `GetTableName()`
```csharp
var tableName = db.GetTableName();
```

### Q: 如何处理 NULL 值？
A: 使用可空类型
```csharp
public class User : IDataBase
{
    public int Id { get; set; }
    public string? MiddleName { get; set; }  // 可为 null
    public int? Age { get; set; }            // 可为 null
}
```

### Q: 如何处理日期时间？
A: 使用 DateTime 或 long (Unix 时间戳)
```csharp
public class User : IDataBase
{
    public DateTime CreatedAt { get; set; }
    public long UpdatedAtUnix { get; set; }  // Unix 时间戳
}
```

### Q: 如何处理 GUID？
A: 使用 string 类型存储
```csharp
public class User : IDataBase
{
    public int Id { get; set; }
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
}
```

### Q: 如何处理 JSON 数据？
A: 序列化为 string
```csharp
using System.Text.Json;

public class User : IDataBase
{
    public int Id { get; set; }
    public string MetadataJson { get; set; }

    public Dictionary<string, object> GetMetadata()
    {
        return JsonSerializer.Deserialize<Dictionary<string, object>>(MetadataJson);
    }
}
```

### Q: 如何处理枚举？
A: 转换为 int 或 string
```csharp
public enum UserStatus { Active, Inactive, Deleted }

public class User : IDataBase
{
    public int Id { get; set; }
    public int Status { get; set; }  // 存储为 int

    public UserStatus GetStatus() => (UserStatus)Status;
}
```

### Q: 如何处理加密数据？
A: 在应用层加密/解密
```csharp
public class User : IDataBase
{
    private string _encryptedPassword;

    public void SetPassword(string password)
    {
        _encryptedPassword = EncryptionService.Encrypt(password);
    }

    public bool VerifyPassword(string password)
    {
        return EncryptionService.Verify(password, _encryptedPassword);
    }
}
```

---

## 性能问题诊断

### 性能检查清单

- [ ] 使用批量操作而不是逐条操作
- [ ] 使用流式查询处理大数据集
- [ ] 在数据库中过滤而不是在应用层
- [ ] 使用异步操作
- [ ] 使用事务处理多个操作
- [ ] 定期清理缓存
- [ ] 创建适当的数据库索引
- [ ] 避免 N+1 查询问题
- [ ] 使用连接池
- [ ] 监测内存使用

### 性能基准

```
操作                    耗时        吞吐量
批量插入 (10000)       32ms        312,500 ops/s
查询所有 (10000)       38ms        263,157 ops/s
按 ID 查询 (5000)      18ms        277,777 ops/s
条件查询 (10000)       42ms        238,095 ops/s
更新 (1000)            8ms         125,000 ops/s
```

---

## 调试技巧

### 1. 启用 SQL 日志

```csharp
// 创建自定义 ORM 类以记录 SQL
public class DebugSqliteV2<T> : SqliteV2<T> where T : class, IDataBase, new()
{
    public override void Insert(T item)
    {
        Console.WriteLine($"[SQL] INSERT INTO {TableName} ...");
        base.Insert(item);
    }
}
```

### 2. 使用断点调试

```csharp
var user = db.SelectById(1);
// 在这里设置断点
Console.WriteLine(user.Name);
```

### 3. 记录异常

```csharp
try
{
    db.Insert(user);
}
catch (Exception ex)
{
    Console.WriteLine($"错误: {ex.Message}");
    Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
}
```

### 4. 性能分析

```csharp
var sw = Stopwatch.StartNew();
var users = db.SelectAll().ToList();
sw.Stop();
Console.WriteLine($"查询耗时: {sw.ElapsedMilliseconds}ms");
```

---

## 获取更多帮助

- 查看 [完整指南](SQLITE_V2_GUIDE.md)
- 查看 [API 参考](SQLITE_V2_API_REFERENCE.md)
- 查看 [最佳实践](SQLITE_V2_BEST_PRACTICES.md)
- 查看 [性能优化](SQLITE_V2_PERFORMANCE.md)
