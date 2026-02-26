# SQLite V2 API 参考手册

## 目录
- [SqliteV2<T> 类](#sqlitev2t-类)
- [SqliteUnitOfWork<T> 类](#sqliteunitofworkt-类)
- [SqliteBatchBuffer<T> 类](#sqlitebatchbuffert-类)
- [TableList<T> 类](#tablelistt-类)
- [接口定义](#接口定义)

---

## SqliteV2<T> 类

主 ORM 类，提供所有数据库操作功能。

### 构造函数

```csharp
public SqliteV2(string connectionString)
```

**参数：**
- `connectionString` (string): SQLite 连接字符串，例如 `"Data Source=app.db"`

**示例：**
```csharp
var db = new SqliteV2<User>("Data Source=app.db");
```

### 表管理

#### CreateTableIfNotExists
```csharp
public void CreateTableIfNotExists()
```
创建表（如果不存在）。根据泛型类型 T 的属性自动生成表结构。

#### DropTable
```csharp
public void DropTable()
```
删除表。

#### GetTableName
```csharp
public string GetTableName()
```
获取表名。

---

### 插入操作

#### Insert
```csharp
public void Insert(T item)
```
插入单条记录。

**参数：**
- `item` (T): 要插入的对象

**示例：**
```csharp
var user = new User { Name = "John", Email = "john@example.com" };
db.Insert(user);
```

#### InsertBatch
```csharp
public void InsertBatch(IEnumerable<T> items)
```
同步批量插入。

**参数：**
- `items` (IEnumerable<T>): 要插入的对象集合

#### InsertBatchAsync
```csharp
public async Task InsertBatchAsync(
    IEnumerable<T> items,
    int batchSize = 1000,
    CancellationToken cancellationToken = default)
```
异步批量插入，支持分批处理。

**参数：**
- `items` (IEnumerable<T>): 要插入的对象集合
- `batchSize` (int): 每批的大小，默认 1000
- `cancellationToken` (CancellationToken): 取消令牌

**示例：**
```csharp
var users = GetUsers(10000);
await db.InsertBatchAsync(users, batchSize: 500);
```

---

### 查询操作

#### SelectAll
```csharp
public IEnumerable<T> SelectAll()
```
查询所有记录。

**返回值：** IEnumerable<T>

**示例：**
```csharp
var allUsers = db.SelectAll().ToList();
```

#### SelectAllAsync
```csharp
public async Task<List<T>> SelectAllAsync(
    CancellationToken cancellationToken = default)
```
异步查询所有记录。

#### SelectAllStreamAsync
```csharp
public async IAsyncEnumerable<T> SelectAllStreamAsync(
    CancellationToken cancellationToken = default)
```
异步流式查询，适合大数据集。

**示例：**
```csharp
await foreach (var user in db.SelectAllStreamAsync())
{
    Console.WriteLine(user.Name);
}
```

#### SelectById
```csharp
public T? SelectById(int id)
```
按 ID 查询单条记录。

**参数：**
- `id` (int): 记录 ID

**返回值：** T 或 null

#### SelectByIdAsync
```csharp
public async Task<T?> SelectByIdAsync(
    int id,
    CancellationToken cancellationToken = default)
```
异步按 ID 查询。

#### SelectWhere
```csharp
public IEnumerable<T> SelectWhere(Expression<Func<T, bool>> predicate)
```
条件查询。使用 Lambda 表达式作为查询条件。

**参数：**
- `predicate` (Expression<Func<T, bool>>): 查询条件

**示例：**
```csharp
var activeUsers = db.SelectWhere(u => u.IsActive && u.Age > 18);
var result = db.SelectWhere(u => u.Name.Contains("John"));
```

#### SelectWhereAsync
```csharp
public async Task<List<T>> SelectWhereAsync(
    Expression<Func<T, bool>> predicate,
    CancellationToken cancellationToken = default)
```
异步条件查询。

#### Count
```csharp
public int Count()
```
获取表中的记录总数。

#### CountWhere
```csharp
public int CountWhere(Expression<Func<T, bool>> predicate)
```
获取满足条件的记录数。

---

### 更新操作

#### Update
```csharp
public void Update(T item)
```
更新单条记录。

**参数：**
- `item` (T): 要更新的对象

**示例：**
```csharp
user.Age = 31;
db.Update(user);
```

#### UpdateAsync
```csharp
public async Task UpdateAsync(
    T item,
    CancellationToken cancellationToken = default)
```
异步更新单条记录。

#### UpdateBatch
```csharp
public void UpdateBatch(IEnumerable<T> items)
```
同步批量更新。

#### UpdateBatchAsync
```csharp
public async Task UpdateBatchAsync(
    IEnumerable<T> items,
    int batchSize = 1000,
    CancellationToken cancellationToken = default)
```
异步批量更新。

#### UpdateWhere
```csharp
public void UpdateWhere(
    Expression<Func<T, bool>> predicate,
    Action<T> updateAction)
```
条件更新。

**参数：**
- `predicate` (Expression<Func<T, bool>>): 查询条件
- `updateAction` (Action<T>): 更新操作

**示例：**
```csharp
db.UpdateWhere(
    u => u.IsActive == false,
    u => u.LastLogin = DateTime.Now);
```

---

### 删除操作

#### DeleteById
```csharp
public void DeleteById(int id)
```
按 ID 删除记录。

#### DeleteByIdAsync
```csharp
public async Task DeleteByIdAsync(
    int id,
    CancellationToken cancellationToken = default)
```
异步按 ID 删除。

#### DeleteBatch
```csharp
public void DeleteBatch(IEnumerable<int> ids)
```
批量删除。

**参数：**
- `ids` (IEnumerable<int>): 要删除的 ID 集合

#### DeleteBatchAsync
```csharp
public async Task DeleteBatchAsync(
    IEnumerable<int> ids,
    CancellationToken cancellationToken = default)
```
异步批量删除。

#### DeleteWhere
```csharp
public void DeleteWhere(Expression<Func<T, bool>> predicate)
```
条件删除。

**参数：**
- `predicate` (Expression<Func<T, bool>>): 删除条件

**示例：**
```csharp
db.DeleteWhere(u => u.IsActive == false);
```

#### DeleteAll
```csharp
public void DeleteAll()
```
删除所有记录。

---

### 属性

#### ConnectionString
```csharp
public string ConnectionString { get; }
```
获取连接字符串。

#### TableName
```csharp
public string TableName { get; }
```
获取表名。

---

## SqliteUnitOfWork<T> 类

工作单元模式实现，用于事务管理。

### 构造函数

```csharp
public SqliteUnitOfWork(SqliteV2<T> db)
```

### 方法

#### BeginTransactionAsync
```csharp
public async Task BeginTransactionAsync()
```
开始事务。

#### Add
```csharp
public void Add(T item)
```
标记对象为新增。

#### Modify
```csharp
public void Modify(T item)
```
标记对象为修改。

#### Remove
```csharp
public void Remove(T item)
```
标记对象为删除。

#### CommitAsync
```csharp
public async Task CommitAsync(CancellationToken cancellationToken = default)
```
提交所有变更。执行顺序：删除 → 插入 → 更新。

**示例：**
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
catch (Exception ex)
{
    Console.WriteLine($"事务失败: {ex.Message}");
    // 自动回滚
}
```

#### RollbackAsync
```csharp
public async Task RollbackAsync(CancellationToken cancellationToken = default)
```
回滚事务。

#### Dispose
```csharp
public void Dispose()
```
释放资源。

---

## SqliteBatchBuffer<T> 类

批处理缓冲区，用于高效的批量插入。

### 构造函数

```csharp
public SqliteBatchBuffer(SqliteV2<T> db, int batchSize = 1000)
```

**参数：**
- `db` (SqliteV2<T>): ORM 实例
- `batchSize` (int): 缓冲区大小，默认 1000

### 方法

#### Add
```csharp
public async Task AddAsync(T item, CancellationToken cancellationToken = default)
```
添加项到缓冲区。当缓冲区满时自动刷新。

#### AddRange
```csharp
public async Task AddRangeAsync(
    IEnumerable<T> items,
    CancellationToken cancellationToken = default)
```
批量添加项。

#### FlushAsync
```csharp
public async Task FlushAsync(CancellationToken cancellationToken = default)
```
手动刷新缓冲区，将所有项插入数据库。

#### GetBufferedCount
```csharp
public int GetBufferedCount()
```
获取缓冲区中的项数。

#### Clear
```csharp
public void Clear()
```
清空缓冲区（不保存）。

**示例：**
```csharp
var buffer = new SqliteBatchBuffer<User>(db, batchSize: 500);

for (int i = 0; i < 10000; i++)
{
    var user = new User { Name = $"User{i}" };
    await buffer.AddAsync(user);
}

// 刷新剩余项
await buffer.FlushAsync();
```

---

## TableList<T> 类

子表集合，实现 IList<T> 接口。

### 构造函数

```csharp
public TableList(int parentId, SqliteV2<T> db)
```

### 方法

#### Add
```csharp
public void Add(T item)
```
添加子表项。立即同步到数据库。

#### Remove
```csharp
public bool Remove(T item)
```
移除子表项。立即同步到数据库。

#### Clear
```csharp
public void Clear()
```
清空所有子表项。

#### Contains
```csharp
public bool Contains(T item)
```
检查是否包含项。

#### Count
```csharp
public int Count { get; }
```
获取子表项数。

#### LINQ 支持
```csharp
// Where
var expiredMods = mods.Where(m => m.ExpiresAt < DateTime.Now);

// FirstOrDefault
var firstMod = mods.FirstOrDefault(m => m.ModId == 100);

// Any
bool hasActiveMods = mods.Any(m => m.IsActive);

// GroupBy
var grouped = mods.GroupBy(m => m.Category);
```

---

## 接口定义

### IDataBase
```csharp
public interface IDataBase
{
    int Id { get; set; }
    string TableName { get; }
}
```
主表数据模型接口。

### IDataTable
```csharp
public interface IDataTable
{
    int Id { get; set; }
    int ParentId { get; set; }
    string TableName { get; }
}
```
子表数据模型接口（整数 ID）。

### IDataTableV2
```csharp
public interface IDataTableV2
{
    string Id { get; set; }
    int ParentId { get; set; }
    string TableName { get; }
}
```
子表数据模型接口（字符串 ID）。

---

## 常见模式

### 分页查询
```csharp
public List<T> GetPage(int pageNumber, int pageSize)
{
    var skip = (pageNumber - 1) * pageSize;
    return db.SelectAll()
        .Skip(skip)
        .Take(pageSize)
        .ToList();
}
```

### 条件计数
```csharp
var activeCount = db.CountWhere(u => u.IsActive);
var totalCount = db.Count();
var percentage = (activeCount * 100.0) / totalCount;
```

### 批量操作链
```csharp
var users = db.SelectWhere(u => u.Age > 18);
foreach (var user in users)
{
    user.IsVerified = true;
}
await db.UpdateBatchAsync(users);
```

### 事务中的复杂操作
```csharp
using var uow = new SqliteUnitOfWork<Order>(db);
await uow.BeginTransactionAsync();

try
{
    var order = new Order { CustomerId = 1, Total = 100 };
    uow.Add(order);

    var items = GetOrderItems(order.Id);
    foreach (var item in items)
    {
        uow.Add(item);
    }

    await uow.CommitAsync();
}
catch
{
    await uow.RollbackAsync();
    throw;
}
```
