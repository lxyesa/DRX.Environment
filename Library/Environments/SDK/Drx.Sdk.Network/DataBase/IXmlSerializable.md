# IXmlSerializable 接口

`IXmlSerializable` 是一个关键接口，定义了对象与XML节点之间的序列化和反序列化契约。它使对象能够将其状态保存到XML结构中，并从中恢复，是XML数据库系统中对象持久化的基础。

## 基本信息

- **命名空间**: `Drx.Sdk.Network.DataBase`
- **类型**: 公共接口
- **主要职责**: 定义对象与XML之间的序列化和反序列化方法

## 方法

### WriteToXml

```csharp
void WriteToXml(IXmlNode node);
```

将对象的状态写入XML节点。

**参数**:
- `node`: 目标XML节点，用于存储对象数据

**行为**:
- 实现此方法时，应将对象的所有相关属性写入提供的XML节点
- 通常使用 `node.Push*` 方法族来写入各种类型的数据

### ReadFromXml

```csharp
void ReadFromXml(IXmlNode node);
```

从XML节点读取对象的状态。

**参数**:
- `node`: 源XML节点，包含对象数据

**行为**:
- 实现此方法时，应从提供的XML节点读取所有相关属性
- 通常使用 `node.Get*` 方法族来读取各种类型的数据

## 实现指南

实现 `IXmlSerializable` 接口时，应遵循以下原则:

1. **完整性**: 确保所有需要持久化的属性都被序列化和反序列化
2. **一致性**: `WriteToXml` 和 `ReadFromXml` 方法应该是对称的，确保正确恢复对象状态
3. **健壮性**: 处理可能的空值、默认值和类型转换
4. **结构化**: 使用有意义的节点和属性名称，组织数据结构

## 示例实现

### 基本实现

```csharp
public class User : IXmlSerializable
{
    public string Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public int Age { get; set; }
    public bool IsActive { get; set; }
    
    public void WriteToXml(IXmlNode node)
    {
        // 将属性写入XML节点
        node.PushString("info", "id", Id);
        node.PushString("info", "username", Username);
        node.PushString("info", "email", Email);
        node.PushInt("info", "age", Age);
        node.PushBool("info", "isActive", IsActive);
    }
    
    public void ReadFromXml(IXmlNode node)
    {
        // 从XML节点读取属性
        Id = node.GetString("info", "id");
        Username = node.GetString("info", "username");
        Email = node.GetString("info", "email");
        Age = node.GetInt("info", "age");
        IsActive = node.GetBool("info", "isActive");
    }
}
```

### 处理复杂属性

```csharp
public class Product : IXmlSerializable
{
    public string Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public List<string> Categories { get; set; } = new List<string>();
    public DateTime CreatedDate { get; set; }
    
    public void WriteToXml(IXmlNode node)
    {
        // 基本属性
        node.PushString("info", "id", Id);
        node.PushString("info", "name", Name);
        node.PushDecimal("info", "price", Price);
        
        // 日期 - 转换为字符串
        node.PushString("info", "createdDate", CreatedDate.ToString("o"));
        
        // 集合 - 序列化为多个值或单独的节点
        if (Categories.Count > 0)
        {
            node.PushString("categories", "items", Categories.ToArray());
        }
    }
    
    public void ReadFromXml(IXmlNode node)
    {
        // 基本属性
        Id = node.GetString("info", "id");
        Name = node.GetString("info", "name");
        Price = node.GetDecimal("info", "price", 0);
        
        // 日期 - 从字符串解析
        string dateStr = node.GetString("info", "createdDate");
        if (DateTime.TryParse(dateStr, out DateTime date))
        {
            CreatedDate = date;
        }
        
        // 集合 - 反序列化
        Categories = new List<string>(node.GetStringArray("categories", "items"));
    }
}
```

### 处理嵌套对象

```csharp
public class Address : IXmlSerializable
{
    public string Street { get; set; }
    public string City { get; set; }
    public string ZipCode { get; set; }
    
    public void WriteToXml(IXmlNode node)
    {
        node.PushString("data", "street", Street);
        node.PushString("data", "city", City);
        node.PushString("data", "zipCode", ZipCode);
    }
    
    public void ReadFromXml(IXmlNode node)
    {
        Street = node.GetString("data", "street");
        City = node.GetString("data", "city");
        ZipCode = node.GetString("data", "zipCode");
    }
}

public class Customer : IXmlSerializable
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Address BillingAddress { get; set; } = new Address();
    public Address ShippingAddress { get; set; } = new Address();
    
    public void WriteToXml(IXmlNode node)
    {
        // 基本属性
        node.PushString("info", "id", Id);
        node.PushString("info", "name", Name);
        
        // 嵌套对象 - 创建子节点
        var billingNode = node.PushNode("billingAddress");
        BillingAddress.WriteToXml(billingNode);
        
        var shippingNode = node.PushNode("shippingAddress");
        ShippingAddress.WriteToXml(shippingNode);
    }
    
    public void ReadFromXml(IXmlNode node)
    {
        // 基本属性
        Id = node.GetString("info", "id");
        Name = node.GetString("info", "name");
        
        // 嵌套对象 - 获取子节点
        var billingNode = node.GetNode("billingAddress");
        if (billingNode != null)
        {
            BillingAddress = new Address();
            BillingAddress.ReadFromXml(billingNode);
        }
        
        var shippingNode = node.GetNode("shippingAddress");
        if (shippingNode != null)
        {
            ShippingAddress = new Address();
            ShippingAddress.ReadFromXml(shippingNode);
        }
    }
}
```

### 处理对象集合

```csharp
public class OrderItem : IXmlSerializable
{
    public string ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    
    public void WriteToXml(IXmlNode node)
    {
        node.PushString("data", "productId", ProductId);
        node.PushInt("data", "quantity", Quantity);
        node.PushDecimal("data", "unitPrice", UnitPrice);
    }
    
    public void ReadFromXml(IXmlNode node)
    {
        ProductId = node.GetString("data", "productId");
        Quantity = node.GetInt("data", "quantity");
        UnitPrice = node.GetDecimal("data", "unitPrice", 0);
    }
}

public class Order : IXmlSerializable
{
    public string Id { get; set; }
    public DateTime OrderDate { get; set; }
    public List<OrderItem> Items { get; set; } = new List<OrderItem>();
    
    public void WriteToXml(IXmlNode node)
    {
        // 基本属性
        node.PushString("info", "id", Id);
        node.PushString("info", "orderDate", OrderDate.ToString("o"));
        
        // 对象集合 - 使用SerializeList
        node.SerializeList("items", Items);
    }
    
    public void ReadFromXml(IXmlNode node)
    {
        // 基本属性
        Id = node.GetString("info", "id");
        
        string dateStr = node.GetString("info", "orderDate");
        if (DateTime.TryParse(dateStr, out DateTime date))
        {
            OrderDate = date;
        }
        
        // 对象集合 - 使用DeserializeList
        Items = node.DeserializeList<OrderItem>("items");
    }
}
```

## 与其他接口组合

`IXmlSerializable` 接口通常与 `IIndexable` 接口一起使用，特别是在 `IndexedRepository<T>` 上下文中:

```csharp
public class Product : IIndexable, IXmlSerializable
{
    public string Id { get; set; } // 实现IIndexable
    public string Name { get; set; }
    public decimal Price { get; set; }
    
    // 实现IXmlSerializable
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
// 定义实现IXmlSerializable的类
public class User : IIndexable, IXmlSerializable
{
    public string Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    
    public void WriteToXml(IXmlNode node)
    {
        node.PushString("info", "username", Username);
        node.PushString("info", "email", Email);
    }
    
    public void ReadFromXml(IXmlNode node)
    {
        Username = node.GetString("info", "username");
        Email = node.GetString("info", "email");
    }
}

// 使用IndexedRepository
var repository = new IndexedRepository<User>("data/users");

// 添加用户
var user = new User { Id = "user1", Username = "john", Email = "john@example.com" };
repository.Save(user);

// 检索用户
var retrievedUser = repository.Get("user1");
```

### 直接使用XmlNode

```csharp
public class Settings : IXmlSerializable
{
    public string Theme { get; set; }
    public bool NotificationsEnabled { get; set; }
    public int RefreshInterval { get; set; }
    
    public void WriteToXml(IXmlNode node)
    {
        node.PushString("display", "theme", Theme);
        node.PushBool("notifications", "enabled", NotificationsEnabled);
        node.PushInt("data", "refreshInterval", RefreshInterval);
    }
    
    public void ReadFromXml(IXmlNode node)
    {
        Theme = node.GetString("display", "theme", "default");
        NotificationsEnabled = node.GetBool("notifications", "enabled", true);
        RefreshInterval = node.GetInt("data", "refreshInterval", 60);
    }
}

// 使用XmlDatabase直接保存设置
var database = new XmlDatabase();
var rootNode = database.CreateRoot("settings.xml");

var settings = new Settings
{
    Theme = "dark",
    NotificationsEnabled = true,
    RefreshInterval = 30
};

// 序列化设置
settings.WriteToXml(rootNode);
database.SaveChanges();

// 反序列化设置
var loadedSettings = new Settings();
loadedSettings.ReadFromXml(rootNode);
```

## 最佳实践

1. **结构化数据**: 使用有意义的节点名称组织数据，避免扁平结构
2. **默认值**: 在 `ReadFromXml` 中为可选属性提供合理的默认值
3. **类型安全**: 使用适当的类型转换和验证，处理可能的解析错误
4. **版本兼容**: 设计序列化格式时考虑向后兼容性，以支持数据格式的演变
5. **空值处理**: 适当处理null值，避免序列化和反序列化过程中的空引用异常

## 注意事项

- 确保 `WriteToXml` 和 `ReadFromXml` 方法是对称的，以正确恢复对象状态
- 处理复杂类型（如日期、集合、嵌套对象）时需要特别注意
- 考虑数据格式的版本控制，以支持数据结构随时间的变化
- 避免在序列化中包含敏感信息，或考虑加密敏感数据 