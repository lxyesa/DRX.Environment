# SQLite 类库迁移指南

## 概述

我们已经将 `Sqlite<T>` 和 `SqliteRelationship` 两个类的功能合并到一个新的统一类 `SqliteUnified<T>` 中。为了保持向后兼容性，原来的类仍然可用，但已标记为过时。

## 迁移时间表

### 当前阶段（v1.0）
- ✅ `SqliteUnified<T>` 可用，包含所有功能
- ✅ `Sqlite<T>` 仍然可用，但标记为 `[Obsolete]`
- ✅ `SqliteRelationship` 仍然可用，但标记为 `[Obsolete]`
- ✅ 完全向后兼容

### 下一版本（v2.0）
- ⚠️ `Sqlite<T>` 和 `SqliteRelationship` 将生成编译警告
- 📋 建议开始迁移到 `SqliteUnified<T>`

### 未来版本（v3.0）
- ❌ `Sqlite<T>` 和 `SqliteRelationship` 将被移除
- ✅ 只保留 `SqliteUnified<T>`

## 迁移方式

### 1. 简单替换（推荐）

最简单的迁移方式是直接替换类名：

```csharp
// 旧代码
var userDb = new Sqlite<User>("users.db");

// 新代码
var userDb = new SqliteUnified<User>("users.db");
```

### 2. 使用类型别名

如果您有大量代码需要迁移，可以使用类型别名：

```csharp
// 在文件顶部添加
using Sqlite = Drx.Sdk.Network.Sqlite.SqliteUnified;

// 然后您的代码无需更改
var userDb = new Sqlite<User>("users.db");
```

### 3. 全局替换

使用 IDE 的查找替换功能：
- 查找：`new Sqlite<`
- 替换为：`new SqliteUnified<`

### 4. 关联表功能迁移

如果您之前单独使用 `SqliteRelationship`：

```csharp
// 旧代码
var relationDb = new SqliteRelationship("users.db");
relationDb.SaveRelationship<User, Order>(userId, orders, "Order", "UserId");

// 新代码 - 自动处理
var userDb = new SqliteUnified<User>("users.db");
userDb.Save(user); // 自动保存关联的订单

// 或者手动操作关联表
userDb.RepairRelationship(userId, orders, "Order", "UserId", "ProductName", typeof(Order));
```

## 新功能优势

迁移到 `SqliteUnified<T>` 的优势：

### 1. 统一的 API
```csharp
var userDb = new SqliteUnified<User>("users.db");

// 基础操作
userDb.Save(user);
userDb.SaveAll(users);
var users = userDb.Read();
userDb.Delete(user);

// 关联表操作
userDb.RepairRelationshipItem(userId, order, "UserId", "ProductName", typeof(Order));
userDb.QueryRelationship(userId, conditions, "UserId", typeof(Order));
```

### 2. 自动关联表处理
```csharp
public class User : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    // 自动处理关联表
    [SqliteRelation("Order", "UserId")]
    public List<Order> Orders { get; set; } = new List<Order>();
}

// 保存用户时自动保存订单
userDb.Save(user);

// 查询用户时自动加载订单
var userWithOrders = userDb.FindById(1);
```

### 3. 更好的事务处理
```csharp
// 所有操作都在事务中执行
userDb.Save(user); // 包含主表和关联表的完整事务
```

### 4. 增强的修复功能
```csharp
// 更灵活的修复操作
userDb.Repair(user, new Dictionary<string, object>
{
    { "Username", "john" },
    { "Email", "old@email.com" }
});

// 关联表修复
userDb.RepairRelationship(userId, orders, "Order", "UserId", "ProductName", typeof(Order));
```

## 兼容性说明

### 完全兼容的操作
以下操作在新旧版本中完全相同：
- `Save(item)`
- `SaveAll(items)`
- `Read(conditions)`
- `ReadSingle(where, value)`
- `FindById(id)`
- `Delete(item)`
- `DeleteWhere(conditions)`
- `Repair(item, conditions)`

### 新增功能
以下是新版本独有的功能：
- 自动关联表处理
- `RepairRelationship` 系列方法
- `QueryRelationship` 方法
- `UpdateRelationshipItem` 方法
- `DeleteRelationshipItem` 方法
- `AddRelationshipItem` 方法

## 迁移检查清单

### 步骤 1：代码迁移
- [ ] 替换 `Sqlite<T>` 为 `SqliteUnified<T>`
- [ ] 移除单独的 `SqliteRelationship` 使用
- [ ] 更新关联表操作代码

### 步骤 2：功能验证
- [ ] 测试基础 CRUD 操作
- [ ] 测试关联表自动处理
- [ ] 测试事务完整性
- [ ] 测试修复功能

### 步骤 3：性能验证
- [ ] 对比迁移前后的性能
- [ ] 验证内存使用情况
- [ ] 测试大数据量操作

### 步骤 4：清理工作
- [ ] 移除对旧类的引用
- [ ] 更新文档和注释
- [ ] 更新单元测试

## 常见问题

### Q: 什么时候必须迁移？
A: 当前不是必须的，但建议在 v2.0 发布前完成迁移以避免编译警告。

### Q: 迁移会破坏现有数据吗？
A: 不会。新类使用相同的数据库结构和数据格式。

### Q: 性能有影响吗？
A: 新类的性能更好，特别是在关联表处理方面。

### Q: 可以同时使用新旧类吗？
A: 可以，但不建议在同一个项目中混合使用。

### Q: 如果遇到迁移问题怎么办？
A: 可以先使用类型别名进行过渡，或者逐步迁移部分功能。

## 技术支持

如果在迁移过程中遇到问题，请：
1. 查看示例代码：`SqliteUnifiedExample.cs`
2. 阅读完整文档：`README.md`
3. 检查单元测试用例
4. 提交问题报告

## 总结

迁移到 `SqliteUnified<T>` 将为您提供：
- 更统一的 API
- 更强大的功能
- 更好的性能
- 更简洁的代码

建议尽早开始迁移计划，以充分利用新功能并避免未来的兼容性问题。
