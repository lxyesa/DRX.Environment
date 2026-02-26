## Interfaces 数据库接口层

本目录定义了 DRX SQLite ORM 框架的核心契约接口，实现了数据库实体、主表、子表的标准抽象。

### 📋 接口概览

#### IDataBase
- **用途**：主表实体的基础接口
- **关键属性**：
  - `Id`：主键（整数型）
  - `TableName`：表名（可选，默认使用类名）
- **适用场景**：所有需要持久化的主数据模型

#### IDataTable
- **用途**：List<T> 一对多子表的基础接口  
- **关键属性**：
  - `Id`：子表记录ID（整数型自增）
  - `ParentId`：父表ID（外键）
  - `TableName`：子表表名
- **存储特征**：
  - 使用 INTEGER 类型作为主键
  - 支持自增 ID
- **适用场景**：简单的一对多关系，如订单→订单明细

#### IDataTableV2
- **用途**：TableList<T> 一对多子表的基础接口
- **关键属性**：
  - `Id`：子表唯一标识（String/GUID）
  - `ParentId`：父表ID（外键）
  - `CreatedAt`：创建时间戳（毫秒）
  - `UpdatedAt`：更新时间戳（毫秒）
  - `TableName`：子表表名
- **存储特征**：
  - 使用 TEXT 类型存储 GUID
  - 支持时间戳追踪
  - 支持撤销/恢复操作
- **适用场景**：复杂的一对多关系，需要审计日志和版本控制

### 🔗 接口关系图

```
IDataBase
  ├─ 主表实体 (SingleTable)
  └─ 需要序列化的对象
  
IDataTable (V1)
  ├─ List<IDataTable> → 一对多关系
  └─ 整数ID，自增
  
IDataTableV2 (V2)
  ├─ TableList<IDataTableV2> → 一对多关系
  └─ GUID ID，时间戳，支持版本控制
```

### 💡 使用示例

#### 定义主表实体
```csharp
using Drx.Sdk.Network.DataBase;

public class Player : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Level { get; set; }
    
    public string TableName => "game_players";
}
```

#### 定义 V1 子表（简单关系）
```csharp
public class PlayerEquipment : IDataTable
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string Name { get; set; }
    public int Durability { get; set; }
    
    public string TableName => "game_equipment";
}
```

#### 定义 V2 子表（复杂关系，带版本控制）
```csharp
public class PlayerAchievement : IDataTableV2
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int ParentId { get; set; }
    public string AchievementName { get; set; }
    public DateTime UnlockedAt { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    
    public string TableName => "game_achievements";
}
```

### 📚 与其他模块的关系

- **SqliteV2<T>**：使用这些接口做类型约束
- **CRUD 操作**：类型检查基于这些接口
- **TableList.cs**：实现对 IDataTableV2 的集合管理

> **注意**: 接口定义位于 `Database/` 根目录下（命名空间 `Drx.Sdk.Network.DataBase`），本目录仅保留使用说明文档。

### ✅ 接口设计原则

1. **最小化**：只声明必要的契约，具体实现在 ORM 层
2. **稳定性**：接口一旦发布，应避免破坏性变更
3. **可扩展性**：通过 TableName 属性支持自定义映射
4. **性能中立**：接口定义不涉及性能相关的细节

### 🔄 版本说明

| 接口 | 发布版本 | 推荐用途 | 性能特性 |
|------|---------|---------|---------|
| IDataBase | V1 | 所有主表 | 标准（无开销） |
| IDataTable | V1 | 简单子表 | 高效（自增ID） |
| IDataTableV2 | V2 | 复杂子表审计 | 标准（GUID字符串） |

### 📖 参考文档

- [SqliteV2 API 参考](../Docs/SQLITE_V2_API_REFERENCE.md)
- [子表系统详解](../Docs/SQLITE_V2_SUBTABLE_SYSTEM.md)
- [最佳实践](../Docs/SQLITE_V2_BEST_PRACTICES.md)
