# SQLite V2 完整项目示例

## 项目概述

这是一个完整的电商订单管理系统示例，展示如何在实际项目中使用 SqliteV2。

**功能：**
- 用户管理
- 产品管理
- 订单管理（包含订单项）
- 库存管理
- 订单处理流程

---

## 项目结构

```
ECommerceApp/
├── Models/
│   ├── User.cs
│   ├── Product.cs
│   ├── Order.cs
│   ├── OrderItem.cs
│   └── Inventory.cs
├── Repositories/
│   ├── IRepository.cs
│   ├── UserRepository.cs
│   ├── ProductRepository.cs
│   ├── OrderRepository.cs
│   └── InventoryRepository.cs
├── Services/
│   ├── UserService.cs
│   ├── ProductService.cs
│   ├── OrderService.cs
│   └── InventoryService.cs
├── Data/
│   └── AppDbContext.cs
└── Program.cs
```

---

## 数据模型

### User.cs
```csharp
using Drx.Sdk.Network.DataBase.Sqlite.V2;

public class User : IDataBase
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public string FullName { get; set; }
    public string PhoneNumber { get; set; }
    public string Address { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public string TableName => "users";
}
```

### Product.cs
```csharp
public class Product : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
    public string Sku { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public string TableName => "products";
}
```

### Order.cs
```csharp
public class Order : IDataBase
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string OrderNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; }  // pending, processing, shipped, delivered, cancelled
    public string ShippingAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    // 子表：订单项
    public TableList<OrderItem> Items { get; set; } = new();

    public string TableName => "orders";
}
```

### OrderItem.cs
```csharp
public class OrderItem : IDataTableV2
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int ParentId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal => Quantity * UnitPrice;

    public string TableName => "order_items";
}
```

### Inventory.cs
```csharp
public class Inventory : IDataBase
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity => Quantity - ReservedQuantity;
    public DateTime LastUpdatedAt { get; set; }

    public string TableName => "inventory";
}
```

---

## 数据库上下文

### AppDbContext.cs
```csharp
using Drx.Sdk.Network.DataBase.Sqlite.V2;

public class AppDbContext
{
    private readonly string _connectionString;

    public AppDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SqliteV2<User> Users => new(_connectionString);
    public SqliteV2<Product> Products => new(_connectionString);
    public SqliteV2<Order> Orders => new(_connectionString);
    public SqliteV2<Inventory> Inventories => new(_connectionString);

    public void InitializeDatabase()
    {
        Users.CreateTableIfNotExists();
        Products.CreateTableIfNotExists();
        Orders.CreateTableIfNotExists();
        Inventories.CreateTableIfNotExists();

        Console.WriteLine("✓ 数据库初始化完成");
    }
}
```

---

## 仓储层

### IRepository.cs
```csharp
public interface IRepository<T> where T : class, IDataBase, new()
{
    Task<T> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task AddAsync(T item);
    Task UpdateAsync(T item);
    Task DeleteAsync(int id);
    Task<int> CountAsync();
}
```

### UserRepository.cs
```csharp
public class UserRepository : IRepository<User>
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

    public async Task AddAsync(User item)
    {
        _db.Insert(item);
    }

    public async Task UpdateAsync(User item)
    {
        await _db.UpdateAsync(item);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.DeleteByIdAsync(id);
    }

    public async Task<int> CountAsync()
    {
        return _db.Count();
    }

    public async Task<User> GetByUsernameAsync(string username)
    {
        return _db.SelectWhere(u => u.Username == username).FirstOrDefault();
    }

    public async Task<List<User>> GetActiveUsersAsync()
    {
        return await _db.SelectWhereAsync(u => u.IsActive);
    }
}
```

### OrderRepository.cs
```csharp
public class OrderRepository : IRepository<Order>
{
    private readonly SqliteV2<Order> _db;

    public OrderRepository(SqliteV2<Order> db)
    {
        _db = db;
    }

    public async Task<Order> GetByIdAsync(int id)
    {
        return await _db.SelectByIdAsync(id);
    }

    public async Task<List<Order>> GetAllAsync()
    {
        return await _db.SelectAllAsync();
    }

    public async Task AddAsync(Order item)
    {
        _db.Insert(item);
    }

    public async Task UpdateAsync(Order item)
    {
        await _db.UpdateAsync(item);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.DeleteByIdAsync(id);
    }

    public async Task<int> CountAsync()
    {
        return _db.Count();
    }

    public async Task<List<Order>> GetUserOrdersAsync(int userId)
    {
        return await _db.SelectWhereAsync(o => o.UserId == userId);
    }

    public async Task<List<Order>> GetOrdersByStatusAsync(string status)
    {
        return await _db.SelectWhereAsync(o => o.Status == status);
    }

    public async Task<decimal> GetTotalSalesAsync()
    {
        var orders = await _db.SelectWhereAsync(o => o.Status == "delivered");
        return orders.Sum(o => o.TotalAmount);
    }
}
```

---

## 业务服务层

### OrderService.cs
```csharp
public class OrderService
{
    private readonly OrderRepository _orderRepository;
    private readonly InventoryRepository _inventoryRepository;
    private readonly SqliteV2<Order> _orderDb;
    private readonly SqliteV2<Inventory> _inventoryDb;

    public OrderService(
        OrderRepository orderRepository,
        InventoryRepository inventoryRepository,
        SqliteV2<Order> orderDb,
        SqliteV2<Inventory> inventoryDb)
    {
        _orderRepository = orderRepository;
        _inventoryRepository = inventoryRepository;
        _orderDb = orderDb;
        _inventoryDb = inventoryDb;
    }

    /// <summary>
    /// 创建订单（包含库存检查和扣减）
    /// </summary>
    public async Task<Order> CreateOrderAsync(int userId, List<OrderItemRequest> items)
    {
        using var uow = new SqliteUnitOfWork<Order>(_orderDb);
        await uow.BeginTransactionAsync();

        try
        {
            // 1. 验证库存
            foreach (var item in items)
            {
                var inventory = await _inventoryRepository.GetByProductIdAsync(item.ProductId);
                if (inventory == null || inventory.AvailableQuantity < item.Quantity)
                {
                    throw new InvalidOperationException(
                        $"产品 {item.ProductId} 库存不足");
                }
            }

            // 2. 创建订单
            var order = new Order
            {
                UserId = userId,
                OrderNumber = GenerateOrderNumber(),
                Status = "pending",
                CreatedAt = DateTime.Now,
                TotalAmount = 0
            };
            uow.Add(order);

            // 3. 添加订单项并扣减库存
            decimal totalAmount = 0;
            foreach (var item in items)
            {
                var product = await _inventoryRepository.GetProductAsync(item.ProductId);
                var orderItem = new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price
                };
                order.Items.Add(orderItem);
                totalAmount += orderItem.Subtotal;

                // 扣减库存
                var inventory = await _inventoryRepository.GetByProductIdAsync(item.ProductId);
                inventory.ReservedQuantity += item.Quantity;
                await _inventoryRepository.UpdateAsync(inventory);
            }

            // 4. 更新订单总额
            order.TotalAmount = totalAmount;
            uow.Modify(order);

            // 5. 提交事务
            await uow.CommitAsync();
            Console.WriteLine($"✓ 订单 {order.OrderNumber} 创建成功");

            return order;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 订单创建失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 发货
    /// </summary>
    public async Task ShipOrderAsync(int orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new InvalidOperationException("订单不存在");

        if (order.Status != "processing")
            throw new InvalidOperationException("只能发货处理中的订单");

        order.Status = "shipped";
        order.ShippedAt = DateTime.Now;

        // 扣减库存
        foreach (var item in order.Items)
        {
            var inventory = await _inventoryRepository.GetByProductIdAsync(item.ProductId);
            inventory.Quantity -= item.Quantity;
            inventory.ReservedQuantity -= item.Quantity;
            await _inventoryRepository.UpdateAsync(inventory);
        }

        await _orderRepository.UpdateAsync(order);
        Console.WriteLine($"✓ 订单 {order.OrderNumber} 已发货");
    }

    /// <summary>
    /// 取消订单
    /// </summary>
    public async Task CancelOrderAsync(int orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
            throw new InvalidOperationException("订单不存在");

        if (order.Status == "delivered" || order.Status == "cancelled")
            throw new InvalidOperationException("无法取消已完成或已取消的订单");

        order.Status = "cancelled";

        // 恢复库存
        foreach (var item in order.Items)
        {
            var inventory = await _inventoryRepository.GetByProductIdAsync(item.ProductId);
            inventory.ReservedQuantity -= item.Quantity;
            await _inventoryRepository.UpdateAsync(inventory);
        }

        await _orderRepository.UpdateAsync(order);
        Console.WriteLine($"✓ 订单 {order.OrderNumber} 已取消");
    }

    private string GenerateOrderNumber()
    {
        return $"ORD-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}";
    }
}

public class OrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}
```

---

## 使用示例

### Program.cs
```csharp
using ECommerceApp;

// 初始化
var context = new AppDbContext("Data Source=ecommerce.db");
context.InitializeDatabase();

// 创建仓储
var userRepo = new UserRepository(context.Users);
var productRepo = new ProductRepository(context.Products);
var orderRepo = new OrderRepository(context.Orders);
var inventoryRepo = new InventoryRepository(context.Inventories);

// 创建服务
var orderService = new OrderService(orderRepo, inventoryRepo, context.Orders, context.Inventories);

// 示例 1：创建用户
var user = new User
{
    Username = "alice",
    Email = "alice@example.com",
    PasswordHash = "hashed_password",
    FullName = "Alice Smith",
    PhoneNumber = "123-456-7890",
    Address = "123 Main St",
    Balance = 1000,
    IsActive = true,
    CreatedAt = DateTime.Now
};
await userRepo.AddAsync(user);
Console.WriteLine("✓ 用户创建成功");

// 示例 2：创建产品
var products = new[]
{
    new Product
    {
        Name = "笔记本电脑",
        Description = "高性能笔记本",
        Price = 999.99m,
        Category = "电子产品",
        Sku = "LAPTOP-001",
        IsActive = true,
        CreatedAt = DateTime.Now
    },
    new Product
    {
        Name = "无线鼠标",
        Description = "人体工学设计",
        Price = 29.99m,
        Category = "配件",
        Sku = "MOUSE-001",
        IsActive = true,
        CreatedAt = DateTime.Now
    }
};

foreach (var product in products)
{
    await productRepo.AddAsync(product);
}
Console.WriteLine("✓ 产品创建成功");

// 示例 3：创建库存
var inventories = new[]
{
    new Inventory { ProductId = 1, Quantity = 50, ReservedQuantity = 0, LastUpdatedAt = DateTime.Now },
    new Inventory { ProductId = 2, Quantity = 200, ReservedQuantity = 0, LastUpdatedAt = DateTime.Now }
};

foreach (var inventory in inventories)
{
    await inventoryRepo.AddAsync(inventory);
}
Console.WriteLine("✓ 库存创建成功");

// 示例 4：创建订单
var orderItems = new List<OrderItemRequest>
{
    new { ProductId = 1, Quantity = 1 },
    new { ProductId = 2, Quantity = 2 }
};

var order = await orderService.CreateOrderAsync(user.Id, orderItems);
Console.WriteLine($"✓ 订单创建成功: {order.OrderNumber}");
Console.WriteLine($"  订单总额: ¥{order.TotalAmount}");
Console.WriteLine($"  订单项数: {order.Items.Count}");

// 示例 5：查询用户订单
var userOrders = await orderRepo.GetUserOrdersAsync(user.Id);
Console.WriteLine($"✓ 用户订单数: {userOrders.Count}");

// 示例 6：统计销售额
var totalSales = await orderRepo.GetTotalSalesAsync();
Console.WriteLine($"✓ 总销售额: ¥{totalSales}");

// 示例 7：批量操作
var manyUsers = Enumerable.Range(1, 1000)
    .Select(i => new User
    {
        Username = $"user{i}",
        Email = $"user{i}@example.com",
        PasswordHash = "hashed",
        FullName = $"User {i}",
        IsActive = true,
        CreatedAt = DateTime.Now
    })
    .ToList();

await context.Users.InsertBatchAsync(manyUsers, batchSize: 500);
Console.WriteLine($"✓ 批量创建 {manyUsers.Count} 个用户");

// 示例 8：流式查询
Console.WriteLine("✓ 流式查询所有用户:");
await foreach (var u in context.Users.SelectAllStreamAsync())
{
    Console.WriteLine($"  - {u.Username}: {u.Email}");
}
```

---

## 性能优化建议

1. **批量操作**：使用 `InsertBatchAsync` 而不是循环 `Insert`
2. **流式查询**：处理大数据集时使用 `SelectAllStreamAsync`
3. **事务管理**：复杂操作使用 `SqliteUnitOfWork`
4. **索引**：为常查询的字段添加数据库索引
5. **连接池**：复用数据库连接

---

## 总结

这个示例展示了：
- ✅ 完整的数据模型设计
- ✅ 仓储模式的实现
- ✅ 业务服务层的设计
- ✅ 事务管理的使用
- ✅ 子表系统的应用
- ✅ 批量操作的优化
- ✅ 实际业务流程的处理
