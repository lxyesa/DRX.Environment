# SQLite V2 文档索引

## 📚 完整文档列表

### 🚀 入门指南
- **[快速开始](SQLITE_V2_QUICKSTART.md)** - 5 分钟快速上手
  - 安装和初始化
  - 基本 CRUD 操作
  - 常见任务示例

### 📖 详细指南
- **[完整指南](SQLITE_V2_GUIDE.md)** - 全面的功能介绍
  - 核心特性概述
  - 架构设计
  - 使用示例
  - 性能基准

- **[API 参考](SQLITE_V2_API_REFERENCE.md)** - 详细的 API 文档
  - SqliteV2<T> 类
  - SqliteUnitOfWork<T> 类
  - SqliteBatchBuffer<T> 类
  - TableList<T> 类
  - 接口定义

### ⚡ 性能优化
- **[性能优化指南](SQLITE_V2_PERFORMANCE.md)** - 性能调优技巧
  - 性能基准数据
  - 优化策略
  - 批量操作优化
  - 查询优化
  - 内存管理
  - 并发处理
  - 性能监测

### 📊 高级特性
- **[子表系统](SQLITE_V2_SUBTABLE_SYSTEM.md)** - 一对多关系管理
  - 子表系统概述
  - 核心概念
  - TableList<T> 详解
  - 实现示例
  - 高级用法
  - 性能优化

- **[事务管理](SQLITE_V2_TRANSACTIONS.md)** - 事务和工作单元模式
  - 事务基础
  - 工作单元模式
  - 实现示例
  - 错误处理
  - 最佳实践

### ✅ 最佳实践
- **[最佳实践指南](SQLITE_V2_BEST_PRACTICES.md)** - 设计和编码建议
  - 设计原则
  - 代码组织
  - 性能最佳实践
  - 安全最佳实践
  - 测试策略
  - 常见陷阱

---

## 🎯 按场景选择文档

### 我是新手，想快速上手
→ 阅读 [快速开始](SQLITE_V2_QUICKSTART.md)

### 我想了解所有功能
→ 阅读 [完整指南](SQLITE_V2_GUIDE.md)

### 我需要查找特定 API
→ 查看 [API 参考](SQLITE_V2_API_REFERENCE.md)

### 我的应用性能不理想
→ 阅读 [性能优化指南](SQLITE_V2_PERFORMANCE.md)

### 我需要处理一对多关系
→ 阅读 [子表系统](SQLITE_V2_SUBTABLE_SYSTEM.md)

### 我需要事务支持
→ 阅读 [事务管理](SQLITE_V2_TRANSACTIONS.md)

### 我想写出高质量代码
→ 阅读 [最佳实践指南](SQLITE_V2_BEST_PRACTICES.md)

---

## 📋 核心概念速查

### 数据模型
```csharp
// 主表
public class User : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string TableName => "users";
}

// 子表
public class Order : IDataTableV2
{
    public string Id { get; set; }
    public int ParentId { get; set; }
    public string TableName => "orders";
}
```

### 基本操作
```csharp
var db = new SqliteV2<User>("Data Source=app.db");

// CRUD
db.Insert(user);
var user = db.SelectById(1);
db.Update(user);
db.DeleteById(1);

// 批量
await db.InsertBatchAsync(users);
await db.UpdateBatchAsync(users);
await db.DeleteBatchAsync(ids);

// 查询
var active = db.SelectWhere(u => u.IsActive);
await foreach (var u in db.SelectAllStreamAsync()) { }
```

### 事务
```csharp
using var uow = new SqliteUnitOfWork<User>(db);
await uow.BeginTransactionAsync();
uow.Add(newUser);
uow.Modify(existingUser);
uow.Remove(userToDelete);
await uow.CommitAsync();
```

### 子表
```csharp
var player = db.SelectById(1);
player.Achievements.Add(new Achievement { ... });
player.Achievements.Remove(achievement);
var filtered = player.Achievements.Where(a => a.Unlocked);
```

---

## 🔍 常见问题

### Q: SqliteV2 相比 V1 快多少？
A: 性能提升 200-300 倍。详见 [性能基准](SQLITE_V2_GUIDE.md#性能基准)

### Q: 如何处理大数据集？
A: 使用流式查询 `SelectAllStreamAsync()`。详见 [查询优化](SQLITE_V2_PERFORMANCE.md#查询优化)

### Q: 如何实现一对多关系？
A: 使用子表系统 `TableList<T>`。详见 [子表系统](SQLITE_V2_SUBTABLE_SYSTEM.md)

### Q: 如何确保数据一致性？
A: 使用工作单元模式 `SqliteUnitOfWork<T>`。详见 [事务管理](SQLITE_V2_TRANSACTIONS.md)

### Q: 批量操作的最佳批次大小是多少？
A: 通常 500-2000。详见 [批量操作优化](SQLITE_V2_PERFORMANCE.md#批量操作)

### Q: 如何监测性能？
A: 使用性能计时工具。详见 [性能监测](SQLITE_V2_PERFORMANCE.md#性能监测)

---

## 📊 版本信息

| 特性 | V1 | V2 |
|------|----|----|
| 性能 | 基准 | 200-300x |
| 编译表达式缓存 | ❌ | ✅ |
| 对象池 | ❌ | ✅ |
| 子表系统 | ❌ | ✅ |
| 异步支持 | 基础 | 完整 |
| 事务管理 | ❌ | ✅ |
| 工作单元模式 | ❌ | ✅ |

---

## 🛠️ 工具和实用程序

### SqliteV2<T>
主 ORM 类，提供所有数据库操作。

### SqliteUnitOfWork<T>
工作单元模式实现，用于事务管理。

### SqliteBatchBuffer<T>
批处理缓冲区，用于高效的批量插入。

### TableList<T>
子表集合，支持自动同步和 LINQ 操作。

---

## 📞 获取帮助

- 查看相关文档中的示例代码
- 阅读 [最佳实践指南](SQLITE_V2_BEST_PRACTICES.md) 中的常见陷阱
- 检查 [API 参考](SQLITE_V2_API_REFERENCE.md) 中的方法签名

---

## 📝 文档更新日志

- **2026-02-24** - 创建完整文档集
  - 快速开始指南
  - 完整功能指南
  - API 参考手册
  - 性能优化指南
  - 子表系统指南
  - 事务管理指南
  - 最佳实践指南
  - 文档索引
