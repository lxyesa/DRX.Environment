# IIndexable 接口

`IIndexable` 是一个简单而重要的接口，为对象提供唯一标识符功能。它是 `IndexedRepository<T>` 类的基础，使对象能够被索引和有效地检索。

## 基本信息

- **命名空间**: `Drx.Sdk.Network.DataBase`
- **类型**: 公共接口
- **主要职责**: 为对象提供唯一标识符

## 属性

### Id

```csharp
string Id { get; }
```

获取对象的唯一标识符。

**返回值**:
- 表示对象唯一标识符的字符串

## 实现指南

实现 `IIndexable` 接口时，应确保 `Id` 属性返回的值在对象集合中是唯一的。这通常可以通过以下方式实现:

1. 使用自然唯一标识符（例如用户名、电子邮件等）
2. 使用GUID或UUID
3. 使用自增ID或数据库生成的ID
4. 使用复合键的哈希值

## 示例实现

### 基本实现

```csharp
public class User : IIndexable
{
    public string Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    
    // 其他属性和方法...
}
```

### 使用GUID作为ID

```csharp
public class Document : IIndexable
{
    private string _id;
    
    public Document()
    {
        _id = Guid.NewGuid().ToString();
    }
    
    public string Id => _id;
    
    public string Title { get; set; }
    public string Content { get; set; }
    
    // 其他属性和方法...
}
```

### 使用复合键

```csharp
public class OrderItem : IIndexable
{
    public string OrderId { get; set; }
    public string ProductId { get; set; }
    
    // 复合键: OrderId + ProductId
    public string Id => $"{OrderId}_{ProductId}";
    
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    
    // 其他属性和方法...
}
```

## 与其他接口组合

`IIndexable` 接口通常与 `IXmlSerializable` 接口一起使用，特别是在 `IndexedRepository<T>` 上下文中:

```csharp
public class Product : IIndexable, IXmlSerializable
{
    public string Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    
    public void WriteToXml(IXmlNode node)
    {
        node.PushString("info", "name", Name);
        node.PushDecimal("info", "price", Price);
    }
    
    public void ReadFromXml(IXmlNode node)
    {
        Name = node.GetString("info", "name");
        Price = node.GetDecimal("info", "price", 0);
    }
}
```

## 使用场景

### 在IndexedRepository中使用

```csharp
// 创建实现IIndexable的类
public class Customer : IIndexable, IXmlSerializable
{
    public string Id { get; set; }
    public string Name { get; set; }
    
    public void WriteToXml(IXmlNode node)
    {
        node.PushString("data", "name", Name);
    }
    
    public void ReadFromXml(IXmlNode node)
    {
        Name = node.GetString("data", "name");
    }
}

// 使用IndexedRepository
var repository = new IndexedRepository<Customer>("data/customers");

// 添加客户
var customer = new Customer { Id = "cust001", Name = "ACME Corp" };
repository.Save(customer);

// 通过ID检索客户
var retrievedCustomer = repository.Get("cust001");
```

### 在集合中使用

```csharp
public class Task : IIndexable
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string Title { get; set; }
    public bool IsCompleted { get; set; }
}

// 创建任务字典，使用ID作为键
Dictionary<string, Task> taskDict = new Dictionary<string, Task>();

// 添加任务
var task1 = new Task { Title = "完成报告" };
var task2 = new Task { Title = "准备演示" };

taskDict.Add(task1.Id, task1);
taskDict.Add(task2.Id, task2);

// 通过ID检索任务
if (taskDict.TryGetValue(task1.Id, out var foundTask))
{
    Console.WriteLine($"找到任务: {foundTask.Title}");
}
```

## 最佳实践

1. **唯一性**: 确保ID在对象集合中是唯一的
2. **不可变性**: 一旦分配，ID应该不可变，避免在对象生命周期中更改ID
3. **持久性**: ID应该在对象序列化和反序列化过程中保持不变
4. **有效的XML标签**: 如果与XML系统一起使用，确保ID是有效的XML标签名（或使用前缀/转换）
5. **可读性**: 在可能的情况下，使用可读的ID，便于调试和日志记录

## 注意事项

- ID通常应该是字符串类型，以提供最大的灵活性
- 在某些情况下，可能需要确保ID符合特定格式（例如，有效的XML标签名）
- 在使用 `IndexedRepository<T>` 时，可以使用 `keyPrefix` 参数确保ID是有效的XML标签名 