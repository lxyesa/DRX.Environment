# DrxHttpServer JSON 序列化指南

## 问题背景

启用代码裁剪（`PublishTrimmed` 或 `NativeAOT`）后，System.Text.Json 的运行时反射元数据会被删除，导致基于反射的 JSON 序列化失败。

## 解决方案概述

`DrxHttpServer` 现已集成一个灵活的 **JSON 序列化管理系统**（`DrxJsonSerializer.cs`），支持多种序列化策略：

| 序列化器 | 说明 | 适用场景 |
|---------|------|---------|
| **ReflectionJsonSerializer** | 基于反射的序列化，支持任意 .NET 类型 | 开发环境、非裁剪部署 |
| **SafeJsonSerializer** | 反射序列化 + 自动回退，返回错误对象 | 裁剪环境、需要可靠性 |
| **ChainedJsonSerializer** | 链式回退：依次尝试多个序列化器 | 混合环境（推荐默认配置） |
| **CachedJsonSerializer** | 包装其他序列化器，增加缓存优化 | 性能优化 |

## 默认行为

**DrxHttpServer 默认配置为 `ChainedJsonSerializer` 模式**，即：
1. 首先尝试反射序列化（支持任意类型）
2. 如果失败，自动回退到安全模式（返回错误对象）

这样无需用户干预，在所有环境中都能正常工作。

## 使用方式

### 1. 开发环境（不启用裁剪）

默认配置即可，无需额外代码：

```csharp
var server = new DrxHttpServer(new[] { "http://+:8080/" });
// 已自动配置为链式回退模式，支持反射序列化
```

### 2. 启用代码裁剪环境（推荐方案）

#### 方案 A：使用链式回退（最安全，推荐）

```csharp
var server = new DrxHttpServer(new[] { "http://+:8080/" });
// 保持默认配置（已是链式回退模式）
// 自动在反射失败时回退到安全模式
```

#### 方案 B：明确切换到安全模式

```csharp
using Drx.Sdk.Network.V2.Web;

// 在应用启动时调用
DrxHttpServer.ConfigureJsonSerializerSafeMode();

var server = new DrxHttpServer(new[] { "http://+:8080/" });
```

#### 方案 C：为需要序列化的类型添加 DynamicDependency 注解

在你的项目中创建一个保留文件（例如 `PreserveSerializableTypes.cs`）：

```csharp
using System.Diagnostics.CodeAnalysis;

namespace YourNamespace
{
    /// <summary>
    /// 为需要 JSON 序列化的所有类型添加 DynamicDependency 注解
    /// 这样即使启用裁剪，这些类型的元数据也会被保留
    /// </summary>
    public static class PreserveSerializableTypes
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(YourDTO1))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(YourDTO2))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(YourResponseModel))]
        public static void PreserveTypes() { }
    }
}
```

然后配置为反射模式以获得最好的性能和兼容性：

```csharp
DrxHttpServer.ConfigureJsonSerializerReflectionMode();
var server = new DrxHttpServer(new[] { "http://+:8080/" });
```

### 3. 自定义序列化实现

如果需要完全自定义序列化逻辑，实现 `IDrxJsonSerializer` 接口：

```csharp
using Drx.Sdk.Network.V2.Web;

public class CustomJsonSerializer : IDrxJsonSerializer
{
    public string SerializerName => "My Custom Serializer";

    public bool TrySerialize(object obj, out string? json)
    {
        try
        {
            // 你的自定义序列化逻辑
            json = MyCustomJsonLibrary.Serialize(obj);
            return true;
        }
        catch
        {
            json = null;
            return false;
        }
    }
}

// 在应用启动时配置
var customSerializer = new CustomJsonSerializer();
DrxHttpServer.ConfigureJsonSerializer(customSerializer);

var server = new DrxHttpServer(new[] { "http://+:8080/" });
```

## 核心 API 参考

### DrxJsonSerializerManager（全局配置管理器）

```csharp
// 配置为反射模式
DrxJsonSerializerManager.ConfigureReflectionMode();

// 配置为安全模式（推荐裁剪环境）
DrxJsonSerializerManager.ConfigureSafeMode();

// 配置为链式回退模式（推荐混合环境）
DrxJsonSerializerManager.ConfigureChainedMode();

// 配置自定义序列化器
DrxJsonSerializerManager.ConfigureCustom(mySerializer);

// 获取全局序列化器
var serializer = DrxJsonSerializerManager.GlobalSerializer;

// 尝试序列化
if (DrxJsonSerializerManager.TrySerialize(obj, out var json))
{
    Console.WriteLine(json);
}
```

### DrxHttpServer（便捷配置方法）

```csharp
// 在 HttpServer 级别配置序列化器
DrxHttpServer.ConfigureJsonSerializer(mySerializer);
DrxHttpServer.ConfigureJsonSerializerReflectionMode();
DrxHttpServer.ConfigureJsonSerializerSafeMode();
```

## 推荐方案汇总

| 场景 | 推荐方案 | 代码 |
|-----|---------|------|
| **开发/调试** | 保持默认（链式回退） | 无需配置 |
| **生产/裁剪** | 安全模式或为类型加注解 | `ConfigureJsonSerializerSafeMode()` 或添加 `DynamicDependency` |
| **高性能/知道所有类型** | 反射模式 + DynamicDependency | `ConfigureJsonSerializerReflectionMode()` + 注解 |
| **完全自定义** | 实现 IDrxJsonSerializer | 自定义实现并调用 `ConfigureJsonSerializer()` |

## 故障排除

### Q: 在启用 PublishTrimmed 后 JSON 序列化仍然失败

**A:** 请确保：
1. 使用了 `SafeJsonSerializer` 或 `ChainedJsonSerializer`（自动回退）
2. 或为所有需要序列化的类型添加 `DynamicDependency` 注解，并配置为反射模式
3. 查看日志输出，了解具体失败原因

### Q: 序列化器返回 {"error": "..."}

**A:** 这是安全模式的预期行为。说明：
- 原始序列化失败，框架返回了一个错误对象
- 检查你的对象是否为可序列化的 .NET 类型
- 或为该类型添加 `DynamicDependency` 注解

### Q: 如何选择最适合的配置

**A:** 决策树：
```
是否启用 PublishTrimmed/NativeAOT?
├─ 否 → 使用默认配置（链式回退）
└─ 是 → 是否知道所有需要序列化的类型?
    ├─ 否 → 使用安全模式
    └─ 是 → 为类型加 DynamicDependency + 反射模式
```

## 技术细节

### 序列化过程

1. `DrxHttpServer.SendResponse()` 检查 `httpResponse.BodyObject`
2. 调用 `DrxJsonSerializerManager.TrySerialize()` 进行序列化
3. 全局序列化器依据配置的策略尝试序列化
4. 若失败，返回错误响应或使用 `ToString()`

### 性能考虑

- **ReflectionJsonSerializer**：最灵活，但反射开销较大
- **SafeJsonSerializer**：增加回退检查，轻微开销
- **ChainedJsonSerializer**：按顺序尝试，最坏情况下有多次反射
- **CachedJsonSerializer**：缓存成功的序列化模式，提升重复类型性能

## 参考文档

- [System.Text.Json 官方文档](https://learn.microsoft.com/zh-cn/dotnet/standard/serialization/system-text-json/)
- [.NET NativeAOT 部署](https://learn.microsoft.com/zh-cn/dotnet/core/deploying/native-aot/)
- [代码裁剪最佳实践](https://learn.microsoft.com/zh-cn/dotnet/core/deploying/trimming/trim-self-contained)
- [DynamicDependency 注解](https://learn.microsoft.com/zh-cn/dotnet/api/system.diagnostics.codeanalysis.dynamicdependencyattribute)
