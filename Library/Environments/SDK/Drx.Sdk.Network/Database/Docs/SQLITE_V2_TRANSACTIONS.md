# SQLite V2 事务管理与工作单元模式

## 目录
- [事务基础](#事务基础)
- [工作单元模式](#工作单元模式)
- [实现示例](#实现示例)
- [错误处理](#错误处理)
- [最佳实践](#最佳实践)
- [常见问题](#常见问题)

---

## 事务基础

### 什么是事务？

事务是一组数据库操作的逻辑单元，具有 ACID 特性：

- **原子性 (Atomicity)**：全部成功或全部失败
- **一致性 (Consistency)**：数据保持一致状态
- **隔离性 (Isolation)**：并发事务互不影响
- **持久性 (Durability)**：提交后永久保存

### 为什么需要事务？

```csharp
// ❌ 没有事务的问题
public void TransferMoney(int fromUserId, int toUserId, decimal amount)
{
    var fromUser = db.SelectById(fromUserId);
    fromUser.Balance -= amount;
    db.Update(fromUser);  // 如果这里失败...

    var toUser = db.SelectById(toUserId);
    toUser.Balance += amount;
    db.Update(toUser);    // ...钱就丢了！
}

// ✅ 使用事务
public async Task TransferMoneyAsync(int fromUserId, int toUserId, decimal amount)
{
    using var uow = new SqliteUnitOfWork<User>(db);
    await uow.BeginTransactionAsync();

    try
    {
        var fromUser = db.SelectById(fromUserId);
        fromUser.Balance -= amount;
        uow.Modify(fromUser);

        var toUser = db.SelectById(toUserId);
        toUser.Balance += amount;
        uow.Modify(toUser);

        await uow.CommitAsync();  // 全部成功才提交
    }
    catch
    {
        // 自动回滚，钱不会丢
        throw;
    }
}
```

---

## 工作单元模式

### 模式概述

工作单元模式 (Unit of Work) 是一种设计模式，用于协调多个数据库操作。

**核心思想：**
1. 追踪所有变更（新增、修改、删除）
2. 在事务中执行所有变更
3. 全部成功则提交，否则回滚

### SqliteUnitOfWork<T> 类

```csharp
public sealed class SqliteUnitOfWork<T> : IDisposable
    where T : class, IDataBase, new()
{
    // 追踪变化的对象
    private readonly List<T> _addedItems = new();
    private readonly List<T> _modifiedItems = new();
    private readonly List<T> _deletedItems = new();

    // 事务管理
    private SqliteTransaction? _transaction;
    private bool _disposed;
}
```

### 生命周期

```
创建 UoW
    ↓
开始事务 (BeginTransactionAsync)
    ↓
追踪变更 (Add/Modify/Remove)
    ↓
提交 (CommitAsync) 或 回滚 (RollbackAsync)
    ↓
释放资源 (Dispose)
```

---

## 实现示例

### 基础事务示例

```csharp
using var uow = new SqliteUnitOfWork<User>(db);
await uow.BeginTransactionAsync();

try
{
    // 新增用户
    var newUser = new User { Name = "Alice", Email = "alice@example.com" };
    uow.Add(newUser);

    // 修改用户
    var existingUser = db.SelectById(1);
    existingUser.Email = "newemail@example.com";
    uow.Modify(existingUser);

    // 删除用户
    var userToDelete = db.SelectById(2);
    uow.Remove(userToDelete);

    // 提交所有变更
    await uow.CommitAsync();
    Console.WriteLine("事务提交成功");
}
catch (Exception ex)
{
    Console.WriteLine($"事务失败: {ex.Message}");
    // 自动回滚
}
```

### 复杂业务逻辑示例

```csharp
public class OrderService
{
    private readonly SqliteV2<Order> _orderDb;
    private readonly SqliteV2<Inventory> _inventoryDb;

    public async Task CreateOrderAsync(Order order, List<OrderItem> items)
    {
        using var uow = new SqliteUnitOfWork<Order>(_orderDb);
        await uow.BeginTransactionAsync();

        try
        {
            // 1. 创建订单
            uow.Add(order);

            // 2. 添加订单项
            foreach (var item in items)
            {
                order.Items.Add(item);

                // 3. 更新库存
                var inventory = _inventoryDb.SelectById(item.ProductId);
                if (inventory.Quantity < item.Quantity)
                {
                    throw new InvalidOperationException("库存不足");
                }
                inventory.Quantity -= item.Quantity;
                _inventoryDb.Update(inventory);
            }

            // 4. 计算订单总额
            order.TotalAmount = order.Items
                .Sum(i => i.Quantity * i.UnitPrice);
            uow.Modify(order);

            // 全部成功则提交
            await uow.CommitAsync();
            Console.WriteLine($"订单 {order.OrderNumber} 创建成功");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"订单创建失败: {ex.Message}");
            // 自动回滚，库存恢复
            throw;
        }
    }
}
```

### 批量操作示例

```csharp
public async Task BatchUpdateUsersAsync(List<User> users)
{
    using var uow = new SqliteUnitOfWork<User>(db);
    await uow.BeginTransactionAsync();

    try
    {
        foreach (var user in users)
        {
            if (user.IsActive)
            {
                user.LastLogin = DateTime.Now;
                uow.Modify(user);
            }
            else
            {
                uow.Remove(user);
            }
        }

        await uow.CommitAsync();
        Console.WriteLine($"成功更新 {users.Count} 个用户");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"批量更新失败: {ex.Message}");
        throw;
    }
}
```

### 嵌套事务示例

```csharp
public async Task ProcessPaymentAsync(Payment payment)
{
    using var uow = new SqliteUnitOfWork<Payment>(db);
    await uow.BeginTransactionAsync();

    try
    {
        // 记录支付
        uow.Add(payment);

        // 更新订单状态
        var order = db.SelectById(payment.OrderId);
        order.Status = "paid";
        uow.Modify(order);

        // 发送通知（可能失败）
        try
        {
            await SendNotificationAsync(order);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"通知发送失败: {ex.Message}");
            // 继续处理，不中断事务
        }

        await uow.CommitAsync();
    }
    catch
    {
        throw;
    }
}
```

---

## 错误处理

### 常见异常

#### InvalidOperationException
```csharp
// 事务未开始
try
{
    var uow = new SqliteUnitOfWork<User>(db);
    await uow.CommitAsync();  // ❌ 异常：事务未开始
}
catch (InvalidOperationException ex)
{
    Console.WriteLine(ex.Message);  // "事务未开始"
}
```

#### SqliteException
```csharp
// 数据库错误
try
{
    using var uow = new SqliteUnitOfWork<User>(db);
    await uow.BeginTransactionAsync();

    var user = new User { Id = 1, Name = "Alice" };  // ID 重复
    uow.Add(user);

    await uow.CommitAsync();  // ❌ 异常：主键冲突
}
catch (SqliteException ex)
{
    Console.WriteLine($"数据库错误: {ex.Message}");
}
```

### 错误恢复策略

```csharp
public async Task RobustTransactionAsync()
{
    int retryCount = 0;
    const int maxRetries = 3;

    while (retryCount < maxRetries)
    {
        using var uow = new SqliteUnitOfWork<User>(db);

        try
        {
            await uow.BeginTransactionAsync();

            // 执行操作
            uow.Add(newUser);
            await uow.CommitAsync();

            return;  // 成功
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 5)  // 数据库锁定
        {
            retryCount++;
            if (retryCount >= maxRetries)
            {
                throw;
            }

            // 等待后重试
            await Task.Delay(100 * retryCount);
        }
        catch
        {
            throw;  // 其他错误直接抛出
        }
    }
}
```

---

## 最佳实践

### 1. 保持事务简短

```csharp
// ❌ 不好：事务中包含长时间操作
using var uow = new SqliteUnitOfWork<User>(db);
await uow.BeginTransactionAsync();

var user = new User { Name = "Alice" };
uow.Add(user);

// 长时间操作（网络请求、文件 I/O）
var result = await FetchDataFromExternalApiAsync();

await uow.CommitAsync();  // 事务持续时间过长

// ✅ 好：事务只包含数据库操作
var result = await FetchDataFromExternalApiAsync();

using var uow = new SqliteUnitOfWork<User>(db);
await uow.BeginTransactionAsync();

var user = new User { Name = "Alice", ExternalData = result };
uow.Add(user);

await uow.CommitAsync();  // 事务时间短
```

### 2. 使用 using 语句

```csharp
// ✅ 推荐：自动释放资源
using var uow = new SqliteUnitOfWork<User>(db);
await uow.BeginTransactionAsync();
// ... 操作
await uow.CommitAsync();
// 自动调用 Dispose()

// ❌ 避免：手动管理
var uow = new SqliteUnitOfWork<User>(db);
await uow.BeginTransactionAsync();
// ... 操作
await uow.CommitAsync();
uow.Dispose();  // 容易忘记
```

### 3. 明确的错误处理

```csharp
// ✅ 好：清晰的错误处理
using var uow = new SqliteUnitOfWork<User>(db);

try
{
    await uow.BeginTransactionAsync();
    // ... 操作
    await uow.CommitAsync();
}
catch (SqliteException ex)
{
    Console.WriteLine($"数据库错误: {ex.Message}");
    throw;
}
catch (Exception ex)
{
    Console.WriteLine($"未知错误: {ex.Message}");
    throw;
}
```

### 4. 验证数据

```csharp
// ✅ 好：事务前验证
public async Task CreateUserAsync(User user)
{
    // 验证数据
    if (string.IsNullOrEmpty(user.Name))
        throw new ArgumentException("用户名不能为空");

    if (!IsValidEmail(user.Email))
        throw new ArgumentException("邮箱格式不正确");

    // 数据有效，开始事务
    using var uow = new SqliteUnitOfWork<User>(db);
    await uow.BeginTransactionAsync();

    try
    {
        uow.Add(user);
        await uow.CommitAsync();
    }
    catch
    {
        throw;
    }
}
```

### 5. 日志记录

```csharp
// ✅ 好：记录事务操作
public async Task LoggedTransactionAsync()
{
    using var uow = new SqliteUnitOfWork<User>(db);

    try
    {
        _logger.LogInformation("开始事务");
        await uow.BeginTransactionAsync();

        uow.Add(newUser);
        _logger.LogInformation("添加用户: {UserId}", newUser.Id);

        await uow.CommitAsync();
        _logger.LogInformation("事务提交成功");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "事务失败");
        throw;
    }
}
```

---

## 常见问题

### Q1: 事务中可以执行多少个操作？

**A:** 理论上无限制，但建议：
- 单个事务不超过 10,000 个操作
- 保持事务时间在 1 秒以内
- 对于大批量操作，分批处理

```csharp
// 分批处理 100,000 条记录
var items = GetItems(100000);
var batchSize = 10000;

for (int i = 0; i < items.Count; i += batchSize)
{
    var batch = items.Skip(i).Take(batchSize).ToList();

    using var uow = new SqliteUnitOfWork<Item>(db);
    await uow.BeginTransactionAsync();

    foreach (var item in batch)
    {
        uow.Add(item);
    }

    await uow.CommitAsync();
}
```

### Q2: 事务中的异常会自动回滚吗？

**A:** 是的。当异常发生时，事务会自动回滚。但建议显式处理：

```csharp
using var uow = new SqliteUnitOfWork<User>(db);

try
{
    await uow.BeginTransactionAsync();
    // ... 操作
    await uow.CommitAsync();
}
catch
{
    // 自动回滚，但可以在这里记录日志
    _logger.LogError("事务失败");
    throw;
}
```

### Q3: 可以在事务中执行查询吗？

**A:** 可以，但要注意：

```csharp
using var uow = new SqliteUnitOfWork<User>(db);
await uow.BeginTransactionAsync();

try
{
    // 查询（在事务中）
    var user = db.SelectById(1);

    // 修改
    user.Name = "NewName";
    uow.Modify(user);

    await uow.CommitAsync();
}
catch
{
    throw;
}
```

### Q4: 如何处理并发事务冲突？

**A:** 使用重试机制：

```csharp
public async Task HandleConcurrencyAsync()
{
    for (int attempt = 0; attempt < 3; attempt++)
    {
        try
        {
            using var uow = new SqliteUnitOfWork<User>(db);
            await uow.BeginTransactionAsync();

            var user = db.SelectById(1);
            user.Version++;
            uow.Modify(user);

            await uow.CommitAsync();
            return;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 5)
        {
            if (attempt == 2) throw;
            await Task.Delay(100 * (attempt + 1));
        }
    }
}
```

---

## 性能考虑

### 事务开销

| 操作 | 耗时 |
|------|------|
| 开始事务 | ~0.1ms |
| 提交事务 | ~1-5ms |
| 回滚事务 | ~0.5-2ms |

### 优化建议

1. **批量操作使用事务**
   ```csharp
   // 快：1 个事务，1000 个操作
   await db.InsertBatchAsync(items);

   // 慢：1000 个事务，1000 个操作
   foreach (var item in items)
   {
       db.Insert(item);
   }
   ```

2. **避免长事务**
   ```csharp
   // 快：事务时间短
   using var uow = new SqliteUnitOfWork<User>(db);
   await uow.BeginTransactionAsync();
   uow.Add(user);
   await uow.CommitAsync();  // 几毫秒

   // 慢：事务时间长
   using var uow = new SqliteUnitOfWork<User>(db);
   await uow.BeginTransactionAsync();
   uow.Add(user);
   await Task.Delay(5000);  // 等待 5 秒
   await uow.CommitAsync();
   ```

3. **合理分批**
   ```csharp
   // 推荐：每批 1000-5000 条
   await db.InsertBatchAsync(items, batchSize: 2000);
   ```
