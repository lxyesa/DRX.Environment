# SQLite V2 子表系统完全指南

## 目录
- [子表系统概述](#子表系统概述)
- [核心概念](#核心概念)
- [TableList<T> 详解](#tablelistt-详解)
- [实现示例](#实现示例)
- [高级用法](#高级用法)
- [性能优化](#性能优化)
- [常见问题](#常见问题)

---

## 子表系统概述

### 什么是子表系统？

子表系统是 SqliteV2 V2 版本的核心特性，用于管理主表与子表之间的一对多关系。

**特点：**
- 自动同步：修改子表时自动同步到数据库
- 完整 LINQ 支持：支持 Where、Select、GroupBy 等操作
- 高效查询：避免 N+1 查询问题
- 类型安全：编译时类型检查

### 应用场景

| 场景 | 主表 | 子表 | 示例 |
|------|------|------|------|
| 电商 | 订单 | 订单项 | Order → OrderItem |
| 游戏 | 玩家 | 激活 Mod | Player → ActiveMod |
| 社交 | 用户 | 成就 | User → Achievement |
| 博客 | 文章 | 评论 | Article → Comment |
| 库存 | 仓库 | 库存项 | Warehouse → InventoryItem |

---

## 核心概念

### 1. 主表接口 (IDataBase)

```csharp
public interface IDataBase
{
    int Id { get; set; }
    string TableName { get; }
}
```

主表必须实现 `IDataBase` 接口，包含：
- `Id` 属性（主键）
- `TableName` 属性（表名）

### 2. 子表接口 (IDataTableV2)

```csharp
public interface IDataTableV2
{
    string Id { get; set; }           // 子表主键（String 类型）
    int ParentId { get; set; }        // 父表 ID
    string TableName { get; }         // 表名
}
```

子表必须实现 `IDataTableV2` 接口，包含：
- `Id` 属性（String 类型主键，通常为 GUID）
- `ParentId` 属性（指向主表的外键）
- `TableName` 属性（表名）

### 3. TableList<T> 集合

```csharp
public class TableList<T> : IList<T>, IAsyncEnumerable<T>
    where T : class, IDataTableV2, new()
{
    // 自动同步到数据库
    // 支持 LINQ 操作
    // 支持异步枚举
}
```

---

## TableList<T> 详解

### 属性

#### Count
```csharp
public int Count { get; }
```
获取子表中的项数。

#### IsReadOnly
```csharp
public bool IsReadOnly { get; }
```
获取是否为只读（始终为 false）。

#### IsSynchronized
```csharp
public bool IsSynchronized { get; }
```
获取是否已同步到数据库。

### 方法

#### Add
```csharp
public void Add(T item)
```
添加项到子表。自动设置 `ParentId` 并同步到数据库。

**示例：**
```csharp
var player = db.SelectById(1);
player.ActiveMods.Add(new ActiveMod
{
    ModId = 100,
    CreatedAt = DateTimeOffset.Now.ToUnixTimeSeconds()
});
// 自动同步到数据库
```

#### AddRange
```csharp
public void AddRange(IEnumerable<T> items)
```
批量添加项。

**示例：**
```csharp
var mods = new[]
{
    new ActiveMod { ModId = 100 },
    new ActiveMod { ModId = 101 },
    new ActiveMod { ModId = 102 }
};
player.ActiveMods.AddRange(mods);
```

#### Remove
```csharp
public bool Remove(T item)
```
移除项。自动从数据库删除。

**示例：**
```csharp
var mod = player.ActiveMods.First();
player.ActiveMods.Remove(mod);  // 自动删除
```

#### RemoveAt
```csharp
public void RemoveAt(int index)
```
按索引移除项。

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

#### IndexOf
```csharp
public int IndexOf(T item)
```
获取项的索引。

#### Insert
```csharp
public void Insert(int index, T item)
```
在指定位置插入项。

#### GetEnumerator
```csharp
public IEnumerator<T> GetEnumerator()
```
获取枚举器，支持 foreach 循环。

#### GetAsyncEnumerator
```csharp
public IAsyncEnumerator<T> GetAsyncEnumerator(
    CancellationToken cancellationToken = default)
```
获取异步枚举器。

**示例：**
```csharp
await foreach (var mod in player.ActiveMods)
{
    Console.WriteLine(mod.ModId);
}
```

### LINQ 支持

TableList<T> 完全支持 LINQ 操作：

```csharp
// Where
var activeMods = player.ActiveMods
    .Where(m => m.IsActive)
    .ToList();

// Select
var modIds = player.ActiveMods
    .Select(m => m.ModId)
    .ToList();

// FirstOrDefault
var firstMod = player.ActiveMods
    .FirstOrDefault(m => m.ModId == 100);

// Any
bool hasExpiredMods = player.ActiveMods
    .Any(m => m.ExpiresAt < DateTime.Now);

// GroupBy
var groupedByStatus = player.ActiveMods
    .GroupBy(m => m.Status)
    .ToList();

// OrderBy
var sortedMods = player.ActiveMods
    .OrderBy(m => m.CreatedAt)
    .ToList();

// Count
int totalMods = player.ActiveMods.Count();

// Sum
long totalExpiration = player.ActiveMods
    .Sum(m => m.ExpiresAt);
```

---

## 实现示例

### 基础示例：订单系统

```csharp
// 定义主表
public class Order : IDataBase
{
    public int Id { get; set; }
    public string OrderNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }

    // 子表集合
    public TableList<OrderItem> Items { get; set; } = new();

    public string TableName => "orders";
}

// 定义子表
public class OrderItem : IDataTableV2
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int ParentId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public string TableName => "order_items";
}

// 使用
var db = new SqliteV2<Order>("Data Source=shop.db");
db.CreateTableIfNotExists();

// 创建订单
var order = new Order
{
    OrderNumber = "ORD-001",
    CreatedAt = DateTime.Now,
    TotalAmount = 0
};
db.Insert(order);

// 添加订单项
order.Items.Add(new OrderItem
{
    ProductId = 1,
    Quantity = 2,
    UnitPrice = 99.99m
});

order.Items.Add(new OrderItem
{
    ProductId = 2,
    Quantity = 1,
    UnitPrice = 49.99m
});

// 更新订单总额
order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
db.Update(order);

// 查询订单及其项
var savedOrder = db.SelectById(order.Id);
Console.WriteLine($"订单 {savedOrder.OrderNumber} 包含 {savedOrder.Items.Count} 项");

foreach (var item in savedOrder.Items)
{
    Console.WriteLine($"  产品 {item.ProductId}: {item.Quantity} × {item.UnitPrice}");
}
```

### 游戏示例：玩家 Mod 系统

```csharp
public class Player : IDataBase
{
    public int Id { get; set; }
    public string PlayerName { get; set; }
    public int Level { get; set; }
    public long RegisteredAt { get; set; }

    public TableList<ActiveMod> ActiveMods { get; set; } = new();
    public TableList<PlayerAchievement> Achievements { get; set; } = new();

    public string TableName => "players";
}

public class ActiveMod : IDataTableV2
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int ParentId { get; set; }
    public int ModId { get; set; }
    public long ActivatedAt { get; set; }
    public long ExpiresAt { get; set; }
    public string Status { get; set; } = "active";

    public string TableName => "active_mods";
}

public class PlayerAchievement : IDataTableV2
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int ParentId { get; set; }
    public int AchievementId { get; set; }
    public long UnlockedAt { get; set; }

    public string TableName => "player_achievements";
}

// 使用
var db = new SqliteV2<Player>("Data Source=game.db");

var player = db.SelectById(1);

// 激活 Mod
var now = DateTimeOffset.Now.ToUnixTimeSeconds();
player.ActiveMods.Add(new ActiveMod
{
    ModId = 100,
    ActivatedAt = now,
    ExpiresAt = now + 7 * 24 * 3600  // 7 天后过期
});

// 解锁成就
player.Achievements.Add(new PlayerAchievement
{
    AchievementId = 1,
    UnlockedAt = now
});

// 同步到数据库
db.Update(player);

// 查询已过期的 Mod
var expiredMods = player.ActiveMods
    .Where(m => m.ExpiresAt < now)
    .ToList();

// 移除已过期的 Mod
foreach (var mod in expiredMods)
{
    player.ActiveMods.Remove(mod);
}

db.Update(player);
```

---

## 高级用法

### 1. 条件查询和过滤

```csharp
var player = db.SelectById(1);

// 查询活跃的 Mod
var activeMods = player.ActiveMods
    .Where(m => m.Status == "active")
    .ToList();

// 查询即将过期的 Mod（7 天内）
var now = DateTimeOffset.Now.ToUnixTimeSeconds();
var expiringMods = player.ActiveMods
    .Where(m => m.ExpiresAt - now < 7 * 24 * 3600)
    .OrderBy(m => m.ExpiresAt)
    .ToList();

// 按 Mod ID 分组统计
var modStats = player.ActiveMods
    .GroupBy(m => m.ModId)
    .Select(g => new
    {
        ModId = g.Key,
        Count = g.Count(),
        FirstActivated = g.Min(m => m.ActivatedAt)
    })
    .ToList();
```

### 2. 批量操作

```csharp
var player = db.SelectById(1);

// 批量添加
var newMods = Enumerable.Range(1, 100)
    .Select(i => new ActiveMod
    {
        ModId = i,
        ActivatedAt = DateTimeOffset.Now.ToUnixTimeSeconds()
    })
    .ToList();

player.ActiveMods.AddRange(newMods);

// 批量移除
var modsToRemove = player.ActiveMods
    .Where(m => m.ModId > 50)
    .ToList();

foreach (var mod in modsToRemove)
{
    player.ActiveMods.Remove(mod);
}

// 同步所有变更
db.Update(player);
```

### 3. 异步操作

```csharp
var player = db.SelectById(1);

// 异步枚举子表
await foreach (var mod in player.ActiveMods)
{
    await ProcessModAsync(mod);
}

// 异步更新主表（包括子表变更）
await db.UpdateAsync(player);
```

### 4. 多个子表

```csharp
public class User : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; }

    // 多个子表
    public TableList<Order> Orders { get; set; } = new();
    public TableList<Review> Reviews { get; set; } = new();
    public TableList<Wishlist> Wishlist { get; set; } = new();

    public string TableName => "users";
}

// 使用
var user = db.SelectById(1);

// 操作不同的子表
user.Orders.Add(new Order { /* ... */ });
user.Reviews.Add(new Review { /* ... */ });
user.Wishlist.Add(new Wishlist { /* ... */ });

db.Update(user);
```

---

## 性能优化

### 1. 避免重复查询

```csharp
// ❌ 不好：每次都查询主表
for (int i = 0; i < 100; i++)
{
    var player = db.SelectById(1);  // 重复查询
    player.ActiveMods.Add(new ActiveMod { /* ... */ });
}

// ✅ 好：查询一次，多次操作
var player = db.SelectById(1);
for (int i = 0; i < 100; i++)
{
    player.ActiveMods.Add(new ActiveMod { /* ... */ });
}
db.Update(player);
```

### 2. 批量操作优化

```csharp
// ❌ 不好：逐条添加并更新
foreach (var mod in mods)
{
    player.ActiveMods.Add(mod);
    db.Update(player);  // 每次都更新
}

// ✅ 好：批量添加后一次更新
player.ActiveMods.AddRange(mods);
db.Update(player);
```

### 3. 内存优化

```csharp
// ❌ 不好：加载所有数据到内存
var allPlayers = db.SelectAll();
foreach (var player in allPlayers)
{
    var totalMods = player.ActiveMods.Count;
}

// ✅ 好：流式处理
await foreach (var player in db.SelectAllStreamAsync())
{
    var totalMods = player.ActiveMods.Count;
}
```

---

## 常见问题

### Q1: 如何初始化子表？

```csharp
public class Player : IDataBase
{
    public int Id { get; set; }

    // ✅ 正确：使用属性初始化器
    public TableList<ActiveMod> ActiveMods { get; set; } = new();

    // ❌ 错误：不初始化会导致 NullReferenceException
    public TableList<ActiveMod> BadMods { get; set; }
}
```

### Q2: 子表项的 ID 如何生成？

```csharp
public class ActiveMod : IDataTableV2
{
    // ✅ 推荐：使用 GUID
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // ✅ 也可以：使用其他唯一标识
    public string Id { get; set; } = $"{ParentId}_{DateTime.Now.Ticks}";
}
```

### Q3: 如何删除所有子表项？

```csharp
var player = db.SelectById(1);

// 方式 1：Clear
player.ActiveMods.Clear();

// 方式 2：逐条删除
foreach (var mod in player.ActiveMods.ToList())
{
    player.ActiveMods.Remove(mod);
}

db.Update(player);
```

### Q4: 子表变更是否自动同步？

```csharp
var player = db.SelectById(1);

// 添加项
player.ActiveMods.Add(new ActiveMod { /* ... */ });
// 此时已自动同步到数据库

// 但更新主表时需要显式调用
player.Level = 10;
db.Update(player);  // 必须调用
```

### Q5: 如何查询特定条件的子表项？

```csharp
var player = db.SelectById(1);

// 使用 LINQ
var activeMods = player.ActiveMods
    .Where(m => m.Status == "active")
    .OrderBy(m => m.CreatedAt)
    .ToList();

// 使用 FirstOrDefault
var firstMod = player.ActiveMods
    .FirstOrDefault(m => m.ModId == 100);

// 使用 Any
bool hasExpiredMods = player.ActiveMods
    .Any(m => m.ExpiresAt < DateTime.Now);
```

---

## 最佳实践总结

1. **始终初始化子表**：使用属性初始化器 `= new()`
2. **批量操作**：使用 `AddRange` 而不是逐条 `Add`
3. **及时同步**：修改主表属性后调用 `db.Update()`
4. **避免重复查询**：查询一次主表，多次操作子表
5. **使用 LINQ**：充分利用 LINQ 进行查询和过滤
6. **异步处理**：大数据集使用异步流式查询
7. **性能监测**：使用性能工具监测子表操作性能
