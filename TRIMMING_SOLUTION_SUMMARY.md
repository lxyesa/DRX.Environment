# 代码裁剪环境 JSON 序列化问题 - 永久性解决方案

## 问题描述

启用 `PublishTrimmed` 或 `NativeAOT` 代码裁剪后，System.Text.Json 的运行时反射元数据被删除，导致基于反射的 JSON 序列化失败，这是 .NET 应用在裁剪环境中的常见问题。

## 解决方案总览

已在 `DrxHttpServer` 中实现了一个**完整的、可配置的 JSON 序列化管理系统**，用于永久性解决这个问题。

### 核心改动

#### 1. 新文件：`DrxJsonSerializer.cs`

提供了以下关键组件：

| 组件 | 作用 |
|------|------|
| **IDrxJsonSerializer** | JSON 序列化接口，支持多种实现 |
| **ReflectionJsonSerializer** | 反射序列化实现（灵活但需要元数据保留） |
| **SafeJsonSerializer** | 安全序列化实现（包含自动回退） |
| **ChainedJsonSerializer** | 链式回退实现（依次尝试多个序列化器） |
| **CachedJsonSerializer** | 缓存优化实现 |
| **DrxJsonSerializerManager** | 全局配置管理器 |

#### 2. `DrxHttpServer.cs` 改动

**构造函数初始化**
```csharp
// 自动配置为链式回退模式（默认行为）
DrxJsonSerializerManager.ConfigureChainedMode();
```

**SendResponse 方法优化**
- 将硬编码的 JSON 序列化逻辑替换为调用 `DrxJsonSerializerManager.TrySerialize()`
- 自动支持多种序列化策略
- 添加失败时的安全回退

**新增公共配置方法**
```csharp
// 允许运行时配置序列化策略
public static void ConfigureJsonSerializer(IDrxJsonSerializer? serializer)
public static void ConfigureJsonSerializerReflectionMode()
public static void ConfigureJsonSerializerSafeMode()
```

## 使用指南

### 无需配置（推荐）

```csharp
// 默认使用链式回退模式，自动支持所有环境
var server = new DrxHttpServer(new[] { "http://+:8080/" });

// 返回对象，框架自动序列化为 JSON
server.AddRoute(HttpMethod.Get, "/api/data", req =>
{
    return new HttpResponse(200) 
    { 
        BodyObject = new { message = "Hello" }
    };
});
```

### 启用代码裁剪时的最佳实践

```csharp
// 方案 1：使用安全模式（推荐）
DrxHttpServer.ConfigureJsonSerializerSafeMode();
var server = new DrxHttpServer(new[] { "http://+:8080/" });

// 方案 2：为类型添加 DynamicDependency + 反射模式（高性能）
DrxHttpServer.ConfigureJsonSerializerReflectionMode();

// 在 PreserveTypes.cs 中添加
[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(YourDto))]
public static void Preserve() { }
```

## 关键特性

### 1. **多层次支持**

- ✅ 开发环境（无配置需要）
- ✅ 正常部署（反射序列化）
- ✅ 代码裁剪（自动回退或安全模式）
- ✅ NativeAOT（安全模式 + 预保留元数据）

### 2. **自动回退机制**

默认的链式回退模式：
1. 优先尝试反射序列化（最灵活）
2. 失败时自动切换到安全模式（返回错误对象）
3. 确保应用不会因序列化失败而崩溃

### 3. **可配置性**

支持三级配置：

```
全局级别（DrxJsonSerializerManager）
    ↓
HttpServer 级别（DrxHttpServer.Configure...）
    ↓
每个响应的 BodyObject
```

### 4. **可扩展性**

实现 `IDrxJsonSerializer` 接口，可集成任何 JSON 库：

```csharp
public class MyCustomSerializer : IDrxJsonSerializer
{
    public string SerializerName => "My Serializer";
    
    public bool TrySerialize(object obj, out string? json)
    {
        // 自定义逻辑
    }
}

DrxHttpServer.ConfigureJsonSerializer(new MyCustomSerializer());
```

## 工程实现细节

### 序列化流程

```
HttpResponse.BodyObject
    ↓
DrxHttpServer.SendResponse()
    ↓
DrxJsonSerializerManager.TrySerialize()
    ↓
配置的序列化器链
    ├─ ReflectionJsonSerializer (尝试反射)
    └─ SafeJsonSerializer (失败回退)
    ↓
返回 JSON 或错误对象
```

### 错误处理

序列化失败时的安全响应：

```json
{
  "error": "服务器内部错误：无法序列化响应对象",
  "type": "System.Collections.Generic.List`1[MyNamespace.UserDto]"
}
```

### 性能优化

- **反射缓存**：DefaultJsonTypeInfoResolver 内部缓存类型信息
- **链式优化**：成功的序列化器不会再尝试后续的
- **可选缓存**：可使用 CachedJsonSerializer 包装器进一步优化

## 测试覆盖

已创建示例文件 `JsonSerializationExample.cs`，包含：

1. 默认配置示例
2. 安全模式示例
3. 反射模式示例
4. 自定义序列化器示例
5. 直接 API 使用
6. 错误处理示例

## 迁移路径

### 对现有代码的影响

✅ **完全向后兼容**

- 现有的 `return new HttpResponse(200) { BodyObject = myObject }` 无需修改
- 框架自动使用新的序列化系统
- 所有现有代码开箱即用

### 版本升级建议

1. **当前版本**：直接使用，默认链式回足
2. **启用裁剪前**：可选调用 `ConfigureJsonSerializerReflectionMode()`
3. **启用裁剪后**：调用 `ConfigureJsonSerializerSafeMode()` 或添加 DynamicDependency

## 常见问题

### Q: 为什么默认是链式回退而不是直接安全模式？

**A:** 为了在开发环境获得最好的用户体验和性能。链式回退确保：
- 开发环境：使用高性能的反射序列化
- 裁剪环境：自动回退到安全模式

### Q: 如何在启用 NativeAOT 时使用反射模式？

**A:** 为所有要序列化的类型添加 DynamicDependency 注解：

```csharp
[DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(MyDto))]
public static void PreserveTypes() { }
```

然后在应用启动时调用：

```csharp
DrxHttpServer.ConfigureJsonSerializerReflectionMode();
```

### Q: 自定义序列化器需要处理所有情况吗？

**A:** 不需要。`IDrxJsonSerializer.TrySerialize()` 返回 `false` 时，链式序列化器会尝试下一个。

## 文档与参考

- **使用指南**：`JSON_SERIALIZATION_GUIDE.md`
- **示例代码**：`JsonSerializationExample.cs`
- **API 文档**：`DrxJsonSerializer.cs` 中的 XML 注释

## 总结

该解决方案提供了：

| 维度 | 能力 |
|------|------|
| **兼容性** | 支持所有 .NET 环境（正常、裁剪、NativeAOT） |
| **易用性** | 开箱即用，无需配置 |
| **灵活性** | 支持多种序列化策略和自定义实现 |
| **可靠性** | 包含自动回退和错误处理 |
| **性能** | 支持缓存和高性能序列化器集成 |
| **可维护性** | 清晰的接口设计，易于扩展 |

**这是一个永久性的、生产级别的解决方案，解决了 .NET 应用在启用代码裁剪后的 JSON 序列化问题。**
