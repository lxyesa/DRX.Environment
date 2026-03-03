# Models 目录 - 数据模型

## 概述
Models 目录定义了 HTTP 框架中使用的基础数据模型类。

## 文件说明

### DataModelBase.cs
**数据模型基类**
- 所有数据模型的基础类
- 提供通用的数据模型功能
- 特点：
  - ID 主键管理（`Id`）

**主要属性：**
- `Id` - 主键 (通常为数据库自增 ID)

### AuthAppDataModel.cs
**OpenAuth 客户端应用注册模型**
- 用于管理允许参与 OpenAuth 的客户端应用
- 可与 `SqliteV2<AuthAppDataModel>` 直接持久化
- 支持 `client_id`、`redirect_uri`、`scope`、启用状态与密钥哈希存储

**主要字段：**
- `ClientId` - 客户端标识
- `ClientSecretHash` - 客户端密钥哈希（可为空）
- `ApplicationName` - 应用展示名
- `ApplicationDescription` - 应用描述
- `RedirectUri` - 回调地址
- `Scopes` - 默认授权范围
- `IsEnabled` - 是否启用
- `CreatedAt` / `UpdatedAt` - 时间戳（Unix 秒）

**示例：**
```csharp
public class User : DataModelBase
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
}
```

### DataModels.cs
**具体的数据模型集合**
- 定义了框架中使用的具体数据模型
- 包可能包括：
  - 用户模型 (User)
  - 会话模型 (Session)
  - 令牌模型 (Token)
  - 日志模型 (Log)
  - 等其他模型

## 数据模型设计原则

1. **继承 DataModelBase** - 获得统一的 ID 和时间戳管理
2. **属性标记** - 使用 [Required]、[StringLength] 等数据注解
3. **关系定义** - 定义与其他模型的关系
4. **验证** - 实现必要的数据验证逻辑

## 使用场景

1. **数据库映射** - ORM 映射到数据库表
2. **API 传输** - 作为请求/响应的数据结构
3. **业务逻辑** - 表示业务实体
4. **缓存** - 缓存模型实例
5. **日志记录** - 记录模型变化

## 典型模型结构

```csharp
public class YourModel : DataModelBase
{
    // 业务字段
    [Required]
    public string Name { get; set; }
    
    [StringLength(500)]
    public string Description { get; set; }
    
    // 关系
    public int CategoryId { get; set; }
    public Category Category { get; set; }
    
    // 状态字段
    [JsonIgnore]
    public bool IsActive { get; set; }
    
    // 自定义方法
    public bool IsValid() { /* ... */ }
}
```

## 与其他模块的关系

- **与 Serialization 的关系** - 模型被序列化为 JSON
- **与 Protocol 的关系** - 作为请求/响应的内容
- **与 Server 的关系** - 处理器返回模型数据
- **与 Client 的关系** - 客户端接收和发送模型
- **与 Session/Auth 的关系** - 用户和会话模型

## 最佳实践

1. **单一职责** - 每个模型负责单一概念
2. **属性验证** - 使用数据注解进行验证
3. **关系管理** - 明确定义模型间的关系
4. **命名规范** - 使用清晰的属性名
5. **文档** - 为模型属性添加注释
6. **版本控制** - 模型变化要考虑向后兼容性

## 相关文档
- 参见 Framework 层的 DataModel 文档了解更多
- 参见 [../Server/README.md](../Server/README.md) 了解处理器中的使用
