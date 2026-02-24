# SQLite V2 最佳实践指南

## 目录
- [设计原则](#设计原则)
- [代码组织](#代码组织)
- [性能最佳实践](#性能最佳实践)
- [安全最佳实践](#安全最佳实践)
- [测试策略](#测试策略)
- [常见陷阱](#常见陷阱)

---

## 设计原则

### 1. 单一职责原则

**原则：** 每个类只负责一个职责。

```csharp
// ❌ 不好：混合了数据访问和业务逻辑
public class UserService
{
    public void CreateUser(string name, string email)
    {
        var db = new SqliteV2<User>("Data Source=app.db");

        // 验证
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("名称不能为空");

        // 业务逻辑
        var user = new User { Name = name, Email = email };

        // 数据访问
        db.Insert(user);

        // 发送邮件
        SendWelcomeEmail(email);
    }
}

// ✅ 好：分离职责
public class UserRepository
{
    private readonly SqliteV2<User> _db;

    public UserRepository(SqliteV2<User> db)
    {
        _db = db;
    }

    public void Add(User user) => _db.Insert(user);
    public User GetById(int id) => _db.SelectById(id);
}

public class UserService
{
    private readonly UserRepository _repository;
    private readonly EmailService _emailService;

    public async Task CreateUserAsync(string name, string email)
    {
        // 验证
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("名称不能为空");

        // 业务逻辑
        var user = new User { Name = name, Email = email };
        _repository.Add(user);

        // 发送邮件
        await _emailService.SendWelcomeEmailAsync(email);
    }
}
```

### 2. 依赖注入

**原则：** 通过构造函数注入依赖，而不是在类内部创建。

```csharp
// ❌ 不好：硬编码依赖
public class OrderService
{
    public void CreateOrder(Order order)
    {
        var db = new SqliteV2<Order>("Data Source=app.db");
        db.Insert(order);
    }
}

// ✅ 好：注入依赖
public class OrderService
{
    private readonly SqliteV2<Order> _db;

    public OrderService(SqliteV2<Order> db)
    {
        _db = db;
    }

    public void CreateOrder(Order order)
    {
        _db.Insert(order);
    }
}

// 使用
var db = new SqliteV2<Order>("Data Source=app.db");
var service = new OrderService(db);
service.CreateOrder(order);
```

### 3. 接口隔离

**原则：** 定义细粒度的接口。

```csharp
// ❌ 不好：大而全的接口
public interface IRepository<T>
{
    void Insert(T item);
    void Update(T item);
    void Delete(int id);
    T GetById(int id);
    IEnumerable<T> GetAll();
    void CreateTable();
    void DropTable();
    void Backup();
}

// ✅ 好：细粒度接口
public interface IReadRepository<T>
{
    T GetById(int id);
    IEnumerable<T> GetAll();
}

public interface IWriteRepository<T>
{
    void Insert(T item);
    void Update(T item);
    void Delete(int id);
}

public interface IRepository<T> : IReadRepository<T>, IWriteRepository<T>
{
}
```

---

## 代码组织

### 项目结构

```
MyProject/
├── Models/
│   ├── User.cs
│   ├── Order.cs
│   └── OrderItem.cs
├── Repositories/
│   ├── IUserRepository.cs
│   ├── UserRepository.cs
│   ├── IOrderRepository.cs
│   └── OrderRepository.cs
├── Services/
│   ├── UserService.cs
│   ├── OrderService.cs
│   └── PaymentService.cs
├── Data/
│   ├── DbContext.cs
│   └── Migrations/
└── Tests/
    ├── RepositoryTests/
    ├── ServiceTests/
    └── IntegrationTests/
```

### 数据库上下文

```csharp
public class AppDbContext
{
    private readonly string _connectionString;

    public AppDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SqliteV2<User> Users => new(_connectionString);
    public SqliteV2<Order> Orders => new(_connectionString);
    public SqliteV2<Product> Products => new(_connectionString);

    public void InitializeDatabase()
    {
        Users.CreateTableIfNotExists();
        Orders.CreateTableIfNotExists();
        Products.CreateTableIfNotExists();
    }
}

// 使用
var context = new AppDbContext("Data Source=app.db");
context.InitializeDatabase();

var userDb = context.Users;
var user = userDb.SelectById(1);
```

### 仓储模式

```csharp
public interface IUserRepository
{
    Task<User> GetByIdAsync(int id);
    Task<List<User>> GetAllAsync();
    Task<List<User>> GetActiveUsersAsync();
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(int id);
}

public class UserRepository : IUserRepository
{
    private readonly SqliteV2<User> _db;

    public UserRepository(SqliteV2<User> db)
    {
        _db = db;
    }

    public async Task<User> GetByIdAsync(int id)
    {
        return await _db.SelectByIdAsync(id);
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await _db.SelectAllAsync();
    }

    public async Task<List<User>> GetActiveUsersAsync()
    {
        return await _db.SelectWhereAsync(u => u.IsActive);
    }

    public async Task AddAsync(User user)
    {
        _db.Insert(user);
    }

    public async Task UpdateAsync(User user)
    {
        await _db.UpdateAsync(user);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.DeleteByIdAsync(id);
    }
}
```

---

## 性能最佳实践

### 1. 批量操作优化

```csharp
// ❌ 不好：逐条操作
public void InsertUsers(List<User> users)
{
    foreach (var user in users)
    {
        _db.Insert(user);  // 10000 次数据库往返
    }
}

// ✅ 好：批量操作
public async Task InsertUsersAsync(List<User> users)
{
    await _db.InsertBatchAsync(users, batchSize: 1000);
}

// ✅ 更好：使用缓冲区
public async Task InsertUsersWithBufferAsync(List<User> users)
{
    var buffer = new SqliteBatchBuffer<User>(_db, batchSize: 1000);
    foreach (var user in users)
    {
        buffer.Add(user);
    }
    await buffer.FlushAsync();
}
```

### 2. 查询优化

```csharp
// ❌ 不好：加载所有数据到内存
public void ProcessAllUsers()
{
    var allUsers = _db.SelectAll().ToList();  // 加载 100 万条
    foreach (var user in allUsers)
    {
        ProcessUser(user);
    }
}

// ✅ 好：流式处理
public async Task ProcessAllUsersAsync()
{
    await foreach (var user in _db.SelectAllStreamAsync())
    {
        ProcessUser(user);
    }
}

// ✅ 好：条件查询
public void ProcessActiveUsers()
{
    var activeUsers = _db.SelectWhere(u => u.IsActive);
    foreach (var user in activeUsers)
    {
        ProcessUser(user);
    }
}
```

### 3. 缓存策略

```csharp
public class CachedUserRepository
{
    private readonly IUserRepository _repository;
    private readonly Dictionary<int, User> _cache = new();
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    private DateTime _lastCacheTime = DateTime.MinValue;

    public async Task<User> GetByIdAsync(int id)
    {
        // 检查缓存
        if (_cache.TryGetValue(id, out var cachedUser))
        {
            return cachedUser;
        }

        // 从数据库查询
        var user = await _repository.GetByIdAsync(id);
        if (user != null)
        {
            _cache[id] = user;
        }

        return user;
    }

    public void InvalidateCache()
    {
        _cache.Clear();
        _lastCacheTime = DateTime.MinValue;
    }

    public async Task RefreshCacheAsync()
    {
        if (DateTime.Now - _lastCacheTime > _cacheExpiration)
        {
            _cache.Clear();
            var users = await _repository.GetAllAsync();
            foreach (var user in users)
            {
                _cache[user.Id] = user;
            }
            _lastCacheTime = DateTime.Now;
        }
    }
}
```

---

## 安全最佳实践

### 1. 参数验证

```csharp
// ✅ 验证输入
public async Task<User> GetUserAsync(int id)
{
    if (id <= 0)
        throw new ArgumentException("ID 必须大于 0", nameof(id));

    return await _db.SelectByIdAsync(id);
}

public async Task CreateUserAsync(string name, string email)
{
    if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("名称不能为空", nameof(name));

    if (!IsValidEmail(email))
        throw new ArgumentException("邮箱格式无效", nameof(email));

    var user = new User { Name = name, Email = email };
    _db.Insert(user);
}
```

### 2. 事务安全

```csharp
// ✅ 使用事务确保数据一致性
public async Task TransferMoneyAsync(int fromId, int toId, decimal amount)
{
    if (amount <= 0)
        throw new ArgumentException("金额必须大于 0");

    using var uow = new SqliteUnitOfWork<Account>(_db);
    await uow.BeginTransactionAsync();

    try
    {
        var fromAccount = _db.SelectById(fromId);
        if (fromAccount.Balance < amount)
            throw new InvalidOperationException("余额不足");

        fromAccount.Balance -= amount;
        uow.Modify(fromAccount);

        var toAccount = _db.SelectById(toId);
        toAccount.Balance += amount;
        uow.Modify(toAccount);

        await uow.CommitAsync();
    }
    catch
    {
        throw;
    }
}
```

### 3. 敏感数据处理

```csharp
// ✅ 不要在日志中输出敏感数据
public async Task<User> AuthenticateAsync(string email, string password)
{
    var user = _db.SelectWhere(u => u.Email == email).FirstOrDefault();

    if (user == null)
    {
        // ❌ 不要这样做
        // Console.WriteLine($"用户 {email} 不存在");

        // ✅ 这样做
        Console.WriteLine("认证失败");
        throw new UnauthorizedAccessException();
    }

    if (!VerifyPassword(password, user.PasswordHash))
    {
        Console.WriteLine("认证失败");
        throw new UnauthorizedAccessException();
    }

    return user;
}
```

---

## 测试策略

### 单元测试

```csharp
[TestClass]
public class UserRepositoryTests
{
    private SqliteV2<User> _db;
    private UserRepository _repository;

    [TestInitialize]
    public void Setup()
    {
        // 使用内存数据库进行测试
        _db = new SqliteV2<User>("Data Source=:memory:");
        _db.CreateTableIfNotExists();
        _repository = new UserRepository(_db);
    }

    [TestMethod]
    public async Task AddAsync_ShouldInsertUser()
    {
        // Arrange
        var user = new User { Name = "John", Email = "john@example.com" };

        // Act
        await _repository.AddAsync(user);

        // Assert
        var result = await _repository.GetByIdAsync(user.Id);
        Assert.IsNotNull(result);
        Assert.AreEqual("John", result.Name);
    }

    [TestMethod]
    public async Task GetActiveUsersAsync_ShouldReturnOnlyActiveUsers()
    {
        // Arrange
        _db.Insert(new User { Name = "Active", IsActive = true });
        _db.Insert(new User { Name = "Inactive", IsActive = false });

        // Act
        var result = await _repository.GetActiveUsersAsync();

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Active", result[0].Name);
    }
}
```

### 集成测试

```csharp
[TestClass]
public class OrderServiceIntegrationTests
{
    private SqliteV2<Order> _orderDb;
    private SqliteV2<Inventory> _inventoryDb;
    private OrderService _service;

    [TestInitialize]
    public void Setup()
    {
        _orderDb = new SqliteV2<Order>("Data Source=test.db");
        _inventoryDb = new SqliteV2<Inventory>("Data Source=test.db");
        _orderDb.CreateTableIfNotExists();
        _inventoryDb.CreateTableIfNotExists();
        _service = new OrderService(_orderDb, _inventoryDb);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _orderDb.DropTable();
        _inventoryDb.DropTable();
    }

    [TestMethod]
    public async Task CreateOrderAsync_ShouldUpdateInventory()
    {
        // Arrange
        var inventory = new Inventory { ProductId = 1, Quantity = 100 };
        _inventoryDb.Insert(inventory);

        var order = new Order { OrderNumber = "ORD-001" };
        var item = new OrderItem { ProductId = 1, Quantity = 10 };

        // Act
        await _service.CreateOrderAsync(order, new[] { item });

        // Assert
        var updatedInventory = _inventoryDb.SelectById(1);
        Assert.AreEqual(90, updatedInventory.Quantity);
    }
}
```

---

## 常见陷阱

### 1. 忘记创建表

```csharp
// ❌ 错误：表不存在
var db = new SqliteV2<User>("Data Source=app.db");
var user = db.SelectById(1);  // 异常：表不存在

// ✅ 正确
var db = new SqliteV2<User>("Data Source=app.db");
db.CreateTableIfNotExists();
var user = db.SelectById(1);
```

### 2. 连接字符串错误

```csharp
// ❌ 错误：路径不存在
var db = new SqliteV2<User>("Data Source=/invalid/path/app.db");

// ✅ 正确
var db = new SqliteV2<User>("Data Source=app.db");
// 或
var db = new SqliteV2<User>("Data Source=./data/app.db");
```

### 3. 忘记释放资源

```csharp
// ❌ 错误：资源泄漏
var uow = new SqliteUnitOfWork<User>(db);
await uow.BeginTransactionAsync();
// ... 忘记释放

// ✅ 正确
using var uow = new SqliteUnitOfWork<User>(db);
await uow.BeginTransactionAsync();
// 自动释放
```

### 4. 在循环中创建新连接

```csharp
// ❌ 错误：性能差
for (int i = 0; i < 1000; i++)
{
    var db = new SqliteV2<User>("Data Source=app.db");
    var user = db.SelectById(i);
}

// ✅ 正确
var db = new SqliteV2<User>("Data Source=app.db");
for (int i = 0; i < 1000; i++)
{
    var user = db.SelectById(i);
}
```

### 5. 混淆同步和异步

```csharp
// ❌ 错误：阻塞线程
public void ProcessUsers()
{
    var task = db.SelectAllAsync();
    task.Wait();  // 阻塞
}

// ✅ 正确
public async Task ProcessUsersAsync()
{
    var users = await db.SelectAllAsync();
}
```
