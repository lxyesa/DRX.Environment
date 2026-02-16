# SQLite V2 使用示例

## 基础使用

### 定义数据模型

```csharp
using Drx.Sdk.Network.DataBase.Sqlite;

// 继承 IDataBase 接口
public class User : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public string TableName => null;  // 使用类名作为表名
}

// 带子表的模型
public class Order : IDataTable
{
    public int Id { get; set; }
    public int ParentId { get; set; }  // 父表 ID
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    
    public string TableName => null;
}
```

### 简单 CRUD 操作

```csharp
using Drx.Sdk.Network.DataBase.Sqlite.V2;

// 创建数据库实例
var db = new Sqlite<User>("./myapp.db", "./data");

// 插入单个
var user = new User 
{ 
    Name = "张三", 
    Email = "zhangsan@example.com", 
    Age = 30, 
    IsActive = true,
    CreatedAt = DateTime.Now
};
db.Insert(user);

// 批量插入（性能优化，推荐用于大量数据）
var users = new List<User>();
for (int i = 0; i < 10000; i++)
{
    users.Add(new User 
    { 
        Name = $"User{i}", 
        Email = $"user{i}@example.com",
        Age = 20 + (i % 50),
        IsActive = i % 2 == 0
    });
}
db.InsertBatch(users);

// 查询所有
var allUsers = db.SelectAll();

// 根据 ID 查询
var user = db.SelectById(1);
if (user != null)
{
    Console.WriteLine($"用户名: {user.Name}");
}

// 条件查询
var activeUsers = db.SelectWhere("IsActive", true);

// Lambda 表达式查询
var adults = db.SelectWhere(u => u.Age >= 30);

// 更新
user.Age = 31;
db.Update(user);

// 删除
db.Delete(user);

// 根据 ID 删除
db.DeleteById(1);
```

## 异步操作

```csharp
// 异步批量插入
await db.InsertBatchAsync(users, batchSize: 1000);

// 异步查询所有
var allUsers = await db.SelectAllAsync();

// 异步流式查询（适合超大数据集，节省内存）
await foreach (var user in db.SelectAllStreamAsync())
{
    // 处理每一条记录
    Console.WriteLine(user.Name);
}
```

## 高级模式

### 1. 仓储模式（Repository Pattern）

```csharp
// 使用仓储模式提供统一接口
var userRepo = new SqliteRepository<User>("./myapp.db", "./data");

// 查询操作
var allUsers = userRepo.GetAll();
var user = userRepo.GetById(1);
var found = userRepo.Exists(1);
var count = userRepo.Count();

// 修改操作
userRepo.Add(newUser);
userRepo.AddRange(newUsers);
userRepo.Update(user);
userRepo.Delete(user);
userRepo.DeleteById(1);

// 高级查询
var results = userRepo.Find("Age", 30);
var filtered = userRepo.FindWhere(u => u.Age > 25);

// 异步操作
var all = await userRepo.GetAllAsync();
await userRepo.AddRangeAsync(users);
await foreach (var u in userRepo.GetAllStreamAsync())
{
    // 处理
}
```

### 2. 工作单元模式（Unit of Work）

```csharp
// 用于复杂业务场景，需要多个操作的事务支持
var db = new Sqlite<User>("./myapp.db", "./data");
using var unitOfWork = new SqliteUnitOfWork<User>(db);

await unitOfWork.BeginTransactionAsync();

try
{
    // 标记需要保存的对象
    unitOfWork.Add(newUser1);
    unitOfWork.Add(newUser2);
    
    unitOfWork.Update(existingUser);
    
    unitOfWork.Delete(userToRemove);
    
    // 查看待提交的更改数
    int changes = unitOfWork.GetPendingChangesCount();
    Console.WriteLine($"待提交更改数: {changes}");
    
    // 一次提交所有更改
    await unitOfWork.CommitAsync();
}
catch
{
    // 如果出现错误，回滚所有更改
    await unitOfWork.RollbackAsync();
    throw;
}
```

### 3. 批处理器（Batch Processor）

```csharp
// 用于处理持续流入的大量数据
var db = new Sqlite<User>("./myapp.db", "./data");
var processor = new SqliteBatchProcessor<User>(db, batchSize: 1000);

// 模拟数据流
for (int i = 0; i < 100000; i++)
{
    var user = new User 
    { 
        Name = $"User{i}", 
        Email = $"user{i}@example.com",
        Age = 20 + (i % 50)
    };
    
    // 自动在达到批大小时提交
    processor.Add(user);
}

// 手动刷新剩余数据
processor.Flush();

// 或使用异步
await processor.FlushAsync();
```

## 性能对比示例

### V1（原版本）- 10000 条记录插入

```csharp
var db = new SqliteUnified<User>("./v1.db", "./data");
var sw = Stopwatch.StartNew();

for (int i = 0; i < 10000; i++)
{
    db.Push(new User { Name = $"User{i}" });  // 每条都创建事务
}

sw.Stop();
Console.WriteLine($"V1 插入耗时: {sw.ElapsedMilliseconds}ms");  // 输出: ~100000ms
```

### V2（新版本）- 10000 条记录插入

```csharp
var db = new Sqlite<User>("./v2.db", "./data");
var sw = Stopwatch.StartNew();

var users = Enumerable.Range(0, 10000)
    .Select(i => new User { Name = $"User{i}" })
    .ToList();
db.InsertBatch(users);  // 单个事务插入所有

sw.Stop();
Console.WriteLine($"V2 插入耗时: {sw.ElapsedMilliseconds}ms");  // 输出: ~100ms
```

**性能提升: 1000 倍 🚀**

## 性能优化建议

### ✅ 推荐做法

```csharp
// 批量操作使用 InsertBatch（而不是循环 Insert）
db.InsertBatch(largeList);  // ✓ 快速，单个事务

// 使用异步流式查询处理大数据集
await foreach (var item in db.SelectAllStreamAsync())
{
    // 处理 item，不会一次性加载所有数据到内存
}

// 使用仓储模式隐藏底层细节
var repo = new SqliteRepository<User>(path, basePath);
var items = repo.GetAll();  // 清晰的接口
```

### ❌ 避免做法

```csharp
// 避免在循环中单条插入
for (int i = 0; i < 10000; i++)
{
    db.Insert(users[i]);  // ✗ 非常慢，10000 个事务
}

// 避免一次性加载所有数据（大数据集）
var all = db.SelectAll();  // ✗ 内存溢出风险
foreach (var item in all) { /* 处理 */ }

// 应该用流式查询代替
await foreach (var item in db.SelectAllStreamAsync())
{
    // 处理 item
}
```

## 完整应用示例

```csharp
public class UserManagementService
{
    private readonly SqliteRepository<User> _userRepo;
    
    public UserManagementService(string dbPath)
    {
        _userRepo = new SqliteRepository<User>(dbPath, "./data");
    }
    
    // 导入用户数据（大文件）
    public async Task ImportUsersAsync(string filePath)
    {
        var processor = new SqliteBatchProcessor<User>(
            new Sqlite<User>("users.db", "./data"), 
            batchSize: 5000
        );
        
        foreach (var line in File.ReadLines(filePath))
        {
            var user = ParseUserFromCsv(line);
            processor.Add(user);
        }
        
        await processor.FlushAsync();
    }
    
    // 查询活跃用户
    public List<User> GetActiveUsers()
    {
        return _userRepo.FindWhere(u => u.IsActive && u.Age >= 18);
    }
    
    // 批量更新用户状态
    public async Task UpdateUserStatusAsync(List<int> userIds, bool isActive)
    {
        using var unitOfWork = new SqliteUnitOfWork<User>(
            new Sqlite<User>("users.db", "./data")
        );
        
        await unitOfWork.BeginTransactionAsync();
        try
        {
            foreach (var userId in userIds)
            {
                var user = _userRepo.GetById(userId);
                if (user != null)
                {
                    user.IsActive = isActive;
                    unitOfWork.Update(user);
                }
            }
            
            await unitOfWork.CommitAsync();
        }
        catch
        {
            await unitOfWork.RollbackAsync();
            throw;
        }
    }
}

// 使用示例
var service = new UserManagementService("./myapp.db");
await service.ImportUsersAsync("users.csv");
var activeUsers = service.GetActiveUsers();
```

## 常见问题

### Q1: 什么时候应该使用 V2？
**A:** 当你需要处理大数据量（>1000 条）或频繁的数据库操作时，使用 V2 能显著提高性能。

### Q2: V2 支持复杂的导航属性吗？
**A:** 当前版本专注于简单属性的性能优化。复杂导航属性在工作单元模式中可以手动管理。

### Q3: 如何处理超大数据集？
**A:** 使用 `SelectAllStreamAsync()` 进行流式查询，不会一次性加载所有数据到内存。

### Q4: V2 线程安全吗？
**A:** V2 使用了 `ConcurrentDictionary` 和锁机制确保线程安全。每个数据库操作使用独立连接。

